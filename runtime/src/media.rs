use std::collections::HashMap;
use std::convert::Infallible;
use std::fs::{self, File};
use std::path::{Path as FilePath, PathBuf};
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Mutex, RwLock};

use axum::extract::Path;
use axum::http::StatusCode;
use axum::response::sse::{Event, KeepAlive, Sse};
use axum::response::IntoResponse;
use axum::Json;
use file_format::{FileFormat, Kind};
use futures::Stream;
use once_cell::sync::Lazy;
use ropus::{Application, Bitrate, Channels as OpusChannels, DecodeMode, Decoder as OpusDecoder, Encoder as OpusEncoder};
use rubato::audioadapter::Adapter;
use rubato::audioadapter_buffers::direct::SequentialSliceOfVecs;
use rubato::{Fft, FixedSync, Resampler};
use serde::{Deserialize, Serialize};
use symphonia::core::audio::sample::Sample;
use symphonia::core::codecs::audio::{well_known::CODEC_ID_OPUS, AudioDecoder, AudioDecoderOptions};
use symphonia::core::codecs::CodecParameters;
use symphonia::core::errors::Error as SymphoniaError;
use symphonia::core::formats::{FormatOptions, Track, TrackFlags, TrackType};
use symphonia::core::io::MediaSourceStream;
use symphonia::core::meta::MetadataOptions;
use symphonia::core::formats::probe::Hint;
use symphonia::core::units::Timestamp;
use tokio::sync::broadcast;
use tokio_stream::wrappers::BroadcastStream;
use tokio_stream::StreamExt;
use webm_iterable::matroska_spec::{Master, MatroskaSpec, SimpleBlock};
use webm_iterable::{WebmIterator, WebmWriter, WriteOptions};

use crate::api_token::APIToken;

const OUTPUT_SAMPLE_RATE: u32 = 48_000;
const OPUS_FRAME_SAMPLES: usize = 960;
const OPUS_BITRATE: u32 = 32_000;
const CLUSTER_DURATION_MS: u64 = 30_000;
const OPUS_PRE_SKIP: u16 = 312;
const DEFAULT_MAX_PASS_THROUGH_BYTES: u64 = 25 * 1024 * 1024;

static JOBS: Lazy<RwLock<HashMap<String, Arc<MediaJob>>>> = Lazy::new(|| RwLock::new(HashMap::new()));

#[derive(Debug, Deserialize)]
pub struct CreateMediaJobRequest {
    pub input_path: String,
    pub output_path: Option<String>,
    pub max_pass_through_bytes: Option<u64>,
}

#[derive(Debug, Serialize)]
pub struct CreateMediaJobResponse {
    pub job_id: String,
}

#[derive(Clone, Debug, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum MediaJobPhase {
    Probing,
    Transcoding,
    Completed,
    Failed,
    Cancelled,
}

#[derive(Clone, Debug, Serialize)]
pub struct MediaJobEvent {
    pub phase: MediaJobPhase,
    pub progress: Option<f64>,
    pub result: Option<MediaJobResult>,
    pub error: Option<MediaError>,
}

#[derive(Clone, Debug, Serialize)]
pub struct MediaJobResult {
    pub output_path: String,
    pub detected_format: String,
    pub detected_codec: String,
    pub duration_ms: u64,
    pub pass_through: bool,
}

#[derive(Clone, Debug, Serialize)]
pub struct MediaError {
    pub code: String,
    pub message: String,
}

impl MediaError {
    fn new(code: &str, message: impl Into<String>) -> Self {
        Self { code: code.to_string(), message: message.into() }
    }
}

struct MediaJob {
    cancelled: AtomicBool,
    current: Mutex<MediaJobEvent>,
    events: broadcast::Sender<MediaJobEvent>,
}

impl MediaJob {
    fn new() -> Self {
        let initial = MediaJobEvent {
            phase: MediaJobPhase::Probing,
            progress: Some(0.0),
            result: None,
            error: None,
        };

        let (events, _) = broadcast::channel(32);
        Self { cancelled: AtomicBool::new(false), current: Mutex::new(initial), events }
    }

