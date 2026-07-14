//! Asynchronous media normalization jobs producing bounded mono WebM/Opus output.

use std::collections::HashMap;
use std::convert::Infallible;
use std::fs::{self, File};
use std::io::{Read, Write};
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
use rubato::audioadapter_buffers::direct::SequentialSliceOfVecs;
use rubato::{Fft, FixedSync, Indexing, Resampler};
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

/// Sample rate required by the normalized WebM/Opus output contract.
const OUTPUT_SAMPLE_RATE: u32 = 48_000;

/// Number of samples in one 20 ms Opus frame at 48 kHz.
const OPUS_FRAME_SAMPLES: usize = 960;

/// Target bitrate for mono speech-oriented Opus output.
const OPUS_BITRATE: u32 = 32_000;

/// Maximum duration of a WebM cluster before rotating it.
const CLUSTER_DURATION_MS: u64 = 30_000;

/// Encoder look-ahead advertised as the output track's codec delay.
const OPUS_PRE_SKIP: u16 = 312;

/// Default size ceiling for copying an already-normalized file unchanged.
const DEFAULT_MAX_PASS_THROUGH_BYTES: u64 = 25 * 1024 * 1024;

/// Bounded input block used for streaming resampling.
const RESAMPLE_INPUT_BLOCK_SAMPLES: usize = 2_048;

/// Bounded block used by the cancellation-aware pass-through copy.
const COPY_BLOCK_BYTES: usize = 64 * 1024;

/// Time a terminal job remains available for late SSE subscribers.
const TERMINAL_JOB_RETENTION: std::time::Duration = std::time::Duration::from_secs(10 * 60);

/// In-memory registry of running and recently completed media jobs.
static JOBS: Lazy<RwLock<HashMap<String, Arc<MediaJob>>>> = Lazy::new(|| RwLock::new(HashMap::new()));

/// Request body for starting a media normalization job.
#[derive(Debug, Deserialize)]
pub struct CreateMediaJobRequest {
    /// Absolute path of the source media file.
    pub input_path: String,

    /// Optional absolute output path; a sibling WebM path is derived when omitted.
    pub output_path: Option<String>,

    /// Optional size ceiling for pass-through files.
    pub max_pass_through_bytes: Option<u64>,
}

/// Response returned immediately after a media job has been registered.
#[derive(Debug, Serialize)]
pub struct CreateMediaJobResponse {
    /// Opaque identifier used by the event and cancellation routes.
    pub job_id: String,
}

/// Observable lifecycle phases of a media job.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum MediaJobPhase {
    /// The runtime is identifying the container and selecting an audio track.
    Probing,

    /// The runtime is decoding and normalizing the selected track.
    Transcoding,

    /// The normalized output was committed atomically.
    Completed,

    /// The job ended with a stable media error.
    Failed,

    /// Cancellation completed and temporary output has been removed.
    Cancelled,
}

/// Stable, machine-readable failure categories returned by the media API.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum MediaErrorCode {
    /// The requested input file does not exist.
    FileNotFound,

    /// The file type could not be identified.
    UnknownFormat,

    /// Executable input was rejected.
    UnsafeFile,

    /// The identified input is not audio or video.
    NotMedia,

    /// The input file could not be opened.
    FileOpenFailed,

    /// Symphonia does not support the container.
    UnsupportedContainer,

    /// The container has no audio track.
    NoAudioTrack,

    /// No audio track has a supported decoder.
    UnsupportedCodec,

    /// Required decoded stream parameters are absent or changed.
    InvalidAudioParameters,

    /// The Opus identification header is invalid.
    InvalidOpusHeader,

    /// The Opus mapping requires unsupported multistream decoding.
    UnsupportedOpusMapping,

    /// The decoder could not be initialized.
    DecoderInitFailed,

    /// The encoder could not be initialized.
    EncoderInitFailed,

    /// The stream requested a decoder reset.
    StreamReset,

    /// The media container is truncated or malformed.
    DamagedContainer,

    /// Audio decoding failed.
    DecodeFailed,

    /// Audio resampling failed.
    ResampleFailed,

    /// Opus encoding failed.
    EncodeFailed,

    /// The output directory could not be created.
    OutputCreateFailed,

    /// Output bytes could not be written.
    OutputWriteFailed,

    /// The partial output could not be committed.
    OutputCommitFailed,

    /// A WebM relative timestamp exceeded its safe range.
    WebmTimestampOverflow,

    /// WebM output serialization failed.
    WebmWriteFailed,

    /// The job was cancelled.
    Cancelled,

    /// The worker task terminated unexpectedly.
    InternalError,
}

/// Snapshot delivered through the media job SSE stream.
#[derive(Clone, Debug, Serialize)]
pub struct MediaJobEvent {
    /// Current lifecycle phase.
    pub phase: MediaJobPhase,

    /// Optional progress fraction between zero and one.
    pub progress: Option<f64>,

    /// Terminal result, present only for completed jobs.
    pub result: Option<MediaJobResult>,

    /// Terminal diagnostic, present only for failed jobs.
    pub error: Option<MediaError>,
}

/// Successful normalized-media result.
#[derive(Clone, Debug, Serialize)]
pub struct MediaJobResult {
    /// Path at which the normalized output was committed.
    pub output_path: String,

    /// Human-readable detected container description for diagnostics.
    pub detected_format: String,

    /// Human-readable selected codec description for diagnostics.
    pub detected_codec: String,

    /// Duration of the normalized playable audio.
    pub duration_ms: u64,

    /// Whether the input was copied unchanged.
    pub pass_through: bool,
}

/// Stable error code plus an English diagnostic intended for logs.
#[derive(Clone, Debug, Serialize)]
pub struct MediaError {
    /// Machine-readable failure category used for localization by the client.
    pub code: MediaErrorCode,

    /// US-English diagnostic detail for logging and support.
    pub message: String,
}

