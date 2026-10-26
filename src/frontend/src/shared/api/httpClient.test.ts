import { afterEach, describe, expect, test, vi } from 'vitest';

import { mockApi, problem } from '@/test/mockApi.ts';

import {
  ApiError,
  apiDownload,
  apiRequest,
  apiUpload,
  attachmentFileName,
  configureAuthentication,
} from './httpClient.ts';

function authentication(overrides: Partial<Parameters<typeof configureAuthentication>[0]> = {}) {
  const handlers = {
    accessToken: vi.fn(() => 'token-1'),
    refresh: vi.fn(() => Promise.resolve(false)),
    stepUp: vi.fn(() => Promise.resolve(false)),
    ...overrides,
  };
  configureAuthentication(handlers);
  return handlers;
}

afterEach(() => {
  configureAuthentication(null);
});

describe('apiRequest', () => {
  test('a read carries the bearer token and no idempotency key, and returns the ETag', async () => {
    authentication();
    const api = mockApi().on('GET', /^\/users\/1$/, {
      body: { id: '1' },
      headers: { ETag: '"4"' },
    });

    const response = await apiRequest<{ id: string }>('/users/1', {
      query: { q: 'a', empty: '', none: undefined },
    });

    expect(response).toEqual({ data: { id: '1' }, etag: '"4"' });
    const [request] = api.requests;
    expect(request?.headers.get('Authorization')).toBe('Bearer token-1');
    expect(request?.headers.get('Idempotency-Key')).toBeNull();
    expect(request?.query.toString()).toBe('q=a');
  });

  test('a list in the query is sent as the parameter repeated', async () => {
    authentication();
    const api = mockApi().on('GET', /^\/kpi-portfolio-aggregates$/, { body: {} });

    await apiRequest('/kpi-portfolio-aggregates', {
      query: { kpiDefinitionId: ['k1'], projectId: ['p1', 'p2'], none: [] },
    });

    expect(api.requests[0]?.query.toString()).toBe('kpiDefinitionId=k1&projectId=p1&projectId=p2');
  });

  test('a write carries a uuid Idempotency-Key and the If-Match it is given', async () => {
    authentication();
    const api = mockApi().on('PUT', /^\/roles\/1$/, { body: {} });

    await apiRequest('/roles/1', { method: 'PUT', body: { name: 'x' }, ifMatch: '"3"' });

    const [request] = api.requests;
    expect(request?.headers.get('Idempotency-Key')).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/,
    );
    expect(request?.headers.get('If-Match')).toBe('"3"');
    expect(request?.body).toEqual({ name: 'x' });
  });

  test('a delete sends If-Match, no idempotency key, and resolves on 204 without a body', async () => {
    authentication();
    const api = mockApi().on('DELETE', /^\/projects\/1$/, { status: 204 });

    const response = await apiRequest('/projects/1', { method: 'DELETE', ifMatch: '"7"' });

    expect(response).toEqual({ data: undefined, etag: null });
    const [request] = api.requests;
    expect(request?.headers.get('If-Match')).toBe('"7"');
    expect(request?.headers.get('Idempotency-Key')).toBeNull();
  });

  test('a refusal becomes an ApiError with its code, field issues and correlation id', async () => {
    authentication();
    mockApi().on(
      'POST',
      /^\/users$/,
      problem(400, 'VALIDATION_FAILED', [{ field: 'email', code: 'MALFORMED' }]),
    );

    const error = await apiRequest('/users', { method: 'POST', body: {} }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({
      status: 400,
      code: 'VALIDATION_FAILED',
      fieldIssues: [{ field: 'email', code: 'MALFORMED' }],
      correlationId: 'test-correlation-id',
    });
  });

  test('a 401 is retried once after a successful refresh', async () => {
    let token = 'expired';
    const handlers = authentication({
      accessToken: vi.fn(() => token),
      refresh: vi.fn(() => {
        token = 'fresh';
        return Promise.resolve(true);
      }),
    });
    const api = mockApi().on('GET', /^\/roles$/, (request) =>
      request.headers.get('Authorization') === 'Bearer fresh'
        ? { body: [] }
        : problem(401, 'AUTHENTICATION_REQUIRED'),
    );

    await expect(apiRequest('/roles')).resolves.toMatchObject({ data: [] });
    expect(handlers.refresh).toHaveBeenCalledOnce();
    expect(api.requests).toHaveLength(2);
  });

  test('STEP_UP_REQUIRED is retried with the same idempotency key once the step-up succeeds', async () => {
    let steppedUp = false;
    authentication({
      stepUp: vi.fn(() => {
        steppedUp = true;
        return Promise.resolve(true);
      }),
    });
    const api = mockApi().on('POST', /\/disable$/, () =>
      steppedUp ? { body: {} } : problem(403, 'STEP_UP_REQUIRED'),
    );

    await apiRequest('/users/1/disable', { method: 'POST' });

    const [first, retry] = api.requests;
    expect(retry?.headers.get('Idempotency-Key')).toBe(first?.headers.get('Idempotency-Key'));
  });

  test('a declined step-up surfaces the STEP_UP_REQUIRED refusal, and PERMISSION_DENIED never asks', async () => {
    const handlers = authentication();
    mockApi()
      .on('POST', /\/disable$/, problem(403, 'STEP_UP_REQUIRED'))
      .on('POST', /\/activate$/, problem(403, 'PERMISSION_DENIED'));

    await expect(apiRequest('/users/1/disable', { method: 'POST' })).rejects.toMatchObject({
      code: 'STEP_UP_REQUIRED',
    });
    await expect(apiRequest('/users/1/activate', { method: 'POST' })).rejects.toMatchObject({
      code: 'PERMISSION_DENIED',
    });
    expect(handlers.stepUp).toHaveBeenCalledOnce();
  });

  test('an anonymous call sends no token and never refreshes', async () => {
    const handlers = authentication();
    const api = mockApi().on('POST', /^\/sessions$/, problem(401, 'AUTHENTICATION_REQUIRED'));

    await expect(
      apiRequest('/sessions', { method: 'POST', body: {}, anonymous: true }),
    ).rejects.toMatchObject({ status: 401 });
    expect(api.requests[0]?.headers.get('Authorization')).toBeNull();
    expect(handlers.refresh).not.toHaveBeenCalled();
  });
});