    fn publish(&self, event: MediaJobEvent) {
        *self.current.lock().unwrap() = event.clone();
        let _ = self.events.send(event);
    }

    fn is_cancelled(&self) -> bool {
        self.cancelled.load(Ordering::Relaxed)
    }
}

pub async fn create_job(
    _token: APIToken,
    Json(request): Json<CreateMediaJobRequest>,
) -> Result<Json<CreateMediaJobResponse>, (StatusCode, Json<MediaError>)> {
    let input_path = PathBuf::from(&request.input_path);
    if !input_path.is_file() {
        return Err((StatusCode::BAD_REQUEST, Json(MediaError::new("file_not_found", "The selected media file does not exist."))));
    }

    let output_path = request.output_path.map(PathBuf::from).unwrap_or_else(|| {
        let parent = input_path.parent().unwrap_or_else(|| FilePath::new("."));
        let stem = input_path.file_stem().and_then(|value| value.to_str()).unwrap_or("media");
        parent.join(format!("{stem}-normalized.webm"))
    });

    let job_id = format!("{}-{}", std::process::id(), rand::random::<u64>());
    let job = Arc::new(MediaJob::new());
    JOBS.write().unwrap().insert(job_id.clone(), Arc::clone(&job));
    let completed_job_id = job_id.clone();

    tauri::async_runtime::spawn(async move {
        let max_pass_through_bytes = request.max_pass_through_bytes.unwrap_or(DEFAULT_MAX_PASS_THROUGH_BYTES);
        let task_job = Arc::clone(&job);
        let result = tokio::task::spawn_blocking(move || normalize_media(&input_path, &output_path, max_pass_through_bytes, &task_job)).await;
        match result {
            Ok(Ok(result)) => job.publish(MediaJobEvent {
                phase: MediaJobPhase::Completed,
                progress: Some(1.0),
                result: Some(result),
                error: None,
            }),
            Ok(Err(error)) if error.code == "cancelled" => job.publish(MediaJobEvent {
                phase: MediaJobPhase::Cancelled,
                progress: None,
                result: None,
                error: None,
            }),
            Ok(Err(error)) => job.publish(MediaJobEvent {
                phase: MediaJobPhase::Failed,
                progress: None,
                result: None,
                error: Some(error),
            }),
            Err(error) => job.publish(MediaJobEvent {
                phase: MediaJobPhase::Failed,
                progress: None,
                result: None,
                error: Some(MediaError::new("internal_error", format!("The media worker failed: {error}"))),
            }),
        }
        tokio::time::sleep(std::time::Duration::from_secs(600)).await;
        JOBS.write().unwrap().remove(&completed_job_id);
    });

    Ok(Json(CreateMediaJobResponse { job_id }))
}

pub async fn get_job_events(
    _token: APIToken,
    Path(job_id): Path<String>,
) -> Result<Sse<impl Stream<Item = Result<Event, Infallible>>>, StatusCode> {
    let job = JOBS.read().unwrap().get(&job_id).cloned().ok_or(StatusCode::NOT_FOUND)?;
    let current = job.current.lock().unwrap().clone();
    let initial = tokio_stream::once(current);
    let updates = BroadcastStream::new(job.events.subscribe()).filter_map(|event| event.ok());
    let stream = initial.chain(updates).map(|event| {
        let data = serde_json::to_string(&event).unwrap_or_else(|_| "{}".to_string());
        Ok(Event::default().event(phase_name(&event.phase)).data(data))
    });

    Ok(Sse::new(stream).keep_alive(KeepAlive::default()))
}

pub async fn cancel_job(_token: APIToken, Path(job_id): Path<String>) -> impl IntoResponse {
    match JOBS.read().unwrap().get(&job_id) {
        Some(job) => {
            job.cancelled.store(true, Ordering::Relaxed);
            StatusCode::NO_CONTENT
        }

        None => StatusCode::NOT_FOUND,
    }
}

fn phase_name(phase: &MediaJobPhase) -> &'static str {
    match phase {
        MediaJobPhase::Probing => "probing",
        MediaJobPhase::Transcoding => "transcoding",
        MediaJobPhase::Completed => "completed",
        MediaJobPhase::Failed => "failed",
        MediaJobPhase::Cancelled => "cancelled",
    }
}

