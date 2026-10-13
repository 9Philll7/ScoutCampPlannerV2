import { desktopCommand } from '../core/desktop-runtime';

export async function openDesktopPackage(): Promise<Blob | null> {
  const selected = await desktopCommand<{ id: string; size: number } | null>('open_camp_package');
  if (selected === null) return null;
  try {
    if (!selected.id || !Number.isSafeInteger(selected.size) || selected.size <= 0 || selected.size > 100 * 1024 * 1024)
      throw new Error('Ungültige Dateiauswahl. Bitte die aktuelle Desktop-App installieren.');
    const bytes = new Uint8Array(selected.size);
    for (let offset = 0; offset < bytes.length;) {
      const chunk = await desktopCommand<number[]>('read_camp_package_chunk', { id: selected.id, offset });
      if (!Array.isArray(chunk) || !chunk.length || chunk.length > 1024 || offset + chunk.length > bytes.length ||
          chunk.some(value => !Number.isInteger(value) || value < 0 || value > 255))
        throw new Error('Die Paketdatei wurde unvollständig übertragen.');
      bytes.set(chunk, offset); offset += chunk.length;
    }
    return new Blob([bytes], { type: 'application/octet-stream' });
  } finally {
    // Cleanup must never hide the read error or cause an already completed import to be retried.
    await desktopCommand('close_camp_package', { id: selected.id }).catch(() => {});
  }
}
