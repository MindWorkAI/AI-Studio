//! Local image preparation: decode a file, apply the size policy, and return it as a Data URL.
//!
//! This module is deliberately free of any feature-specific behavior so that every part of
//! AI Studio that needs an embeddable image can use it. The size policy is a single maximum edge
//! length; callers that want the original bytes pass `optimize = false`.
//!
//! Images for AI models take a route of their own, see [`prepare_image_for_model`]: they always
//! leave as JPEG or PNG and have to fit into the byte budget of the provider.

use std::io::Cursor;
use std::path::Path;

use axum::Json;
use axum::http::StatusCode;
use base64::{Engine as _, engine::general_purpose};
use image::codecs::jpeg::JpegEncoder;
use image::imageops::FilterType;
use image::{DynamicImage, ImageDecoder, ImageError, ImageFormat, ImageReader, Limits, Rgb, RgbImage};
use log::{info, warn};
use serde::{Deserialize, Serialize};
use tokio::sync::Semaphore;

/// The longest edge an optimized image may have. Larger images are scaled down proportionally.
const MAX_EDGE_PIXELS: u32 = 2_560;

/// The quality used when re-encoding JPEG images. Pinned so that repeated runs are byte-identical.
const JPEG_QUALITY: u8 = 85;

/// The request to prepare one local image file.
#[derive(Debug, Deserialize)]
pub struct PrepareImageRequest {
    /// The absolute path of the image file to read.
    path: String,

    /// Whether the size policy and re-encoding are applied. When false, the original bytes are used.
    optimize: bool,
}

/// The prepared image together with the dimensions the caller can lay out against.
#[derive(Debug, Serialize)]
#[serde(rename_all = "snake_case")]
pub struct PrepareImageResponse {
    /// The complete `data:` URL, ready to embed.
    data_url: String,

    /// The MIME type matching the source format.
    mime_type: String,

    /// The width of the prepared image in pixels.
    width: u32,

    /// The height of the prepared image in pixels.
    height: u32,

    /// Whether the size policy actually scaled the image down.
    was_resized: bool,
}

/// Decodes one supported image, applies the size policy, and returns a Data URL.
///
/// Decoding runs on a blocking worker because it is CPU-bound and would otherwise stall the
/// async runtime for large images.
pub async fn prepare_image(
    Json(request): Json<PrepareImageRequest>,
) -> Result<Json<PrepareImageResponse>, (StatusCode, String)> {
    tokio::task::spawn_blocking(move || prepare_image_sync(&request))
        .await
        .map_err(|error| {
            (
                StatusCode::INTERNAL_SERVER_ERROR,
                format!("The image worker failed: {error}"),
            )
        })?
        .map(Json)
}

/// Performs the blocking part of [`prepare_image`].
///
/// Only absolute paths to existing files are accepted, and the decoded format has to match the
/// file extension. Rejecting a mismatch keeps a file that merely claims to be an image from being
/// embedded under a MIME type derived from its name.
fn prepare_image_sync(
    request: &PrepareImageRequest,
) -> Result<PrepareImageResponse, (StatusCode, String)> {
    let path = Path::new(&request.path);
    if !path.is_absolute() || !path.is_file() {
        return Err((
            StatusCode::BAD_REQUEST,
            "The image path is not an accessible absolute file path.".to_string(),
        ));
    }

    let format = supported_format(path)?;

    //
    // Upright as the photo was taken: re-encoding drops the EXIF data which told a viewer how to
    // turn it. The original bytes keep that note, and browsers apply it, so the dimensions we
    // report fit both ways:
    //
    let decoded = decode_oriented(path, format).map_err(|failure| (StatusCode::BAD_REQUEST, failure.message))?;

    let original_width = decoded.width();
    let original_height = decoded.height();
    let should_resize = request.optimize && original_width.max(original_height) > MAX_EDGE_PIXELS;
    
    let prepared = if should_resize {
        resize_to_max_edge(decoded)
    } else {
        decoded
    };
    
    let width = prepared.width();
    let height = prepared.height();
    
    let bytes = if request.optimize {
        encode(&prepared, format)?
    } else {
        std::fs::read(path).map_err(|error| {
            (
                StatusCode::BAD_REQUEST,
                format!("The image could not be read: {error}"),
            )
        })?
    };
    
    let mime_type = match format {
        ImageFormat::Jpeg => "image/jpeg",
        ImageFormat::Png => "image/png",
        ImageFormat::WebP => "image/webp",
        _ => unreachable!(),
    }
    .to_string();

    Ok(PrepareImageResponse {
        data_url: format!(
            "data:{mime_type};base64,{}",
            general_purpose::STANDARD.encode(bytes)
        ),
        mime_type,
        width,
        height,
        was_resized: should_resize,
    })
}

/// Maps a file extension to the one image format AI Studio embeds.
///
/// The result is only the expected format; [`prepare_image_sync`] still verifies it against the
/// actual file content.
fn supported_format(path: &Path) -> Result<ImageFormat, (StatusCode, String)> {
    match path
        .extension()
        .and_then(|extension| extension.to_str())
        .map(str::to_ascii_lowercase)
        .as_deref()
    {
        Some("jpg" | "jpeg") => Ok(ImageFormat::Jpeg),
        Some("png") => Ok(ImageFormat::Png),
        Some("webp") => Ok(ImageFormat::WebP),
        
        _ => Err((
            StatusCode::BAD_REQUEST,
            "Images must be PNG, JPEG, or WebP files.".to_string(),
        )),
    }
}

/// Scales an image down so that its longest edge equals [`MAX_EDGE_PIXELS`].
///
/// The aspect ratio is preserved, and both edges stay at least one pixel wide.
fn resize_to_max_edge(image: DynamicImage) -> DynamicImage {
    resize_to_longest_edge(image, MAX_EDGE_PIXELS)
}