fn normalize_media(input_path: &FilePath, output_path: &FilePath, max_pass_through_bytes: u64, job: &MediaJob) -> Result<MediaJobResult, MediaError> {
    check_cancelled(job)?;

    let detected = FileFormat::from_file(input_path)
        .map_err(|error| MediaError::new("unknown_format", format!("The file type could not be identified: {error}")))?;

    if detected.kind() == Kind::Executable {
        return Err(MediaError::new("unsafe_file", "The selected file contains executable data and cannot be processed as media."));
    }

    if !matches!(detected.kind(), Kind::Audio | Kind::Video)
        && !matches!(detected, FileFormat::ExtensibleBinaryMetaLanguage)
    {
        return Err(MediaError::new("not_media", format!("The selected file is not supported media (detected as {detected:?}).")));
    }

    let file = File::open(input_path).map_err(|error| MediaError::new("file_open_failed", error.to_string()))?;
    let mss = MediaSourceStream::new(Box::new(file), Default::default());

    let mut hint = Hint::new();
    if let Some(extension) = input_path.extension().and_then(|value| value.to_str()) {
        hint.with_extension(extension);
    }

    let mut format = symphonia::default::get_probe()
        .probe(&hint, mss, FormatOptions::default(), MetadataOptions::default())
        .map_err(map_probe_error)?;

    let detected_format = format!("{detected:?} / {}", format.format_info().long_name);

    let tracks = format.tracks();
    let mut audio_tracks: Vec<&Track> = tracks.iter().filter(|track| {
        matches!(track.codec_params, Some(CodecParameters::Audio(_)))
    }).collect();

    if audio_tracks.is_empty() {
        return Err(MediaError::new("no_audio_track", "The selected media file does not contain an audio track."));
    }

    audio_tracks.sort_by_key(|track| !track.flags.contains(TrackFlags::DEFAULT));
    let selected = audio_tracks.into_iter().find(|track| is_decodable(track))
        .ok_or_else(|| MediaError::new("unsupported_codec", "None of the audio tracks uses a supported codec."))?;

    let track_id = selected.id;
    let params = selected.codec_params.as_ref().and_then(CodecParameters::audio).unwrap().clone();
    let detected_codec = if params.codec == CODEC_ID_OPUS { "opus".to_string() } else { format!("{}", params.codec) };
    let duration_ms = selected.time_base.zip(selected.duration).and_then(|(time_base, duration)| {
        let timestamp = Timestamp::new(i64::try_from(duration.get()).ok()?);
        let time = time_base.calc_time(timestamp)?;
        Some((time.as_secs_f64() * 1000.0).max(0.0).round() as u64)
    }).unwrap_or(0);

    let audio_only = tracks.iter().all(|track| track.track_type() != Some(TrackType::Video));
    let channels = params.channels.as_ref().map(|value| value.count()).unwrap_or(0);
    let pass_through = is_webm_container(input_path)
        && audio_only
        && tracks.iter().filter(|track| track.track_type() == Some(TrackType::Audio)).count() == 1
        && params.codec == CODEC_ID_OPUS
        && params.sample_rate == Some(OUTPUT_SAMPLE_RATE)
        && channels == 1
        && input_path.metadata().map(|metadata| metadata.len() <= max_pass_through_bytes).unwrap_or(false);

    let partial_path = partial_path(output_path);
    if let Some(parent) = partial_path.parent() {
        fs::create_dir_all(parent).map_err(|error| MediaError::new("output_create_failed", error.to_string()))?;
    }

    let result = if pass_through {
        check_cancelled(job)?;
        fs::copy(input_path, &partial_path).map_err(|error| MediaError::new("output_write_failed", error.to_string()))?;
        check_cancelled(job)?;
        Ok(MediaJobResult {
            output_path: output_path.to_string_lossy().into_owned(),
            detected_format: detected_format.clone(),
            detected_codec,
            duration_ms,
            pass_through: true,
        })
    } else {
        job.publish(MediaJobEvent {
            phase: MediaJobPhase::Transcoding,
            progress: Some(0.0),
            result: None,
            error: None,
        });

        transcode(&mut *format, track_id, &params, &partial_path, output_path, detected_format, detected_codec, duration_ms, job)
    };

    match result {
        Ok(result) => {
            if let Err(error) = fs::rename(&partial_path, output_path) {
                let _ = fs::remove_file(&partial_path);
                return Err(MediaError::new("output_commit_failed", error.to_string()));
            }

            Ok(result)
        }

        Err(error) => {
            let _ = fs::remove_file(&partial_path);
            Err(error)
        }
    }
}

