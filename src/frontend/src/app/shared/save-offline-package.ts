interface PackageFileHandle {
  createWritable(): Promise<{ write(data: Blob): Promise<void>; close(): Promise<void> }>;
}

type PackagePickerWindow = Window & {
  showSaveFilePicker?: (options: { suggestedName: string }) => Promise<PackageFileHandle>;
};

export async function saveOfflinePackage(
  fileName: string,
  createPackage: () => Promise<Blob>,
  browser: PackagePickerWindow = window,
): Promise<boolean> {
  if (!browser.showSaveFilePicker) {
    throw new Error('Bitte verwende für den Offlineexport einen Browser mit Dateiauswahl, beispielsweise Chrome oder Edge.');
  }
  let handle: PackageFileHandle;
  try {
    handle = await browser.showSaveFilePicker({ suggestedName: fileName });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return false;
    throw error;
  }
  const writable = await handle.createWritable();
  const blob = await createPackage();
  await writable.write(blob);
  await writable.close();
  return true;
}