/// Scales an image so that its longest edge equals the given length.
///
/// The aspect ratio is preserved, and both edges stay at least one pixel wide.
fn resize_to_longest_edge(image: DynamicImage, longest_edge: u32) -> DynamicImage {
    let width = image.width();
    let height = image.height();
    let scale = longest_edge as f64 / width.max(height) as f64;
    let target_width = (width as f64 * scale).round().max(1.0) as u32;
    let target_height = (height as f64 * scale).round().max(1.0) as u32;
    image.resize_exact(target_width, target_height, FilterType::Lanczos3)
}

/// Encodes a prepared image back into its source format.
///
/// JPEG uses the pinned [`JPEG_QUALITY`] so that the same input always produces the same bytes,
/// which keeps artifact hashes stable across runs.
fn encode(image: &DynamicImage, format: ImageFormat) -> Result<Vec<u8>, (StatusCode, String)> {
    let mut bytes = Vec::new();
    match format {
        ImageFormat::Jpeg => JpegEncoder::new_with_quality(&mut bytes, JPEG_QUALITY)
            .encode_image(image)
            .map_err(|error| {
                (
                    StatusCode::INTERNAL_SERVER_ERROR,
                    format!("The JPEG image could not be encoded: {error}"),
                )
            })?,
            
        ImageFormat::Png | ImageFormat::WebP => image
            .write_to(&mut Cursor::new(&mut bytes), format)
            .map_err(|error| {
                (
                    StatusCode::INTERNAL_SERVER_ERROR,
                    format!("The image could not be encoded: {error}"),
                )
            })?,
            
        _ => unreachable!(),
    }

    Ok(bytes)
}

//
// Preparation for AI models
//

/// The JPEG qualities tried, in this order, while an image does not fit into its byte budget.
///
/// Below the last one, compression artifacts start to blur small text such as the labels of a
/// diagram. A smaller image keeps such text more legible than a still lower quality does, which
/// is why the budget shrinks the image instead from there on.
const BUDGET_JPEG_QUALITIES: [u8; 3] = [JPEG_QUALITY, 70, 55];

/// How much the longest edge shrinks in each round, once no quality fits into the budget.
const BUDGET_EDGE_FACTOR: f64 = 0.75;

/// The longest edge the budget may shrink an image to. Models no longer read the details of a
/// photo or a document below that, so such an image is refused instead of sent unreadable.
const MIN_BUDGET_EDGE_PIXELS: u32 = 1_024;

/// How many images are prepared for models at the same time.
///
/// A request with several photos would otherwise decode all of them at once, and every decoded
/// photo holds a pixel buffer of up to the decoder's limit of 512 MiB. That is too much for small
/// devices such as a Raspberry Pi, while two at a time still keep a multi-core computer busy.
const MAX_PARALLEL_MODEL_PREPARATIONS: usize = 2;

/// Lets no more than [`MAX_PARALLEL_MODEL_PREPARATIONS`] images be prepared at once.
static MODEL_PREPARATION_SLOTS: Semaphore = Semaphore::const_new(MAX_PARALLEL_MODEL_PREPARATIONS);

/// Why an image could not be prepared, so the .NET app can tell the user in their language.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
#[serde(rename_all = "SCREAMING_SNAKE_CASE")]
pub enum ImageErrorCode {
    /// The image was prepared.
    None,

    /// The path is not absolute, or no file exists there.
    FileNotFound,

    /// The file exists, but could not be opened, e.g., for lack of permission.
    FileNotReadable,

    /// AI Studio cannot decode this image format, e.g., HEIC, AVIF, or SVG.
    UnsupportedFormat,

    /// The content of the file does not match its extension.
    ContentMismatch,

    /// The file claims the right format but is damaged or uses a variant we cannot decode.
    NotDecodable,

    /// Decoding the image would need more memory than the decoder allows.
    TooManyPixels,

    /// Not even the smallest image and lowest quality we accept fit into the byte budget.
    BudgetNotReachable,

    /// Any failure which does not carry a code of its own, e.g., a failed encoder.
    Internal,
}

/// A failed preparation: the code for the app and a message for the log.
#[derive(Debug)]
struct ImageFailure {
    code: ImageErrorCode,
    message: String,
}

impl ImageFailure {
    fn new(code: ImageErrorCode, message: impl Into<String>) -> Self {
        Self { code, message: message.into() }
    }
}

/// The request to prepare one local image file for an AI model.
#[derive(Debug, Deserialize)]
pub struct PrepareModelImageRequest {
    /// The absolute path of the image file to read.
    path: String,

    /// The largest number of bytes the encoded image may have, before base64.
    max_bytes: u64,
}

/// The image as it goes to the model, or why it cannot go.
#[derive(Debug, Serialize)]
#[serde(rename_all = "snake_case")]
pub struct PrepareModelImageResponse {
    /// `None` when the image was prepared; then all other fields describe it.
    error_code: ImageErrorCode,

    /// The encoded image as base64, without a `data:` prefix. The MIME type travels next to it.
    base64: String,

    /// Either `image/jpeg` or `image/png`, describing the encoded bytes.
    mime_type: String,

    /// The width of the prepared image in pixels.
    width: u32,

    /// The height of the prepared image in pixels.
    height: u32,

    /// Whether the image was scaled down, by the size policy or to fit into the byte budget.
    was_resized: bool,
}

impl PrepareModelImageResponse {
    fn failure(code: ImageErrorCode) -> Self {
        Self {
            error_code: code,
            base64: String::new(),
            mime_type: String::new(),
            width: 0,
            height: 0,
            was_resized: false,
        }
    }
}

