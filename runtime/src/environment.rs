pub fn is_dev() -> bool {
    cfg!(debug_assertions)
}

pub fn is_prod() -> bool {
    !is_dev()
}