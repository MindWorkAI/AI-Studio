//! Zooms the webview to the interface size the user chose.
//!
//! The app draws the same number of CSS pixels on every system. How large a CSS pixel appears
//! depends on the density of the screen and on the scaling of the system: a 13-inch laptop with
//! 1920 pixels across shows normal text half again as large at a scaling of 150 percent as at 100
//! percent. Web content cannot learn the physical size of a screen, since CSS fixes its inch at 96
//! pixels. The runtime can ask the system, though, and zoom the webview accordingly.
//!
//! The user chooses one of three sizes for every screen, as the systems keep their scaling per
//! monitor:
//!
//! - **System scaling** leaves the webview as the system scales it.
//! - **App default** zooms the screen of a laptop to a reference density, so that the
//!   same text has the same size on every laptop. A large external monitor stands farther away, and
//!   text of the same physical size would look smaller on it. Such a monitor, like any screen which
//!   does not report its size, keeps the scaling of the system.
//! - **Custom** applies a scale of the user's own, counted like the scaling of the system: device
//!   pixels per CSS pixel, so that 150 percent on Windows means what it means in its settings.
//!
//! A screen the user set nothing for gets App default when it is the screen of a laptop, and the
//! scaling of the system otherwise, which is what App default would show there anyway.
//!
//! Every screen limits the scales it allows: the layout needs a minimum width in CSS pixels, and text
//! must stay large enough to read. A custom scale beyond the limits of a screen is brought within
//! them there, while the setting itself stays as the user chose it.
//!
//! The webview shows in the default size of its screen from the moment the window opens, long before
//! the app has loaded the settings and can send the sizes the user chose. Most users never choose
//! one, and their screens thus never change size while AI Studio starts.
//!
//! Whenever the size on the screen changes, because the user changed it or the window moved to
//! another screen, the runtime tells the app, so that its settings show the screen the window is on.

use std::collections::HashMap;
use std::sync::Mutex;
use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};
use std::time::Duration;
use axum::Json;
use log::{info, warn};
use once_cell::sync::Lazy;
use serde::{Deserialize, Serialize};
use tauri::{Monitor, WebviewWindow};
use crate::app_window::{Event, TauriEventType};

/// The density App default aims for on the screen of a laptop, in CSS pixels per inch.
///
/// Normal text, 16 CSS pixels tall, is about three millimeters tall at this density, which proved
/// comfortable to read while it leaves plenty of room for content. A 14-inch laptop screen with
/// 1920 pixels across shows at about 120 percent with it, a 13.3-inch one at about 126 percent,
/// where Windows uses 150, and the 14-inch screen of a MacBook Pro at about 188 percent, where
/// macOS uses 200.
const REFERENCE_CSS_PIXELS_PER_INCH: f64 = 135.0;

/// The highest density a scale may leave, in CSS pixels per inch. Normal text, 16 CSS pixels tall,
/// stays at least about two millimeters tall this way.
const MAX_CSS_PIXELS_PER_INCH: f64 = 200.0;

/// The narrowest a screen may become in CSS pixels, which is the narrowest layout AI Studio supports.
const MIN_CSS_WIDTH: f64 = 1024.0;

/// The smallest scale on any screen, as device pixels per CSS pixel. Keep in sync with
/// MIN_CUSTOM_PERCENT in InterfaceSizeService of the app.
const MIN_SCALE: f64 = 0.6;

/// The largest scale on any screen, as device pixels per CSS pixel. Keep in sync with
/// MAX_CUSTOM_PERCENT in InterfaceSizeService of the app.
const MAX_SCALE: f64 = 3.0;

/// How far a scale may shrink below the scaling of the system on a screen of unknown size, where
/// the density which keeps text readable is unknown.
const MIN_SHARE_OF_SYSTEM_SCALE_ON_UNKNOWN_SCREENS: f64 = 0.5;

/// The largest screen which still counts as the screen of a laptop, as its diagonal in inches.
const LARGEST_LAPTOP_SCREEN_INCHES: f64 = 17.0;

/// The steps of the scale in the settings, in percent. The limits a screen reports lie on these steps.
const SCALE_STEP_PERCENT: f64 = 5.0;

/// How long a moved window has to rest before we measure its screen again.
///
/// Moving a window emits an event for every step of the way. Only once the window rests is the
/// screen it ends up on settled, and measuring then is enough.
const SCREEN_CHANGE_DELAY: Duration = Duration::from_millis(300);

const MILLIMETERS_PER_INCH: f64 = 25.4;

/// The interface size of a screen, as the app sends it and as the runtime reports it.
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Serialize)]
pub enum InterfaceSize {
    /// The same size on every laptop, after the reference density.
    AppDefault,

    /// The scaling of the system, unchanged.
    SystemScaling,

    /// A scale of the user's own, counted like the scaling of the system.
    Custom,
}

/// What the user chose for one screen.
#[derive(Clone, Copy, Debug, PartialEq)]
struct ScreenPolicy {
    size: InterfaceSize,

    /// The custom scale as device pixels per CSS pixel, or none when the user never set one. The
    /// screen keeps it while another size applies, so that it returns with the custom size.
    custom_scale: Option<f64>,
}