describe('apiUpload', () => {
  test('sends the form with the token and an idempotency key, and reports progress', async () => {
    authentication();
    const api = mockApi().on('POST', /^\/documents$/, {
      status: 201,
      body: { id: 'd1' },
      headers: { ETag: '"1"' },
    });
    const form = new FormData();
    form.append('file', new File(['x'], 'a.txt'));
    const progress: number[] = [];

    const response = await apiUpload<{ id: string }>('/documents', form, {
      onProgress: (fraction) => progress.push(fraction),
    });

    expect(response).toEqual({ data: { id: 'd1' }, etag: '"1"' });
    expect(progress).toEqual([0.5, 1]);
    const [request] = api.requests;
    expect(request?.body).toBe(form);
    expect(request?.headers.get('Authorization')).toBe('Bearer token-1');
    expect(request?.headers.get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/);
    // The browser writes the multipart Content-Type and its boundary.
    expect(request?.headers.get('Content-Type')).toBeNull();
  });

  test('a refused upload is an ApiError, and a 401 is refreshed and retried with the same key', async () => {
    const handlers = authentication({ refresh: vi.fn(() => Promise.resolve(true)) });
    let calls = 0;
    const api = mockApi().on('POST', /^\/documents$/, () => {
      calls += 1;
      return calls === 1
        ? problem(401, 'AUTHENTICATION_REQUIRED')
        : problem(413, 'PAYLOAD_TOO_LARGE');
    });

    await expect(apiUpload('/documents', new FormData())).rejects.toMatchObject({
      status: 413,
      code: 'PAYLOAD_TOO_LARGE',
    });
    expect(handlers.refresh).toHaveBeenCalledOnce();
    const [first, retry] = api.requests;
    expect(retry?.headers.get('Idempotency-Key')).toBe(first?.headers.get('Idempotency-Key'));
  });
});

describe('apiDownload', () => {
  test('returns the bytes and the attachment’s name', async () => {
    authentication();
    mockApi().on('GET', /\/content$/, {
      body: new Blob(['%PDF']),
      headers: { 'Content-Disposition': "attachment; filename=x.pdf; filename*=UTF-8''%D8%AA.pdf" },
    });

    const file = await apiDownload('/documents/1/versions/2/content');

    expect(await file.content.text()).toBe('%PDF');
    expect(file.fileName).toBe('ت.pdf');
  });

  test('a refusal is the R-23 envelope as ApiError', async () => {
    authentication();
    mockApi().on('GET', /\/content$/, problem(409, 'DOCUMENT_NOT_AVAILABLE'));
    await expect(apiDownload('/documents/1/versions/2/content')).rejects.toBeInstanceOf(ApiError);
  });

  test('reads the plain and the RFC 6266 file name forms', () => {
    expect(attachmentFileName(null)).toBeNull();
    expect(attachmentFileName('attachment')).toBeNull();
    expect(attachmentFileName('attachment; filename="a b.pdf"')).toBe('a b.pdf');
    expect(attachmentFileName("attachment; filename*=UTF-8''%E0%A4%A.pdf; filename=b.pdf")).toBe(
      'b.pdf',
    );
  });
});