fn is_decodable(track: &Track) -> bool {
    let Some(params) = track.codec_params.as_ref().and_then(CodecParameters::audio) else { return false; };
    params.codec == CODEC_ID_OPUS || symphonia::default::get_codecs().make_audio_decoder(params, &AudioDecoderOptions::default()).is_ok()
}

fn transcode(
    format: &mut dyn symphonia::core::formats::FormatReader,
    track_id: u32,
    params: &symphonia::core::codecs::audio::AudioCodecParameters,
    partial_path: &FilePath,
    output_path: &FilePath,
    detected_format: String,
    detected_codec: String,
    expected_duration_ms: u64,
    job: &MediaJob,
) -> Result<MediaJobResult, MediaError> {
    let input_rate = params.sample_rate.unwrap_or(OUTPUT_SAMPLE_RATE);
    let input_channels = params.channels.as_ref().map(|value| value.count()).unwrap_or(1).max(1);

    let mut symphonia_decoder: Option<Box<dyn AudioDecoder>> = if params.codec == CODEC_ID_OPUS {
        None
    } else {
        Some(symphonia::default::get_codecs().make_audio_decoder(params, &AudioDecoderOptions::default())
            .map_err(|_| MediaError::new("unsupported_codec", "The selected audio codec is not supported."))?)
    };

    let mut opus_decoder = if params.codec == CODEC_ID_OPUS {
        let channels = if input_channels == 1 { OpusChannels::Mono } else { OpusChannels::Stereo };
        Some(OpusDecoder::new(OUTPUT_SAMPLE_RATE, channels)
            .map_err(|error| MediaError::new("decoder_init_failed", error.to_string()))?)
    } else { None };

    let mut opus_encoder = OpusEncoder::builder(OUTPUT_SAMPLE_RATE, OpusChannels::Mono, Application::Audio)
        .bitrate(Bitrate::Bits(OPUS_BITRATE))
        .vbr(true)
        .build()
        .map_err(|error| MediaError::new("encoder_init_failed", error.to_string()))?;

    let file = File::create(partial_path).map_err(|error| MediaError::new("output_create_failed", error.to_string()))?;
    let mut writer = WebmOpusWriter::new(file)?;
    let mut pending = Vec::<f32>::with_capacity(OPUS_FRAME_SAMPLES * 3);
    let mut interleaved = Vec::<f32>::new();
    let mut opus_pcm = vec![0i16; 5_760 * input_channels];
    let mut encoded = [0u8; 4_000];
    let mut produced_samples = 0u64;
    let mut input_samples = 0u64;
    let mut resampled_samples = 0u64;

    loop {
        check_cancelled(job)?;
        let packet = match format.next_packet() {
            Ok(Some(packet)) => packet,
            Ok(None) => break,

            Err(SymphoniaError::ResetRequired) => return Err(MediaError::new("stream_reset", "The media stream changed unexpectedly.")),
            Err(SymphoniaError::IoError(error)) if error.kind() == std::io::ErrorKind::UnexpectedEof => break,
            Err(error) => return Err(MediaError::new("damaged_container", format!("The media container is damaged: {error}"))),
        };

        if packet.track_id != track_id {
            continue;
        }

        let mono = if let Some(decoder) = symphonia_decoder.as_mut() {
            let decoded = match decoder.decode(&packet) {
                Ok(decoded) => decoded,
                Err(SymphoniaError::DecodeError(_)) => continue,
                Err(error) => return Err(MediaError::new("decode_failed", format!("Audio decoding failed: {error}"))),
            };

            interleaved.resize(decoded.samples_interleaved(), f32::MID);
            decoded.copy_to_slice_interleaved(&mut interleaved);
            downmix_to_mono(&interleaved, decoded.num_planes())
        } else {
            let decoder = opus_decoder.as_mut().unwrap();
            let frames = decoder.decode(&packet.data, &mut opus_pcm, DecodeMode::Normal)
                .map_err(|error| MediaError::new("decode_failed", error.to_string()))?;

            let samples = &opus_pcm[..frames * input_channels];
            if input_channels == 1 {
                samples.iter().map(|sample| f32::from(*sample) / 32768.0).collect()
            } else {
                samples.chunks_exact(input_channels).map(|frame| {
                    frame.iter().map(|sample| f32::from(*sample) / 32768.0).sum::<f32>() / input_channels as f32
                }).collect()
            }
        };

        input_samples += mono.len() as u64;
        let expected_resampled_samples = input_samples.saturating_mul(u64::from(OUTPUT_SAMPLE_RATE)) / u64::from(input_rate);
        let expected_chunk_samples = expected_resampled_samples.saturating_sub(resampled_samples) as usize;
        let mut resampled = resample_chunk(&mono, input_rate)?;
        resampled.resize(expected_chunk_samples, 0.0);
        resampled.truncate(expected_chunk_samples);
        resampled_samples += resampled.len() as u64;
        pending.extend_from_slice(&resampled);

        let mut consumed = 0usize;
        while pending.len() - consumed >= OPUS_FRAME_SAMPLES {
            check_cancelled(job)?;
            let frame = &pending[consumed..consumed + OPUS_FRAME_SAMPLES];
            let length = opus_encoder.encode_float(frame, &mut encoded)
                .map_err(|error| MediaError::new("encode_failed", error.to_string()))?;

            writer.write_packet(&encoded[..length], produced_samples)?;
            produced_samples += OPUS_FRAME_SAMPLES as u64;
            consumed += OPUS_FRAME_SAMPLES;
        }

        if consumed > 0 {
            pending.drain(..consumed);

        }

        if expected_duration_ms > 0 {
            let current_ms = produced_samples.saturating_mul(1000) / u64::from(OUTPUT_SAMPLE_RATE);
            job.publish(MediaJobEvent {
                phase: MediaJobPhase::Transcoding,
                progress: Some((current_ms as f64 / expected_duration_ms as f64).clamp(0.0, 0.99)),
                result: None,
                error: None,
            });
        }
    }

    if !pending.is_empty() {
        pending.resize(OPUS_FRAME_SAMPLES, 0.0);
        let length = opus_encoder.encode_float(&pending, &mut encoded)
            .map_err(|error| MediaError::new("encode_failed", error.to_string()))?;
        writer.write_packet(&encoded[..length], produced_samples)?;
        produced_samples += OPUS_FRAME_SAMPLES as u64;
    }

    writer.finish()?;
    check_cancelled(job)?;

    Ok(MediaJobResult {
        output_path: output_path.to_string_lossy().into_owned(),
        detected_format,
        detected_codec,
        duration_ms: produced_samples.saturating_mul(1000) / u64::from(OUTPUT_SAMPLE_RATE),
        pass_through: false,
    })
}