/// The encoding an image leaves in for a model.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum ModelEncoding {
    Jpeg,
    Png,
}

/// An image encoded for a model, before base64.
struct EncodedModelImage {
    bytes: Vec<u8>,
    encoding: ModelEncoding,

    /// The JPEG quality which fit into the budget; PNG has none.
    jpeg_quality: Option<u8>,

    width: u32,
    height: u32,
    was_resized: bool,
}

/// Prepares one local image for an AI model.
///
/// Every image is decoded, turned upright according to its EXIF orientation, scaled down to
/// [`MAX_EDGE_PIXELS`], and encoded anew as JPEG or PNG, the two formats every provider and every
/// self-hosted engine accepts. Encoding anew also drops the metadata of the file, among them the
/// location where a photo was taken. When the result exceeds the byte budget, the quality and then
/// the size go down until it fits.
///
/// The answer is always a success at the HTTP level; `error_code` tells whether the image was
/// prepared. Decoding runs on a blocking worker, and only a few images at a time, see
/// [`MAX_PARALLEL_MODEL_PREPARATIONS`].
pub async fn prepare_image_for_model(
    Json(request): Json<PrepareModelImageRequest>,
) -> Json<PrepareModelImageResponse> {
    //
    // The semaphore is never closed, so acquiring it cannot fail. Should it fail anyway, the
    // image is prepared without a slot rather than not at all:
    //
    let _slot = MODEL_PREPARATION_SLOTS.acquire().await.ok();
    let response = tokio::task::spawn_blocking(move || prepare_image_for_model_sync(&request))
        .await
        .unwrap_or_else(|error| {
            warn!("The image worker for an AI model failed: {error}");
            PrepareModelImageResponse::failure(ImageErrorCode::Internal)
        });

    Json(response)
}

/// Performs the blocking part of [`prepare_image_for_model`].
fn prepare_image_for_model_sync(request: &PrepareModelImageRequest) -> PrepareModelImageResponse {
    match prepare_for_model(Path::new(&request.path), request.max_bytes) {
        Ok(encoded) => PrepareModelImageResponse {
            error_code: ImageErrorCode::None,
            base64: general_purpose::STANDARD.encode(&encoded.bytes),
            mime_type: match encoded.encoding {
                ModelEncoding::Jpeg => "image/jpeg",
                ModelEncoding::Png => "image/png",
            }
            .to_string(),
            width: encoded.width,
            height: encoded.height,
            was_resized: encoded.was_resized,
        },

        Err(failure) => {
            warn!("Preparing the image '{}' for an AI model failed ({:?}): {}", request.path, failure.code, failure.message);
            PrepareModelImageResponse::failure(failure.code)
        }
    }
}

/// Decodes, orients, scales, and encodes one image for a model.
fn prepare_for_model(path: &Path, max_bytes: u64) -> Result<EncodedModelImage, ImageFailure> {
    if !path.is_absolute() || !path.is_file() {
        return Err(ImageFailure::new(ImageErrorCode::FileNotFound, "The image path is not an accessible absolute file path."));
    }

    let format = model_input_format(path)?;
    let source_bytes = std::fs::metadata(path).map(|metadata| metadata.len()).unwrap_or(0);
    let decoded = decode_oriented(path, format)?;
    let (source_width, source_height) = (decoded.width(), decoded.height());
    let was_resized = source_width.max(source_height) > MAX_EDGE_PIXELS;
    let image = if was_resized {
        resize_to_max_edge(decoded)
    } else {
        decoded
    };

    let encoding = model_encoding(format, &image);
    let encoded = fit_into_budget(image, encoding, max_bytes, was_resized)?;
    let encoded_bytes = encoded.bytes.len() as u64;
    info!(
        "image prepared for model: path='{}', source={format:?} {source_width}x{source_height} {}, result={:?} {}x{} {} ({} of the original), quality={}, budget={}, resized={}",
        path.display(),
        megabytes(source_bytes),
        encoded.encoding,
        encoded.width,
        encoded.height,
        megabytes(encoded_bytes),
        percentage(encoded_bytes, source_bytes),
        encoded.jpeg_quality.map_or_else(|| "lossless".to_string(), |quality| quality.to_string()),
        megabytes(max_bytes),
        encoded.was_resized,
    );

    Ok(encoded)
}

/// Writes a number of bytes as megabytes, for the log.
fn megabytes(bytes: u64) -> String {
    format!("{:.2} MB", bytes as f64 / 1_000_000.0)
}

/// Writes how large a part is compared to the whole, for the log.
fn percentage(part: u64, whole: u64) -> String {
    if whole == 0 {
        return "n/a".to_string();
    }

    format!("{:.0} %", part as f64 * 100.0 / whole as f64)
}

/// Maps a file extension to the image format we expect for a model input.
///
/// The result is only the expected format; [`decode_oriented`] still verifies it against the
/// actual file content.
fn model_input_format(path: &Path) -> Result<ImageFormat, ImageFailure> {
    match path
        .extension()
        .and_then(|extension| extension.to_str())
        .map(str::to_ascii_lowercase)
        .as_deref()
    {
        Some("jpg" | "jpeg") => Ok(ImageFormat::Jpeg),
        Some("png") => Ok(ImageFormat::Png),
        Some("webp") => Ok(ImageFormat::WebP),
        Some("gif") => Ok(ImageFormat::Gif),
        Some("bmp") => Ok(ImageFormat::Bmp),
        Some("tif" | "tiff") => Ok(ImageFormat::Tiff),

        _ => Err(ImageFailure::new(ImageErrorCode::UnsupportedFormat, "Images for AI models must be JPEG, PNG, WebP, GIF, BMP, or TIFF files.")),
    }
}

