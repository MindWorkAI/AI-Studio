use std::collections::HashMap;
use std::fs;
use std::path::{Path, PathBuf};
use std::sync::{Arc, Mutex};

use axum::Json;
use log::{error, info, warn};
use once_cell::sync::Lazy;
use qdrant_edge::external::serde_json::json;
use qdrant_edge::external::uuid::Uuid;
use qdrant_edge::{
    Condition, Distance, EdgeConfig, EdgeOptimizersConfig, EdgeShard, EdgeVectorParams,
    FieldCondition, Filter, HnswIndexConfig, Match, MatchValue, PointId, PointInsertOperations,
    PointOperations, PointStruct, UpdateOperation, ValueVariants, Vectors,
};
use serde::{Deserialize, Serialize};
use tauri::Manager;

use crate::api_token::APIToken;
use crate::environment::DATA_DIRECTORY;
use crate::metadata::META_DATA;

const VECTOR_NAME: &str = "embedding";
const HNSW_M: usize = 16;
const HNSW_EF_CONSTRUCT: usize = 100;
const HNSW_FULL_SCAN_THRESHOLD_KB: usize = 10_000;
const HNSW_MAX_INDEXING_THREADS: usize = 0;
const VECTOR_INDEXING_THRESHOLD_KB: usize = 10_000;

type QdrantEdgeResult<T> = Result<T, Box<dyn std::error::Error + Send + Sync>>;

static QDRANT_EDGE_DATABASE: Lazy<Arc<Mutex<Option<QdrantEdgeDatabase>>>> =
    Lazy::new(|| Arc::new(Mutex::new(None)));

static QDRANT_EDGE_STATUS: Lazy<Mutex<QdrantEdgeStatusInfo>> =
    Lazy::new(|| Mutex::new(QdrantEdgeStatusInfo::default()));

#[derive(Default)]
struct QdrantEdgeStatusInfo {
    status: QdrantEdgeStatus,
    unavailable_reason: Option<String>,
}

#[derive(Clone, Copy, Default, Serialize, PartialEq, Eq)]
pub enum QdrantEdgeStatus {
    #[default]
    Starting,
    Available,
    Unavailable,
}

#[derive(Serialize)]
pub struct QdrantEdgeServiceInfo {
    pub status: QdrantEdgeStatus,
    pub name: String,
    pub version: String,
    pub path: String,
    pub stores_count: usize,
    pub is_available: bool,
    pub unavailable_reason: Option<String>,
}

#[derive(Clone, Deserialize)]
pub struct QdrantEdgeStoragePoint {
    pub point_id: String,
    pub vector: Vec<f32>,
    pub data_source_id: String,
    pub data_source_name: String,
    pub data_source_type: String,
    pub file_path: String,
    pub file_name: String,
    pub relative_path: String,
    pub chunk_index: i32,
    pub text: String,
    pub fingerprint: String,
    pub last_write_utc: String,
    pub embedded_at_utc: String,
}

#[derive(Deserialize)]
pub struct EnsureQdrantEdgeStoreRequest {
    pub store_name: String,
    pub vector_size: usize,
}

#[derive(Deserialize)]
pub struct InsertQdrantEdgeEmbeddingRequest {
    pub store_name: String,
    pub points: Vec<QdrantEdgeStoragePoint>,
}

#[derive(Deserialize)]
pub struct DeleteQdrantEdgeEmbeddingByFileRequest {
    pub store_name: String,
    pub file_path: String,
}

#[derive(Deserialize)]
pub struct DeleteQdrantEdgeStoreRequest {
    pub store_name: String,
}

#[derive(Serialize)]
pub struct QdrantEdgeOperationResponse {
    pub success: bool,
    pub issue: String,
}

#[derive(Clone, Serialize)]
pub struct QdrantEdgeInfo {
    pub name: String,
    pub version: String,
    pub path: String,
    pub stores_count: usize,
}

pub struct QdrantEdgeDatabase {
    base_path: PathBuf,
    shards: HashMap<String, EdgeShard>,
}

impl QdrantEdgeDatabase {
    pub fn new(base_path: PathBuf) -> Self {
        Self {
            base_path,
            shards: HashMap::new(),
        }
    }

    fn store_path(&self, store_name: &str) -> QdrantEdgeResult<PathBuf> {
        validate_store_name(store_name)?;
        Ok(self.base_path.join("stores").join(store_name))
    }

