//! Local image preparation: decode a file, apply the size policy, and return it as a Data URL.
//!
//! This module is deliberately free of any feature-specific behavior so that every part of
//! AI Studio that needs an embeddable image can use it. The size policy is a single maximum edge
//! length; callers that want the original bytes pass `optimize = false`.

use std::io::Cursor;
use std::path::Path;

use axum::Json;
use axum::http::StatusCode;
use base64::{Engine as _, engine::general_purpose};
use image::codecs::jpeg::JpegEncoder;
use image::imageops::FilterType;
use image::{DynamicImage, ImageFormat, ImageReader};
use serde::{Deserialize, Serialize};

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
    let reader = ImageReader::open(path)
        .and_then(|reader| reader.with_guessed_format())
        .map_err(|error| {
            (
                StatusCode::BAD_REQUEST,
                format!("The image could not be opened: {error}"),
            )
        })?;
        
    if reader.format() != Some(format) {
        return Err((
            StatusCode::BAD_REQUEST,
            "The image content does not match its file extension.".to_string(),
        ));
    }

    let decoded = reader.decode().map_err(|error| {
        (
            StatusCode::BAD_REQUEST,
            format!("The image could not be decoded: {error}"),
        )
    })?;
    
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
    let width = image.width();
    let height = image.height();
    let scale = MAX_EDGE_PIXELS as f64 / width.max(height) as f64;
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

#[cfg(test)]
mod tests {
    use super::*;

    fn temporary_image_path(extension: &str) -> std::path::PathBuf {
        let unique = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        std::env::temp_dir().join(format!("mwai-visual-briefing-test-{unique}.{extension}"))
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
}