/// Decodes an image of the expected format and turns it upright according to its EXIF orientation.
///
/// Phones store a photo as the sensor saw it and only note in the EXIF data how to turn it.
/// Without applying that note, a photo taken upright would lie on its side once encoded anew,
/// because the new file no longer carries the note. The content has to match the expected format,
/// so that a file which merely claims to be an image is not decoded as one.
fn decode_oriented(path: &Path, format: ImageFormat) -> Result<DynamicImage, ImageFailure> {
    let reader = ImageReader::open(path)
        .and_then(|reader| reader.with_guessed_format())
        .map_err(|error| ImageFailure::new(ImageErrorCode::FileNotReadable, format!("The image could not be opened: {error}")))?;

    if reader.format() != Some(format) {
        return Err(ImageFailure::new(ImageErrorCode::ContentMismatch, "The image content does not match its file extension."));
    }

    let mut decoder = reader.into_decoder().map_err(decoding_failure)?;
    let orientation = decoder.orientation().map_err(decoding_failure)?;

    //
    // The same check ImageReader::decode makes: the pixel buffer must fit into the decoder's
    // memory limit. into_decoder only checks the dimensions.
    //
    Limits::default().reserve(decoder.total_bytes()).map_err(decoding_failure)?;

    let mut image = DynamicImage::from_decoder(decoder).map_err(decoding_failure)?;
    image.apply_orientation(orientation);
    Ok(image)
}

/// Tells a decoder which hit its memory limit apart from any other decoding failure.
fn decoding_failure(error: ImageError) -> ImageFailure {
    match error {
        ImageError::Limits(_) => ImageFailure::new(ImageErrorCode::TooManyPixels, format!("The image is too large to decode: {error}")),
        _ => ImageFailure::new(ImageErrorCode::NotDecodable, format!("The image could not be decoded: {error}")),
    }
}

/// Chooses the encoding of an image for a model.
///
/// JPEG and PNG keep their format: PNG holds screenshots and diagrams without loss, and JPEG
/// holds photos. Any other format becomes PNG when it uses transparency, which JPEG cannot hold,
/// and JPEG otherwise.
fn model_encoding(source: ImageFormat, image: &DynamicImage) -> ModelEncoding {
    match source {
        ImageFormat::Jpeg => ModelEncoding::Jpeg,
        ImageFormat::Png => ModelEncoding::Png,
        _ if uses_transparency(image) => ModelEncoding::Png,
        _ => ModelEncoding::Jpeg,
    }
}

/// Whether any pixel of the image is not fully opaque.
///
/// An alpha channel alone says nothing: GIF decodes into RGBA, and many files carry a channel in
/// which every pixel is opaque.
fn uses_transparency(image: &DynamicImage) -> bool {
    match image {
        DynamicImage::ImageLumaA8(buffer) => buffer.pixels().any(|pixel| pixel.0[1] < u8::MAX),
        DynamicImage::ImageRgba8(buffer) => buffer.pixels().any(|pixel| pixel.0[3] < u8::MAX),
        DynamicImage::ImageLumaA16(buffer) => buffer.pixels().any(|pixel| pixel.0[1] < u16::MAX),
        DynamicImage::ImageRgba16(buffer) => buffer.pixels().any(|pixel| pixel.0[3] < u16::MAX),
        DynamicImage::ImageRgba32F(buffer) => buffer.pixels().any(|pixel| pixel.0[3] < 1.0),
        _ => image.color().has_alpha() && image.to_rgba8().pixels().any(|pixel| pixel.0[3] < u8::MAX),
    }
}

/// Encodes the image and lowers quality and size until it fits into the byte budget.
fn fit_into_budget(image: DynamicImage, encoding: ModelEncoding, max_bytes: u64, was_resized: bool) -> Result<EncodedModelImage, ImageFailure> {
    if encoding == ModelEncoding::Png {
        let png = to_png_pixels(&image);
        let bytes = encode_model_image(&png, ModelEncoding::Png, JPEG_QUALITY)?;
        if bytes.len() as u64 <= max_bytes {
            return Ok(EncodedModelImage { bytes, encoding, jpeg_quality: None, width: png.width(), height: png.height(), was_resized });
        }
    }

    //
    // From here on, the image goes as JPEG. A PNG which does not fit loses its transparency,
    // which is laid onto white, the background most documents and chats show it on:
    //
    let mut current = to_jpeg_pixels(&image);
    drop(image);

    let mut was_resized = was_resized;
    loop {
        for quality in BUDGET_JPEG_QUALITIES {
            let bytes = encode_model_image(&current, ModelEncoding::Jpeg, quality)?;
            if bytes.len() as u64 <= max_bytes {
                return Ok(EncodedModelImage { bytes, encoding: ModelEncoding::Jpeg, jpeg_quality: Some(quality), width: current.width(), height: current.height(), was_resized });
            }
        }

        let longest_edge = current.width().max(current.height());
        if longest_edge <= MIN_BUDGET_EDGE_PIXELS {
            return Err(ImageFailure::new(ImageErrorCode::BudgetNotReachable, format!("The image does not fit into {max_bytes} bytes, not even with {longest_edge} pixels on its longest edge.")));
        }

        let next_edge = ((longest_edge as f64 * BUDGET_EDGE_FACTOR).round() as u32).max(MIN_BUDGET_EDGE_PIXELS);
        current = resize_to_longest_edge(current, next_edge);
        was_resized = true;
    }
}

/// Brings an image into 8 bits per channel for PNG, keeping the transparency where it is used.
fn to_png_pixels(image: &DynamicImage) -> DynamicImage {
    let is_gray = !image.color().has_color();
    match (is_gray, uses_transparency(image)) {
        (true, true) => DynamicImage::ImageLumaA8(image.to_luma_alpha8()),
        (true, false) => DynamicImage::ImageLuma8(image.to_luma8()),
        (false, true) => DynamicImage::ImageRgba8(image.to_rgba8()),
        (false, false) => DynamicImage::ImageRgb8(image.to_rgb8()),
    }
}