/// What decides the zoom, as the app sent it last.
#[derive(Clone, Debug, Default, PartialEq)]
struct ZoomPolicy {
    /// What the user chose for the screens they set a size for, by the key of their screen, see
    /// [`Screen::key`]. Any other screen gets its default size, see [`default_size`].
    screens: HashMap<String, ScreenPolicy>,
}

/// The policy the app sent last. Until it sends one, every screen gets its default size.
///
/// The window events read this on the main thread. Whoever locks it must therefore never call into
/// Tauri while holding the lock: on macOS, such a call may wait for the main thread, which in turn
/// would wait for the lock.
static POLICY: Lazy<Mutex<ZoomPolicy>> = Lazy::new(|| Mutex::new(ZoomPolicy::default()));

/// What the runtime told the app last, so that it tells it only about a change. The same rule
/// applies as for [`POLICY`]: never call into Tauri while holding the lock.
static LAST_REPORT: Lazy<Mutex<Option<InterfaceSizeReport>>> = Lazy::new(|| Mutex::new(None));

/// The zoom the webview has right now, as the bits of an f64.
///
/// An atomic rather than a lock, because the window events read it on the main thread for every
/// drag and drop, see `cursor_position_in_css_pixels` in `app_window.rs`.
static CURRENT_ZOOM: AtomicU64 = AtomicU64::new(1.0f64.to_bits());

/// Whether a measurement after a move of the window is already scheduled.
static SCREEN_CHANGE_PENDING: AtomicBool = AtomicBool::new(false);

/// The zoom the webview has right now.
pub fn current_zoom() -> f64 {
    f64::from_bits(CURRENT_ZOOM.load(Ordering::Acquire))
}

/// What the app sends to set the interface size.
#[derive(Deserialize)]
pub struct SetInterfaceSizeRequest {
    /// What the user chose for the screens they set a size for, each screen once.
    screens: Vec<ScreenSizeRequest>,
}

/// What the user chose for one screen, as the app sends it.
#[derive(Deserialize)]
struct ScreenSizeRequest {
    /// The screen, as the runtime reported it in `screen_key`.
    screen_key: String,

    size: InterfaceSize,

    /// The custom scale in percent, counted like the scaling of the system, or null when the user
    /// never set one for this screen.
    custom_percent: Option<u32>,
}

/// What the runtime answers about the interface size on the screen the window shows on.
///
/// All percentages count device pixels per CSS pixel, like the scaling of the system.
#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct InterfaceSizeReport {
    /// The zoom the webview has.
    zoom: f64,

    /// The size on this screen: the one the user chose for it, or else its default size.
    size: InterfaceSize,

    /// The size of this screen while the user chose none for it, see [`default_size`].
    default_size: InterfaceSize,

    /// What the app stores the size of this screen by, see [`Screen::key`].
    screen_key: String,

    /// The width of the screen in device pixels.
    screen_width: u32,

    /// The height of the screen in device pixels.
    screen_height: u32,

    /// The diagonal of the screen in inches, or null when the screen does not report its size.
    screen_diagonal_inches: Option<f64>,

    /// The scaling of the system on this screen.
    system_percent: u32,

    /// The scale AI Studio shows with.
    current_percent: u32,

    /// The scale App default gives this screen, which is also where its custom scale starts.
    app_default_percent: u32,

    /// The smallest custom scale this screen allows.
    min_percent: u32,

    /// The largest custom scale this screen allows.
    max_percent: u32,
}

/// The answer to a request about the interface size.
#[derive(Serialize)]
pub struct InterfaceSizeResponse {
    success: bool,
    error_message: String,

    #[serde(flatten)]
    report: InterfaceSizeReport,
}

/// Sets the interface size, and zooms the webview accordingly.
pub async fn set_interface_size(Json(request): Json<SetInterfaceSizeRequest>) -> Json<InterfaceSizeResponse> {
    let screens = request.screens.into_iter()
        .map(|screen| (screen.screen_key, ScreenPolicy {
            size: screen.size,
            custom_scale: screen.custom_percent.map(|percent| f64::from(percent) / 100.0),
        }))
        .collect();

    *POLICY.lock().unwrap() = ZoomPolicy { screens };
    report_on_main_thread().await
}

/// Reports the interface size on the screen the window shows on, as the settings show it.
pub async fn get_interface_size() -> Json<InterfaceSizeResponse> {
    report_on_main_thread().await
}

/// Measures the screen on the main thread, as GTK requires on Linux, zooms the webview when its
/// zoom no longer fits, and reports. Waits for the main thread without blocking the worker.
async fn report_on_main_thread() -> Json<InterfaceSizeResponse> {
    let window = crate::app_window::MAIN_WINDOW.lock().unwrap().clone();
    let Some(window) = window else {
        return failure("The main window is not available.");
    };

    let (sender, receiver) = tokio::sync::oneshot::channel();
    let ui_window = window.clone();
    if let Err(error) = window.run_on_main_thread(move || {
        let _ = sender.send(apply_zoom(&ui_window, false));
    }) {
        return failure(&format!("Failed to schedule the measurement of the screen: {error}"));
    }

    match receiver.await {
        Ok(report) => Json(InterfaceSizeResponse {
            success: true,
            error_message: String::new(),
            report,
        }),

        Err(_) => failure("The measurement of the screen did not return a result."),
    }
}

