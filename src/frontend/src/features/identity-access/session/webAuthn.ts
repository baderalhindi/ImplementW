// Passkeys in the browser (13 Nov 2026 review). The API speaks WebAuthn's JSON form, binary values base64url; the browser's
// navigator.credentials wants ArrayBuffers. These helpers convert between the two and say what unlocks a passkey here.

function toBuffer(value: string): ArrayBuffer {
  const base64 = value.replace(/-/g, '+').replace(/_/g, '/');
  const binary = atob(base64 + '='.repeat((4 - (base64.length % 4)) % 4));
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index += 1) {
    bytes[index] = binary.charCodeAt(index);
  }
  return bytes.buffer;
}

function toBase64Url(buffer: ArrayBuffer): string {
  let binary = '';
  for (const byte of new Uint8Array(buffer)) {
    binary += String.fromCharCode(byte);
  }
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/** Drops nulls, which WebAuthn's dictionaries do not accept where a member is simply absent. */
function withoutNulls(value: Record<string, unknown>): Record<string, unknown> {
  return Object.fromEntries(Object.entries(value).filter(([, member]) => member !== null));
}

interface CredentialDescriptorJson {
  id: string;
  type: string;
  transports?: string[] | null;
}

const descriptors = (list: unknown): PublicKeyCredentialDescriptor[] =>
  ((list ?? []) as CredentialDescriptorJson[]).map((descriptor) => ({
    type: 'public-key',
    id: toBuffer(descriptor.id),
    ...(descriptor.transports
      ? { transports: descriptor.transports as AuthenticatorTransport[] }
      : {}),
  }));

export function creationOptions(json: Record<string, unknown>): PublicKeyCredentialCreationOptions {
  const options = withoutNulls(json);
  const user = options.user as { id: string; name: string; displayName: string };
  return {
    ...(options as unknown as PublicKeyCredentialCreationOptions),
    challenge: toBuffer(options.challenge as string),
    user: { ...user, id: toBuffer(user.id) },
    excludeCredentials: descriptors(options.excludeCredentials),
  };
}

export function requestOptions(json: Record<string, unknown>): PublicKeyCredentialRequestOptions {
  const options = withoutNulls(json);
  return {
    ...(options as unknown as PublicKeyCredentialRequestOptions),
    challenge: toBuffer(options.challenge as string),
    allowCredentials: descriptors(options.allowCredentials),
  };
}

/** The device's answer as the API reads it (PasskeyCredential): every binary value base64url. */
export function credentialJson(credential: PublicKeyCredential): unknown {
  const response = credential.response as AuthenticatorResponse &
    Partial<AuthenticatorAttestationResponse> &
    Partial<AuthenticatorAssertionResponse>;
  const body: Record<string, unknown> = { clientDataJson: toBase64Url(response.clientDataJSON) };
  if (response.attestationObject !== undefined) {
    body.attestationObject = toBase64Url(response.attestationObject);
    body.transports = response.getTransports?.() ?? [];
  }
  if (response.authenticatorData !== undefined && response.signature !== undefined) {
    body.authenticatorData = toBase64Url(response.authenticatorData);
    body.signature = toBase64Url(response.signature);
    body.userHandle = response.userHandle ? toBase64Url(response.userHandle) : null;
  }
  return {
    id: credential.id,
    rawId: toBase64Url(credential.rawId),
    type: credential.type,
    response: body,
  };
}

/** Whether this browser can use a passkey unlocked by the device itself (Face ID, Touch ID, Windows Hello, fingerprint). */
export async function platformPasskeysAvailable(): Promise<boolean> {
  if (typeof PublicKeyCredential === 'undefined' || !window.isSecureContext) {
    return false;
  }
  try {
    return await PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable();
  } catch {
    return false;
  }
}

/** What unlocks a passkey on this device, for the button's label. A guess from the platform, never a security decision. */
export type DeviceUnlock = 'faceId' | 'touchId' | 'windowsHello' | 'fingerprint' | 'deviceUnlock';

export function deviceUnlock(
  userAgent: string = navigator.userAgent,
  touchPoints: number = navigator.maxTouchPoints,
): DeviceUnlock {
  const mac = userAgent.includes('Macintosh');
  if (/iPhone|iPad|iPod/.test(userAgent) || (mac && touchPoints > 1)) {
    return 'faceId';
  }
  if (mac || userAgent.includes('Mac OS X')) {
    return 'touchId';
  }
  if (userAgent.includes('Windows')) {
    return 'windowsHello';
  }
  if (userAgent.includes('Android')) {
    return 'fingerprint';
  }
  return 'deviceUnlock';
}

const DEVICE_NAMES: Record<DeviceUnlock, string> = {
  faceId: 'iPhone',
  touchId: 'Mac',
  windowsHello: 'Windows',
  fingerprint: 'Android',
  deviceUnlock: 'Device',
};

/** A name for the device in hand, for the person's list of devices. */
export function deviceName(unlock: DeviceUnlock = deviceUnlock()): string {
  return DEVICE_NAMES[unlock];
}

/** The person closed the device's prompt, or the device had no passkey for this site. */
export function isPasskeyCancelled(error: unknown): boolean {
  return (
    error instanceof DOMException &&
    (error.name === 'NotAllowedError' || error.name === 'AbortError')
  );
}