    // To ensure a shard exists and that you can insert a vector
    fn get_or_create_store(&mut self, store_name: &str, vector_size: usize) -> QdrantEdgeResult<&EdgeShard> {
        if self.shards.contains_key(store_name) {
            return Ok(self.shards.get(store_name).unwrap());
        }

        let path = self.store_path(store_name)?;
        let shard = if has_existing_store(&path) {
            EdgeShard::load(&path, None)?
        } else {
            fs::create_dir_all(&path)?;
            EdgeShard::new(&path, edge_config(vector_size))?
        };

        self.shards.insert(store_name.to_string(), shard);
        Ok(self.shards.get(store_name).unwrap())
    }

    // To check whether a shard exists so you can delete a file from it
    fn get_existing_store(&mut self, store_name: &str) -> QdrantEdgeResult<Option<&EdgeShard>> {
        if self.shards.contains_key(store_name) {
            return Ok(self.shards.get(store_name));
        }

        let path = self.store_path(store_name)?;
        if !has_existing_store(&path) {
            return Ok(None);
        }

        let shard = EdgeShard::load(&path, None)?;
        self.shards.insert(store_name.to_string(), shard);
        Ok(self.shards.get(store_name))
    }

    fn info(&self) -> QdrantEdgeResult<QdrantEdgeInfo> {
        let stores_path = self.base_path.join("stores");
        let stores_count = if stores_path.exists() {
            fs::read_dir(stores_path)?
                .filter_map(Result::ok)
                .filter(|entry| entry.path().is_dir())
                .count()
        } else {
            0
        };

        Ok(QdrantEdgeInfo {
            name: "Qdrant Edge".to_string(),
            version: vector_store_version()?,
            path: self.base_path.to_string_lossy().to_string(),
            stores_count,
        })
    }

    fn ensure_store_exists(&mut self, store_name: &str, vector_size: usize) -> QdrantEdgeResult<()> {
        self.get_or_create_store(store_name, vector_size)?;
        Ok(())
    }

    fn insert_embedding(&mut self, store_name: &str, points: Vec<QdrantEdgeStoragePoint>) -> QdrantEdgeResult<()> {
        let Some(first_point) = points.first() else {
            return Ok(());
        };

        let vector_size = first_point.vector.len();
        if points.iter().any(|point| point.vector.len() != vector_size) {
            return Err("All vectors in one insert request must have the same size.".into());
        }

        let shard = self.get_or_create_store(store_name, vector_size)?;
        let points = points
            .into_iter()
            .map(to_qdrant_edge_point)
            .collect::<Vec<_>>();

        shard.update(UpdateOperation::PointOperation(
            PointOperations::UpsertPoints(PointInsertOperations::PointsList(points)),
        ))?;
        shard.flush();
        Ok(())
    }

    fn delete_embedding_by_file(&mut self, store_name: &str, file_path: &str) -> QdrantEdgeResult<()> {
        let Some(shard) = self.get_existing_store(store_name)? else {
            return Ok(());
        };

        shard.update(UpdateOperation::PointOperation(
            PointOperations::DeletePointsByFilter(match_keyword_filter("file_path", file_path)?),
        ))?;
        shard.flush();
        Ok(())
    }

    fn delete_store(&mut self, store_name: &str) -> QdrantEdgeResult<()> {
        self.shards.remove(store_name);

        let path = self.store_path(store_name)?;
        if path.exists() {
            fs::remove_dir_all(path)?;
        }

        Ok(())
    }

    fn base_path(&self) -> PathBuf {
        self.base_path.clone()
    }
}

fn qdrant_edge_base_path() -> PathBuf {
    Path::new(DATA_DIRECTORY.get().unwrap())
        .join("databases")
        .join("vector_database")
}

pub async fn qdrant_edge_info(_token: APIToken) -> Json<QdrantEdgeServiceInfo> {
    let status = QDRANT_EDGE_STATUS.lock().unwrap();
    let current_status = status.status;
    let unavailable_reason = status.unavailable_reason.clone();
    drop(status);

    let database_guard = QDRANT_EDGE_DATABASE.lock().unwrap();
    let database_info = database_guard
        .as_ref()
        .and_then(|database| database.info().ok());

    let is_available = current_status == QdrantEdgeStatus::Available && database_info.is_some();
    Json(QdrantEdgeServiceInfo {
        status: current_status,
        name: database_info.as_ref().map(|info| info.name.clone()).unwrap_or_default(),
        version: database_info.as_ref().map(|info| info.version.clone()).unwrap_or_default(),
        path: database_info.as_ref().map(|info| info.path.clone()).unwrap_or_default(),
        stores_count: database_info.as_ref().map(|info| info.stores_count).unwrap_or_default(),
        is_available,
        unavailable_reason,
    })
}

pub async fn ensure_qdrant_edge_store(_token: APIToken, Json(request): Json<EnsureQdrantEdgeStoreRequest>) -> Json<QdrantEdgeOperationResponse> {
    execute_qdrant_edge_operation(|database| {
        database.ensure_store_exists(&request.store_name, request.vector_size)
    })
}

