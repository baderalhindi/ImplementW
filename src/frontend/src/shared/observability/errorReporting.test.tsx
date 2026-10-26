import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest';
import { createMemoryRouter, RouterProvider } from 'react-router';

import { ApiError } from '@/shared/api/httpClient.ts';
import { I18nProvider } from '@/shared/i18n/I18nProvider.tsx';
import { mockApi, problem } from '@/test/mockApi.ts';

import {
  installErrorReporting,
  MAX_REPORTS_PER_PAGE,
  reportError,
  resetErrorReporting,
} from './errorReporting.ts';
import { RouteErrorPage } from './RouteErrorPage.tsx';

const REPORTS = /^\/client-error-reports$/;

function reportsApi() {
  return mockApi().on('POST', REPORTS, { status: 204 });
}

beforeEach(() => {
  vi.stubEnv('VITE_APM_ENVIRONMENT_TAG', 'test');
  window.history.pushState({}, '', '/projects/p1?name=Someone#details');
});

afterEach(() => {
  cleanup();
  resetErrorReporting();
  vi.unstubAllEnvs();
});

describe('reportError', () => {
  test('sends the error with its path only, anonymously, and the correlation id of a failed API call', async () => {
    const api = reportsApi();
    const error = new ApiError(500, 'INTERNAL_ERROR', [], '0f8fad5b-d9cb-469f-a165-70867728950e');

    reportError(error, 'UNHANDLED_REJECTION');

    await vi.waitFor(() => {
      expect(api.requests).toHaveLength(1);
    });
    const [request] = api.requests;
    expect(request?.headers.get('Authorization')).toBeNull();
    expect(request?.body).toEqual({
      source: 'UNHANDLED_REJECTION',
      errorType: 'ApiError',
      message: '500 INTERNAL_ERROR',
      stack: error.stack,
      path: '/projects/p1',
      correlationId: '0f8fad5b-d9cb-469f-a165-70867728950e',
    });
  });

  test('a thrown value that is not an Error is reported by its type, and long text is cut to the API limits', async () => {
    const api = reportsApi();

    reportError('x'.repeat(3_000), 'WINDOW_ERROR');

    await vi.waitFor(() => {
      expect(api.requests).toHaveLength(1);
    });
    const body = api.requests[0]?.body as { errorType: string; message: string; stack?: string };
    expect(body.errorType).toBe('string');
    expect(body.message).toHaveLength(2_000);
    expect(body.stack).toBeUndefined();
  });

  test('the same error is sent once, and a page sends at most its quota', async () => {
    const api = reportsApi();

    reportError(new Error('same'), 'WINDOW_ERROR');
    reportError(new Error('same'), 'WINDOW_ERROR');
    for (let i = 0; i < MAX_REPORTS_PER_PAGE + 5; i += 1) {
      reportError(new Error(`error ${String(i)}`), 'WINDOW_ERROR');
    }

    await vi.waitFor(() => {
      expect(api.requests).toHaveLength(MAX_REPORTS_PER_PAGE);
    });
    expect(
      api.requests.filter((r) => (r.body as { message: string }).message === 'same'),
    ).toHaveLength(1);
  });

  test('nothing is sent from a build without an environment tag', () => {
    vi.stubEnv('VITE_APM_ENVIRONMENT_TAG', '');
    const api = reportsApi();

    reportError(new Error('off'), 'WINDOW_ERROR');

    expect(api.requests).toHaveLength(0);
  });

  test('a report the API refuses is dropped without a further error', async () => {
    const api = mockApi().on('POST', REPORTS, problem(429, 'RATE_LIMITED'));
    const unhandled = vi.fn();
    window.addEventListener('unhandledrejection', unhandled);

    reportError(new Error('refused'), 'WINDOW_ERROR');

    await vi.waitFor(() => {
      expect(api.requests).toHaveLength(1);
    });
    await new Promise((resolve) => setTimeout(resolve, 0));
    window.removeEventListener('unhandledrejection', unhandled);
    expect(unhandled).not.toHaveBeenCalled();
  });
});

describe('installErrorReporting', () => {
  test('reports script errors and unhandled rejections until it is removed', async () => {
    const api = reportsApi();
    const target = new EventTarget() as Window;
    const remove = installErrorReporting(target);

    target.dispatchEvent(new ErrorEvent('error', { error: new TypeError('x is undefined') }));
    target.dispatchEvent(
      Object.assign(new Event('unhandledrejection'), { reason: new RangeError('bad') }),
    );
    target.dispatchEvent(new ErrorEvent('error', { message: 'Script error.' }));
    remove();
    target.dispatchEvent(new ErrorEvent('error', { error: new Error('after removal') }));

    await vi.waitFor(() => {
      expect(api.requests).toHaveLength(3);
    });
    expect(
      api.requests.map((r) => r.body as { source: string; errorType: string; message: string }),
    ).toEqual([
      expect.objectContaining({
        source: 'WINDOW_ERROR',
        errorType: 'TypeError',
        message: 'x is undefined',
      }),
      expect.objectContaining({
        source: 'UNHANDLED_REJECTION',
        errorType: 'RangeError',
        message: 'bad',
      }),
      expect.objectContaining({
        source: 'WINDOW_ERROR',
        errorType: 'Error',
        message: 'Script error.',
      }),
    ]);
  });
});

describe('RouteErrorPage', () => {
  test('a screen that throws is reported and replaced by an explanation, never by the error', async () => {
    const api = reportsApi();
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    function Broken(): never {
      throw new Error('render failed: secret detail');
    }
    const router = createMemoryRouter(
      [{ path: '/projects/:id', element: <Broken />, errorElement: <RouteErrorPage /> }],
      { initialEntries: ['/projects/p1'] },
    );

    render(
      <I18nProvider>
        <RouterProvider router={router} />
      </I18nProvider>,
    );

    expect(await screen.findByRole('heading', { name: 'تعذّر عرض هذه الصفحة' })).toBeTruthy();
    expect(screen.queryByText(/secret detail/)).toBeNull();
    await vi.waitFor(() => {
      expect(api.requests).toHaveLength(1);
    });
    expect(api.requests[0]?.body).toEqual(
      expect.objectContaining({ source: 'RENDER_ERROR', message: 'render failed: secret detail' }),
    );
  });
});
