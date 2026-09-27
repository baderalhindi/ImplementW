import { vi } from 'vitest';

// A route table standing in for the API: each handler answers one method and path, and every request is recorded
// with its headers and body so a test can assert what the SPA sent.

export interface RecordedRequest {
  method: string;
  path: string;
  query: URLSearchParams;
  headers: Headers;
  body: unknown;
}

export interface MockReply {
  status?: number;
  body?: unknown;
  headers?: Record<string, string>;
}

type Handler = (request: RecordedRequest) => MockReply | Promise<MockReply>;

interface Route {
  method: string;
  pattern: RegExp;
  handler: Handler;
}

export interface MockApi {
  on: (method: string, pattern: RegExp, handler: Handler | MockReply) => MockApi;
  requests: RecordedRequest[];
  requestsTo: (method: string, pattern: RegExp) => RecordedRequest[];
}

export function problem(
  status: number,
  code: string,
  errors: { field: string; code: string }[] = [],
): MockReply {
  return {
    status,
    body: {
      type: `urn:pmplatform:problem:${code.toLowerCase().replaceAll('_', '-')}`,
      title: code,
      status,
      code,
      correlationId: 'test-correlation-id',
      timestamp: '2026-09-27T10:00:00Z',
      ...(errors.length === 0 ? {} : { errors }),
    },
  };
}

export function page<T>(items: T[], pageSize = 25) {
  return { items, page: 1, pageSize, totalCount: items.length };
}

/** Replaces fetch for the test. Routes added later win, so a test can override a default with a specific answer. */
export function mockApi(): MockApi {
  const routes: Route[] = [];
  const requests: RecordedRequest[] = [];

  // The client always calls fetch(url: string, init), so a Request object never reaches here.
  const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
    const url = new URL(input, 'http://localhost');
    const method = init?.method ?? 'GET';
    const recorded: RecordedRequest = {
      method,
      path: url.pathname.replace(/^\/api\/v1/, ''),
      query: url.searchParams,
      headers: new Headers(init?.headers),
      body: typeof init?.body === 'string' ? JSON.parse(init.body) : undefined,
    };
    requests.push(recorded);
    const route = [...routes]
      .reverse()
      .find((candidate) => candidate.method === method && candidate.pattern.test(recorded.path));
    if (route === undefined) {
      throw new Error(`No mock for ${method} ${recorded.path}`);
    }
    const reply = await route.handler(recorded);
    const status = reply.status ?? 200;
    const contentType = status >= 400 ? 'application/problem+json' : 'application/json';
    return new Response(reply.body === undefined ? null : JSON.stringify(reply.body), {
      status,
      headers: { 'Content-Type': contentType, ...reply.headers },
    });
  });
  vi.stubGlobal('fetch', fetchMock);

  const api: MockApi = {
    on: (method, pattern, handler) => {
      routes.push({
        method,
        pattern,
        handler: typeof handler === 'function' ? handler : () => handler,
      });
      return api;
    },
    requests,
    requestsTo: (method, pattern) =>
      requests.filter((request) => request.method === method && pattern.test(request.path)),
  };
  return api;
}