pub async fn insert_qdrant_edge_embedding(_token: APIToken, Json(request): Json<InsertQdrantEdgeEmbeddingRequest>) -> Json<QdrantEdgeOperationResponse> {
    execute_qdrant_edge_operation(|database| {
        database.insert_embedding(&request.store_name, request.points)
    })
}

pub async fn delete_qdrant_edge_embedding_by_file(_token: APIToken, Json(request): Json<DeleteQdrantEdgeEmbeddingByFileRequest>) -> Json<QdrantEdgeOperationResponse> {
    execute_qdrant_edge_operation(|database| {
        database.delete_embedding_by_file(&request.store_name, &request.file_path)
    })
}

pub async fn delete_qdrant_edge_store(_token: APIToken, Json(request): Json<DeleteQdrantEdgeStoreRequest>) -> Json<QdrantEdgeOperationResponse> {
    execute_qdrant_edge_operation(|database| {
        database.delete_store(&request.store_name)
    })
}

pub fn start_qdrant_edge_database<R: tauri::Runtime>(app_handle: tauri::AppHandle<R>) {
    set_qdrant_edge_starting();
    remove_obsolete_qdrant_sidecar_files(&app_handle);

    let path = qdrant_edge_base_path();
    match fs::create_dir_all(&path) {
        Ok(_) => {
            let database = QdrantEdgeDatabase::new(path.clone());
            *QDRANT_EDGE_DATABASE.lock().unwrap() = Some(database);
            set_qdrant_edge_available();
            info!(Source = "Qdrant Edge"; "Qdrant Edge is available at '{}'.", path.display());
        },
        Err(e) => {
            let reason = format!("The Qdrant Edge data directory could not be created: {e}");
            error!(Source = "Qdrant Edge"; "{reason}");
            set_qdrant_edge_unavailable(reason);
        },
    }
}

pub fn stop_qdrant_edge_database() {
    if let Some(database) = QDRANT_EDGE_DATABASE.lock().unwrap().take() {
        info!(Source = "Qdrant Edge"; "Stopping Qdrant Edge at '{}'.", database.base_path().display());
        drop(database);
    }

    set_qdrant_edge_unavailable("Qdrant Edge was stopped.".to_string());
}

fn execute_qdrant_edge_operation<F>(operation: F) -> Json<QdrantEdgeOperationResponse>
where
    F: FnOnce(&mut QdrantEdgeDatabase) -> QdrantEdgeResult<()>,
{
    let mut database_guard = QDRANT_EDGE_DATABASE.lock().unwrap();
    let Some(database) = database_guard.as_mut() else {
        return Json(QdrantEdgeOperationResponse {
            success: false,
            issue: "Qdrant Edge is not available.".to_string(),
        });
    };

    match operation(database) {
        Ok(_) => Json(QdrantEdgeOperationResponse {
            success: true,
            issue: String::new(),
        }),
        Err(e) => {
            let issue = e.to_string();
            error!(Source = "Qdrant Edge"; "Qdrant Edge operation failed: {issue}");
            Json(QdrantEdgeOperationResponse {
                success: false,
                issue,
            })
        },
    }
}

fn set_qdrant_edge_available() {
    let mut status = QDRANT_EDGE_STATUS.lock().unwrap();
    status.status = QdrantEdgeStatus::Available;
    status.unavailable_reason = None;
}

fn set_qdrant_edge_starting() {
    let mut status = QDRANT_EDGE_STATUS.lock().unwrap();
    status.status = QdrantEdgeStatus::Starting;
    status.unavailable_reason = None;
}

fn set_qdrant_edge_unavailable(reason: String) {
    let mut status = QDRANT_EDGE_STATUS.lock().unwrap();
    status.status = QdrantEdgeStatus::Unavailable;
    status.unavailable_reason = Some(reason);
}

fn remove_obsolete_qdrant_sidecar_files<R: tauri::Runtime>(app_handle: &tauri::AppHandle<R>) {
    let mut paths = Vec::new();

    if let Some(data_directory) = DATA_DIRECTORY.get() {
        let databases_directory = Path::new(data_directory).join("databases");
        paths.push(databases_directory.join("qdrant"));
        paths.push(databases_directory.join("qdrant_test"));
    }

    if let Ok(resource_dir) = app_handle.path().resource_dir() {
        paths.push(resource_dir.join("target").join("databases").join("qdrant"));
        paths.push(resource_dir.join("resources").join("databases").join("qdrant"));
    }

    if let Ok(current_exe) = std::env::current_exe() && let Some(exe_dir) = current_exe.parent() {
        paths.push(exe_dir.join("target").join("databases").join("qdrant"));
        paths.push(exe_dir.join("qdrant.exe"));
        paths.push(exe_dir.join("qdrant"));
    }

    for path in paths {
        remove_obsolete_qdrant_path(&path);
    }
}

