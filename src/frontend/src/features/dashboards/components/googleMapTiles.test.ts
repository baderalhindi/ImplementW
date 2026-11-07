import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  mapTileUrl,
  openMapTilesSession,
  readMapCopyright,
  REFERRER_POLICY,
  resetMapTilesSessions,
} from './googleMapTiles.ts';

const VIEWPORT = { zoom: 7, north: 26, south: 24, east: 51, west: 48 };
const inTwoWeeks = (): string => String(Math.floor(Date.now() / 1000) + 14 * 24 * 3600);

function respondWith(status: number, body: unknown): ReturnType<typeof vi.fn> {
  const fetchMock = vi.fn(() => Promise.resolve(new Response(JSON.stringify(body), { status })));
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function requestOf(fetchMock: ReturnType<typeof vi.fn>): [string, RequestInit] {
  return fetchMock.mock.calls[0] as [string, RequestInit];
}

describe('googleMapTiles', () => {
  beforeEach(() => {
    resetMapTilesSessions();
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
    vi.stubEnv('VITE_GOOGLE_MAPS_API_KEY', 'test-key');
  });

  it('asks Google for nothing without a key', async () => {
    vi.stubEnv('VITE_GOOGLE_MAPS_API_KEY', '');
    const fetchMock = respondWith(200, {});

    await expect(openMapTilesSession('en')).resolves.toBeNull();

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('opens a roadmap session for Saudi Arabia in the language, sending the referrer the key is restricted to', async () => {
    const fetchMock = respondWith(200, { session: 'abc', expiry: inTwoWeeks() });

    const session = await openMapTilesSession('ar');

    expect(session?.token).toBe('abc');
    const [url, init] = requestOf(fetchMock);
    expect(url).toBe('https://tile.googleapis.com/v1/createSession?key=test-key');
    expect(init.referrerPolicy).toBe(REFERRER_POLICY);
    expect(JSON.parse(init.body as string)).toMatchObject({
      mapType: 'roadmap',
      language: 'ar',
      region: 'SA',
    });
  });

  it('asks for sharper tiles only on a high-density screen', async () => {
    vi.stubGlobal('devicePixelRatio', 2);
    const fetchMock = respondWith(200, { session: 'abc', expiry: inTwoWeeks() });

    await openMapTilesSession('en');

    expect(JSON.parse(requestOf(fetchMock)[1].body as string)).toMatchObject({
      scale: 'scaleFactor2x',
      highDpi: true,
    });
  });

  it('reuses a live session per language', async () => {
    const fetchMock = respondWith(200, { session: 'abc', expiry: inTwoWeeks() });

    await Promise.all([openMapTilesSession('en'), openMapTilesSession('en')]);
    await openMapTilesSession('en');
    expect(fetchMock).toHaveBeenCalledTimes(1);

    await openMapTilesSession('ar');
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('opens a new session once the last has expired', async () => {
    const fetchMock = respondWith(200, { session: 'old', expiry: '1000' });

    await openMapTilesSession('en');
    await openMapTilesSession('en');

    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('gives no session when Google refuses or cannot be reached, and tries again next time', async () => {
    respondWith(403, { error: 'refused' });
    await expect(openMapTilesSession('en')).resolves.toBeNull();

    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('offline')));
    await expect(openMapTilesSession('en')).resolves.toBeNull();

    respondWith(200, { session: 'abc', expiry: inTwoWeeks() });
    await expect(openMapTilesSession('en')).resolves.toMatchObject({ token: 'abc' });
  });

  it('builds the tile address Leaflet fills in', () => {
    expect(mapTileUrl({ token: 'a b', expiry: 0 })).toBe(
      'https://tile.googleapis.com/v1/2dtiles/{z}/{x}/{y}?session=a%20b&key=test-key',
    );
  });

  it('reads the credit for the viewport, and null when it cannot', async () => {
    const session = { token: 'abc', expiry: 0 };
    const fetchMock = respondWith(200, { copyright: 'Map data ©2026 Google' });

    await expect(readMapCopyright(session, VIEWPORT)).resolves.toBe('Map data ©2026 Google');
    const [url, init] = requestOf(fetchMock);
    expect(url).toContain('/tile/v1/viewport?session=abc&key=test-key&zoom=7&north=26');
    expect(init.referrerPolicy).toBe(REFERRER_POLICY);

    respondWith(400, {});
    await expect(readMapCopyright(session, VIEWPORT)).resolves.toBeNull();

    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('offline')));
    await expect(readMapCopyright(session, VIEWPORT)).resolves.toBeNull();
  });
});
