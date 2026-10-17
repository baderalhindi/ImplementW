import { createHmac, randomUUID } from 'node:crypto';

import { type APIRequestContext, request } from '@playwright/test';

import { credentialOf, environment, type Credential, type Persona } from './environment.ts';

// The platform's API as the SPA calls it (src/shared/api/httpClient.ts): same origin, /api/v1, a bearer token, an
// Idempotency-Key on every write and If-Match from the ETag. Journeys use it to prepare what a step needs and to read back
// what the browser cannot show (a status code, a denied write); everything a journey asserts as the user's experience is
// asserted in the browser.

export interface Session {
  accessToken: string;
  refreshToken: string;
  user: {
    id: string;
    userType: 'INTERNAL' | 'EXTERNAL';
    username: string;
    displayName: string;
    roleAssignments: {
      roleCode: string;
      departmentId: string | null;
      externalEntityId: string | null;
      projectId: string | null;
    }[];
  };
}

export interface ApiResponse<T = unknown> {
  status: number;
  body: T;
  etag: string | null;
  /** The problem body's code on a refusal, e.g. FORBIDDEN or VERSION_CONFLICT. */
  code: string | null;
}

export interface WriteOptions {
  ifMatch?: string | null;
  idempotencyKey?: string;
}

const API = '/api/v1';
const SIGN_IN_ATTEMPTS = 6;

/** RFC 6238 TOTP (SHA-1, 30 s, 6 digits) for a real enrolment's base32 secret. */
function totp(secret: string, now = Date.now()): string {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  let bits = '';
  for (const char of secret.replace(/=+$/, '').toUpperCase()) {
    bits += alphabet.indexOf(char).toString(2).padStart(5, '0');
  }
  const key = Buffer.from((bits.match(/.{8}/g) ?? []).map((byte) => parseInt(byte, 2)));
  const counter = Buffer.alloc(8);
  counter.writeBigUInt64BE(BigInt(Math.floor(now / 30000)));
  const hmac = createHmac('sha1', key).update(counter).digest();
  const offset = (hmac[hmac.length - 1] ?? 0) & 0xf;
  return ((hmac.readUInt32BE(offset) & 0x7fffffff) % 1_000_000).toString().padStart(6, '0');
}

function secondFactor(credential: Credential): string {
  if (credential.totpSecret !== undefined) return totp(credential.totpSecret);
  if (credential.mfaCode !== undefined) return credential.mfaCode;
  throw new Error(`${credential.username} needs a second factor and the credentials give none.`);
}

async function context(): Promise<APIRequestContext> {
  const baseURL = environment.baseUrl();
  // The API refuses a write whose Origin is not the SPA's (TASK-078) and a tokenless write without JSON (TASK-079).
  return request.newContext({
    baseURL,
    extraHTTPHeaders: { Origin: new URL(baseURL).origin },
    ignoreHTTPSErrors: environment.target === 'compose', // the e2e stack's internal CA (playwright.config.ts)
  });
}

async function read<T>(
  response: Awaited<ReturnType<APIRequestContext['fetch']>>,
): Promise<ApiResponse<T>> {
  const text = await response.text();
  const body = (text === '' ? undefined : JSON.parse(text)) as T;
  const code =
    !response.ok() && typeof (body as { code?: unknown } | undefined)?.code === 'string'
      ? (body as { code: string }).code
      : null;
  return { status: response.status(), body, etag: response.headers().etag ?? null, code };
}

/**
 * A sign-in call that waits out the throttle (30 a minute per client address, on every sign-in step) rather than failing
 * on it: several journeys share one client address.
 */
async function postThrottled(http: APIRequestContext, path: string, data: unknown) {
  for (let attempt = 1; ; attempt++) {
    const response = await http.post(`${API}${path}`, { data });
    if (response.status() !== 429 || attempt >= SIGN_IN_ATTEMPTS) return response;
    const wait = Number(response.headers()['retry-after'] ?? '10');
    await new Promise((resolve) => setTimeout(resolve, (wait + 1) * 1000));
  }
}