fn failure(message: &str) -> Json<InterfaceSizeResponse> {
    warn!(Source = "Interface size"; "{message}");
    Json(InterfaceSizeResponse {
        success: false,
        error_message: message.to_string(),
        report: report_for(&ZoomPolicy::default(), None),
    })
}

/// Zooms the webview to the size of the screen it shows on, right away.
///
/// Runs on the main thread. The window calls this once it opens, so that its screen shows in its
/// default size from the start, and again whenever a page has loaded: a webview may forget its zoom
/// when it loads a page from another origin, as the page of the app is. The zoom is therefore set
/// anew even when we believe the webview has it already.
pub fn zoom_window(window: &WebviewWindow) {
    apply_zoom(window, true);
}

/// Measures the screen again once a moved window rests, since it may rest on another screen now.
///
/// The window events call this on the main thread, for every move and every change of the scale
/// factor. It schedules a single measurement after the move instead of measuring each step.
pub fn follow_window(window: &WebviewWindow) {
    if SCREEN_CHANGE_PENDING.swap(true, Ordering::AcqRel) {
        return;
    }

    let window = window.clone();
    tauri::async_runtime::spawn(async move {
        tokio::time::sleep(SCREEN_CHANGE_DELAY).await;
        SCREEN_CHANGE_PENDING.store(false, Ordering::Release);

        let ui_window = window.clone();
        if let Err(error) = window.run_on_main_thread(move || {
            apply_zoom(&ui_window, false);
        }) {
            warn!(Source = "Interface size"; "Failed to schedule the zoom after the window moved: {error}");
        }
    });
}

/// Measures the screen the window shows on, zooms the webview after the policy, and reports.
///
/// Runs on the main thread. Sets the zoom only when it differs from the one the webview has, unless
/// forced to. Tells the app whenever the report differs from the one it heard of last, e.g., after
/// the window moved to another screen.
fn apply_zoom(window: &WebviewWindow, force: bool) -> InterfaceSizeReport {
    let policy = POLICY.lock().unwrap().clone();
    let screen = measure_screen(window);
    let mut report = report_for(&policy, screen);
    if force || (report.zoom - current_zoom()).abs() >= 0.001 {
        match window.set_zoom(report.zoom) {
            Ok(()) => {
                CURRENT_ZOOM.store(report.zoom.to_bits(), Ordering::Release);
                zoom_svg_icons(window, report.zoom);
                info!(Source = "Interface size"; "Zoomed the webview to {:.3} for the screen {screen:?}, which shows AI Studio at {} percent with the size {:?}.", report.zoom, report.current_percent, report.size);
            },

            Err(error) => {
                warn!(Source = "Interface size"; "Failed to zoom the webview to {:.3}: {error}", report.zoom);
                report.zoom = current_zoom();
            },
        }
    }

    let is_news = {
        let mut last_report = LAST_REPORT.lock().unwrap();
        let is_news = last_report.as_ref() != Some(&report);
        if is_news {
            *last_report = Some(report.clone());
        }

        is_news
    };

    if is_news {
        crate::app_window::send_event(Event::new(TauriEventType::InterfaceSizeChanged, Vec::new()));
    }

    report
}

/// Hands the zoom to the SVG icons of the page, on Linux, whose webview does not zoom them.
///
/// WebKitGTK, the webview on Linux, leaves the page zoom out of the font size of an SVG element.
/// The icons of MudBlazor take their size from that font size, so they would keep their size while
/// everything around them grows or shrinks. app.css multiplies their size with the CSS variable set
/// here. The webviews of Windows and macOS zoom SVG elements like everything else.
///
/// The variable belongs to the page and is gone once another one loads. Every page load sets the
/// zoom anew, see [`zoom_window`], and thereby the variable as well.
fn zoom_svg_icons(window: &WebviewWindow, zoom: f64) {
    if !cfg!(target_os = "linux") {
        return;
    }

    let script = format!("document.documentElement.style.setProperty('--svg-icon-zoom', '{zoom}');");
    if let Err(error) = window.eval(&script) {
        warn!(Source = "Interface size"; "Failed to hand the zoom to the SVG icons: {error}");
    }
}

/// The screen the window shows on.
#[derive(Clone, Copy, Debug, PartialEq)]
struct Screen {
    /// The width in device pixels.
    device_width: f64,

    /// The height in device pixels.
    device_height: f64,

    /// The scaling of the system on this screen, as device pixels per CSS pixel: 1.5 for 150 percent.
    system_scale: f64,

    /// The physical width and height in millimeters, as far as the screen reports them.
    size_mm: Option<(f64, f64)>,
}

impl Screen {
    /// Whether the screen can be measured at all.
    fn is_plausible(&self) -> bool {
        // A value which is not a number fails these comparisons, so it counts as implausible, too:
        self.device_width > 0.0 && self.device_height > 0.0 && self.system_scale > 0.0
            && self.device_width.is_finite() && self.device_height.is_finite() && self.system_scale.is_finite()
    }