fn downmix_to_mono(samples: &[f32], channels: usize) -> Vec<f32> {
    if channels <= 1 {
        return samples.to_vec();
    }

    samples.chunks_exact(channels).map(|frame| frame.iter().copied().sum::<f32>() / channels as f32).collect()
}

fn resample_chunk(samples: &[f32], input_rate: u32) -> Result<Vec<f32>, MediaError> {
    if input_rate == OUTPUT_SAMPLE_RATE || samples.is_empty() {
        return Ok(samples.to_vec());
    }

    let input_data = vec![samples.to_vec()];
    let input = SequentialSliceOfVecs::new(&input_data, 1, samples.len())
        .map_err(|error| MediaError::new("resample_failed", error.to_string()))?;

    let mut resampler = Fft::<f32>::new(input_rate as usize, OUTPUT_SAMPLE_RATE as usize, samples.len().max(256), 1, FixedSync::Input)
        .map_err(|error| MediaError::new("resample_failed", error.to_string()))?;

    let output = resampler.process_all(&input, samples.len(), None)
        .map_err(|error| MediaError::new("resample_failed", error.to_string()))?;

    let mut result = Vec::with_capacity(output.frames());
    for frame in 0..output.frames() {
        result.push(output.read_sample(0, frame).unwrap_or(0.0));
    }

    Ok(result)
}