impl MediaError {
    /// Creates a media error without exposing free-form codes on the wire.
    fn new(code: MediaErrorCode, message: impl Into<String>) -> Self {
        Self { code, message: message.into() }
    }
}

/// Mutable state shared by the request routes and blocking worker.
struct MediaJob {
    /// Cooperative cancellation flag checked at bounded intervals.
    cancelled: AtomicBool,

    /// Latest snapshot replayed to a newly connected SSE subscriber.
    current: Mutex<MediaJobEvent>,

    /// Fan-out channel for live state changes.
    events: broadcast::Sender<MediaJobEvent>,
}

impl MediaJob {
    /// Creates a job in the probing phase before its worker is scheduled.
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

    /// Replaces the replay snapshot and notifies all live subscribers.
    fn publish(&self, event: MediaJobEvent) {
        *self.current.lock().unwrap() = event.clone();
        let _ = self.events.send(event);
    }

    /// Returns whether cooperative cancellation was requested.
    fn is_cancelled(&self) -> bool {
        self.cancelled.load(Ordering::Relaxed)
    }
}

/// Registers and immediately schedules a media normalization job.
pub async fn create_job(
    _token: APIToken,
    Json(request): Json<CreateMediaJobRequest>,
) -> Result<Json<CreateMediaJobResponse>, (StatusCode, Json<MediaError>)> {
    let input_path = PathBuf::from(&request.input_path);
    if !input_path.is_file() {
        return Err((StatusCode::BAD_REQUEST, Json(MediaError::new(MediaErrorCode::FileNotFound, "The selected media file does not exist."))));
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

            Ok(Err(error)) if error.code == MediaErrorCode::Cancelled => job.publish(MediaJobEvent {
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
                error: Some(MediaError::new(MediaErrorCode::InternalError, format!("The media worker failed: {error}"))),
            }),
        }

        retain_terminal_job(completed_job_id).await;
    });

    Ok(Json(CreateMediaJobResponse { job_id }))
}

/// Retains a terminal job for late SSE subscribers, then removes it asynchronously.
///
/// Retention starts only after the worker has published a terminal event. Sleeping here neither
/// blocks the originating request nor the blocking media worker.
async fn retain_terminal_job(job_id: String) {
    tokio::time::sleep(TERMINAL_JOB_RETENTION).await;
    JOBS.write().unwrap().remove(&job_id);
}

/// Streams the current snapshot followed by live media job events.
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

/// Requests cooperative cancellation of a running media job.
pub async fn cancel_job(_token: APIToken, Path(job_id): Path<String>) -> impl IntoResponse {
    match JOBS.read().unwrap().get(&job_id) {
        Some(job) => {
            job.cancelled.store(true, Ordering::Relaxed);
            StatusCode::NO_CONTENT
        }

        None => StatusCode::NOT_FOUND,
    }
}

/// Maps a phase to the corresponding SSE event name.
fn phase_name(phase: &MediaJobPhase) -> &'static str {
    match phase {
        MediaJobPhase::Probing => "probing",
        MediaJobPhase::Transcoding => "transcoding",
        MediaJobPhase::Completed => "completed",
        MediaJobPhase::Failed => "failed",
        MediaJobPhase::Cancelled => "cancelled",
    }
}

/// Probes, normalizes, and atomically commits one media file.
fn normalize_media(input_path: &FilePath, output_path: &FilePath, max_pass_through_bytes: u64, job: &MediaJob) -> Result<MediaJobResult, MediaError> {
    check_cancelled(job)?;

    let detected = FileFormat::from_file(input_path)
        .map_err(|error| MediaError::new(MediaErrorCode::UnknownFormat, format!("The file type could not be identified: {error}")))?;

    if detected.kind() == Kind::Executable {
        return Err(MediaError::new(MediaErrorCode::UnsafeFile, "The selected file contains executable data and cannot be processed as media."));
    }

    if !matches!(detected.kind(), Kind::Audio | Kind::Video)
        && !matches!(
            detected,
            FileFormat::ExtensibleBinaryMetaLanguage
                | FileFormat::Id3v2
                | FileFormat::Mpeg4Part14
                | FileFormat::Mpeg4Part14Audio
                | FileFormat::Mpeg4Part14Video
        )
        && !has_supported_media_extension(input_path)
    {
        return Err(MediaError::new(MediaErrorCode::NotMedia, format!("The selected file is not supported media (detected as {detected:?}).")));
    }

    let file = File::open(input_path).map_err(|error| MediaError::new(MediaErrorCode::FileOpenFailed, error.to_string()))?;
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
    if !tracks.iter().any(is_audio_track) {
        return Err(MediaError::new(MediaErrorCode::NoAudioTrack, "The selected media file does not contain an audio track."));
    }

    let selected = select_audio_track(tracks)
        .ok_or_else(|| MediaError::new(MediaErrorCode::UnsupportedCodec, "None of the audio tracks uses a supported codec."))?;

    let track_id = selected.id;
    let params = selected.codec_params.as_ref().and_then(CodecParameters::audio).unwrap().clone();
    let detected_codec = if params.codec == CODEC_ID_OPUS { "opus".to_string() } else { format!("{}", params.codec) };
    let duration_ms = selected.num_frames.zip(params.sample_rate)
        .map(|(frames, rate)| frames.saturating_mul(1_000) / u64::from(rate))
        .or_else(|| selected.time_base.zip(selected.duration).and_then(|(time_base, duration)| {
            let timestamp = Timestamp::new(i64::try_from(duration.get()).ok()?);
            let time = time_base.calc_time(timestamp)?;
            Some((time.as_secs_f64() * 1000.0).max(0.0).round() as u64)
        }))
        .unwrap_or(0);

    let channels = params.channels.as_ref().map(|value| value.count()).unwrap_or(0);
    let pass_through = is_webm_container(input_path)
        && tracks.len() == 1
        && selected.track_type() == Some(TrackType::Audio)
        && params.codec == CODEC_ID_OPUS
        && params.sample_rate == Some(OUTPUT_SAMPLE_RATE)
        && channels == 1
        && input_path.metadata().map(|metadata| metadata.len() <= max_pass_through_bytes).unwrap_or(false);

    let partial_path = partial_path(output_path);
    if let Some(parent) = partial_path.parent() {
        fs::create_dir_all(parent).map_err(|error| MediaError::new(MediaErrorCode::OutputCreateFailed, error.to_string()))?;
    }

    let result = if pass_through {
        copy_with_cancellation(input_path, &partial_path, job)?;
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

        let context = TranscodeContext {
            track_id,
            track_delay: selected.delay.unwrap_or(0),
            track_padding: selected.padding.unwrap_or(0),
            params,
            partial_path: &partial_path,
            output_path,
            detected_format,
            detected_codec,
            expected_duration_ms: duration_ms,
            job,
        };
        transcode(&mut *format, context)
    };

    match result {
        Ok(result) => {
            if let Err(error) = fs::rename(&partial_path, output_path) {
                let _ = fs::remove_file(&partial_path);
                return Err(MediaError::new(MediaErrorCode::OutputCommitFailed, error.to_string()));
            }

            Ok(result)
        }

        Err(error) => {
            let _ = fs::remove_file(&partial_path);
            Err(error)
        }
    }
}