    /// The physical width and height in millimeters, when the screen reports a plausible size.
    fn plausible_size_mm(&self) -> Option<(f64, f64)> {
        self.size_mm.filter(|(width_mm, height_mm)| *width_mm > 0.0 && *height_mm > 0.0 && width_mm.is_finite() && height_mm.is_finite())
    }

    /// The density in device pixels per inch, when the screen reports a plausible size.
    fn device_pixels_per_inch(&self) -> Option<f64> {
        let (width_mm, _) = self.plausible_size_mm()?;
        Some(self.device_width / (width_mm / MILLIMETERS_PER_INCH))
    }

    /// The diagonal in inches, when the screen reports a plausible size.
    fn diagonal_inches(&self) -> Option<f64> {
        self.plausible_size_mm().map(|(width_mm, height_mm)| width_mm.hypot(height_mm) / MILLIMETERS_PER_INCH)
    }

    /// Whether the screen is the size of a laptop's own. A screen of unknown size never is.
    fn is_laptop_sized(&self) -> bool {
        self.diagonal_inches().is_some_and(|diagonal| diagonal <= LARGEST_LAPTOP_SCREEN_INCHES)
    }

    /// What the app stores the custom scale of this screen by.
    ///
    /// Built from the resolution and the physical size, so that it names the model of a screen
    /// rather than the port it hangs on, which the names the systems give their monitors depend on.
    /// Two screens of the same model share their scale, which suits a user who meets the same model
    /// at another desk. The scaling of the system stays out, since the user may change it.
    fn key(&self) -> String {
        let resolution = format!("{}x{}", self.device_width.round() as i64, self.device_height.round() as i64);
        match self.plausible_size_mm() {
            Some((width_mm, height_mm)) => format!("{resolution}@{}x{}mm", width_mm.round() as i64, height_mm.round() as i64),
            None => resolution,
        }
    }
}

/// What the interface size comes to for the given policy on the given screen.
///
/// Without a screen, nothing can be measured, and the webview keeps its zoom of 1.0.
fn report_for(policy: &ZoomPolicy, screen: Option<Screen>) -> InterfaceSizeReport {
    let Some(screen) = screen.filter(Screen::is_plausible) else {
        return InterfaceSizeReport {
            zoom: 1.0,
            size: InterfaceSize::SystemScaling,
            default_size: InterfaceSize::SystemScaling,
            screen_key: String::new(),
            screen_width: 0,
            screen_height: 0,
            screen_diagonal_inches: None,
            system_percent: 100,
            current_percent: 100,
            app_default_percent: 100,
            min_percent: to_percent(MIN_SCALE),
            max_percent: to_percent(MAX_SCALE),
        };
    };

    let screen_key = screen.key();
    let (min_percent, max_percent) = custom_limits_percent(&screen);
    let app_default_scale = app_default_scale(&screen);
    let default_size = default_size(&screen);
    let screen_policy = policy.screens.get(&screen_key);
    let (size, scale) = match screen_policy.map_or(default_size, |screen_policy| screen_policy.size) {
        InterfaceSize::SystemScaling => (InterfaceSize::SystemScaling, screen.system_scale),
        InterfaceSize::AppDefault => (InterfaceSize::AppDefault, app_default_scale),
        InterfaceSize::Custom => match screen_policy.and_then(|screen_policy| screen_policy.custom_scale) {
            Some(custom_scale) if custom_scale.is_finite() => {
                let (min_scale, max_scale) = (f64::from(min_percent) / 100.0, f64::from(max_percent) / 100.0);
                (InterfaceSize::Custom, custom_scale.clamp(min_scale, max_scale))
            },

            // The app always sends a scale with the custom size. Should one be missing anyway, the screen shows as with App default:
            _ => (InterfaceSize::Custom, app_default_scale),
        },
    };

    InterfaceSizeReport {
        zoom: scale / screen.system_scale,
        size,
        default_size,
        screen_width: screen.device_width.round() as u32,
        screen_height: screen.device_height.round() as u32,
        screen_diagonal_inches: screen.diagonal_inches(),
        screen_key,
        system_percent: to_percent(screen.system_scale),
        current_percent: to_percent(scale),
        app_default_percent: to_percent(app_default_scale),
        min_percent,
        max_percent,
    }
}

/// The size of a screen the user set nothing for: App default on the screen of a laptop, and the
/// scaling of the system on any other, where App default would show the same.
fn default_size(screen: &Screen) -> InterfaceSize {
    if screen.is_laptop_sized() {
        InterfaceSize::AppDefault
    } else {
        InterfaceSize::SystemScaling
    }
}

/// The scale App default gives a screen: the reference density on the screen of a laptop, and the
/// scaling of the system on any other.
fn app_default_scale(screen: &Screen) -> f64 {
    match screen.device_pixels_per_inch() {
        Some(density) if screen.is_laptop_sized() => {
            let (min_scale, max_scale) = custom_limits(screen);
            (density / REFERENCE_CSS_PIXELS_PER_INCH).clamp(min_scale, max_scale)
        },

        _ => screen.system_scale,
    }
}

