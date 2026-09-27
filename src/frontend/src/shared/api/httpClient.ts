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

async function send(url: string, options: RequestOptions, idempotencyKey: string | undefined) {
  const headers = new Headers({ Accept: 'application/json' });
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
  try {
    return await fetch(url, {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? null : JSON.stringify(options.body),
      signal: options.signal ?? null,
    });
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

export async function apiRequest<T>(
  path: string,
  options: RequestOptions = {},
): Promise<ApiResponse<T>> {
  const method = options.method ?? 'GET';
  const idempotencyKey =
    method === 'GET' ? undefined : (options.idempotencyKey ?? crypto.randomUUID());
  const url = buildUrl(path, options.query);

  let response = await send(url, options, idempotencyKey);

  if (options.anonymous !== true && authentication !== null) {
    if (response.status === 401 && (await authentication.refresh())) {
      response = await send(url, options, idempotencyKey);
    }
    if (
      response.status === 403 &&
      (await problemCode(response)) === 'STEP_UP_REQUIRED' &&
      (await authentication.stepUp())
    ) {
      response = await send(url, options, idempotencyKey);
    }
  }

  if (!response.ok) {
    throw await toApiError(response);
  }
  const data = (await response.json()) as T;
  return { data, etag: response.headers.get('ETag') };
}
