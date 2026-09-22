#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use tauri::Manager;
use tauri_plugin_shell::{process::CommandEvent, ShellExt};

#[tauri::command]
async fn open_camp_package() -> Result<Option<Vec<u8>>, String> {
    tauri::async_runtime::spawn_blocking(|| {
        let Some(path) = rfd::FileDialog::new().add_filter("Lagerpaket", &["scoutcamp"]).pick_file() else {
            return Ok(None);
        };
        if std::fs::metadata(&path).map_err(|e| e.to_string())?.len() > 100 * 1024 * 1024 {
            return Err("Das Paket ist größer als 100 MB.".into());
        }
        std::fs::read(path).map(Some).map_err(|e| e.to_string())
    }).await.map_err(|e| e.to_string())?
}

#[tauri::command]
async fn save_return_package(bytes: Vec<u8>) -> Result<bool, String> {
    tauri::async_runtime::spawn_blocking(move || {
        let Some(path) = rfd::FileDialog::new().add_filter("Lagerpaket", &["scoutcamp"])
            .set_file_name("lager-rueckpaket.scoutcamp").save_file() else { return Ok(false); };
        std::fs::write(path, bytes).map_err(|e| e.to_string())?;
        Ok(true)
    }).await.map_err(|e| e.to_string())?
}

fn main() {
    let result = tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .invoke_handler(tauri::generate_handler![open_camp_package, save_return_package])
        .setup(|app| {
            let data_directory = app.path().app_local_data_dir()?;
            std::fs::create_dir_all(&data_directory)?;
            let database = data_directory.join("scoutcampplanner.db");
            let listener = std::net::TcpListener::bind("127.0.0.1:0")?;
            let port = listener.local_addr()?.port();
            let mut random = [0u8; 32];
            getrandom::getrandom(&mut random).map_err(|e| std::io::Error::other(e.to_string()))?;
            let token: String = random.iter().map(|byte| format!("{byte:02x}")).collect();
            let api_url = format!("http://127.0.0.1:{port}");
            let config = serde_json::json!({ "apiUrl": api_url, "token": token });
            drop(listener);
            let connection_argument = format!("--Database:ConnectionString=Data Source={}", database.display());
            let parent_argument = format!("--ParentProcessId={}", std::process::id());
            let sidecar = app.shell().sidecar("ScoutCampPlanner.Api")?
                .env("SingleDevice__AccessToken", &token).args([
                "--urls".to_string(),
                api_url,
                "--SingleDevice:Enabled=true".to_string(),
                format!("--Audit:Directory={}", data_directory.join("audit-security").display()),
                format!("--IngredientSuggestions:BlsIndexPath={}", app.path().resource_dir()?.join("reference-data/bls-4.0-suggestions.json").display()),
                "--Database:Provider=Sqlite".to_string(),
                connection_argument,
                parent_argument,
            ]);
            let (mut events, child) = sidecar.spawn()?;
            tauri::async_runtime::spawn(async move {
                let _child = child;
                while let Some(event) = events.recv().await {
                    if matches!(event, CommandEvent::Error(_) | CommandEvent::Terminated(_)) {
                        break;
                    }
                }
            });
            tauri::WebviewWindowBuilder::new(app, "main", tauri::WebviewUrl::App("index.html".into()))
                .title("ScoutCampPlanner – Lokal").inner_size(1200.0, 800.0)
                .initialization_script(format!("if (location.hostname === 'tauri.localhost' || location.protocol === 'tauri:') {{ window.__SCP_DESKTOP__ = {config}; }}"))
                .on_navigation(|url| url.scheme() == "tauri" || url.host_str() == Some("tauri.localhost"))
                .build()?;
            Ok(())
        })
        .run(tauri::generate_context!());
    if let Err(error) = result {
        rfd::MessageDialog::new().set_title("ScoutCampPlanner konnte nicht starten")
            .set_description(error.to_string()).show();
    }
}