/// The smallest and the largest scale a screen allows, as device pixels per CSS pixel.
///
/// The largest keeps the screen at least as wide as the narrowest layout AI Studio supports. The
/// smallest keeps text readable: on a screen of known size by its density, on any other by the
/// scaling of the system. Should both collide on a very small screen, the layout wins.
fn custom_limits(screen: &Screen) -> (f64, f64) {
    let max_scale = (screen.device_width / MIN_CSS_WIDTH).min(MAX_SCALE);
    let readable_scale = match screen.device_pixels_per_inch() {
        Some(density) => density / MAX_CSS_PIXELS_PER_INCH,
        None => screen.system_scale * MIN_SHARE_OF_SYSTEM_SCALE_ON_UNKNOWN_SCREENS,
    };

    let min_scale = readable_scale.max(MIN_SCALE).min(max_scale);
    (min_scale, max_scale)
}

/// The limits of a screen in percent, on the steps the settings offer and within the limits.
fn custom_limits_percent(screen: &Screen) -> (u32, u32) {
    let (min_scale, max_scale) = custom_limits(screen);

    // A rounding error of the floating point numbers, such as 0.6 * 100 = 60.000000000000007, must not cost a whole step:
    let min_percent = (min_scale * 100.0 / SCALE_STEP_PERCENT - 1e-9).ceil() * SCALE_STEP_PERCENT;
    let max_percent = (max_scale * 100.0 / SCALE_STEP_PERCENT + 1e-9).floor() * SCALE_STEP_PERCENT;
    (min_percent as u32, max_percent.max(min_percent) as u32)
}

fn to_percent(scale: f64) -> u32 {
    (scale * 100.0).round().max(0.0) as u32
}

/// Measures the screen the window shows on. Runs on the main thread.
///
/// The physical size has to come from the same screen as the pixels and the scaling, or the density
/// mixes two screens. Each platform therefore asks for the screen the way Tao does in `current_monitor`.
fn measure_screen(window: &WebviewWindow) -> Option<Screen> {
    let monitor = window.current_monitor().ok().flatten()?;
    Some(Screen {
        device_width: f64::from(monitor.size().width),
        device_height: f64::from(monitor.size().height),
        system_scale: monitor.scale_factor(),
        size_mm: screen_size_mm(window, &monitor),
    })
}

/// The physical size of the screen the window shows on, in millimeters, as the monitor reports it.
///
/// Windows looks the screen up the same way Tao does for the monitor: by the window, nearest first.
#[cfg(target_os = "windows")]
fn screen_size_mm(window: &WebviewWindow, _monitor: &Monitor) -> Option<(f64, f64)> {
    use windows::Win32::Graphics::Gdi::{GetMonitorInfoW, MonitorFromWindow, MONITORINFO, MONITOR_DEFAULTTONEAREST};
    use windows::Win32::UI::HiDpi::{GetDpiForMonitor, MDT_RAW_DPI};

    let hwnd = window.hwnd().ok()?;
    let monitor = unsafe { MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST) };
    if monitor.is_invalid() {
        return None;
    }

    let mut info = MONITORINFO {
        cbSize: size_of::<MONITORINFO>() as u32,
        ..Default::default()
    };

    if !unsafe { GetMonitorInfoW(monitor, &mut info) }.as_bool() {
        return None;
    }

    // The raw DPI is the density the monitor reports of itself, unlike the one its scaling implies:
    let (mut dpi_x, mut dpi_y) = (0u32, 0u32);
    unsafe { GetDpiForMonitor(monitor, MDT_RAW_DPI, &mut dpi_x, &mut dpi_y) }.ok()?;
    if dpi_x == 0 || dpi_y == 0 {
        return None;
    }

    // Tauri makes the process aware of the DPI of each monitor, so the rectangle is in device pixels:
    let width_px = f64::from(info.rcMonitor.right - info.rcMonitor.left);
    let height_px = f64::from(info.rcMonitor.bottom - info.rcMonitor.top);
    Some((
        width_px / f64::from(dpi_x) * MILLIMETERS_PER_INCH,
        height_px / f64::from(dpi_y) * MILLIMETERS_PER_INCH,
    ))
}

