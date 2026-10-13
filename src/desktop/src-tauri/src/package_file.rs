use std::sync::Mutex;

const MAX_PACKAGE_BYTES: usize = 100 * 1024 * 1024;
const CHUNK_BYTES: usize = 1024; // JSON byte array remains below Tauri's 8192-byte channel threshold.

#[derive(Default)]
pub struct PackageFile(Mutex<Option<(String, Vec<u8>)>>);

#[derive(serde::Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Selection {
    id: String,
    size: usize,
}

impl PackageFile {
    fn select(&self, bytes: Vec<u8>) -> Result<Selection, String> {
        if bytes.is_empty() || bytes.len() > MAX_PACKAGE_BYTES {
            return Err("Das Paket ist leer oder größer als 100 MB.".into());
        }
        let mut random = [0u8; 16];
        getrandom::getrandom(&mut random).map_err(|e| e.to_string())?;
        let id: String = random.iter().map(|byte| format!("{byte:02x}")).collect();
        let size = bytes.len();
        *self.0.lock().map_err(|e| e.to_string())? = Some((id.clone(), bytes));
        Ok(Selection { id, size })
    }

    fn read(&self, id: &str, offset: usize) -> Result<Vec<u8>, String> {
        let guard = self.0.lock().map_err(|e| e.to_string())?;
        let (_, bytes) = guard.as_ref().filter(|(key, _)| key == id)
            .ok_or("Dateiauswahl ist nicht mehr verfügbar. Bitte erneut auswählen.")?;
        if offset >= bytes.len() { return Err("Ungültiger Dateiblock.".into()); }
        Ok(bytes[offset..(offset + CHUNK_BYTES).min(bytes.len())].to_vec())
    }
}

#[tauri::command]
pub async fn open_camp_package(app: tauri::AppHandle) -> Result<Option<Selection>, String> {
    use tauri::Manager;
    tauri::async_runtime::spawn_blocking(move || {
        let Some(path) = rfd::FileDialog::new().add_filter("Lagerpaket", &["scoutcamp"]).pick_file() else {
            return Ok(None);
        };
        // Bound the actual read as well as metadata (the selected file can change).
        use std::io::Read;
        let file = std::fs::File::open(path).map_err(|e| format!("Datei konnte nicht geöffnet werden: {e}"))?;
        let mut bytes = Vec::new();
        file.take(MAX_PACKAGE_BYTES as u64 + 1).read_to_end(&mut bytes)
            .map_err(|e| format!("Datei konnte nicht gelesen werden: {e}"))?;
        app.state::<PackageFile>().select(bytes).map(Some)
    }).await.map_err(|e| e.to_string())?
}

#[tauri::command]
pub fn read_camp_package_chunk(state: tauri::State<'_, PackageFile>, id: String, offset: usize) -> Result<Vec<u8>, String> {
    state.read(&id, offset)
}

#[tauri::command]
pub fn close_camp_package(state: tauri::State<'_, PackageFile>, id: String) -> Result<(), String> {
    let mut guard = state.0.lock().map_err(|e| e.to_string())?;
    if guard.as_ref().is_some_and(|(key, _)| key == &id) { *guard = None; }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn package_roundtrip_and_json_blocks_are_bounded() {
        let state = PackageFile::default();
        let bytes: Vec<u8> = (0..62411).map(|i| (i % 256) as u8).collect();
        let selection = state.select(bytes.clone()).unwrap();
        let mut result = Vec::new();
        while result.len() < selection.size {
            let chunk = state.read(&selection.id, result.len()).unwrap();
            assert!(serde_json::to_vec(&chunk).unwrap().len() < 8192);
            result.extend(chunk);
        }
        assert_eq!(result, bytes);
        assert!(state.read(&selection.id, selection.size).is_err());
    }

    #[test]
    fn only_current_selection_can_be_read_and_empty_is_rejected() {
        let state = PackageFile::default();
        assert!(state.select(vec![]).is_err());
        let old = state.select(vec![1]).unwrap();
        let new = state.select(vec![2]).unwrap();
        assert!(state.read(&old.id, 0).is_err());
        assert_eq!(state.read(&new.id, 0).unwrap(), vec![2]);
    }
}
