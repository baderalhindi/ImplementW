// The SPA's one path to the API (ADR-003 API-01). Same-origin under /api/v1 (TASK-032 D-2); every refusal is the
// api-conventions R-23 envelope, surfaced as ApiError.

const API_BASE_PATH = '/api/v1';

export interface FieldIssue {
  field: string;
  code: string;
}

interface ProblemBody {
  code?: unknown;
  title?: unknown;
  correlationId?: unknown;
  errors?: unknown;
}

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly fieldIssues: FieldIssue[];
  readonly correlationId: string | null;

  constructor(
    status: number,
    code: string,
    fieldIssues: FieldIssue[],
    correlationId: string | null,
  ) {
    super(`${String(status)} ${code}`);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.fieldIssues = fieldIssues;
    this.correlationId = correlationId;
  }
}

/** Raised when the API cannot be reached at all, so the UI can say so instead of showing a server code. */
export const NETWORK_ERROR_CODE = 'NETWORK_ERROR';

export interface ApiResponse<T> {
  data: T;
  /** The R-21 ETag of a single mutable resource, sent back as If-Match; null on collections. */
  etag: string | null;
}

/**
 * What the session layer lends the client. The client stays ignorant of how tokens are stored or how a second
 * factor is collected; it only knows when to ask.
 */
export interface AuthenticationHandlers {
  accessToken: () => string | null;
  /** Exchanges the refresh token after a 401; true when a new access token is available. */
  refresh: () => Promise<boolean>;
  /** Collects a fresh second factor after 403 STEP_UP_REQUIRED (ADR-010); true when the session was stepped up. */
  stepUp: () => Promise<boolean>;
}

let authentication: AuthenticationHandlers | null = null;

export function configureAuthentication(handlers: AuthenticationHandlers | null): void {
  authentication = handlers;
}

export type QueryValue = string | number | boolean | undefined | null;

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT';
  query?: Record<string, QueryValue>;
  body?: unknown;
  ifMatch?: string | null | undefined;
  /** Sensitive writes (R-36). Generated per call when omitted, and reused if the call is retried after a step-up. */
  idempotencyKey?: string;
  /** Sign-in, refresh and step-up calls carry no access token and never trigger refresh or step-up. */
  anonymous?: boolean;
  signal?: AbortSignal | undefined;
}

function buildUrl(path: string, query?: Record<string, QueryValue>): string {
  const search = new URLSearchParams();
  for (const [name, value] of Object.entries(query ?? {})) {
    if (value !== undefined && value !== null && value !== '') {
      search.set(name, String(value));
    }
  }
  const queryString = search.toString();
  return `${API_BASE_PATH}${path}${queryString === '' ? '' : `?${queryString}`}`;
}

function isFieldIssue(value: unknown): value is FieldIssue {
  return (
    typeof value === 'object' &&
    value !== null &&
    typeof (value as FieldIssue).field === 'string' &&
    typeof (value as FieldIssue).code === 'string'
  );
}

async function toApiError(response: Response): Promise<ApiError> {
  let problem: ProblemBody = {};
  try {
    problem = (await response.json()) as ProblemBody;
  } catch {
    // A proxy or gateway may answer without the envelope; the status alone is then all there is.
  }
  const code = typeof problem.code === 'string' ? problem.code : `HTTP_${String(response.status)}`;
  const issues = Array.isArray(problem.errors) ? problem.errors.filter(isFieldIssue) : [];
  const correlationId = typeof problem.correlationId === 'string' ? problem.correlationId : null;
  return new ApiError(response.status, code, issues, correlationId);
}

function requestHeaders(
  options: RequestOptions,
  idempotencyKey: string | undefined,
  accept: string,
): Headers {
  const headers = new Headers({ Accept: accept });
  const token = options.anonymous === true ? null : (authentication?.accessToken() ?? null);
  if (token !== null) {
    headers.set('Authorization', `Bearer ${token}`);
  }
  if (options.body !== undefined) {
    headers.set('Content-Type', 'application/json');
  }
  if (options.ifMatch !== undefined && options.ifMatch !== null) {
    headers.set('If-Match', options.ifMatch);
  }
  if (idempotencyKey !== undefined) {
    headers.set('Idempotency-Key', idempotencyKey);
  }
  return headers;
}

async function sendFetch(url: string, init: RequestInit): Promise<Response> {
  try {
    return await fetch(url, init);
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error;
    }
    throw new ApiError(0, NETWORK_ERROR_CODE, [], null);
  }
}

async function problemCode(response: Response): Promise<string | null> {
  try {
    const body = (await response.clone().json()) as ProblemBody;
    return typeof body.code === 'string' ? body.code : null;
  } catch {
    return null;
  }
}

/** Sends one attempt with the headers it is given; called again, with fresh headers, after a refresh or step-up. */
type Transport = (headers: Headers) => Promise<Response>;

/**
 * The one exchange every call goes through: bearer token, Idempotency-Key on writes (reused on the retries), a 401
 * refreshed once, a STEP_UP_REQUIRED stepped up once, and any refusal raised as ApiError.
 */