/// Recognizes extensions for containers supported by the configured Symphonia readers.
fn has_supported_media_extension(path: &FilePath) -> bool {
    path.extension()
        .and_then(|extension| extension.to_str())
        .is_some_and(|extension| matches!(
            extension.to_ascii_lowercase().as_str(),
            "aac" | "aif" | "aiff" | "caf" | "flac" | "m4a" | "mka" | "mkv" | "mov"
                | "mp1" | "mp2" | "mp3" | "mp4" | "oga" | "ogg" | "opus" | "wav" | "webm"
        ))
}

/// Returns whether a track explicitly contains audio codec parameters.
fn is_audio_track(track: &Track) -> bool {
    matches!(track.codec_params, Some(CodecParameters::Audio(_)))
}

/// Selects the decodable default audio track, falling back to the first decodable audio track.
fn select_audio_track(tracks: &[Track]) -> Option<&Track> {
    tracks
        .iter()
        .filter(|track| is_audio_track(track) && is_decodable(track))
        .min_by_key(|track| !track.flags.contains(TrackFlags::DEFAULT))
}

/// Checks whether the runtime can construct a decoder for the track.
fn is_decodable(track: &Track) -> bool {
    let Some(params) = track.codec_params.as_ref().and_then(CodecParameters::audio) else { return false; };
    params.codec == CODEC_ID_OPUS || symphonia::default::get_codecs().make_audio_decoder(params, &AudioDecoderOptions::default()).is_ok()
}

/// Immutable inputs shared across a single transcoding operation.
struct TranscodeContext<'a> {
    /// Selected input track identifier.
    track_id: u32,

    /// Leading decoded frames to discard.
    track_delay: u32,

    /// Trailing decoded frames to discard.
    track_padding: u32,

    /// Selected track codec parameters.
    params: symphonia::core::codecs::audio::AudioCodecParameters,

    /// Temporary output path used until the job succeeds.
    partial_path: &'a FilePath,

    /// Final output path returned in the result.
    output_path: &'a FilePath,

    /// Detected container diagnostic.
    detected_format: String,

    /// Detected codec diagnostic.
    detected_codec: String,

    /// Container duration used for progress reporting.
    expected_duration_ms: u64,

    /// Cancellation and progress state for the job.
    job: &'a MediaJob,
}

