import { vi } from 'vitest';
import { openDesktopPackage } from './open-desktop-package';
import '../core/desktop-runtime';
const desktopCommand = vi.fn();

describe('Native package transfer', () => {
  beforeEach(() => {
    window.__SCP_DESKTOP__ = { apiUrl: 'http://localhost:5180', token: 'test' };
    window.__TAURI__ = { core: { invoke: desktopCommand } };
  });
  afterEach(() => { vi.resetAllMocks(); delete window.__SCP_DESKTOP__; delete window.__TAURI__; });
  it('cancels without reading or closing a selection', async () => {
    vi.mocked(desktopCommand).mockResolvedValue(null);
    expect(await openDesktopPackage()).toBeNull();
    expect(desktopCommand).toHaveBeenCalledTimes(1);
  });
  it('transfers bounded chunks and releases the native selection', async () => {
    vi.mocked(desktopCommand).mockImplementation(async (name, args) => {
      if (name === 'open_camp_package') return { id: 'selection', size: 2050 };
      if (name === 'read_camp_package_chunk') return Array(Math.min(1024, 2050 - Number(args!['offset']))).fill(255);
      return undefined;
    });
    const result = await openDesktopPackage();
    expect(result?.size).toBe(2050);
    expect(desktopCommand).toHaveBeenLastCalledWith('close_camp_package', { id: 'selection' });
  });
  it('rejects truncated transfers and still releases the selection', async () => {
    vi.mocked(desktopCommand).mockResolvedValueOnce({ id: 'selection', size: 10 }).mockResolvedValueOnce([]).mockResolvedValueOnce(undefined);
    await expect(openDesktopPackage()).rejects.toThrow('unvollständig');
    expect(desktopCommand).toHaveBeenLastCalledWith('close_camp_package', { id: 'selection' });
  });
  it('preserves a native transport failure without retrying the file command', async () => {
    vi.mocked(desktopCommand).mockRejectedValue(new TypeError('Failed to fetch'));
    await expect(openDesktopPackage()).rejects.toThrow('Failed to fetch');
    expect(desktopCommand).toHaveBeenCalledTimes(1);
  });
});