/// Brings an image into 8 bits per channel for JPEG, which has no alpha channel.
fn to_jpeg_pixels(image: &DynamicImage) -> DynamicImage {
    if uses_transparency(image) {
        return flatten_onto_white(image);
    }

    if image.color().has_color() {
        DynamicImage::ImageRgb8(image.to_rgb8())
    } else {
        DynamicImage::ImageLuma8(image.to_luma8())
    }
}

/// Lays a partly transparent image onto a white background.
///
/// Dropping the alpha channel alone would keep whatever color the transparent pixels happen to
/// hold, which is often black.
fn flatten_onto_white(image: &DynamicImage) -> DynamicImage {
    let rgba = image.to_rgba8();
    let mut flattened = RgbImage::new(rgba.width(), rgba.height());
    for (source, target) in rgba.pixels().zip(flattened.pixels_mut()) {
        let [red, green, blue, alpha] = source.0;
        let alpha = u16::from(alpha);
        let blend = |channel: u8| ((u16::from(channel) * alpha + 255 * (255 - alpha) + 127) / 255) as u8;
        *target = Rgb([blend(red), blend(green), blend(blue)]);
    }

    DynamicImage::ImageRgb8(flattened)
}

/// Encodes an image whose pixels already suit the encoding.
fn encode_model_image(image: &DynamicImage, encoding: ModelEncoding, jpeg_quality: u8) -> Result<Vec<u8>, ImageFailure> {
    let mut bytes = Vec::new();
    let result = match encoding {
        ModelEncoding::Jpeg => JpegEncoder::new_with_quality(&mut bytes, jpeg_quality).encode_image(image),
        ModelEncoding::Png => image.write_to(&mut Cursor::new(&mut bytes), ImageFormat::Png),
    };

    result.map_err(|error| ImageFailure::new(ImageErrorCode::Internal, format!("The image could not be encoded: {error}")))?;
    Ok(bytes)
}

#[cfg(test)]
mod tests {
    use super::*;

    /// A path no other test works on.
    ///
    /// The name is counted rather than timed. The clock looks unique but is not: these tests run
    /// in parallel, and two of them reading it within the same tick got the same path, so one
    /// removed the file the other was still working on. That failed about one run in twelve, and
    /// never when the tests ran one after another.
    fn temporary_image_path(extension: &str) -> std::path::PathBuf {
        use std::sync::atomic::{AtomicU32, Ordering};

        //
        // The process id is part of it as well, so that two test runs at once stay apart.
        //
        static NEXT_IMAGE: AtomicU32 = AtomicU32::new(0);
        let unique = NEXT_IMAGE.fetch_add(1, Ordering::Relaxed);
        std::env::temp_dir().join(format!("mwai-visual-briefing-test-{}-{unique}.{extension}", std::process::id()))
    }

    #[test]
    fn rejects_unsupported_visual_asset_extension() {
        let issue = supported_format(Path::new("/tmp/asset.gif")).unwrap_err();
        assert_eq!(issue.0, StatusCode::BAD_REQUEST);
    }

    #[test]
    fn keeps_supported_formats_stable() {
        assert_eq!(
            supported_format(Path::new("/tmp/asset.jpeg")).unwrap(),
            ImageFormat::Jpeg
        );
        assert_eq!(
            supported_format(Path::new("/tmp/asset.png")).unwrap(),
            ImageFormat::Png
        );
        assert_eq!(
            supported_format(Path::new("/tmp/asset.webp")).unwrap(),
            ImageFormat::WebP
        );
    }

    #[test]
    fn serializes_response_in_snake_case_for_the_rust_service_contract() {
        let response = PrepareImageResponse {
            data_url: "data:image/jpeg;base64,/9j/".to_string(),
            mime_type: "image/jpeg".to_string(),
            width: 17,
            height: 11,
            was_resized: false,
        };
        let json = serde_json::to_value(response).unwrap();
        assert_eq!(json["data_url"], "data:image/jpeg;base64,/9j/");
        assert_eq!(json["mime_type"], "image/jpeg");
        assert_eq!(json["width"], 17);
        assert_eq!(json["height"], 11);
        assert_eq!(json["was_resized"], false);
        assert!(json.get("dataUrl").is_none());
        assert!(json.get("mimeType").is_none());
        assert!(json.get("wasResized").is_none());
    }

    #[test]
    fn disabled_optimization_preserves_original_bytes() {
        let path = temporary_image_path("png");
        DynamicImage::new_rgb8(4, 3)
            .save_with_format(&path, ImageFormat::Png)
            .unwrap();
        let original = std::fs::read(&path).unwrap();
        let response = prepare_image_sync(&PrepareImageRequest {
            path: path.to_string_lossy().into_owned(),
            optimize: false,
        })
        .unwrap();
        let encoded = response.data_url.split_once(',').unwrap().1;
        assert_eq!(general_purpose::STANDARD.decode(encoded).unwrap(), original);
        assert!(!response.was_resized);
        std::fs::remove_file(path).unwrap();
    }

    #[test]
    fn optimization_resizes_only_images_over_the_maximum_edge() {
        let path = temporary_image_path("png");
        DynamicImage::new_rgb8(MAX_EDGE_PIXELS + 1, 1)
            .save_with_format(&path, ImageFormat::Png)
            .unwrap();
        let response = prepare_image_sync(&PrepareImageRequest {
            path: path.to_string_lossy().into_owned(),
            optimize: true,
        })
        .unwrap();
        assert_eq!(response.width, MAX_EDGE_PIXELS);
        assert_eq!(response.height, 1);
        assert!(response.was_resized);
        std::fs::remove_file(path).unwrap();
    }