/// Decodes a selected track and writes bounded 20 ms mono Opus frames.
fn transcode(
    format: &mut dyn symphonia::core::formats::FormatReader,
    context: TranscodeContext<'_>,
) -> Result<MediaJobResult, MediaError> {
    let mut decoder = StreamDecoder::new(&context.params, context.track_delay)?;
    let mut opus_encoder = OpusEncoder::builder(OUTPUT_SAMPLE_RATE, OpusChannels::Mono, Application::Audio)
        .bitrate(Bitrate::Bits(OPUS_BITRATE))
        .vbr(true)
        .build()
        .map_err(|error| MediaError::new(MediaErrorCode::EncoderInitFailed, error.to_string()))?;

    let file = File::create(context.partial_path).map_err(|error| MediaError::new(MediaErrorCode::OutputCreateFailed, error.to_string()))?;
    let mut writer = WebmOpusWriter::new(file)?;
    let mut pending = Vec::<f32>::with_capacity(OPUS_FRAME_SAMPLES * 3);
    let mut resampler: Option<StreamResampler> = None;
    let mut decoded_tail = Vec::<f32>::new();
    let mut encoded = [0u8; 4_000];
    let mut produced_samples = 0u64;

    loop {
        check_cancelled(context.job)?;
        let packet = match format.next_packet() {
            Ok(Some(packet)) => packet,
            Ok(None) => break,

            Err(SymphoniaError::ResetRequired) => return Err(MediaError::new(MediaErrorCode::StreamReset, "The media stream changed unexpectedly.")),
            Err(SymphoniaError::IoError(error)) if error.kind() == std::io::ErrorKind::UnexpectedEof => break,
            Err(error) => return Err(MediaError::new(MediaErrorCode::DamagedContainer, format!("The media container is damaged: {error}"))),
        };

        if packet.track_id != context.track_id {
            continue;
        }

        let Some((mono, sample_rate)) = decoder.decode(&packet)? else {
            continue;
        };

        let stream_resampler = match resampler.as_mut() {
            Some(existing) if existing.input_rate() == sample_rate => existing,
            Some(_) => return Err(MediaError::new(MediaErrorCode::InvalidAudioParameters, "The decoded audio sample rate changed during the stream.")),
            None => resampler.insert(StreamResampler::new(sample_rate)?),
        };

        // Retain only the possible end padding so it can never be emitted prematurely.
        decoded_tail.extend_from_slice(&mono);
        let padding = context.track_padding as usize;
        let emit_len = decoded_tail.len().saturating_sub(padding);
        if emit_len > 0 {
            let emit: Vec<_> = decoded_tail.drain(..emit_len).collect();
            append_duration_bounded(
                &mut pending,
                &stream_resampler.push(&emit)?,
                produced_samples,
                context.expected_duration_ms,
            );
        }

        encode_complete_frames(&mut pending, &mut opus_encoder, &mut writer, &mut encoded, &mut produced_samples, context.job)?;

        if context.expected_duration_ms > 0 {
            let current_ms = produced_samples.saturating_mul(1000) / u64::from(OUTPUT_SAMPLE_RATE);
            context.job.publish(MediaJobEvent {
                phase: MediaJobPhase::Transcoding,
                progress: Some((current_ms as f64 / context.expected_duration_ms as f64).clamp(0.0, 0.99)),
                result: None,
                error: None,
            });
        }
    }

    if let Some(stream_resampler) = resampler.as_mut() {
        // Discard the retained decoded tail (container padding), then flush the filter delay.
        append_duration_bounded(
            &mut pending,
            &stream_resampler.finish()?,
            produced_samples,
            context.expected_duration_ms,
        );
    } else {
        return Err(MediaError::new(MediaErrorCode::InvalidAudioParameters, "The selected audio track did not yield decoded audio parameters."));
    }

    encode_complete_frames(&mut pending, &mut opus_encoder, &mut writer, &mut encoded, &mut produced_samples, context.job)?;

    if !pending.is_empty() {
        pending.resize(OPUS_FRAME_SAMPLES, 0.0);
        let length = opus_encoder.encode_float(&pending, &mut encoded)
            .map_err(|error| MediaError::new(MediaErrorCode::EncodeFailed, error.to_string()))?;
        writer.write_packet(&encoded[..length], produced_samples)?;
        produced_samples += OPUS_FRAME_SAMPLES as u64;
    }

    writer.finish()?;
    check_cancelled(context.job)?;

    Ok(MediaJobResult {
        output_path: context.output_path.to_string_lossy().into_owned(),
        detected_format: context.detected_format,
        detected_codec: context.detected_codec,
        duration_ms: produced_samples.saturating_mul(1000) / u64::from(OUTPUT_SAMPLE_RATE),
        pass_through: false,
    })
}

/// Appends resampled PCM without exceeding a reliable container duration.
fn append_duration_bounded(pending: &mut Vec<f32>, samples: &[f32], produced_samples: u64, expected_duration_ms: u64) {
    if expected_duration_ms == 0 {
        pending.extend_from_slice(samples);
        return;
    }

    let target_samples = expected_duration_ms.saturating_mul(u64::from(OUTPUT_SAMPLE_RATE)).div_ceil(1_000);
    let accepted = produced_samples.saturating_add(pending.len() as u64);
    let remaining = target_samples.saturating_sub(accepted) as usize;
    pending.extend_from_slice(&samples[..samples.len().min(remaining)]);
}

/// Downmixes interleaved PCM to mono using a deterministic arithmetic mean.
fn downmix_to_mono(samples: &[f32], channels: usize) -> Vec<f32> {
    if channels <= 1 {
        return samples.to_vec();
    }

    samples.chunks_exact(channels).map(|frame| frame.iter().copied().sum::<f32>() / channels as f32).collect()
}

/// Parsed subset of an Opus identification header supported by the single-stream decoder.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct OpusHeader {
    /// Mono or stereo channel count.
    channels: usize,

    /// Number of leading decoded frames to discard at 48 kHz.
    pre_skip: u16,
}

impl OpusHeader {
    /// Parses and validates a mono/stereo, mapping-family-zero `OpusHead` packet.
    fn parse(data: &[u8]) -> Result<Self, MediaError> {
        if data.len() < 19 || &data[..8] != b"OpusHead" || data[8] > 15 || !matches!(data[9], 1 | 2) {
            return Err(MediaError::new(MediaErrorCode::InvalidOpusHeader, "The Opus identification header is invalid."));
        }

        if data[18] != 0 {
            return Err(MediaError::new(MediaErrorCode::UnsupportedOpusMapping, "The Opus channel mapping requires unsupported multistream decoding."));
        }

        Ok(Self {
            channels: usize::from(data[9]),
            pre_skip: u16::from_le_bytes([data[10], data[11]]),
        })
    }
}

/// Ropus adapter that validates Symphonia parameters and applies Opus pre-skip exactly once.
struct RopusOpusDecoder {
    /// Underlying libopus single-stream decoder.
    decoder: OpusDecoder,

    /// Number of interleaved channels produced by the decoder.
    channels: usize,

    /// Remaining leading frames to discard.
    delay_remaining: usize,

    /// Reused bounded PCM output buffer.
    pcm: Vec<i16>,
}

impl RopusOpusDecoder {
    /// Builds a mono/stereo decoder from the codec's `OpusHead` private data.
    fn new(params: &symphonia::core::codecs::audio::AudioCodecParameters, track_delay: u32) -> Result<Self, MediaError> {
        let header = OpusHeader::parse(params.extra_data.as_deref().unwrap_or_default())?;
        let declared_channels = params.channels.as_ref().map(|channels| channels.count())
            .ok_or_else(|| MediaError::new(MediaErrorCode::InvalidAudioParameters, "The Opus stream does not declare its channel count."))?;

        if declared_channels != header.channels || params.sample_rate != Some(OUTPUT_SAMPLE_RATE) {
            return Err(MediaError::new(MediaErrorCode::InvalidAudioParameters, "The Opus stream parameters do not match its identification header."));
        }

        let opus_channels = if header.channels == 1 { OpusChannels::Mono } else { OpusChannels::Stereo };
        let decoder = OpusDecoder::new(OUTPUT_SAMPLE_RATE, opus_channels)
            .map_err(|error| MediaError::new(MediaErrorCode::DecoderInitFailed, error.to_string()))?;

        let delay = if track_delay == 0 { u32::from(header.pre_skip) } else { track_delay };

        Ok(Self {
            decoder,
            channels: header.channels,
            delay_remaining: delay as usize,
            pcm: vec![0; 5_760 * header.channels],
        })
    }

