import { vi } from 'vitest';
import { saveOfflinePackage } from './save-offline-package';

describe('saveOfflinePackage', () => {
  it('does not start a transfer when the save dialog is cancelled', async () => {
    const create = vi.fn();
    const browser = { showSaveFilePicker: vi.fn().mockRejectedValue(new DOMException('Cancelled', 'AbortError')) };
    expect(await saveOfflinePackage('camp.scoutcamp', create, browser as unknown as Window)).toBe(false);
    expect(create).not.toHaveBeenCalled();
  });

  it('chooses and opens the destination before starting the transfer', async () => {
    const calls: string[] = [];
    const blob = new Blob(['package']);
    const write = vi.fn(async () => { calls.push('write'); });
    const close = vi.fn(async () => { calls.push('close'); });
    const browser = { showSaveFilePicker: async () => {
      calls.push('choose');
      return { createWritable: async () => { calls.push('open'); return { write, close }; } };
    } };
    expect(await saveOfflinePackage('camp.scoutcamp', async () => {
      calls.push('transfer'); return blob;
    }, browser as unknown as Window)).toBe(true);
    expect(calls).toEqual(['choose', 'open', 'transfer', 'write', 'close']);
    expect(write).toHaveBeenCalledWith(blob);
  });
});