    #[test]
    fn optimization_keeps_images_at_the_maximum_edge_unchanged() {
        let path = temporary_image_path("png");
        DynamicImage::new_rgb8(MAX_EDGE_PIXELS, 2)
            .save_with_format(&path, ImageFormat::Png)
            .unwrap();
        let response = prepare_image_sync(&PrepareImageRequest {
            path: path.to_string_lossy().into_owned(),
            optimize: true,
        })
        .unwrap();
        assert_eq!(response.width, MAX_EDGE_PIXELS);
        assert_eq!(response.height, 2);
        assert!(!response.was_resized);
        std::fs::remove_file(path).unwrap();
    }

    #[test]
    fn optimized_jpeg_uses_the_pinned_quality_encoder() {
        let path = temporary_image_path("jpg");
        let image = DynamicImage::new_rgb8(17, 11);
        image.save_with_format(&path, ImageFormat::Jpeg).unwrap();
        let response = prepare_image_sync(&PrepareImageRequest {
            path: path.to_string_lossy().into_owned(),
            optimize: true,
        })
        .unwrap();
        let actual = general_purpose::STANDARD
            .decode(response.data_url.split_once(',').unwrap().1)
            .unwrap();
        let mut expected = Vec::new();
        JpegEncoder::new_with_quality(&mut expected, JPEG_QUALITY)
            .encode_image(&image)
            .unwrap();
        assert_eq!(actual, expected);
        assert_eq!(response.mime_type, "image/jpeg");
        std::fs::remove_file(path).unwrap();
    }

    #[test]
    fn optimization_keeps_png_and_webp_formats_stable() {
        for (extension, format, expected_mime) in [
            ("png", ImageFormat::Png, "image/png"),
            ("webp", ImageFormat::WebP, "image/webp"),
        ] {
            let path = temporary_image_path(extension);
            DynamicImage::new_rgba8(9, 7)
                .save_with_format(&path, format)
                .unwrap();
            let response = prepare_image_sync(&PrepareImageRequest {
                path: path.to_string_lossy().into_owned(),
                optimize: true,
            })
            .unwrap();
            let bytes = general_purpose::STANDARD
                .decode(response.data_url.split_once(',').unwrap().1)
                .unwrap();
            assert_eq!(image::guess_format(&bytes).unwrap(), format);
            assert_eq!(response.mime_type, expected_mime);
            std::fs::remove_file(path).unwrap();
        }
    }

    #[test]
    fn rejects_file_contents_that_do_not_match_the_extension() {
        let path = temporary_image_path("png");
        std::fs::write(&path, b"not an image").unwrap();
        let issue = prepare_image_sync(&PrepareImageRequest {
            path: path.to_string_lossy().into_owned(),
            optimize: true,
        })
        .unwrap_err();
        assert_eq!(issue.0, StatusCode::BAD_REQUEST);
        std::fs::remove_file(path).unwrap();
    }

    /// Noise which compresses badly, the same on every run.
    fn noise_image(width: u32, height: u32) -> RgbImage {
        let mut state: u32 = 0x1234_5678;
        RgbImage::from_fn(width, height, |_, _| {
            let mut channel = || {
                state = state.wrapping_mul(1_664_525).wrapping_add(1_013_904_223);
                (state >> 24) as u8
            };

            Rgb([channel(), channel(), channel()])
        })
    }

    /// Writes a black JPEG whose EXIF data says how to turn it.
    ///
    /// The encoder writes no EXIF data, so the segment is put in by hand, right after the start
    /// of the image: a big-endian TIFF header with one entry, the orientation.
    fn write_jpeg_with_orientation(path: &Path, width: u32, height: u32, orientation: u8) {
        let mut jpeg = Vec::new();
        JpegEncoder::new_with_quality(&mut jpeg, JPEG_QUALITY)
            .encode_image(&DynamicImage::new_rgb8(width, height))
            .unwrap();

        let exif: [u8; 36] = [
            0xFF, 0xE1, 0x00, 0x22, // APP1 and its length, which counts itself but not the marker
            b'E', b'x', b'i', b'f', 0x00, 0x00,
            b'M', b'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08, // big-endian, first IFD at offset 8
            0x00, 0x01, // one entry
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, orientation, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, // no further IFD
        ];

        let mut file = jpeg[..2].to_vec();
        file.extend_from_slice(&exif);
        file.extend_from_slice(&jpeg[2..]);
        std::fs::write(path, file).unwrap();
    }

    #[test]
    fn model_preparation_scales_down_only_above_the_maximum_edge() {
        let larger = temporary_image_path("png");
        DynamicImage::new_rgb8(MAX_EDGE_PIXELS + 1, 1).save_with_format(&larger, ImageFormat::Png).unwrap();
        let prepared = prepare_for_model(&larger, u64::MAX).unwrap();
        assert_eq!((prepared.width, prepared.height), (MAX_EDGE_PIXELS, 1));
        assert!(prepared.was_resized);
        std::fs::remove_file(larger).unwrap();

        let at_the_edge = temporary_image_path("png");
        DynamicImage::new_rgb8(MAX_EDGE_PIXELS, 2).save_with_format(&at_the_edge, ImageFormat::Png).unwrap();
        let prepared = prepare_for_model(&at_the_edge, u64::MAX).unwrap();
        assert_eq!((prepared.width, prepared.height), (MAX_EDGE_PIXELS, 2));
        assert!(!prepared.was_resized);
        std::fs::remove_file(at_the_edge).unwrap();
    }

