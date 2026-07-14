//! Asynchronous media normalization jobs producing bounded mono WebM/Opus output.

use std::collections::HashMap;
use std::convert::Infallible;
use std::fs::{self, File};
use std::io::{Read, Seek, SeekFrom, Write};
use std::path::{Path as FilePath, PathBuf};
use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};
use std::sync::{Arc, Mutex, RwLock};
use std::time::{Duration as StdDuration, Instant};

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
use symphonia::core::io::{MediaSource, MediaSourceStream};
use symphonia::core::meta::MetadataOptions;
use symphonia::core::formats::probe::Hint;
use symphonia::core::units::{TimeBase, Timestamp};
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

/// Minimum interval between non-terminal progress events in one phase.
const PROGRESS_EVENT_INTERVAL: StdDuration = StdDuration::from_secs(6);

/// Timestamp differences above this threshold are recorded as discontinuities.
const LARGE_DISCONTINUITY_MS: i64 = 1_000;

/// Maximum full-scale peak still treated as practical silence.
const SILENCE_MAX_PEAK_DBFS: f32 = -60.0;

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

    /// Whether the normalized audio exceeds the practical-silence threshold.
    pub has_audible_signal: bool,
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
    cancelled: Arc<AtomicBool>,

    /// The latest snapshot replayed to a newly connected SSE subscriber.
    current: Mutex<MediaJobEvent>,

    /// Fan-out channel for live state changes.
    events: broadcast::Sender<MediaJobEvent>,

    /// Last running progress publication, used to protect Blazor from render storms.
    last_progress: Mutex<Option<(MediaJobPhase, Instant)>>,
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
        Self {
            cancelled: Arc::new(AtomicBool::new(false)),
            current: Mutex::new(initial),
            events,
            last_progress: Mutex::new(None),
        }
    }

    /// Replaces the replay snapshot and notifies all live subscribers.
    fn publish(&self, event: MediaJobEvent) {
        *self.current.lock().unwrap() = event.clone();
        let _ = self.events.send(event);
    }

    /// Publishes running progress no more than once per interval and always on a phase change.
    fn publish_progress(&self, phase: MediaJobPhase, progress: Option<f64>) {
        let now = Instant::now();
        let mut last = self.last_progress.lock().unwrap();
        if last.is_some_and(|(last_phase, last_at)| last_phase == phase && now.duration_since(last_at) < PROGRESS_EVENT_INTERVAL) {
            return;
        }

        *last = Some((phase, now));
        drop(last);
        self.publish(MediaJobEvent { phase, progress, result: None, error: None });
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
        let started_at = Instant::now();
        log::info!("media job registered: job_id={completed_job_id}");
        let max_pass_through_bytes = request.max_pass_through_bytes.unwrap_or(DEFAULT_MAX_PASS_THROUGH_BYTES);
        let task_job = Arc::clone(&job);
        let result = tokio::task::spawn_blocking(move || normalize_media(&input_path, &output_path, max_pass_through_bytes, &task_job)).await;
        match result {
            Ok(Ok(result)) => {
                log::info!("media job completed: job_id={completed_job_id}, elapsed_ms={}", started_at.elapsed().as_millis());
                job.publish(MediaJobEvent {
                    phase: MediaJobPhase::Completed,
                    progress: Some(1.0),
                    result: Some(result),
                    error: None,
                });
            }

            Ok(Err(error)) if error.code == MediaErrorCode::Cancelled => {
                log::info!("media job cancelled: job_id={completed_job_id}, elapsed_ms={}", started_at.elapsed().as_millis());
                job.publish(MediaJobEvent {
                    phase: MediaJobPhase::Cancelled,
                    progress: None,
                    result: None,
                    error: None,
                });
            }

            Ok(Err(error)) => {
                log::error!("media job failed: job_id={completed_job_id}, code={:?}, diagnostic={}, elapsed_ms={}", error.code, error.message, started_at.elapsed().as_millis());
                job.publish(MediaJobEvent {
                    phase: MediaJobPhase::Failed,
                    progress: None,
                    result: None,
                    error: Some(error),
                });
            }

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

/// Shared byte position retained after the source is moved into Symphonia.
#[derive(Clone)]
struct SourceProgress {
    bytes_read: Arc<AtomicU64>,
    length: u64,
}

impl SourceProgress {
    /// Returns monotonically clamped sequential read progress.
    fn fraction(&self) -> Option<f64> {
        (self.length > 0).then(|| (self.bytes_read.load(Ordering::Relaxed) as f64 / self.length as f64).clamp(0.0, 0.99))
    }
}

/// File source that checks cancellation inside every read and seek operation.
struct CancellationMediaSource {
    file: File,
    cancelled: Arc<AtomicBool>,
    bytes_read: Arc<AtomicU64>,
    length: u64,
}

impl CancellationMediaSource {
    /// Wraps a regular file and exposes a progress handle to the transcoder.
    fn new(file: File, cancelled: Arc<AtomicBool>) -> std::io::Result<(Self, SourceProgress)> {
        let length = file.metadata()?.len();
        let bytes_read = Arc::new(AtomicU64::new(0));
        let progress = SourceProgress { bytes_read: Arc::clone(&bytes_read), length };
        Ok((Self { file, cancelled, bytes_read, length }, progress))
    }

    /// Converts cancellation into an interrupted I/O operation understood by the reader.
    fn check_cancelled(&self) -> std::io::Result<()> {
        if self.cancelled.load(Ordering::Relaxed) {
            Err(std::io::Error::new(std::io::ErrorKind::Interrupted, "media job cancelled"))
        } else {
            Ok(())
        }
    }
}

impl Read for CancellationMediaSource {
    fn read(&mut self, buffer: &mut [u8]) -> std::io::Result<usize> {
        self.check_cancelled()?;
        let count = self.file.read(buffer)?;
        self.bytes_read.fetch_add(count as u64, Ordering::Relaxed);
        self.check_cancelled()?;
        Ok(count)
    }
}

impl Seek for CancellationMediaSource {
    fn seek(&mut self, position: SeekFrom) -> std::io::Result<u64> {
        self.check_cancelled()?;
        let position = self.file.seek(position)?;
        self.check_cancelled()?;
        Ok(position)
    }
}

impl MediaSource for CancellationMediaSource {
    fn is_seekable(&self) -> bool {
        true
    }

    fn byte_len(&self) -> Option<u64> {
        Some(self.length)
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

    let file_size = input_path.metadata().map(|metadata| metadata.len()).unwrap_or(0);
    log::info!("media job started: input='{}', output='{}', size_bytes={}, detected_file_format={detected:?}", input_path.display(), output_path.display(), file_size);

    let file = File::open(input_path).map_err(|error| MediaError::new(MediaErrorCode::FileOpenFailed, error.to_string()))?;
    let (source, source_progress) = CancellationMediaSource::new(file, Arc::clone(&job.cancelled))
        .map_err(|error| MediaError::new(MediaErrorCode::FileOpenFailed, error.to_string()))?;
    let mss = MediaSourceStream::new(Box::new(source), Default::default());

    let mut hint = Hint::new();
    if let Some(extension) = input_path.extension().and_then(|value| value.to_str()) {
        hint.with_extension(extension);
    }

    let mut format = match symphonia::default::get_probe()
        .probe(&hint, mss, FormatOptions::default(), MetadataOptions::default())
    {
        Ok(format) => format,
        Err(_) if job.is_cancelled() => return Err(MediaError::new(MediaErrorCode::Cancelled, "The media job was cancelled.")),
        Err(error) => return Err(map_probe_error(error)),
    };

    let detected_format = format!("{detected:?} / {}", format.format_info().long_name);

    let tracks = format.tracks();
    for track in tracks {
        if let Some(params) = track.codec_params.as_ref().and_then(CodecParameters::audio) {
            log::info!(
                "media track: id={}, type={:?}, default={}, codec={}, sample_rate={:?}, channels={:?}, duration={:?}, time_base={:?}",
                track.id,
                track.track_type(),
                track.flags.contains(TrackFlags::DEFAULT),
                params.codec,
                params.sample_rate,
                params.channels.as_ref().map(|channels| channels.count()),
                track.duration,
                track.time_base,
            );
        } else {
            log::info!("media track: id={}, type={:?}, default={}, non_audio=true", track.id, track.track_type(), track.flags.contains(TrackFlags::DEFAULT));
        }
    }
    if !tracks.iter().any(is_audio_track) {
        return Err(MediaError::new(MediaErrorCode::NoAudioTrack, "The selected media file does not contain an audio track."));
    }

    let selected = select_audio_track(tracks)
        .ok_or_else(|| MediaError::new(MediaErrorCode::UnsupportedCodec, "None of the audio tracks uses a supported codec."))?;

    let track_id = selected.id;
    let track_delay = selected.delay.unwrap_or(0);
    let track_padding = selected.padding.unwrap_or(0);
    let track_time_base = selected.time_base;
    let params = selected.codec_params.as_ref().and_then(CodecParameters::audio).unwrap().clone();
    let detected_codec = if params.codec == CODEC_ID_OPUS { "opus".to_string() } else { format!("{}", params.codec) };
    let track_duration_ms = selected.num_frames.zip(params.sample_rate)
        .map(|(frames, rate)| frames.saturating_mul(1_000) / u64::from(rate))
        .or_else(|| selected.time_base.zip(selected.duration).and_then(|(time_base, duration)| {
            let timestamp = Timestamp::new(i64::try_from(duration.get()).ok()?);
            let time = time_base.calc_time(timestamp)?;
            Some((time.as_secs_f64() * 1000.0).max(0.0).round() as u64)
        }));
    let container_duration_ms = format.media_info().time_base.zip(format.media_info().duration).and_then(|(time_base, duration)| {
        let timestamp = Timestamp::new(i64::try_from(duration.get()).ok()?);
        let time = time_base.calc_time(timestamp)?;
        Some((time.as_secs_f64() * 1000.0).max(0.0).round() as u64)
    });
    let duration_ms = track_duration_ms.or(container_duration_ms).unwrap_or(0);
    log::info!(
        "media audio selection: track_id={}, default={}, track_duration_ms={:?}, container_duration_ms={:?}, progress_duration_ms={}",
        track_id,
        selected.flags.contains(TrackFlags::DEFAULT),
        track_duration_ms,
        container_duration_ms,
        duration_ms,
    );

    let channels = params.channels.as_ref().map(|value| value.count()).unwrap_or(0);
    let pass_through = is_webm_container(input_path)
        && tracks.len() == 1
        && selected.track_type() == Some(TrackType::Audio)
        && params.codec == CODEC_ID_OPUS
        && params.sample_rate == Some(OUTPUT_SAMPLE_RATE)
        && channels == 1
        && input_path.metadata().map(|metadata| metadata.len() <= max_pass_through_bytes).unwrap_or(false);
    log::info!("media normalization decision: track_id={track_id}, pass_through={pass_through}, codec={detected_codec}, channels={channels}");

    let partial_path = partial_path(output_path);
    if let Some(parent) = partial_path.parent() {
        fs::create_dir_all(parent).map_err(|error| MediaError::new(MediaErrorCode::OutputCreateFailed, error.to_string()))?;
    }

    let result = if pass_through {
        job.publish_progress(MediaJobPhase::Transcoding, Some(0.0));
        let analysis_context = SignalAnalysisContext {
            track_id,
            params: &params,
            track_delay,
            track_padding,
            expected_duration_ms: duration_ms,
            time_base: track_time_base,
            source_progress: &source_progress,
            job,
        };
        let signal = analyze_audio_signal(&mut *format, analysis_context)?;
        copy_with_cancellation(input_path, &partial_path, job)?;
        Ok(MediaJobResult {
            output_path: output_path.to_string_lossy().into_owned(),
            detected_format: detected_format.clone(),
            detected_codec,
            duration_ms,
            pass_through: true,
            has_audible_signal: signal.has_audible_signal(),
        })
    } else {
        job.publish_progress(MediaJobPhase::Transcoding, Some(0.0));

        let context = TranscodeContext {
            track_id,
            track_delay,
            track_padding,
            params,
            partial_path: &partial_path,
            output_path,
            detected_format,
            detected_codec,
            expected_duration_ms: duration_ms,
            time_base: track_time_base,
            source_progress,
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

/// Streaming peak measurement over normalized full-scale floating-point samples.
#[derive(Default)]
struct AudioPeakDetector {
    /// Highest absolute sample observed so far.
    max_amplitude: f32,
}

impl AudioPeakDetector {
    /// Includes one bounded sample block in the maximum-peak measurement.
    fn observe(&mut self, samples: &[f32]) {
        for sample in samples {
            let amplitude = sample.abs();
            self.max_amplitude = if amplitude.is_finite() {
                self.max_amplitude.max(amplitude)
            } else {
                f32::INFINITY
            };
        }
    }

    /// Returns whether any retained sample exceeds the configured silence ceiling.
    fn has_audible_signal(&self) -> bool {
        self.max_amplitude > 10.0_f32.powf(SILENCE_MAX_PEAK_DBFS / 20.0)
    }

    /// Returns the measured full-scale peak for diagnostics.
    fn max_peak_dbfs(&self) -> f32 {
        if self.max_amplitude == 0.0 {
            f32::NEG_INFINITY
        } else {
            20.0 * self.max_amplitude.log10()
        }
    }
}

/// Inputs required to scan an otherwise pass-through-compatible audio track.
struct SignalAnalysisContext<'a> {
    /// Selected track identifier.
    track_id: u32,

    /// Selected track codec parameters.
    params: &'a symphonia::core::codecs::audio::AudioCodecParameters,

    /// Leading decoded frames to discard.
    track_delay: u32,

    /// Trailing decoded frames to discard.
    track_padding: u32,

    /// Container duration used for progress reporting.
    expected_duration_ms: u64,

    /// Selected track timebase used for progress reporting.
    time_base: Option<TimeBase>,

    /// Sequential byte progress fallback when the track has no duration.
    source_progress: &'a SourceProgress,

    /// Cancellation and progress state for the job.
    job: &'a MediaJob,
}

/// Decodes an otherwise pass-through-compatible track solely to classify practical silence.
fn analyze_audio_signal(
    format: &mut dyn symphonia::core::formats::FormatReader,
    context: SignalAnalysisContext<'_>,
) -> Result<AudioPeakDetector, MediaError> {
    let mut decoder = StreamDecoder::new(context.params, context.track_delay)?;
    let mut detector = AudioPeakDetector::default();
    let mut decoded_tail = Vec::<f32>::new();
    let mut first_packet_pts = None::<i64>;
    let mut decoded_packets = 0u64;
    let mut last_progress = 0.0f64;

    loop {
        check_cancelled(context.job)?;
        let packet = match format.next_packet() {
            Ok(Some(packet)) => packet,
            Ok(None) => break,

            Err(SymphoniaError::ResetRequired) => return Err(MediaError::new(MediaErrorCode::StreamReset, "The media stream changed unexpectedly.")),
            Err(SymphoniaError::IoError(error)) if error.kind() == std::io::ErrorKind::Interrupted && context.job.is_cancelled() => {
                return Err(MediaError::new(MediaErrorCode::Cancelled, "The media job was cancelled."));
            }

            Err(SymphoniaError::IoError(error)) if error.kind() == std::io::ErrorKind::UnexpectedEof => break,
            Err(error) => return Err(MediaError::new(MediaErrorCode::DamagedContainer, format!("The media container is damaged: {error}"))),
        };

        if packet.track_id != context.track_id {
            continue;
        }

        let packet_pts = packet.pts.get();
        first_packet_pts.get_or_insert(packet_pts);
        let Some((mono, _)) = decoder.decode(&packet)? else { continue; };
        decoded_packets += 1;

        decoded_tail.extend_from_slice(&mono);
        let emit_len = decoded_tail.len().saturating_sub(context.track_padding as usize);
        if emit_len > 0 {
            detector.observe(&decoded_tail[..emit_len]);
            drop(decoded_tail.drain(..emit_len));
        }

        let timestamp_ms = packet_timestamp_ms(packet_pts, first_packet_pts, context.time_base);
        let progress = if context.expected_duration_ms > 0 {
            timestamp_ms.map(|current_ms| (current_ms as f64 / context.expected_duration_ms as f64).clamp(0.0, 0.99))
        } else {
            context.source_progress.fraction()
        };

        if let Some(progress) = progress {
            last_progress = last_progress.max(progress);
        }

        context.job.publish_progress(MediaJobPhase::Transcoding, progress.map(|_| last_progress));
    }

    if decoded_packets == 0 {
        return Err(MediaError::new(MediaErrorCode::InvalidAudioParameters, "The selected audio track did not yield decoded audio samples."));
    }

    log::info!(
        "media signal analysis completed: track_id={}, max_peak_dbfs={}, silence_threshold_dbfs={}, has_audible_signal={}",
        context.track_id,
        detector.max_peak_dbfs(),
        SILENCE_MAX_PEAK_DBFS,
        detector.has_audible_signal(),
    );

    Ok(detector)
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

    /// Selected track timebase used to align packet presentation timestamps.
    time_base: Option<TimeBase>,

    /// Sequential byte progress fallback when the selected track has no duration.
    source_progress: SourceProgress,

    /// Cancellation and progress state for the job.
    job: &'a MediaJob,
}

/// Decodes a selected track and writes timestamp-aligned 20 ms mono Opus frames.
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
    let mut signal = AudioPeakDetector::default();
    let mut resampler: Option<StreamResampler> = None;
    let mut decoded_tail = Vec::<f32>::new();
    let mut encoded = [0u8; 4_000];
    let mut produced_samples = 0u64;
    let mut first_packet_pts = None::<i64>;
    let mut last_packet_pts = None::<i64>;
    let mut decoded_packets = 0u64;
    let mut discarded_packets = 0u64;
    let mut decode_errors = 0u64;
    let mut discontinuities = 0u64;
    let mut last_progress = 0.0f64;

    loop {
        check_cancelled(context.job)?;
        let packet = match format.next_packet() {
            Ok(Some(packet)) => packet,
            Ok(None) => break,

            Err(SymphoniaError::ResetRequired) => return Err(MediaError::new(MediaErrorCode::StreamReset, "The media stream changed unexpectedly.")),
            Err(SymphoniaError::IoError(error)) if error.kind() == std::io::ErrorKind::Interrupted && context.job.is_cancelled() => {
                return Err(MediaError::new(MediaErrorCode::Cancelled, "The media job was cancelled."));
            }

            Err(SymphoniaError::IoError(error)) if error.kind() == std::io::ErrorKind::UnexpectedEof => break,
            Err(error) => return Err(MediaError::new(MediaErrorCode::DamagedContainer, format!("The media container is damaged: {error}"))),
        };

        if packet.track_id != context.track_id {
            discarded_packets += 1;
            continue;
        }

        let packet_pts = packet.pts.get();
        first_packet_pts.get_or_insert(packet_pts);
        last_packet_pts = Some(packet_pts);
        let Some((mono, sample_rate)) = decoder.decode(&packet)? else {
            decode_errors += 1;
            continue;
        };

        decoded_packets += 1;
        let stream_resampler = match resampler.as_mut() {
            Some(existing) if existing.input_rate() == sample_rate => existing,
            Some(_) => return Err(MediaError::new(MediaErrorCode::InvalidAudioParameters, "The decoded audio sample rate changed during the stream.")),

            None => {
                log::info!("media resampling: input_rate={sample_rate}, output_rate={OUTPUT_SAMPLE_RATE}, enabled={}", sample_rate != OUTPUT_SAMPLE_RATE);
                resampler.insert(StreamResampler::new(sample_rate)?)
            }
        };

        // Retain only the possible end padding so it can never be emitted prematurely.
        decoded_tail.extend_from_slice(&mono);
        let padding = context.track_padding as usize;
        let emit_len = decoded_tail.len().saturating_sub(padding);
        if emit_len > 0 {
            let emit: Vec<_> = decoded_tail.drain(..emit_len).collect();
            let resampled = stream_resampler.push(&emit)?;

            // FFT resamplers buffer across packet boundaries, so their returned samples no longer
            // begin at the current packet PTS. Native 48-kHz streams retain exact packet alignment.
            let desired_start = (sample_rate == OUTPUT_SAMPLE_RATE)
                .then(|| packet_output_start(packet_pts, first_packet_pts, context.time_base))
                .flatten();

            let current_start = produced_samples.saturating_add(pending.len() as u64);
            let previous_pending_len = pending.len();
            append_timestamp_aligned(
                &mut pending,
                &resampled,
                current_start,
                desired_start,
                context.track_id,
                packet_pts,
                &mut discontinuities,
            );

            signal.observe(&pending[previous_pending_len..]);
        }

        encode_complete_frames(&mut pending, &mut opus_encoder, &mut writer, &mut encoded, &mut produced_samples, context.job)?;

        let timestamp_ms = packet_timestamp_ms(packet_pts, first_packet_pts, context.time_base);
        let progress = if context.expected_duration_ms > 0 {
            timestamp_ms.map(|current_ms| (current_ms as f64 / context.expected_duration_ms as f64).clamp(0.0, 0.99))
        } else {
            context.source_progress.fraction()
        };

        if let Some(progress) = progress {
            last_progress = last_progress.max(progress);
        }

        context.job.publish_progress(MediaJobPhase::Transcoding, progress.map(|_| last_progress));
    }

    if let Some(stream_resampler) = resampler.as_mut() {
        // Discard the retained decoded tail (container padding), then flush the filter delay.
        let flushed = stream_resampler.finish()?;
        signal.observe(&flushed);
        pending.extend_from_slice(&flushed);
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

    let output_size = fs::metadata(context.partial_path).map(|metadata| metadata.len()).unwrap_or(0);
    let output_duration_ms = produced_samples.saturating_mul(1000) / u64::from(OUTPUT_SAMPLE_RATE);
    if context.expected_duration_ms > 0 && output_duration_ms.abs_diff(context.expected_duration_ms) > 2_000 {
        log::warn!(
            "media duration mismatch: track_id={}, expected_duration_ms={}, decoded_duration_ms={}",
            context.track_id,
            context.expected_duration_ms,
            output_duration_ms,
        );
    }

    log::info!(
        "media transcode completed: track_id={}, decoded_packets={}, discarded_packets={}, recoverable_decode_errors={}, first_pts={:?}, last_pts={:?}, discontinuities={}, output_bytes={}, duration_ms={}, max_peak_dbfs={}, silence_threshold_dbfs={}, has_audible_signal={}",
        context.track_id,
        decoded_packets,
        discarded_packets,
        decode_errors,
        first_packet_pts,
        last_packet_pts,
        discontinuities,
        output_size,
        output_duration_ms,
        signal.max_peak_dbfs(),
        SILENCE_MAX_PEAK_DBFS,
        signal.has_audible_signal(),
    );

    Ok(MediaJobResult {
        output_path: context.output_path.to_string_lossy().into_owned(),
        detected_format: context.detected_format,
        detected_codec: context.detected_codec,
        duration_ms: output_duration_ms,
        pass_through: false,
        has_audible_signal: signal.has_audible_signal(),
    })
}

/// Converts a packet PTS to its output sample offset relative to the first audio packet.
fn packet_output_start(packet_pts: i64, first_packet_pts: Option<i64>, time_base: Option<TimeBase>) -> Option<u64> {
    packet_timestamp_ms(packet_pts, first_packet_pts, time_base)
        .map(|milliseconds| milliseconds.saturating_mul(u64::from(OUTPUT_SAMPLE_RATE)) / 1_000)
}

/// Converts a packet PTS to milliseconds relative to the first selected-track packet.
fn packet_timestamp_ms(packet_pts: i64, first_packet_pts: Option<i64>, time_base: Option<TimeBase>) -> Option<u64> {
    let delta = packet_pts.checked_sub(first_packet_pts?)?;
    if delta < 0 {
        return Some(0);
    }

    let time = time_base?.calc_time(Timestamp::new(delta))?;
    Some((time.as_secs_f64() * 1_000.0).max(0.0).round() as u64)
}

/// Inserts silence for forward timestamp gaps and trims overlapping decoded samples.
fn append_timestamp_aligned(
    pending: &mut Vec<f32>,
    samples: &[f32],
    current_start: u64,
    desired_start: Option<u64>,
    track_id: u32,
    packet_pts: i64,
    discontinuities: &mut u64,
) {
    let Some(desired_start) = desired_start else {
        pending.extend_from_slice(samples);
        return;
    };

    let delta = i128::from(desired_start) - i128::from(current_start);
    let delta_ms = delta.saturating_mul(1_000) / i128::from(OUTPUT_SAMPLE_RATE);
    if delta_ms.unsigned_abs() >= LARGE_DISCONTINUITY_MS as u128 {
        *discontinuities += 1;
        log::warn!("audio timestamp discontinuity: track_id={track_id}, pts={packet_pts}, delta_ms={delta_ms}");
    }

    if delta > 0 {
        let silence = usize::try_from(delta).unwrap_or(usize::MAX);
        pending.resize(pending.len().saturating_add(silence), 0.0);
        pending.extend_from_slice(samples);
    } else {
        let overlap = usize::try_from(delta.unsigned_abs()).unwrap_or(usize::MAX).min(samples.len());
        pending.extend_from_slice(&samples[overlap..]);
    }
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
    use std::num::NonZeroU32;
    use tokio::sync::broadcast::error::TryRecvError;
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

    /// Verifies the configured dBFS ceiling is inclusive and a higher peak is audible.
    #[test]
    fn practical_silence_uses_the_configured_maximum_peak() {
        let threshold = 10.0_f32.powf(SILENCE_MAX_PEAK_DBFS / 20.0);
        let mut detector = AudioPeakDetector::default();
        detector.observe(&[-threshold, threshold]);
        assert!(!detector.has_audible_signal());

        detector.observe(&[threshold * 1.01]);
        assert!(detector.has_audible_signal());
    }

    /// Verifies timestamp gaps become silence and overlaps do not duplicate decoded samples.
    #[test]
    fn timestamp_alignment_inserts_gaps_and_trims_overlaps() {
        let mut discontinuities = 0;
        let mut gap = vec![1.0; 960];
        append_timestamp_aligned(&mut gap, &vec![2.0; 960], 960, Some(1_920), 7, 40, &mut discontinuities);
        assert_eq!(gap.len(), 2_880);
        assert!(gap[960..1_920].iter().all(|sample| *sample == 0.0));
        assert!(gap[1_920..].iter().all(|sample| *sample == 2.0));

        let mut overlap = vec![1.0; 960];
        append_timestamp_aligned(&mut overlap, &vec![2.0; 960], 960, Some(480), 7, 10, &mut discontinuities);
        assert_eq!(overlap.len(), 1_440);
        assert!(overlap[960..].iter().all(|sample| *sample == 2.0));
    }

    /// Verifies packet progress uses a selected-track-relative time axis.
    #[test]
    fn packet_timestamps_are_relative_to_the_first_audio_packet() {
        let time_base = TimeBase::new(NonZeroU32::new(1).unwrap(), NonZeroU32::new(1_000).unwrap());
        assert_eq!(packet_timestamp_ms(5_250, Some(5_000), Some(time_base)), Some(250));
        assert_eq!(packet_output_start(5_250, Some(5_000), Some(time_base)), Some(12_000));
    }

    /// Verifies running updates are throttled while a phase transition remains immediate.
    #[test]
    fn running_progress_is_throttled_but_phase_changes_are_immediate() {
        let job = MediaJob::new();
        let mut events = job.events.subscribe();
        job.publish_progress(MediaJobPhase::Transcoding, Some(0.1));
        job.publish_progress(MediaJobPhase::Transcoding, Some(0.2));
        job.publish_progress(MediaJobPhase::Probing, Some(0.0));

        assert_eq!(events.try_recv().unwrap().progress, Some(0.1));
        assert_eq!(events.try_recv().unwrap().phase, MediaJobPhase::Probing);
        assert!(matches!(events.try_recv(), Err(TryRecvError::Empty)));
    }

    /// Verifies cancellation interrupts source reads rather than waiting for another packet.
    #[test]
    fn cancellation_aware_source_interrupts_reads() {
        let path = std::env::temp_dir().join(format!("ai-studio-source-cancel-{}", rand::random::<u64>()));
        fs::write(&path, vec![0u8; COPY_BLOCK_BYTES * 2]).unwrap();
        let cancelled = Arc::new(AtomicBool::new(false));
        let (mut source, _) = CancellationMediaSource::new(File::open(&path).unwrap(), Arc::clone(&cancelled)).unwrap();
        cancelled.store(true, Ordering::Relaxed);
        let error = source.read(&mut [0u8; 16]).unwrap_err();
        let _ = fs::remove_file(path);
        assert_eq!(error.kind(), std::io::ErrorKind::Interrupted);
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
        assert!(!result.has_audible_signal);
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
        assert!(!result.has_audible_signal);
        let _ = fs::remove_dir_all(directory);
    }

    /// Verifies an above-threshold PCM peak survives normalization classification.
    #[test]
    fn audible_wav_is_not_classified_as_silence() {
        let directory = std::env::temp_dir().join(format!("ai-studio-media-audible-{}", rand::random::<u64>()));
        fs::create_dir_all(&directory).unwrap();
        let input = directory.join("input.wav");
        let output = directory.join("output.webm");
        fs::write(&input, wav_constant(48_000, 960, 1_000)).unwrap();
        let result = normalize_media(&input, &output, DEFAULT_MAX_PASS_THROUGH_BYTES, &MediaJob::new()).unwrap();
        assert!(result.has_audible_signal);
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
        wav_constant(sample_rate, samples, 0)
    }

    /// Constructs a minimal mono 16-bit PCM WAV containing one constant sample value.
    fn wav_constant(sample_rate: u32, samples: u32, sample: i16) -> Vec<u8> {
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
        for _ in 0..samples {
            wav.extend_from_slice(&sample.to_le_bytes());
        }
        wav
    }
}
