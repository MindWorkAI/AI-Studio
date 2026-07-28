//! Local visual-briefing image optimization and Data-URL preparation.

use std::io::Cursor;
use std::path::Path;

use axum::Json;
use axum::http::StatusCode;
use base64::{Engine as _, engine::general_purpose};
use image::codecs::jpeg::JpegEncoder;
use image::imageops::FilterType;
use image::{DynamicImage, ImageFormat, ImageReader};
use serde::{Deserialize, Serialize};

const MAX_EDGE_PIXELS: u32 = 2_560;
const JPEG_QUALITY: u8 = 85;

#[derive(Debug, Deserialize)]
pub struct PrepareImageRequest {
    path: String,
    optimize: bool,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PrepareImageResponse {
    data_url: String,
    mime_type: String,
    width: u32,
    height: u32,
    was_resized: bool,
}

/// Decodes one supported image, applies the visual-briefing size policy, and returns a Data URL.
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

fn prepare_image_sync(
    request: &PrepareImageRequest,
) -> Result<PrepareImageResponse, (StatusCode, String)> {
    let path = Path::new(&request.path);
    if !path.is_absolute() || !path.is_file() {
        return Err((
            StatusCode::BAD_REQUEST,
            "The visual asset path is not an accessible absolute file path.".to_string(),
        ));
    }

    let format = supported_format(path)?;
    let reader = ImageReader::open(path)
        .and_then(|reader| reader.with_guessed_format())
        .map_err(|error| {
            (
                StatusCode::BAD_REQUEST,
                format!("The visual asset could not be opened: {error}"),
            )
        })?;
        
    if reader.format() != Some(format) {
        return Err((
            StatusCode::BAD_REQUEST,
            "The visual asset content does not match its file extension.".to_string(),
        ));
    }

    let decoded = reader.decode().map_err(|error| {
        (
            StatusCode::BAD_REQUEST,
            format!("The visual asset could not be decoded: {error}"),
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
                format!("The visual asset could not be read: {error}"),
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
            "Visual assets must be PNG, JPEG, or WebP files.".to_string(),
        )),
    }
}

fn resize_to_max_edge(image: DynamicImage) -> DynamicImage {
    let width = image.width();
    let height = image.height();
    let scale = MAX_EDGE_PIXELS as f64 / width.max(height) as f64;
    let target_width = (width as f64 * scale).round().max(1.0) as u32;
    let target_height = (height as f64 * scale).round().max(1.0) as u32;
    image.resize_exact(target_width, target_height, FilterType::Lanczos3)
}

fn encode(image: &DynamicImage, format: ImageFormat) -> Result<Vec<u8>, (StatusCode, String)> {
    let mut bytes = Vec::new();
    match format {
        ImageFormat::Jpeg => JpegEncoder::new_with_quality(&mut bytes, JPEG_QUALITY)
            .encode_image(image)
            .map_err(|error| {
                (
                    StatusCode::INTERNAL_SERVER_ERROR,
                    format!("The JPEG visual asset could not be encoded: {error}"),
                )
            })?,
            
        ImageFormat::Png | ImageFormat::WebP => image
            .write_to(&mut Cursor::new(&mut bytes), format)
            .map_err(|error| {
                (
                    StatusCode::INTERNAL_SERVER_ERROR,
                    format!("The visual asset could not be encoded: {error}"),
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