async function exchange(
  options: RequestOptions,
  accept: string,
  transport: Transport,
): Promise<Response> {
  const method = options.method ?? 'GET';
  const idempotencyKey =
    method === 'GET' ? undefined : (options.idempotencyKey ?? crypto.randomUUID());
  const attempt = () => transport(requestHeaders(options, idempotencyKey, accept));

  let response = await attempt();

  if (options.anonymous !== true && authentication !== null) {
    if (response.status === 401 && (await authentication.refresh())) {
      response = await attempt();
    }
    if (
      response.status === 403 &&
      (await problemCode(response)) === 'STEP_UP_REQUIRED' &&
      (await authentication.stepUp())
    ) {
      response = await attempt();
    }
  }

  if (!response.ok) {
    throw await toApiError(response);
  }
  return response;
}

export async function apiRequest<T>(
  path: string,
  options: RequestOptions = {},
): Promise<ApiResponse<T>> {
  const url = buildUrl(path, options.query);
  const response = await exchange(options, 'application/json', (headers) =>
    sendFetch(url, {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? null : JSON.stringify(options.body),
      signal: options.signal ?? null,
    }),
  );
  const data = (await response.json()) as T;
  return { data, etag: response.headers.get('ETag') };
}

export interface UploadOptions {
  method?: 'POST' | 'PUT';
  /** The share of the request body sent so far, 0 to 1, as the browser reports it. */
  onProgress?: (fraction: number) => void;
  signal?: AbortSignal | undefined;
}

function responseHeaders(raw: string): Headers {
  const headers = new Headers();
  for (const line of raw.trim().split(/[\r\n]+/)) {
    const separator = line.indexOf(':');
    if (separator > 0) {
      headers.append(line.slice(0, separator).trim(), line.slice(separator + 1).trim());
    }
  }
  return headers;
}

/** fetch cannot report upload progress, so a multipart body goes through XMLHttpRequest and comes back as a Response. */
function sendWithProgress(
  url: string,
  method: string,
  headers: Headers,
  body: FormData,
  options: UploadOptions,
): Promise<Response> {
  return new Promise((resolve, reject) => {
    const request = new XMLHttpRequest();
    request.open(method, url);
    headers.forEach((value, name) => {
      request.setRequestHeader(name, value);
    });
    request.upload.onprogress = (event) => {
      if (event.lengthComputable && event.total > 0) {
        options.onProgress?.(event.loaded / event.total);
      }
    };
    request.onload = () => {
      resolve(
        new Response(request.status === 204 ? null : request.responseText, {
          status: request.status,
          headers: responseHeaders(request.getAllResponseHeaders()),
        }),
      );
    };
    request.onerror = () => {
      reject(new ApiError(0, NETWORK_ERROR_CODE, [], null));
    };
    request.onabort = () => {
      reject(new DOMException('The upload was cancelled.', 'AbortError'));
    };
    if (options.signal?.aborted === true) {
      request.abort();
      return;
    }
    options.signal?.addEventListener('abort', () => {
      request.abort();
    });
    request.send(body);
  });
}

/**
 * A multipart/form-data write (R-8): a sensitive write like any other, with its Idempotency-Key. The browser sets the
 * multipart Content-Type and boundary itself.
 */
export async function apiUpload<T>(
  path: string,
  body: FormData,
  options: UploadOptions = {},
): Promise<ApiResponse<T>> {
  const method = options.method ?? 'POST';
  const url = buildUrl(path);
  const response = await exchange(
    { method, signal: options.signal },
    'application/json',
    (headers) => sendWithProgress(url, method, headers, body, options),
  );
  const data = (await response.json()) as T;
  return { data, etag: response.headers.get('ETag') };
}

export interface DownloadedFile {
  content: Blob;
  /** The attachment's file name from Content-Disposition, or null when the server sent none. */
  fileName: string | null;
}

/** The file name of an attachment, preferring the RFC 6266 UTF-8 form (`filename*`) that non-Latin names need. */
export function attachmentFileName(disposition: string | null): string | null {
  if (disposition === null) {
    return null;
  }
  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(disposition)?.[1];
  if (encoded !== undefined) {
    try {
      return decodeURIComponent(encoded.trim());
    } catch {
      // Malformed percent-encoding: fall through to the plain form.
    }
  }
  const plain = /filename="?([^";]+)"?/i.exec(disposition)?.[1];
  return plain === undefined ? null : plain.trim();
}

/** A binary read (R-8 content paths): the bytes and the attachment's name. A refusal is the R-23 envelope as usual. */
export async function apiDownload(
  path: string,
  options: { signal?: AbortSignal | undefined } = {},
): Promise<DownloadedFile> {
  const url = buildUrl(path);
  const response = await exchange(
    { signal: options.signal },
    'application/octet-stream, application/problem+json',
    (headers) => sendFetch(url, { method: 'GET', headers, signal: options.signal ?? null }),
  );
  return {
    content: await response.blob(),
    fileName: attachmentFileName(response.headers.get('Content-Disposition')),
  };
}