    /// Decodes one Opus packet, downmixes it, and removes leading codec delay.
    fn decode(&mut self, packet: &[u8]) -> Result<Vec<f32>, MediaError> {
        let frames = self.decoder.decode(packet, &mut self.pcm, DecodeMode::Normal)
            .map_err(|error| MediaError::new(MediaErrorCode::DecodeFailed, error.to_string()))?;

        let skip = self.delay_remaining.min(frames);
        self.delay_remaining -= skip;

        let samples = &self.pcm[skip * self.channels..frames * self.channels];
        let interleaved: Vec<_> = samples.iter().map(|sample| f32::from(*sample) / 32_768.0).collect();
        Ok(downmix_to_mono(&interleaved, self.channels))
    }
}

/// Decoder abstraction allowing Symphonia demuxing with either its native decoders or Ropus.
enum StreamDecoder {
    /// Decoder supplied by Symphonia for non-Opus codecs.
    Symphonia {
        /// Stateful codec decoder.
        decoder: Box<dyn AudioDecoder>,

        /// Reused interleaved PCM buffer.
        interleaved: Vec<f32>,

        /// First observed decoded sample rate.
        sample_rate: Option<u32>,

        /// First observed decoded channel count.
        channels: Option<usize>,

        /// Remaining track delay to discard.
        delay_remaining: usize,
    },

    /// Symphonia-compatible Opus packet adapter backed by Ropus.
    Opus(Box<RopusOpusDecoder>),
}

impl StreamDecoder {
    /// Creates the appropriate decoder without guessing missing stream parameters.
    fn new(params: &symphonia::core::codecs::audio::AudioCodecParameters, track_delay: u32) -> Result<Self, MediaError> {
        if params.codec == CODEC_ID_OPUS {
            return Ok(Self::Opus(Box::new(RopusOpusDecoder::new(params, track_delay)?)));
        }

        let decoder = symphonia::default::get_codecs().make_audio_decoder(params, &AudioDecoderOptions::default())
            .map_err(|_| MediaError::new(MediaErrorCode::UnsupportedCodec, "The selected audio codec is not supported."))?;

        Ok(Self::Symphonia {
            decoder,
            interleaved: Vec::new(),
            sample_rate: None,
            channels: None,
            delay_remaining: track_delay as usize,
        })
    }

    /// Decodes one packet and returns mono PCM plus the actual decoder sample rate.
    fn decode(&mut self, packet: &symphonia::core::packet::Packet) -> Result<Option<(Vec<f32>, u32)>, MediaError> {
        match self {
            Self::Opus(decoder) => Ok(Some((decoder.decode(&packet.data)?, OUTPUT_SAMPLE_RATE))),

            Self::Symphonia { decoder, interleaved, sample_rate, channels, delay_remaining } => {
                let decoded = match decoder.decode(packet) {
                    Ok(decoded) => decoded,
                    Err(SymphoniaError::DecodeError(_)) => return Ok(None),
                    Err(error) => return Err(MediaError::new(MediaErrorCode::DecodeFailed, format!("Audio decoding failed: {error}"))),
                };

                let actual_rate = decoded.spec().rate();
                let actual_channels = decoded.spec().channels().count();

                if actual_rate == 0 || actual_channels == 0 {
                    return Err(MediaError::new(MediaErrorCode::InvalidAudioParameters, "The decoder returned invalid audio parameters."));
                }

                if sample_rate.is_some_and(|rate| rate != actual_rate) || channels.is_some_and(|count| count != actual_channels) {
                    return Err(MediaError::new(MediaErrorCode::InvalidAudioParameters, "The decoded audio parameters changed during the stream."));
                }

                *sample_rate = Some(actual_rate);
                *channels = Some(actual_channels);
                interleaved.resize(decoded.samples_interleaved(), f32::MID);
                decoded.copy_to_slice_interleaved(&mut *interleaved);

                let mono = downmix_to_mono(interleaved, actual_channels);
                let skip = (*delay_remaining).min(mono.len());

                *delay_remaining -= skip;
                Ok(Some((mono[skip..].to_vec(), actual_rate)))
            }
        }
    }
}

/// Stateful, bounded mono resampler whose filter history spans decoder packets.
enum StreamResampler {
    /// Zero-copy-rate path for already-48-kHz decoded PCM.
    Passthrough {
        /// Input rate retained for stream consistency checks.
        input_rate: u32,
    },

    /// Rubato FFT resampler and its bounded pending input.
    Rubato {
        /// Input rate retained for stream consistency checks.
        input_rate: u32,

        /// Stateful resampler instance used for the entire stream.
        inner: Box<Fft<f32>>,

        /// Samples waiting to fill the next fixed input block.
        pending: Vec<f32>,

        /// Startup-delay output frames still to discard.
        delay_remaining: usize,

        /// Total real input frames accepted.
        total_input: u64,

        /// Total trimmed output frames returned to the encoder.
        total_output: u64,
    },
}