/// The physical size of the given screen, in millimeters, as the display reports it.
#[cfg(target_os = "macos")]
fn screen_size_mm(_window: &WebviewWindow, monitor: &Monitor) -> Option<(f64, f64)> {
    // Core Graphics reads this point, which the compiler does not see:
    #[allow(dead_code)]
    #[repr(C)]
    struct CGPoint {
        x: f64,
        y: f64,
    }

    #[repr(C)]
    struct CGSize {
        width: f64,
        height: f64,
    }

    #[link(name = "CoreGraphics", kind = "framework")]
    unsafe extern "C" {
        fn CGGetDisplaysWithPoint(point: CGPoint, max_displays: u32, displays: *mut u32, matching_display_count: *mut u32) -> i32;
        fn CGDisplayScreenSize(display: u32) -> CGSize;
    }

    //
    // Tao takes the screen macOS shows the window on, the one holding its largest part. Core
    // Graphics has to find that display, too, rather than the one under the center of the window: a
    // window spanning two screens may have its center on the other one, whose size would then meet
    // the pixels of this one. Hence the center of the screen itself.
    //
    // Tao reports the position and the size of a screen in physical pixels, from the top left of the
    // main display. Divided by the scale factor of the screen, its center becomes a point in the
    // global display space of Core Graphics, which counts in points from the same corner:
    //
    let position = monitor.position();
    let size = monitor.size();
    let scale_factor = monitor.scale_factor();
    let center = CGPoint {
        x: (f64::from(position.x) + f64::from(size.width) / 2.0) / scale_factor,
        y: (f64::from(position.y) + f64::from(size.height) / 2.0) / scale_factor,
    };

    let (mut display, mut count) = (0u32, 0u32);
    if unsafe { CGGetDisplaysWithPoint(center, 1, &mut display, &mut count) } != 0 || count == 0 {
        return None;
    }

    let size_mm = unsafe { CGDisplayScreenSize(display) };
    Some((size_mm.width, size_mm.height))
}

/// The physical size of the screen the window shows on, in millimeters, as the monitor reports it.
///
/// GDK looks the screen up the same way Tao does for the monitor: by the window. GTK may only be
/// called on the main thread, which is where the callers run.
#[cfg(target_os = "linux")]
fn screen_size_mm(window: &WebviewWindow, _monitor: &Monitor) -> Option<(f64, f64)> {
    use gtk::prelude::*;

    let gtk_window = window.gtk_window().ok()?;
    let gdk_window = gtk_window.window()?;
    let monitor = gtk_window.display().monitor_at_window(&gdk_window)?;
    let (width_mm, height_mm) = (monitor.width_mm(), monitor.height_mm());
    if width_mm <= 0 || height_mm <= 0 {
        return None;
    }

    Some((f64::from(width_mm), f64::from(height_mm)))
}

/// Other systems report no physical size we know how to read, so their screens count as of unknown size.
#[cfg(not(any(target_os = "windows", target_os = "macos", target_os = "linux")))]
fn screen_size_mm(_window: &WebviewWindow, _monitor: &Monitor) -> Option<(f64, f64)> {
    None
}

#[cfg(test)]
mod tests {
    use super::*;

    /// Sets the given size on the given screen, with the given custom scale in percent.
    fn set(policy: &mut ZoomPolicy, screen: Option<Screen>, size: InterfaceSize, custom_percent: Option<u32>) {
        let custom_scale = custom_percent.map(|percent| f64::from(percent) / 100.0);
        policy.screens.insert(screen.unwrap().key(), ScreenPolicy { size, custom_scale });
    }

    /// The given size on the given screen alone.
    fn policy(screen: Option<Screen>, size: InterfaceSize) -> ZoomPolicy {
        let mut policy = ZoomPolicy::default();
        set(&mut policy, screen, size, None);
        policy
    }

    /// The custom size with the given scale on the given screen alone.
    fn custom(screen: Option<Screen>, percent: u32) -> ZoomPolicy {
        let mut policy = ZoomPolicy::default();
        set(&mut policy, screen, InterfaceSize::Custom, Some(percent));
        policy
    }

    /// A 13.3-inch screen in the ratio of 16:10, with 1920 pixels across.
    fn laptop(system_scale: f64) -> Option<Screen> {
        Some(Screen { device_width: 1920.0, device_height: 1200.0, system_scale, size_mm: Some((286.5, 179.1)) })
    }

    /// A 14-inch screen in the ratio of 16:10, with 1920 pixels across.
    fn fourteen_inch_laptop(system_scale: f64) -> Option<Screen> {
        Some(Screen { device_width: 1920.0, device_height: 1200.0, system_scale, size_mm: Some((301.6, 188.5)) })
    }

    /// The 14-inch screen of a MacBook Pro, which macOS lays out with 1512 points across.
    fn macbook_pro() -> Option<Screen> {
        Some(Screen { device_width: 3024.0, device_height: 1964.0, system_scale: 2.0, size_mm: Some((302.4, 196.4)) })
    }

    /// A 27-inch monitor with 3840 pixels across, at a scaling of 150 percent.
    fn external_monitor() -> Option<Screen> {
        Some(Screen { device_width: 3840.0, device_height: 2160.0, system_scale: 1.5, size_mm: Some((597.0, 336.0)) })
    }

    /// A screen which does not report its size, with 1920 pixels across at a scaling of 150 percent.
    fn screen_of_unknown_size() -> Option<Screen> {
        Some(Screen { device_width: 1920.0, device_height: 1080.0, system_scale: 1.5, size_mm: None })
    }

    fn assert_close(actual: f64, expected: f64) {
        assert!((actual - expected).abs() < 0.01, "expected about {expected}, got {actual}");
    }

    /// The density the screen shows AI Studio with after the zoom of the report, in CSS pixels per inch.
    fn css_pixels_per_inch(screen: Option<Screen>, report: &InterfaceSizeReport) -> f64 {
        let screen = screen.unwrap();
        screen.device_pixels_per_inch().unwrap() / (screen.system_scale * report.zoom)
    }