struct WebmOpusWriter {
    writer: WebmWriter<File>,
    cluster_start_ms: Option<u64>,
}

impl WebmOpusWriter {
    fn new(file: File) -> Result<Self, MediaError> {
        let mut writer = WebmWriter::new(file);
        write_tags(&mut writer, &[
            MatroskaSpec::Ebml(Master::Start),
            MatroskaSpec::EbmlVersion(1),
            MatroskaSpec::EbmlReadVersion(1),
            MatroskaSpec::EbmlMaxIdLength(4),
            MatroskaSpec::EbmlMaxSizeLength(8),
            MatroskaSpec::DocType("webm".to_string()),
            MatroskaSpec::DocTypeVersion(4),
            MatroskaSpec::DocTypeReadVersion(2),
            MatroskaSpec::Ebml(Master::End),
        ])?;

        writer.write_advanced(&MatroskaSpec::Segment(Master::Start), WriteOptions::is_unknown_sized_element()).map_err(webm_error)?;

        write_tags(&mut writer, &[
            MatroskaSpec::Info(Master::Start),
            MatroskaSpec::TimestampScale(1_000_000),
            MatroskaSpec::MuxingApp("MindWork AI Studio".to_string()),
            MatroskaSpec::WritingApp("MindWork AI Studio".to_string()),
            MatroskaSpec::Info(Master::End),
            MatroskaSpec::Tracks(Master::Start),
            MatroskaSpec::TrackEntry(Master::Start),
            MatroskaSpec::TrackNumber(1),
            MatroskaSpec::TrackUID(1),
            MatroskaSpec::TrackType(2),
            MatroskaSpec::FlagDefault(1),
            MatroskaSpec::CodecID("A_OPUS".to_string()),
            MatroskaSpec::CodecPrivate(opus_head()),
            MatroskaSpec::CodecDelay(u64::from(OPUS_PRE_SKIP) * 1_000_000_000 / u64::from(OUTPUT_SAMPLE_RATE)),
            MatroskaSpec::SeekPreRoll(80_000_000),
            MatroskaSpec::Audio(Master::Start),
            MatroskaSpec::SamplingFrequency(f64::from(OUTPUT_SAMPLE_RATE)),
            MatroskaSpec::Channels(1),
            MatroskaSpec::Audio(Master::End),
            MatroskaSpec::TrackEntry(Master::End),
            MatroskaSpec::Tracks(Master::End),
        ])?;

        Ok(Self { writer, cluster_start_ms: None })
    }

    fn write_packet(&mut self, packet: &[u8], sample_position: u64) -> Result<(), MediaError> {
        let timestamp_ms = sample_position.saturating_mul(1000) / u64::from(OUTPUT_SAMPLE_RATE);
        let rotate = self.cluster_start_ms.map(|start| timestamp_ms.saturating_sub(start) >= CLUSTER_DURATION_MS).unwrap_or(true);

        if rotate {
            if self.cluster_start_ms.is_some() {
                self.writer.write(&MatroskaSpec::Cluster(Master::End)).map_err(webm_error)?;
            }

            self.writer.write(&MatroskaSpec::Cluster(Master::Start)).map_err(webm_error)?;
            self.writer.write(&MatroskaSpec::Timestamp(timestamp_ms)).map_err(webm_error)?;
            self.cluster_start_ms = Some(timestamp_ms);
        }

        let relative = timestamp_ms.saturating_sub(self.cluster_start_ms.unwrap_or(timestamp_ms));
        if relative > i16::MAX as u64 {
            return Err(MediaError::new("webm_timestamp_overflow", "The WebM cluster timestamp exceeded its safe range."));
        }

        let block: MatroskaSpec = SimpleBlock::new_uncheked(packet, 1, relative as i16, false, None, false, true).into();
        self.writer.write(&block).map_err(webm_error)
    }

