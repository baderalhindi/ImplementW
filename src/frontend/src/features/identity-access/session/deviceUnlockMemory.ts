// Which National IDs have Face ID / Touch ID set up on this device (13 Nov 2026 review). The sign-in page uses it only to
// choose between asking for Touch ID at once and setting it up first; it decides nothing about access, which the API checks.
// Only a SHA-256 of the number is kept, never the number. Storage may be unavailable (private mode): then nothing is
// remembered and the page offers the setup, which still works.

const KEY = 'pmplatform.deviceUnlock';

async function digest(nationalId: string): Promise<string | null> {
  try {
    const bytes = await crypto.subtle.digest(
      'SHA-256',
      new TextEncoder().encode(`pmplatform:${nationalId}`),
    );
    return Array.from(new Uint8Array(bytes), (byte) => byte.toString(16).padStart(2, '0')).join('');
  } catch {
    return null;
  }
}

function read(): string[] {
  try {
    const value: unknown = JSON.parse(localStorage.getItem(KEY) ?? '[]');
    return Array.isArray(value)
      ? value.filter((item): item is string => typeof item === 'string')
      : [];
  } catch {
    return [];
  }
}

function write(values: string[]): void {
  try {
    localStorage.setItem(KEY, JSON.stringify(values));
  } catch {
    // Not remembered: the next sign-in offers the setup again.
  }
}

/** Whether Face ID / Touch ID was saved on this device for any account: then one press can sign in without a National ID. */
export function hasAnyDeviceUnlock(): boolean {
  return read().length > 0;
}

export async function hasDeviceUnlock(nationalId: string): Promise<boolean> {
  const hash = await digest(nationalId);
  return hash !== null && read().includes(hash);
}

export async function rememberDeviceUnlock(nationalId: string): Promise<void> {
  const hash = await digest(nationalId);
  if (hash !== null && !read().includes(hash)) {
    write([...read(), hash]);
  }
}

export async function forgetDeviceUnlock(nationalId: string): Promise<void> {
  const hash = await digest(nationalId);
  if (hash !== null) {
    write(read().filter((item) => item !== hash));
  }
}