fn remove_obsolete_qdrant_path(path: &Path) {
    if !path.exists() {
        return;
    }

    let result = if path.is_dir() {
        fs::remove_dir_all(path)
    } else {
        fs::remove_file(path)
    };

    match result {
        Ok(_) => warn!(Source = "Qdrant Edge"; "Removed obsolete Qdrant sidecar file or directory '{}'.", path.display()),
        Err(e) => warn!(Source = "Qdrant Edge"; "Could not remove obsolete Qdrant sidecar file or directory '{}': {e}", path.display()),
    }
}

fn edge_config(vector_size: usize) -> EdgeConfig {
    EdgeConfig {
        on_disk_payload: true,
        vectors: HashMap::from([(
            VECTOR_NAME.to_string(),
            EdgeVectorParams {
                size: vector_size,
                distance: Distance::Cosine,
                on_disk: Some(true),
                quantization_config: None,
                multivector_config: None,
                datatype: None,
                hnsw_config: Some(hnsw_config()),
            },
        )]),
        sparse_vectors: HashMap::new(),
        hnsw_config: hnsw_config(),
        quantization_config: None,
        optimizers: edge_optimizers_config(),
    }
}

fn hnsw_config() -> HnswIndexConfig {
    HnswIndexConfig {
        m: HNSW_M,
        ef_construct: HNSW_EF_CONSTRUCT,
        full_scan_threshold: HNSW_FULL_SCAN_THRESHOLD_KB,
        max_indexing_threads: HNSW_MAX_INDEXING_THREADS,
        on_disk: Some(true),
        payload_m: None,
        inline_storage: None,
    }
}

fn edge_optimizers_config() -> EdgeOptimizersConfig {
    EdgeOptimizersConfig {
        indexing_threshold: Some(VECTOR_INDEXING_THRESHOLD_KB),
        prevent_unoptimized: Some(false),
        ..Default::default()
    }
}

fn has_existing_store(path: &Path) -> bool {
    path.join("edge_config.json").exists() || path.join("segments").exists()
}

fn vector_store_version() -> QdrantEdgeResult<String> {
    let metadata = META_DATA
        .lock()
        .map_err(|_| "Metadata lock was poisoned.")?;
    let Some(metadata) = metadata.as_ref() else {
        return Err("Metadata was not initialized.".into());
    };

    Ok(metadata.vector_store_version.clone())
}

fn to_qdrant_edge_point(point: QdrantEdgeStoragePoint) -> qdrant_edge::PointStructPersisted {
    PointStruct::new(
        to_point_id(&point.point_id),
        Vectors::new_named([(VECTOR_NAME, point.vector)]),
        json!({
            "data_source_id": point.data_source_id,
            "data_source_name": point.data_source_name,
            "data_source_type": point.data_source_type,
            "file_path": point.file_path,
            "file_name": point.file_name,
            "relative_path": point.relative_path,
            "chunk_index": point.chunk_index,
            "text": point.text,
            "fingerprint": point.fingerprint,
            "last_write_utc": point.last_write_utc,
            "embedded_at_utc": point.embedded_at_utc,
        }),
    )
    .into()
}

fn to_point_id(point_id: &str) -> PointId {
    Uuid::parse_str(point_id)
        .map(PointId::Uuid)
        .unwrap_or_else(|_| PointId::NumId(stable_u64(point_id)))
}

fn stable_u64(value: &str) -> u64 {
    let mut hash = 0xcbf29ce484222325_u64;
    for byte in value.as_bytes() {
        hash ^= u64::from(*byte);
        hash = hash.wrapping_mul(0x100000001b3);
    }

    hash
}

fn match_keyword_filter(field_name: &str, value: &str) -> QdrantEdgeResult<Filter> {
    Ok(Filter {
        should: None,
        min_should: None,
        must: Some(vec![Condition::Field(FieldCondition::new_match(
            field_name
                .try_into()
                .map_err(|_| format!("Invalid payload field name '{field_name}'."))?,
            Match::Value(MatchValue {
                value: ValueVariants::String(value.to_string()),
            }),
        ))]),
        must_not: None,
    })
}

fn validate_store_name(store_name: &str) -> QdrantEdgeResult<()> {
    if store_name.is_empty() {
        return Err("Vector store name cannot be empty.".into());
    }

    if store_name
        .chars()
        .all(|c| c.is_ascii_alphanumeric() || c == '_' || c == '-' || c == '.')
    {
        return Ok(());
    }

    Err(format!("Vector store name '{store_name}' contains unsupported characters.").into())
}