impl StreamResampler {
    /// Creates one resampler for the decoded stream.
    fn new(input_rate: u32) -> Result<Self, MediaError> {
        if input_rate == 0 {
            return Err(MediaError::new(MediaErrorCode::InvalidAudioParameters, "The decoded audio sample rate is missing."));
        }

        if input_rate == OUTPUT_SAMPLE_RATE {
            return Ok(Self::Passthrough { input_rate });
        }

        let inner = Fft::<f32>::new(
            input_rate as usize,
            OUTPUT_SAMPLE_RATE as usize,
            RESAMPLE_INPUT_BLOCK_SAMPLES,
            1,
            FixedSync::Input,
        ).map_err(|error| MediaError::new(MediaErrorCode::ResampleFailed, error.to_string()))?;

        let delay_remaining = inner.output_delay();

        Ok(Self::Rubato {
            input_rate,
            inner: Box::new(inner),
            pending: Vec::with_capacity(RESAMPLE_INPUT_BLOCK_SAMPLES * 2),
            delay_remaining,
            total_input: 0,
            total_output: 0,
        })
    }

    /// Returns the configured input sample rate.
    fn input_rate(&self) -> u32 {
        match self {
            Self::Passthrough { input_rate } | Self::Rubato { input_rate, .. } => *input_rate,
        }
    }

    /// Accepts arbitrary packet-sized PCM and processes all complete bounded blocks.
    fn push(&mut self, samples: &[f32]) -> Result<Vec<f32>, MediaError> {
        match self {
            Self::Passthrough { .. } => Ok(samples.to_vec()),

            Self::Rubato { inner, pending, delay_remaining, total_input, total_output, .. } => {
                *total_input += samples.len() as u64;
                pending.extend_from_slice(samples);

                let mut output = Vec::new();
                loop {
                    let block_len = inner.input_frames_next();
                    if pending.len() < block_len {
                        break;
                    }

                    let block: Vec<_> = pending.drain(..block_len).collect();
                    append_resampled(inner, &block, None, delay_remaining, &mut output)?;
                }

                *total_output += output.len() as u64;
                Ok(output)
            }
        }
    }

    /// Flushes the last partial block and filter delay, returning the exact rounded duration.
    fn finish(&mut self) -> Result<Vec<f32>, MediaError> {
        match self {
            Self::Passthrough { .. } => Ok(Vec::new()),

            Self::Rubato { input_rate, inner, pending, delay_remaining, total_input, total_output } => {
                let target = total_input.saturating_mul(u64::from(OUTPUT_SAMPLE_RATE)).div_ceil(u64::from(*input_rate));
                let mut output = Vec::new();

                if !pending.is_empty() {
                    let valid = pending.len();
                    let block_len = inner.input_frames_next();
                    pending.resize(block_len, 0.0);
                    append_resampled(inner, pending, Some(valid), delay_remaining, &mut output)?;
                    pending.clear();
                }

                while *total_output + (output.len() as u64) < target {
                    let zeros = vec![0.0; inner.input_frames_next()];
                    append_resampled(inner, &zeros, Some(0), delay_remaining, &mut output)?;
                }

                output.truncate(target.saturating_sub(*total_output) as usize);
                *total_output += output.len() as u64;
                Ok(output)
            }
        }
    }
}

/// Processes one Rubato block and removes the resampler's startup delay.
fn append_resampled(
    resampler: &mut Fft<f32>,
    block: &[f32],
    partial_len: Option<usize>,
    delay_remaining: &mut usize,
    destination: &mut Vec<f32>,
) -> Result<(), MediaError> {
    let input_data = vec![block.to_vec()];
    let input = SequentialSliceOfVecs::new(&input_data, 1, block.len())
        .map_err(|error| MediaError::new(MediaErrorCode::ResampleFailed, error.to_string()))?;

    let indexing = partial_len.map(|length| Indexing::new().partial_len(length));
    let output = resampler.process(&input, indexing.as_ref())
        .map_err(|error| MediaError::new(MediaErrorCode::ResampleFailed, error.to_string()))?;

    let data = output.take_data();
    let skip = (*delay_remaining).min(data.len());
    *delay_remaining -= skip;
    destination.extend_from_slice(&data[skip..]);
    Ok(())
}

/// Encodes every complete 20 ms frame currently buffered.
fn encode_complete_frames(
    pending: &mut Vec<f32>,
    encoder: &mut OpusEncoder,
    writer: &mut WebmOpusWriter,
    encoded: &mut [u8],
    produced_samples: &mut u64,
    job: &MediaJob,
) -> Result<(), MediaError> {
    let mut consumed = 0usize;
    while pending.len().saturating_sub(consumed) >= OPUS_FRAME_SAMPLES {
        check_cancelled(job)?;
        let length = encoder.encode_float(&pending[consumed..consumed + OPUS_FRAME_SAMPLES], encoded)
            .map_err(|error| MediaError::new(MediaErrorCode::EncodeFailed, error.to_string()))?;
        writer.write_packet(&encoded[..length], *produced_samples)?;
        *produced_samples += OPUS_FRAME_SAMPLES as u64;
        consumed += OPUS_FRAME_SAMPLES;
    }

    if consumed > 0 {
        pending.drain(..consumed);
    }

    Ok(())
}

/// Copies a pass-through input in bounded blocks with cooperative cancellation checks.
fn copy_with_cancellation(input_path: &FilePath, output_path: &FilePath, job: &MediaJob) -> Result<(), MediaError> {
    let mut input = File::open(input_path).map_err(|error| MediaError::new(MediaErrorCode::FileOpenFailed, error.to_string()))?;
    let mut output = File::create(output_path).map_err(|error| MediaError::new(MediaErrorCode::OutputCreateFailed, error.to_string()))?;
    let mut buffer = [0u8; COPY_BLOCK_BYTES];

    loop {
        check_cancelled(job)?;
        let count = input.read(&mut buffer).map_err(|error| MediaError::new(MediaErrorCode::FileOpenFailed, error.to_string()))?;
        if count == 0 {
            break;
        }

        output.write_all(&buffer[..count]).map_err(|error| MediaError::new(MediaErrorCode::OutputWriteFailed, error.to_string()))?;
    }

    output.flush().map_err(|error| MediaError::new(MediaErrorCode::OutputWriteFailed, error.to_string()))?;
    check_cancelled(job)
}