    #[test]
    fn the_app_default_shows_every_laptop_with_the_reference_density() {
        for screen in [laptop(1.0), laptop(1.5), fourteen_inch_laptop(1.5), macbook_pro()] {
            let report = report_for(&policy(screen, InterfaceSize::AppDefault), screen);
            assert_close(css_pixels_per_inch(screen, &report), REFERENCE_CSS_PIXELS_PER_INCH);
        }
    }

    #[test]
    fn the_app_default_shows_a_14_inch_laptop_at_120_percent() {
        let report = report_for(&policy(fourteen_inch_laptop(1.5), InterfaceSize::AppDefault), fourteen_inch_laptop(1.5));
        assert_eq!((report.system_percent, report.app_default_percent), (150, 120));
        assert_close(report.zoom, 0.8);
    }

    #[test]
    fn the_app_default_shows_a_13_inch_laptop_at_126_percent() {
        let report = report_for(&policy(laptop(1.5), InterfaceSize::AppDefault), laptop(1.5));
        assert_eq!(report.app_default_percent, 126);
        assert_close(report.zoom, 1.26 / 1.5);
    }

    #[test]
    fn the_app_default_shows_a_macbook_pro_at_188_percent() {
        let report = report_for(&policy(macbook_pro(), InterfaceSize::AppDefault), macbook_pro());
        assert_eq!(report.app_default_percent, 188);
        assert_close(report.zoom, 0.94);
    }

    #[test]
    fn the_app_default_keeps_the_system_scaling_on_an_external_monitor() {
        let report = report_for(&policy(external_monitor(), InterfaceSize::AppDefault), external_monitor());
        assert_close(report.zoom, 1.0);
        assert_eq!(report.app_default_percent, 150);
    }

    #[test]
    fn the_app_default_keeps_the_system_scaling_on_a_screen_of_unknown_size() {
        let report = report_for(&policy(screen_of_unknown_size(), InterfaceSize::AppDefault), screen_of_unknown_size());
        assert_close(report.zoom, 1.0);
    }

    #[test]
    fn the_system_scaling_ignores_the_screen() {
        let report = report_for(&policy(laptop(1.5), InterfaceSize::SystemScaling), laptop(1.5));
        assert_close(report.zoom, 1.0);
        assert_eq!(report.current_percent, 150);
    }

    #[test]
    fn a_custom_scale_counts_like_the_scaling_of_the_system() {
        let report = report_for(&custom(laptop(1.5), 120), laptop(1.5));
        assert_close(report.zoom, 0.8);
        assert_eq!(report.current_percent, 120);
        assert_eq!(report.size, InterfaceSize::Custom);
    }

    #[test]
    fn every_screen_keeps_a_size_of_its_own() {
        let mut zoom_policy = policy(laptop(1.5), InterfaceSize::AppDefault);
        set(&mut zoom_policy, external_monitor(), InterfaceSize::Custom, Some(175));

        let on_the_laptop = report_for(&zoom_policy, laptop(1.5));
        assert_eq!((on_the_laptop.size, on_the_laptop.current_percent), (InterfaceSize::AppDefault, 126));

        let on_the_monitor = report_for(&zoom_policy, external_monitor());
        assert_eq!((on_the_monitor.size, on_the_monitor.current_percent), (InterfaceSize::Custom, 175));
    }

    #[test]
    fn a_screen_keeps_its_custom_scale_while_another_size_applies() {
        let mut policy = ZoomPolicy::default();
        set(&mut policy, laptop(1.5), InterfaceSize::SystemScaling, Some(120));
        assert_eq!(report_for(&policy, laptop(1.5)).current_percent, 150);
    }

    #[test]
    fn a_laptop_the_user_set_nothing_for_gets_the_app_default() {
        let report = report_for(&custom(external_monitor(), 175), laptop(1.5));
        assert_eq!((report.size, report.default_size), (InterfaceSize::AppDefault, InterfaceSize::AppDefault));
        assert_eq!(report.current_percent, 126);
        assert_close(report.zoom, 1.26 / 1.5);
    }

    #[test]
    fn a_large_screen_the_user_set_nothing_for_keeps_the_system_scaling() {
        for screen in [external_monitor(), screen_of_unknown_size()] {
            let report = report_for(&custom(laptop(1.5), 120), screen);
            assert_eq!((report.size, report.default_size), (InterfaceSize::SystemScaling, InterfaceSize::SystemScaling));
            assert_close(report.zoom, 1.0);
        }
    }

    #[test]
    fn a_custom_size_without_a_scale_shows_as_with_the_app_default() {
        let report = report_for(&policy(laptop(1.5), InterfaceSize::Custom), laptop(1.5));
        assert_eq!(report.current_percent, 126);
    }

    #[test]
    fn until_the_app_sends_the_sizes_every_screen_shows_in_its_default_size() {
        let on_the_laptop = report_for(&POLICY.lock().unwrap(), laptop(1.5));
        assert_eq!((on_the_laptop.size, on_the_laptop.current_percent), (InterfaceSize::AppDefault, 126));

        let on_the_monitor = report_for(&POLICY.lock().unwrap(), external_monitor());
        assert_eq!((on_the_monitor.size, on_the_monitor.current_percent), (InterfaceSize::SystemScaling, 150));
    }

