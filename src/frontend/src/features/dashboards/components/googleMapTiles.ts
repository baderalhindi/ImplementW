/**
 * Google's Map Tiles API, which draws the same roadmap as Google Maps inside the platform's own map. The Maps JavaScript API is not used:
 * it needs 'unsafe-inline' or per-request nonces in the CSP, and the SPA is a static build that holds its session token behind that policy.
 */
const ORIGIN = 'https://tile.googleapis.com';
/** Which country's view of borders and names the tiles show. */
const REGION = 'SA';
/** The site's own policy sends no referrer, but Google checks a key restricted to AHDA's sites against one. */
export const REFERRER_POLICY = 'strict-origin-when-cross-origin';

export interface MapTilesSession {
  token: string;
  /** Unix seconds. */
  expiry: number;
}

export interface MapViewport {
  zoom: number;
  north: number;
  south: number;
  east: number;
  west: number;
}

function apiKey(): string {
  return import.meta.env.VITE_GOOGLE_MAPS_API_KEY ?? '';
}

async function createSession(language: string, key: string): Promise<MapTilesSession | null> {
  const highDensity = window.devicePixelRatio > 1;
  const response = await fetch(`${ORIGIN}/v1/createSession?key=${encodeURIComponent(key)}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    referrerPolicy: REFERRER_POLICY,
    body: JSON.stringify({
      mapType: 'roadmap',
      language,
      region: REGION,
      // Twice the pixels in the same tile, so the map stays sharp on a high-density screen.
      ...(highDensity ? { scale: 'scaleFactor2x', highDpi: true } : {}),
    }),
  });
  if (!response.ok) {
    return null;
  }
  const session = (await response.json()) as { session: string; expiry: string };
  return { token: session.session, expiry: Number(session.expiry) };
}

const sessions = new Map<string, Promise<MapTilesSession | null>>();

function open(language: string, key: string): Promise<MapTilesSession | null> {
  const opening = createSession(language, key)
    .catch(() => null)
    .then((session) => {
      if (session === null) {
        sessions.delete(language);
      }
      return session;
    });
  sessions.set(language, opening);
  return opening;
}

/** A session for the language, reused while it lasts. Null without a key or when Google refuses; the map then draws on a plain background. */
export async function openMapTilesSession(language: string): Promise<MapTilesSession | null> {
  const key = apiKey();
  if (key === '') {
    return null;
  }
  const pending = sessions.get(language);
  if (pending === undefined) {
    return open(language, key);
  }
  const session = await pending;
  return session !== null && session.expiry * 1000 > Date.now() ? session : open(language, key);
}

export function mapTileUrl(session: MapTilesSession): string {
  const query = `session=${encodeURIComponent(session.token)}&key=${encodeURIComponent(apiKey())}`;
  return `${ORIGIN}/v1/2dtiles/{z}/{x}/{y}?${query}`;
}

/** The data owners' credit Google requires for what the viewport shows. Null when it cannot be read. */
export async function readMapCopyright(
  session: MapTilesSession,
  viewport: MapViewport,
): Promise<string | null> {
  const query = new URLSearchParams({
    session: session.token,
    key: apiKey(),
    zoom: String(viewport.zoom),
    north: String(viewport.north),
    south: String(viewport.south),
    east: String(viewport.east),
    west: String(viewport.west),
  });
  try {
    const response = await fetch(`${ORIGIN}/tile/v1/viewport?${query.toString()}`, {
      referrerPolicy: REFERRER_POLICY,
    });
    if (!response.ok) {
      return null;
    }
    const { copyright } = (await response.json()) as { copyright?: string };
    return copyright ?? null;
  } catch {
    return null;
  }
}

/** Forgets the sessions, for tests. */
export function resetMapTilesSessions(): void {
  sessions.clear();
}
