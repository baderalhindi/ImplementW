// Which National IDs have Face ID / Touch ID set up on this device (13 Nov 2026 review). The sign-in page uses it only to
// choose between asking for Touch ID at once and setting it up first; it decides nothing about access, which the API checks.
// Only a SHA-256 of the number is kept, never the number. Storage may be unavailable (private mode): then nothing is
// remembered and the page offers the setup, which still works.

const KEY = 'pmplatform.deviceUnlock';

async function digest(nationalId: string): Promise<string | null> {
  return hashOf(`pmplatform:${nationalId}`);
}

async function hashOf(text: string): Promise<string | null> {
  try {
    const bytes = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(text));
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

const SAVED_PASSKEYS_KEY = 'pmplatform.signInMethodPasskeys';

/**
 * The passkeys this browser saved from Sign-in methods (20 Nov 2026: "only if the user saved Face ID / Touch ID from Sign-in methods
 * can they sign in with this button"), by credential id, a public identifier. The button under the Microsoft sign-in shows only when
 * there is one, and asks the device for these alone, so other passkeys the device keeps for the site are not offered.
 */
export function savedPasskeyIds(): string[] {
  try {
    const value: unknown = JSON.parse(localStorage.getItem(SAVED_PASSKEYS_KEY) ?? '[]');
    return Array.isArray(value)
      ? value.filter((item): item is string => typeof item === 'string')
      : [];
  } catch {
    return [];
  }
}

function writeSavedPasskeys(ids: string[]): void {
  try {
    if (ids.length === 0) {
      localStorage.removeItem(SAVED_PASSKEYS_KEY);
    } else {
      localStorage.setItem(SAVED_PASSKEYS_KEY, JSON.stringify(ids));
    }
  } catch {
    // Not remembered: the button stays hidden until Face ID / Touch ID is added here again.
  }
}

export function rememberSavedPasskey(credentialId: string): void {
  const ids = savedPasskeyIds();
  if (!ids.includes(credentialId)) {
    writeSavedPasskeys([...ids, credentialId]);
  }
}

/** The API knew none of them (they were removed from the account): the button goes until one is added again. */
export function forgetSavedPasskeys(): void {
  writeSavedPasskeys([]);
}

/** The server did not know the key this device offered (it was removed): nothing here is set up any more. */
export function forgetAllDeviceUnlocks(): void {
  write([]);
}

export async function forgetDeviceUnlock(nationalId: string): Promise<void> {
  const hash = await digest(nationalId);
  if (hash !== null) {
    write(read().filter((item) => item !== hash));
  }
}
