use tauri::Manager;
use tauri_plugin_shell::{process::CommandEvent, ShellExt};

fn main() {
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .setup(|app| {
            let data_directory = app.path().app_local_data_dir()?;
            std::fs::create_dir_all(&data_directory)?;
            let database = data_directory.join("scoutcampplanner.db");
            let connection_argument = format!("--Database:ConnectionString=Data Source={}", database.display());
            let parent_argument = format!("--ParentProcessId={}", std::process::id());
            let sidecar = app.shell().sidecar("ScoutCampPlanner.Api")?.args([
                "--urls".to_string(),
                "http://127.0.0.1:5180".to_string(),
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
            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("failed to run ScoutCampPlanner desktop application");
}