    #[test]
    fn model_preparation_keeps_jpeg_and_png_and_turns_other_formats_into_one_of_them() {
        let opaque = DynamicImage::ImageRgb8(RgbImage::from_pixel(9, 7, Rgb([200, 10, 10])));
        let mut transparent = opaque.to_rgba8();
        transparent.get_pixel_mut(0, 0).0[3] = 0;
        let transparent = DynamicImage::ImageRgba8(transparent);

        for (extension, source_format, image, expected) in [
            ("jpg", ImageFormat::Jpeg, &opaque, ImageFormat::Jpeg),
            ("png", ImageFormat::Png, &opaque, ImageFormat::Png),
            ("webp", ImageFormat::WebP, &opaque, ImageFormat::Jpeg),
            ("webp", ImageFormat::WebP, &transparent, ImageFormat::Png),
            ("gif", ImageFormat::Gif, &opaque, ImageFormat::Jpeg),
            ("gif", ImageFormat::Gif, &transparent, ImageFormat::Png),
            ("bmp", ImageFormat::Bmp, &opaque, ImageFormat::Jpeg),
            ("tif", ImageFormat::Tiff, &opaque, ImageFormat::Jpeg),
            ("tiff", ImageFormat::Tiff, &transparent, ImageFormat::Png),
        ] {
            let path = temporary_image_path(extension);
            image.save_with_format(&path, source_format).unwrap();
            let prepared = prepare_for_model(&path, u64::MAX).unwrap();
            assert_eq!(image::guess_format(&prepared.bytes).unwrap(), expected, "{extension} with alpha={}", image.color().has_alpha());
            assert_eq!(prepared.encoding == ModelEncoding::Png, expected == ImageFormat::Png, "{extension}");
            assert_eq!((prepared.width, prepared.height), (9, 7), "{extension}");
            std::fs::remove_file(path).unwrap();
        }
    }

    #[test]
    fn model_preparation_refuses_formats_it_cannot_decode() {
        for extension in ["heic", "heif", "avif", "svg", "ico"] {
            let path = temporary_image_path(extension);
            std::fs::write(&path, b"whatever").unwrap();
            let failure = prepare_for_model(&path, u64::MAX).err().unwrap();
            assert_eq!(failure.code, ImageErrorCode::UnsupportedFormat, "{extension}");
            std::fs::remove_file(path).unwrap();
        }
    }

    #[test]
    fn model_preparation_tells_missing_mismatched_and_damaged_files_apart() {
        let missing = prepare_for_model(&temporary_image_path("png"), u64::MAX).err().unwrap();
        assert_eq!(missing.code, ImageErrorCode::FileNotFound);

        let relative = prepare_for_model(Path::new("picture.png"), u64::MAX).err().unwrap();
        assert_eq!(relative.code, ImageErrorCode::FileNotFound);

        let jpeg_named_png = temporary_image_path("png");
        DynamicImage::new_rgb8(4, 3).save_with_format(&jpeg_named_png, ImageFormat::Jpeg).unwrap();
        let mismatch = prepare_for_model(&jpeg_named_png, u64::MAX).err().unwrap();
        assert_eq!(mismatch.code, ImageErrorCode::ContentMismatch);
        std::fs::remove_file(jpeg_named_png).unwrap();

        let damaged = temporary_image_path("png");
        std::fs::write(&damaged, b"not an image").unwrap();
        let failure = prepare_for_model(&damaged, u64::MAX).err().unwrap();
        assert_eq!(failure.code, ImageErrorCode::NotDecodable);
        std::fs::remove_file(damaged).unwrap();
    }

    #[test]
    fn model_preparation_turns_photos_upright() {
        let path = temporary_image_path("jpg");
        write_jpeg_with_orientation(&path, 20, 10, 6);
        let prepared = prepare_for_model(&path, u64::MAX).unwrap();
        assert_eq!((prepared.width, prepared.height), (10, 20), "Orientation 6 turns the image by 90 degrees.");
        std::fs::remove_file(path).unwrap();
    }

    #[test]
    fn optimization_turns_photos_upright() {
        let path = temporary_image_path("jpg");
        write_jpeg_with_orientation(&path, 20, 10, 6);
        let response = prepare_image_sync(&PrepareImageRequest {
            path: path.to_string_lossy().into_owned(),
            optimize: true,
        })
        .unwrap();

        let bytes = general_purpose::STANDARD
            .decode(response.data_url.split_once(',').unwrap().1)
            .unwrap();
        let encoded = image::load_from_memory(&bytes).unwrap();
        assert_eq!((response.width, response.height), (10, 20), "Orientation 6 turns the image by 90 degrees.");
        assert_eq!((encoded.width(), encoded.height()), (10, 20), "The encoded image carries no orientation of its own any longer.");
        std::fs::remove_file(path).unwrap();
    }

    #[test]
    fn model_preparation_gives_the_same_bytes_for_the_same_image() {
        let path = temporary_image_path("jpg");
        DynamicImage::ImageRgb8(noise_image(64, 48)).save_with_format(&path, ImageFormat::Jpeg).unwrap();
        let first = prepare_for_model(&path, u64::MAX).unwrap();
        let second = prepare_for_model(&path, u64::MAX).unwrap();
        assert_eq!(first.bytes, second.bytes);
        std::fs::remove_file(path).unwrap();
    }