    fn finish(mut self) -> Result<(), MediaError> {
        if self.cluster_start_ms.is_some() {
            self.writer.write(&MatroskaSpec::Cluster(Master::End)).map_err(webm_error)?;
        }

        self.writer.write(&MatroskaSpec::Segment(Master::End)).map_err(webm_error)?;
        self.writer.into_inner().map_err(webm_error)?;
        Ok(())
    }
}

fn write_tags(writer: &mut WebmWriter<File>, tags: &[MatroskaSpec]) -> Result<(), MediaError> {
    for tag in tags {
        writer.write(tag).map_err(webm_error)?;
    }

    Ok(())
}

fn opus_head() -> Vec<u8> {
    let mut data = b"OpusHead".to_vec();
    data.push(1);
    data.push(1);
    data.extend_from_slice(&OPUS_PRE_SKIP.to_le_bytes());
    data.extend_from_slice(&OUTPUT_SAMPLE_RATE.to_le_bytes());
    data.extend_from_slice(&0i16.to_le_bytes());
    data.push(0);
    data
}

fn partial_path(output_path: &FilePath) -> PathBuf {
    let mut name = output_path.file_name().unwrap_or_default().to_os_string();
    name.push(".partial");
    output_path.with_file_name(name)
}

fn is_webm_container(path: &FilePath) -> bool {
    let Ok(file) = File::open(path) else { return false; };
    WebmIterator::new(file, &[]).take(16).filter_map(Result::ok).any(|tag| {
        matches!(tag, MatroskaSpec::DocType(doc_type) if doc_type.eq_ignore_ascii_case("webm"))
    })
}

fn check_cancelled(job: &MediaJob) -> Result<(), MediaError> {
    if job.is_cancelled() {
        Err(MediaError::new("cancelled", "The media job was cancelled."))
    } else {
        Ok(())
    }
}

fn map_probe_error(error: SymphoniaError) -> MediaError {
    match error {
        SymphoniaError::Unsupported(_) => MediaError::new("unsupported_container", "This media container or codec is not supported."),
        _ => MediaError::new("damaged_container", format!("The media container could not be read: {error}")),
    }
}