async function signIn(http: APIRequestContext, persona: Persona): Promise<Session> {
  const credential = credentialOf(persona);
  const first = await read<Session & { mfaToken?: string }>(
    await postThrottled(http, '/sessions', {
      username: credential.username,
      password: credential.password,
    }),
  );
  if (first.status !== 200 && first.status !== 201) {
    throw new Error(
      `Sign-in of ${persona} (${credential.username}) answered ${String(first.status)} ${String(first.code)}.`,
    );
  }
  if (first.body.mfaToken === undefined) return first.body;

  const challenge = await read<{ challengeId: string }>(
    await postThrottled(http, '/sessions/mfa-challenge', { mfaToken: first.body.mfaToken }),
  );
  const verified = await read<Session>(
    await postThrottled(http, '/sessions/mfa', {
      mfaToken: first.body.mfaToken,
      challengeId: challenge.body.challengeId,
      code: secondFactor(credential),
    }),
  );
  if (
    (verified.status !== 200 && verified.status !== 201) ||
    'mfaToken' in (verified.body as object)
  ) {
    throw new Error(
      `The second factor of ${persona} was refused: ${String(verified.status)} ${String(verified.code)}.`,
    );
  }
  return verified.body;
}

export class PlatformApi {
  private readonly http: APIRequestContext;
  readonly persona: Persona;
  session: Session;

  private constructor(http: APIRequestContext, persona: Persona, session: Session) {
    this.http = http;
    this.persona = persona;
    this.session = session;
  }

  static async signIn(persona: Persona): Promise<PlatformApi> {
    const http = await context();
    return new PlatformApi(http, persona, await signIn(http, persona));
  }

  get userId(): string {
    return this.session.user.id;
  }

  /** Any request, answered as it comes: for denials, conflicts and replays the journey asserts itself. */
  async send<T = unknown>(
    method: 'GET' | 'POST' | 'PUT' | 'DELETE',
    path: string,
    body?: unknown,
    options: WriteOptions = {},
  ): Promise<ApiResponse<T>> {
    const headers: Record<string, string> = { Authorization: `Bearer ${this.session.accessToken}` };
    if (method !== 'GET' && method !== 'DELETE')
      headers['Idempotency-Key'] = options.idempotencyKey ?? randomUUID();
    if (options.ifMatch !== undefined && options.ifMatch !== null)
      headers['If-Match'] = options.ifMatch;
    const response = await this.http.fetch(`${API}${path}`, {
      method,
      headers,
      ...(body === undefined ? {} : { data: body }),
    });
    return read<T>(response);
  }

  /** A request that must succeed: anything but 2xx fails the journey with the API's own answer. */
  async ok<T = unknown>(
    method: 'GET' | 'POST' | 'PUT' | 'DELETE',
    path: string,
    body?: unknown,
    options: WriteOptions = {},
  ): Promise<ApiResponse<T>> {
    const response = await this.send<T>(method, path, body, options);
    if (response.status < 200 || response.status > 299) {
      throw new Error(
        `${this.persona}: ${method} ${path} answered ${String(response.status)} ${JSON.stringify(response.body)}`,
      );
    }
    return response;
  }

  get<T = unknown>(path: string): Promise<T> {
    return this.ok<T>('GET', path).then((response) => response.body);
  }

  post<T = unknown>(path: string, body?: unknown, options?: WriteOptions): Promise<T> {
    return this.ok<T>('POST', path, body ?? {}, options).then((response) => response.body);
  }

  /** A fresh second factor on this session (ADR-010), for the privileged operations that ask for one. */
  async stepUp(): Promise<void> {
    const challenge = await this.ok<{ challengeId: string }>(
      'POST',
      '/sessions/current/step-up-challenge',
      {
        refreshToken: this.session.refreshToken,
      },
    );
    const stepped = await this.ok<Session>('POST', '/sessions/current/step-up', {
      refreshToken: this.session.refreshToken,
      challengeId: challenge.body.challengeId,
      code: secondFactor(credentialOf(this.persona)),
    });
    this.session = stepped.body;
  }

  async dispose(): Promise<void> {
    await this.http.dispose();
  }
}

/** Polls until the read satisfies the condition; journeys wait for workers and the outbox this way, never by sleeping. */
export async function eventually<T>(
  read: () => Promise<T>,
  done: (value: T) => boolean,
  what: string,
  timeoutMs = 120_000,
): Promise<T> {
  const deadline = Date.now() + timeoutMs;
  let last: T = await read();
  while (!done(last)) {
    if (Date.now() > deadline) {
      throw new Error(
        `Timed out after ${String(timeoutMs / 1000)} s waiting for ${what}; last read: ${JSON.stringify(last)}`,
      );
    }
    await new Promise((resolve) => setTimeout(resolve, 2000));
    last = await read();
  }
  return last;
}