    #[test]
    fn budget_lowers_the_quality_before_the_size() {
        let path = temporary_image_path("jpg");
        DynamicImage::ImageRgb8(noise_image(320, 200)).save_with_format(&path, ImageFormat::Jpeg).unwrap();

        //
        // The sizes the budget will meet, measured on the very pixels it works with:
        //
        let decoded = to_jpeg_pixels(&decode_oriented(&path, ImageFormat::Jpeg).unwrap());
        let at_first_quality = encode_model_image(&decoded, ModelEncoding::Jpeg, BUDGET_JPEG_QUALITIES[0]).unwrap();
        let at_second_quality = encode_model_image(&decoded, ModelEncoding::Jpeg, BUDGET_JPEG_QUALITIES[1]).unwrap();
        assert!(at_second_quality.len() < at_first_quality.len());

        let prepared = prepare_for_model(&path, at_first_quality.len() as u64 - 1).unwrap();
        assert_eq!(prepared.bytes, at_second_quality);
        assert_eq!((prepared.width, prepared.height), (320, 200));
        assert!(!prepared.was_resized);
        std::fs::remove_file(path).unwrap();
    }

    #[test]
    fn budget_shrinks_the_image_once_no_quality_fits() {
        let path = temporary_image_path("jpg");
        DynamicImage::ImageRgb8(noise_image(1_200, 600)).save_with_format(&path, ImageFormat::Jpeg).unwrap();

        let decoded = to_jpeg_pixels(&decode_oriented(&path, ImageFormat::Jpeg).unwrap());
        let at_last_quality = encode_model_image(&decoded, ModelEncoding::Jpeg, *BUDGET_JPEG_QUALITIES.last().unwrap()).unwrap();
        let budget = at_last_quality.len() as u64 - 1;

        let prepared = prepare_for_model(&path, budget).unwrap();
        assert!(prepared.bytes.len() as u64 <= budget);
        assert_eq!((prepared.width, prepared.height), (MIN_BUDGET_EDGE_PIXELS, 512));
        assert!(prepared.was_resized);
        assert_eq!(prepared.encoding, ModelEncoding::Jpeg);
        std::fs::remove_file(path).unwrap();
    }

    #[test]
    fn budget_lays_a_transparent_png_onto_white_when_it_has_to_become_jpeg() {
        let mut pixels = DynamicImage::ImageRgb8(noise_image(300, 200)).to_rgba8();
        for (x, _, pixel) in pixels.enumerate_pixels_mut() {
            if x < 150 {
                pixel.0 = [0, 0, 0, 0];
            }
        }

        let path = temporary_image_path("png");
        DynamicImage::ImageRgba8(pixels).save_with_format(&path, ImageFormat::Png).unwrap();
        let unlimited = prepare_for_model(&path, u64::MAX).unwrap();
        assert_eq!(unlimited.encoding, ModelEncoding::Png);

        let prepared = prepare_for_model(&path, unlimited.bytes.len() as u64 - 1).unwrap();
        assert_eq!(prepared.encoding, ModelEncoding::Jpeg);

        let corner = image::load_from_memory(&prepared.bytes).unwrap().to_rgb8().get_pixel(5, 5).0;
        assert!(corner.iter().all(|&channel| channel > 240), "The transparent half has to be white, not black: {corner:?}");
        std::fs::remove_file(path).unwrap();
    }

    #[test]
    fn budget_reports_the_quality_which_fit() {
        let path = temporary_image_path("jpg");
        DynamicImage::ImageRgb8(noise_image(64, 48)).save_with_format(&path, ImageFormat::Jpeg).unwrap();
        assert_eq!(prepare_for_model(&path, u64::MAX).unwrap().jpeg_quality, Some(JPEG_QUALITY));
        std::fs::remove_file(path).unwrap();

        let path = temporary_image_path("png");
        DynamicImage::new_rgb8(4, 3).save_with_format(&path, ImageFormat::Png).unwrap();
        assert_eq!(prepare_for_model(&path, u64::MAX).unwrap().jpeg_quality, None, "PNG has no quality.");
        std::fs::remove_file(path).unwrap();
    }

    #[test]
    fn log_writes_sizes_in_megabytes_and_the_ratio_in_percent() {
        assert_eq!(megabytes(5_812_345), "5.81 MB");
        assert_eq!(megabytes(0), "0.00 MB");
        assert_eq!(percentage(912_000, 5_812_345), "16 %");
        assert_eq!(percentage(1, 0), "n/a", "An empty source has no ratio.");
    }

    #[test]
    fn budget_refuses_an_image_it_cannot_make_fit() {
        let path = temporary_image_path("jpg");
        DynamicImage::ImageRgb8(noise_image(64, 48)).save_with_format(&path, ImageFormat::Jpeg).unwrap();
        let failure = prepare_for_model(&path, 100).err().unwrap();
        assert_eq!(failure.code, ImageErrorCode::BudgetNotReachable);
        std::fs::remove_file(path).unwrap();
    }

    #[test]
    fn serializes_the_model_response_for_the_rust_service_contract() {
        let failure = serde_json::to_value(PrepareModelImageResponse::failure(ImageErrorCode::BudgetNotReachable)).unwrap();
        assert_eq!(failure["error_code"], "BUDGET_NOT_REACHABLE");
        assert_eq!(failure["base64"], "");

        let path = temporary_image_path("png");
        DynamicImage::new_rgb8(4, 3).save_with_format(&path, ImageFormat::Png).unwrap();
        let success = serde_json::to_value(prepare_image_for_model_sync(&PrepareModelImageRequest {
            path: path.to_string_lossy().into_owned(),
            max_bytes: u64::MAX,
        }))
        .unwrap();

        assert_eq!(success["error_code"], "NONE");
        assert_eq!(success["mime_type"], "image/png");
        assert_eq!(success["width"], 4);
        assert_eq!(success["height"], 3);
        assert_eq!(success["was_resized"], false);
        assert!(!success["base64"].as_str().unwrap().starts_with("data:"));
        assert!(success.get("mimeType").is_none());
        std::fs::remove_file(path).unwrap();
    }
}