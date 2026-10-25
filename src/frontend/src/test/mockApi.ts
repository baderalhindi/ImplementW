import { vi } from 'vitest';

import { sessionStore } from '@/features/identity-access/session/sessionStore.ts';

import { shippedPermissions } from './permissionFixtures.ts';

// A route table standing in for the API: each handler answers one method and path, and every request is recorded
// with its headers and body so a test can assert what the SPA sent. JSON calls arrive through fetch, multipart uploads
// through XMLHttpRequest (for their progress events); both are answered from the same routes.

export interface RecordedRequest {
  method: string;
  path: string;
  query: URLSearchParams;
  headers: Headers;
  /** The parsed JSON body, or the FormData of an upload. */
  body: unknown;
}

export interface MockReply {
  status?: number;
  /** JSON, or a Blob served as application/octet-stream (a content download). */
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
  /** Every request but the shell's approval count (see SHELL_INBOX_COUNT). */
  requests: RecordedRequest[];
  requestsTo: (method: string, pattern: RegExp) => RecordedRequest[];
  /** The shell's approval-count reads. */
  shellInboxCountReads: RecordedRequest[];
  /** The shell's permission reads (see SHELL_PERMISSIONS). */
  shellPermissionReads: RecordedRequest[];
}

/**
 * The shell's Requests and Approvals count reads the inbox's total with `GET /approval-tasks?pageSize=1` on every
 * screen. It is routed by this pattern, not by a screen's `/^\/approval-tasks$/` handler, so a test's inbox reads stay
 * its own; with no handler for it the count is 0. It is kept out of `requests` for the same reason.
 */
export const SHELL_INBOX_COUNT = /^\/approval-tasks\?pageSize=1$/;

function isShellInboxCount(method: string, url: URL): boolean {
  return (
    method === 'GET' &&
    url.pathname === '/api/v1/approval-tasks' &&
    url.searchParams.get('pageSize') === '1'
  );
}

/**
 * The shell reads what the signed-in person holds with `GET /sessions/current/permissions` once per sign-in. With no handler for
 * it, it answers what the session's roles ship with (permissionFixtures.ts), so a test of a screen is offered what that role
 * would be; a test of who is offered what registers its own answer. It is kept out of `requests`, as the approval count is.
 */
export const SHELL_PERMISSIONS = /^\/sessions\/current\/permissions$/;

function isShellPermissions(method: string, url: URL): boolean {
  return method === 'GET' && url.pathname === '/api/v1/sessions/current/permissions';
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

/** The upload progress events the fake XMLHttpRequest reports before the reply: half the body, then all of it. */
const UPLOAD_PROGRESS_STEPS = [0.5, 1];
const UPLOAD_TOTAL_BYTES = 1000;

/**
 * Replaces fetch and XMLHttpRequest for the test. Routes added later win, so a test can override a default with a
 * specific answer.
 */
export function mockApi(): MockApi {
  const routes: Route[] = [];
  const requests: RecordedRequest[] = [];
  const shellInboxCountReads: RecordedRequest[] = [];
  const shellPermissionReads: RecordedRequest[] = [];

  const answer = async (
    method: string,
    input: string,
    headers: Headers,
    body: unknown,
  ): Promise<{ status: number; body: BodyInit | null; headers: Headers }> => {
    const url = new URL(input, 'http://localhost');
    const recorded: RecordedRequest = {
      method,
      path: url.pathname.replace(/^\/api\/v1/, ''),
      query: url.searchParams,
      headers,
      body,
    };
    const shellCount = isShellInboxCount(method, url);
    const shellPermissions = isShellPermissions(method, url);
    (shellCount ? shellInboxCountReads : shellPermissions ? shellPermissionReads : requests).push(
      recorded,
    );
    const routed = shellCount ? '/approval-tasks?pageSize=1' : recorded.path;
    const route = [...routes]
      .reverse()
      .find((candidate) => candidate.method === method && candidate.pattern.test(routed));
    if (route === undefined && !shellCount && !shellPermissions) {
      throw new Error(`No mock for ${method} ${recorded.path}`);
    }
    const reply =
      route !== undefined
        ? await route.handler(recorded)
        : shellPermissions
          ? { body: shippedPermissions(sessionStore.getSnapshot().session?.user) }
          : { body: page([], 1) };
    const status = reply.status ?? 200;
    if (reply.body instanceof Blob) {
      // The bytes, not the Blob: Node's Response does not recognise jsdom's Blob.
      return {
        status,
        body: await reply.body.arrayBuffer(),
        headers: new Headers({ 'Content-Type': 'application/octet-stream', ...reply.headers }),
      };
    }
    const contentType = status >= 400 ? 'application/problem+json' : 'application/json';
    return {
      status,
      body: reply.body === undefined ? null : JSON.stringify(reply.body),
      headers: new Headers({ 'Content-Type': contentType, ...reply.headers }),
    };
  };

  // The client always calls fetch(url: string, init), so a Request object never reaches here.
  const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
    const reply = await answer(
      init?.method ?? 'GET',
      input,
      new Headers(init?.headers),
      typeof init?.body === 'string' ? JSON.parse(init.body) : undefined,
    );
    return new Response(reply.body, { status: reply.status, headers: reply.headers });
  });
  vi.stubGlobal('fetch', fetchMock);

  class FakeXMLHttpRequest {
    readonly upload: { onprogress: ((event: ProgressEvent) => void) | null } = {
      onprogress: null,
    };
    onload: (() => void) | null = null;
    onerror: (() => void) | null = null;
    onabort: (() => void) | null = null;
    status = 0;
    responseText = '';
    private method = 'GET';
    private url = '';
    private readonly headers = new Headers();
    private replyHeaders = new Headers();
    private aborted = false;

    open(method: string, url: string) {
      this.method = method;
      this.url = url;
    }

    setRequestHeader(name: string, value: string) {
      this.headers.set(name, value);
    }

    getAllResponseHeaders() {
      return [...this.replyHeaders.entries()]
        .map(([name, value]) => `${name}: ${value}`)
        .join('\r\n');
    }

    abort() {
      this.aborted = true;
      this.onabort?.();
    }

    send(body: FormData) {
      for (const step of UPLOAD_PROGRESS_STEPS) {
        this.upload.onprogress?.(
          new ProgressEvent('progress', {
            lengthComputable: true,
            loaded: step * UPLOAD_TOTAL_BYTES,
            total: UPLOAD_TOTAL_BYTES,
          }),
        );
      }
      void answer(this.method, this.url, this.headers, body).then(async (reply) => {
        if (this.aborted) {
          return;
        }
        this.status = reply.status;
        this.replyHeaders = reply.headers;
        this.responseText = reply.body === null ? '' : await new Response(reply.body).text();
        this.onload?.();
      });
    }
  }
  vi.stubGlobal('XMLHttpRequest', FakeXMLHttpRequest);

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
    shellInboxCountReads,
    shellPermissionReads,
    requestsTo: (method, pattern) =>
      requests.filter((request) => request.method === method && pattern.test(request.path)),
  };
  return api;
}