/// Minimal streaming WebM writer for one mono Opus track.
struct WebmOpusWriter {
    /// EBML writer owning the partial output file.
    writer: WebmWriter<File>,

    /// Absolute timestamp of the current cluster in milliseconds.
    cluster_start_ms: Option<u64>,
}

impl WebmOpusWriter {
    /// Writes EBML, segment, info, and the single-track header.
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

    /// Writes one Opus packet and rotates clusters before timestamp overflow.
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
            return Err(MediaError::new(MediaErrorCode::WebmTimestampOverflow, "The WebM cluster timestamp exceeded its safe range."));
        }

        let block: MatroskaSpec = SimpleBlock::new_uncheked(packet, 1, relative as i16, false, None, false, true).into();
        self.writer.write(&block).map_err(webm_error)
    }

    /// Closes the active cluster and finalizes the segment and output file.
    fn finish(mut self) -> Result<(), MediaError> {
        if self.cluster_start_ms.is_some() {
            self.writer.write(&MatroskaSpec::Cluster(Master::End)).map_err(webm_error)?;
        }

        self.writer.write(&MatroskaSpec::Segment(Master::End)).map_err(webm_error)?;
        self.writer.into_inner().map_err(webm_error)?;
        Ok(())
    }
}

/// Writes a sequence of Matroska tags with a consistent error mapping.
fn write_tags(writer: &mut WebmWriter<File>, tags: &[MatroskaSpec]) -> Result<(), MediaError> {
    for tag in tags {
        writer.write(tag).map_err(webm_error)?;
    }

    Ok(())
}

/// Builds the mono 48-kHz output track's Opus identification header.
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

/// Derives the operation-owned partial path adjacent to the final output.
fn partial_path(output_path: &FilePath) -> PathBuf {
    let mut name = output_path.file_name().unwrap_or_default().to_os_string();
    name.push(".partial");
    output_path.with_file_name(name)
}

/// Checks the EBML document type rather than trusting the input extension.
fn is_webm_container(path: &FilePath) -> bool {
    let Ok(file) = File::open(path) else { return false; };
    WebmIterator::new(file, &[]).take(16).filter_map(Result::ok).any(|tag| {
        matches!(tag, MatroskaSpec::DocType(doc_type) if doc_type.eq_ignore_ascii_case("webm"))
    })
}

/// Converts the cooperative cancellation flag to a stable terminal error.
fn check_cancelled(job: &MediaJob) -> Result<(), MediaError> {
    if job.is_cancelled() {
        Err(MediaError::new(MediaErrorCode::Cancelled, "The media job was cancelled."))
    } else {
        Ok(())
    }
}

/// Maps probe failures to stable public media error categories.
fn map_probe_error(error: SymphoniaError) -> MediaError {
    match error {
        SymphoniaError::Unsupported(_) => MediaError::new(MediaErrorCode::UnsupportedContainer, "This media container or codec is not supported."),
        _ => MediaError::new(MediaErrorCode::DamagedContainer, format!("The media container could not be read: {error}")),
    }
}