    #[test]
    fn the_key_of_a_screen_names_its_resolution_and_size_but_not_its_scaling() {
        assert_eq!(laptop(1.5).unwrap().key(), "1920x1200@287x179mm");
        assert_eq!(laptop(1.0).unwrap().key(), laptop(1.5).unwrap().key());
        assert_eq!(screen_of_unknown_size().unwrap().key(), "1920x1080");
    }

    #[test]
    fn a_laptop_allows_what_keeps_the_layout_wide_and_the_text_readable() {
        // At most 1920 / 1024 = 1.875, at least a density of 170 / 200 = 0.85 -- on the steps of five:
        let report = report_for(&ZoomPolicy::default(), laptop(1.5));
        assert_eq!((report.min_percent, report.max_percent), (90, 185));
    }

    #[test]
    fn a_macbook_pro_allows_a_range_of_its_own() {
        let report = report_for(&ZoomPolicy::default(), macbook_pro());
        assert_eq!((report.min_percent, report.max_percent), (130, 295));
    }

    #[test]
    fn a_large_monitor_allows_the_whole_range() {
        let report = report_for(&ZoomPolicy::default(), external_monitor());
        assert_eq!((report.min_percent, report.max_percent), (85, 300));
    }

    #[test]
    fn a_screen_of_unknown_size_allows_half_of_the_system_scaling_at_least() {
        let report = report_for(&ZoomPolicy::default(), screen_of_unknown_size());
        assert_eq!((report.min_percent, report.max_percent), (75, 185));
    }

    #[test]
    fn the_smallest_scale_of_all_is_not_lost_to_a_rounding_error() {
        // A 24-inch monitor with 1920 pixels across allows less than 60 percent for readability:
        let low_density_monitor = Some(Screen { device_width: 1920.0, device_height: 1080.0, system_scale: 1.0, size_mm: Some((531.0, 299.0)) });
        assert_eq!(report_for(&ZoomPolicy::default(), low_density_monitor).min_percent, 60);
    }

    #[test]
    fn a_custom_scale_beyond_the_limits_of_a_screen_is_brought_within_them() {
        assert_eq!(report_for(&custom(laptop(1.0), 300), laptop(1.0)).current_percent, 185);
        assert_eq!(report_for(&custom(macbook_pro(), 60), macbook_pro()).current_percent, 130);
    }

    #[test]
    fn a_tiny_screen_lets_the_layout_win_over_the_readable_text() {
        let tiny_screen = Some(Screen { device_width: 800.0, device_height: 600.0, system_scale: 1.0, size_mm: Some((100.0, 60.0)) });
        let report = report_for(&ZoomPolicy::default(), tiny_screen);
        assert!(report.min_percent <= report.max_percent, "min {} above max {}", report.min_percent, report.max_percent);
    }

    #[test]
    fn without_a_screen_nothing_is_zoomed() {
        let policy = custom(laptop(1.0), 150);
        assert_close(report_for(&policy, None).zoom, 1.0);

        let implausible_screen = Some(Screen { device_width: f64::NAN, device_height: 1080.0, system_scale: 1.0, size_mm: None });
        assert_close(report_for(&policy, implausible_screen).zoom, 1.0);
    }

    /// The app sends the size in PascalCase, see RustEnumConverter in the app.
    #[test]
    fn the_request_reads_the_names_the_app_sends() {
        let request: SetInterfaceSizeRequest = serde_json::from_value(serde_json::json!({
            "screens": [
                { "screen_key": "1920x1200@287x179mm", "size": "Custom", "custom_percent": 150 },
                { "screen_key": "3840x2160@597x336mm", "size": "SystemScaling", "custom_percent": null },
            ],
        })).unwrap();

        let on_the_laptop = &request.screens[0];
        assert_eq!(on_the_laptop.screen_key, "1920x1200@287x179mm");
        assert_eq!((on_the_laptop.size, on_the_laptop.custom_percent), (InterfaceSize::Custom, Some(150)));

        let on_the_monitor = &request.screens[1];
        assert_eq!((on_the_monitor.size, on_the_monitor.custom_percent), (InterfaceSize::SystemScaling, None));
    }

    /// The app reads the report as flat fields next to success and error_message, and the sizes in PascalCase.
    #[test]
    fn the_response_names_the_fields_as_the_app_reads_them() {
        let response = InterfaceSizeResponse { success: true, error_message: String::new(), report: report_for(&ZoomPolicy::default(), laptop(1.5)) };
        let json = serde_json::to_value(response).unwrap();
        for field in ["success", "error_message", "zoom", "size", "default_size", "screen_key", "screen_width", "screen_height", "screen_diagonal_inches", "system_percent", "current_percent", "app_default_percent", "min_percent", "max_percent"] {
            assert!(json.get(field).is_some(), "the response lacks the field {field}");
        }

        assert_eq!(json["size"], "AppDefault");
    }
}