fn webm_error(error: impl std::fmt::Display) -> MediaError {
    MediaError::new("webm_write_failed", format!("The WebM output could not be written: {error}"))
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Cursor;

    #[test]
    fn opus_head_describes_48_khz_mono() {
        let head = opus_head();
        assert_eq!(&head[..8], b"OpusHead");
        assert_eq!(head[9], 1);
        assert_eq!(u32::from_le_bytes(head[12..16].try_into().unwrap()), 48_000);
    }

    #[test]
    fn webm_writer_rotates_clusters_before_relative_timestamp_overflow() {
        let path = std::env::temp_dir().join(format!("ai-studio-media-writer-{}.webm", rand::random::<u64>()));
        let file = File::create(&path).unwrap();
        let mut writer = WebmOpusWriter::new(file).unwrap();
        writer.write_packet(&[0xf8, 0xff, 0xfe], 0).unwrap();
        writer.write_packet(&[0xf8, 0xff, 0xfe], 31 * 48_000).unwrap();
        writer.finish().unwrap();
        let bytes = fs::read(&path).unwrap();
        let clusters = WebmIterator::new(Cursor::new(bytes), &[])
            .filter_map(Result::ok)
            .filter(|tag| matches!(tag, MatroskaSpec::Cluster(Master::Start)))
            .count();
        let _ = fs::remove_file(path);
        assert_eq!(clusters, 2);
    }

    #[test]
    fn downmix_is_bounded_and_balanced() {
        assert_eq!(downmix_to_mono(&[1.0, -1.0, 0.5, 0.5], 2), vec![0.0, 0.5]);
    }

    #[test]
    fn wav_is_normalized_to_one_mono_opus_track_with_frame_bounded_duration() {
        let directory = std::env::temp_dir().join(format!("ai-studio-media-test-{}", rand::random::<u64>()));
        fs::create_dir_all(&directory).unwrap();
        let input = directory.join("input.wav");
        let output = directory.join("output.webm");
        fs::write(&input, wav_silence(44_100, 4_410)).unwrap();
        let job = MediaJob::new();
        let result = normalize_media(&input, &output, DEFAULT_MAX_PASS_THROUGH_BYTES, &job).unwrap();
        assert!(!result.pass_through);
        assert!(result.duration_ms.abs_diff(100) <= 20);

        let file = File::open(&output).unwrap();
        let tags: Vec<_> = WebmIterator::new(file, &[])
            .filter_map(Result::ok)
            .collect();
        assert_eq!(tags.iter().filter(|tag| matches!(tag, MatroskaSpec::TrackEntry(Master::Start))).count(), 1);
        assert!(tags.iter().any(|tag| matches!(tag, MatroskaSpec::CodecID(codec) if codec == "A_OPUS")));
        assert!(tags.iter().any(|tag| matches!(tag, MatroskaSpec::Channels(1))));
        assert!(tags.iter().any(|tag| matches!(tag, MatroskaSpec::SamplingFrequency(rate) if *rate == 48_000.0)));
        let _ = fs::remove_dir_all(directory);
    }

    #[test]
    fn cancellation_does_not_leave_an_output_file() {
        let directory = std::env::temp_dir().join(format!("ai-studio-media-cancel-{}", rand::random::<u64>()));
        fs::create_dir_all(&directory).unwrap();
        let input = directory.join("input.wav");
        let output = directory.join("output.webm");
        fs::write(&input, wav_silence(48_000, 960)).unwrap();
        let job = MediaJob::new();
        job.cancelled.store(true, Ordering::Relaxed);
        let error = normalize_media(&input, &output, DEFAULT_MAX_PASS_THROUGH_BYTES, &job).unwrap_err();
        assert_eq!(error.code, "cancelled");
        assert!(!output.exists());
        assert!(!partial_path(&output).exists());
        let _ = fs::remove_dir_all(directory);
    }

    #[test]
    fn suitable_audio_only_webm_opus_is_passed_through() {
        let directory = std::env::temp_dir().join(format!("ai-studio-media-pass-through-{}", rand::random::<u64>()));
        fs::create_dir_all(&directory).unwrap();
        let input = directory.join("input.webm");
        let output = directory.join("output.webm");
        let mut writer = WebmOpusWriter::new(File::create(&input).unwrap()).unwrap();
        writer.write_packet(&[0xf8, 0xff, 0xfe], 0).unwrap();
        writer.finish().unwrap();
        let job = MediaJob::new();
        let result = normalize_media(&input, &output, DEFAULT_MAX_PASS_THROUGH_BYTES, &job).unwrap();
        assert!(result.pass_through);
        assert_eq!(fs::read(input).unwrap(), fs::read(output).unwrap());
        let _ = fs::remove_dir_all(directory);
    }

    fn wav_silence(sample_rate: u32, samples: u32) -> Vec<u8> {
        let data_size = samples * 2;
        let mut wav = Vec::with_capacity(44 + data_size as usize);
        wav.extend_from_slice(b"RIFF");
        wav.extend_from_slice(&(36 + data_size).to_le_bytes());
        wav.extend_from_slice(b"WAVEfmt ");
        wav.extend_from_slice(&16u32.to_le_bytes());
        wav.extend_from_slice(&1u16.to_le_bytes());
        wav.extend_from_slice(&1u16.to_le_bytes());
        wav.extend_from_slice(&sample_rate.to_le_bytes());
        wav.extend_from_slice(&(sample_rate * 2).to_le_bytes());
        wav.extend_from_slice(&2u16.to_le_bytes());
        wav.extend_from_slice(&16u16.to_le_bytes());
        wav.extend_from_slice(b"data");
        wav.extend_from_slice(&data_size.to_le_bytes());
        wav.resize(44 + data_size as usize, 0);
        wav
    }
}