/// Maps WebM writer failures to a stable public media error category.
fn webm_error(error: impl std::fmt::Display) -> MediaError {
    MediaError::new(MediaErrorCode::WebmWriteFailed, format!("The WebM output could not be written: {error}"))
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Cursor;
    use symphonia::core::audio::{Channels, Position};
    use symphonia::core::codecs::audio::well_known::{CODEC_ID_AC3, CODEC_ID_PCM_S16LE};
    use symphonia::core::codecs::audio::AudioCodecParameters;

    /// Returns the checked-in, FFmpeg-free-at-test-time media fixture directory.
    fn fixtures() -> PathBuf {
        FilePath::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/media")
    }

    /// Creates a temporary output and normalizes one checked-in fixture.
    fn normalize_fixture(name: &str) -> Result<(MediaJobResult, PathBuf), MediaError> {
        let output = std::env::temp_dir().join(format!("ai-studio-fixture-{}.webm", rand::random::<u64>()));
        let result = normalize_media(&fixtures().join(name), &output, DEFAULT_MAX_PASS_THROUGH_BYTES, &MediaJob::new())?;
        Ok((result, output))
    }

    /// Verifies the generated output identification header.
    #[test]
    fn opus_head_describes_48_khz_mono() {
        let head = opus_head();
        assert_eq!(&head[..8], b"OpusHead");
        assert_eq!(head[9], 1);
        assert_eq!(u32::from_le_bytes(head[12..16].try_into().unwrap()), 48_000);
    }

    /// Verifies both single-stream channel layouts accepted by the adapter.
    #[test]
    fn opus_adapter_accepts_single_stream_mono_and_stereo_headers() {
        for channels in [1, 2] {
            let mut head = opus_head();
            head[9] = channels;
            let parsed = OpusHeader::parse(&head).unwrap();
            assert_eq!(parsed.channels, usize::from(channels));
            assert_eq!(parsed.pre_skip, OPUS_PRE_SKIP);
        }
    }

    /// Verifies unsupported multistream mappings retain their stable code.
    #[test]
    fn opus_adapter_rejects_multistream_mapping_with_stable_code() {
        let mut head = opus_head();
        head[18] = 1;
        assert_eq!(OpusHeader::parse(&head).unwrap_err().code, MediaErrorCode::UnsupportedOpusMapping);
    }

    /// Verifies a decodable default track wins over an earlier fallback.
    #[test]
    fn track_selection_prefers_decodable_default_audio() {
        let mut first_params = AudioCodecParameters::new();
        first_params.for_codec(CODEC_ID_PCM_S16LE).with_sample_rate(48_000).with_channels(Channels::Positioned(Position::FRONT_LEFT));
        let mut default_params = first_params.clone();
        default_params.for_codec(CODEC_ID_PCM_S16LE);
        let mut first = Track::new(1);
        first.with_codec_params(CodecParameters::Audio(first_params));
        let mut preferred = Track::new(2);
        preferred.with_codec_params(CodecParameters::Audio(default_params)).with_flags(TrackFlags::DEFAULT);
        assert_eq!(select_audio_track(&[first, preferred]).unwrap().id, 2);
    }

    /// Verifies an undecodable default track does not mask a usable fallback.
    #[test]
    fn track_selection_skips_undecodable_default_audio() {
        let mut unsupported = AudioCodecParameters::new();
        unsupported.for_codec(CODEC_ID_AC3).with_sample_rate(48_000).with_channels(Channels::Positioned(Position::FRONT_LEFT));
        let mut supported = AudioCodecParameters::new();
        supported.for_codec(CODEC_ID_PCM_S16LE).with_sample_rate(48_000).with_channels(Channels::Positioned(Position::FRONT_LEFT));
        let mut default = Track::new(1);
        default.with_codec_params(CodecParameters::Audio(unsupported)).with_flags(TrackFlags::DEFAULT);
        let mut fallback = Track::new(2);
        fallback.with_codec_params(CodecParameters::Audio(supported));
        assert_eq!(select_audio_track(&[default, fallback]).unwrap().id, 2);
    }

    /// Verifies long output rotates clusters before signed relative timestamps overflow.
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

    /// Verifies deterministic arithmetic-mean downmixing.
    #[test]
    fn downmix_is_bounded_and_balanced() {
        assert_eq!(downmix_to_mono(&[1.0, -1.0, 0.5, 0.5], 2), vec![0.0, 0.5]);
    }

    /// Verifies output shape and at-most-one-frame duration rounding.
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

    /// Verifies cancellation removes both final and partial outputs.
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
        assert_eq!(error.code, MediaErrorCode::Cancelled);
        assert!(!output.exists());
        assert!(!partial_path(&output).exists());
        let _ = fs::remove_dir_all(directory);
    }

    /// Verifies an exactly compliant one-track WebM is copied unchanged.
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

    /// Exercises every checked-in supported audio container without FFmpeg at test time.
    #[test]
    fn checked_in_audio_fixtures_normalize_without_external_tools() {
        for name in [
            "sample.m4a",
            "sample.mov",
            "sample.mp4",
            "sample.mkv",
            "sample.ogg",
            "sample.mp3",
            "sample.flac",
            "sample.wav",
            "sample.aiff",
            "sample.caf",
        ] {
            let (result, output) = normalize_fixture(name).unwrap_or_else(|error| panic!("{name}: {error:?}"));
            assert!(result.duration_ms > 0 && result.duration_ms <= 200, "{name}: {} ms", result.duration_ms);
            assert!(output.is_file(), "{name}");
            let _ = fs::remove_file(output);
        }
    }

    /// Verifies video and subtitle tracks independently disable pass-through.
    #[test]
    fn pass_through_requires_exactly_one_audio_track() {
        let (audio_only, audio_output) = normalize_fixture("audio-only.webm").unwrap();
        assert!(audio_only.pass_through);
        let _ = fs::remove_file(audio_output);

        let (video, video_output) = normalize_fixture("video.webm").unwrap();
        assert!(!video.pass_through);
        let _ = fs::remove_file(video_output);

        let (subtitle, subtitle_output) = normalize_fixture("subtitle.webm").unwrap();
        assert!(!subtitle.pass_through);
        let _ = fs::remove_file(subtitle_output);
    }

    /// Verifies malformed, audio-less, and unknown-codec fixtures return stable categories.
    #[test]
    fn fixture_errors_are_stable() {
        let damaged_output = std::env::temp_dir().join(format!("ai-studio-damaged-{}.webm", rand::random::<u64>()));
        let damaged = normalize_media(&fixtures().join("damaged.bin"), &damaged_output, DEFAULT_MAX_PASS_THROUGH_BYTES, &MediaJob::new()).unwrap_err();
        assert!(matches!(damaged.code, MediaErrorCode::UnknownFormat | MediaErrorCode::NotMedia | MediaErrorCode::DamagedContainer));

        let no_audio_output = std::env::temp_dir().join(format!("ai-studio-no-audio-{}.webm", rand::random::<u64>()));
        let no_audio = normalize_media(&fixtures().join("no-audio.webm"), &no_audio_output, DEFAULT_MAX_PASS_THROUGH_BYTES, &MediaJob::new()).unwrap_err();
        assert_eq!(no_audio.code, MediaErrorCode::NoAudioTrack);

        let unknown_output = std::env::temp_dir().join(format!("ai-studio-unknown-{}.webm", rand::random::<u64>()));
        let unknown = normalize_media(&fixtures().join("unknown-codec.mkv"), &unknown_output, DEFAULT_MAX_PASS_THROUGH_BYTES, &MediaJob::new()).unwrap_err();
        assert_eq!(unknown.code, MediaErrorCode::UnsupportedCodec);
    }

    /// Verifies long streaming input never grows the resampler's pending buffer unboundedly.
    #[test]
    fn long_stream_resampling_keeps_pending_input_bounded() {
        let mut resampler = StreamResampler::new(44_100).unwrap();
        let chunk = vec![0.0; 441];
        let mut produced = 0usize;
        for _ in 0..6_000 {
            produced += resampler.push(&chunk).unwrap().len();
            if let StreamResampler::Rubato { inner, pending, .. } = &resampler {
                assert!(pending.len() < inner.input_frames_next());
            }
        }
        produced += resampler.finish().unwrap().len();
        assert_eq!(produced, 2_880_000);
    }

    /// Constructs a minimal mono 16-bit PCM WAV fixture in memory.
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