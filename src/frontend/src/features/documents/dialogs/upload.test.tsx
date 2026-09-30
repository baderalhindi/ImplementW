import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest';

import { type ScanState } from '@/features/documents/api/types.ts';
import { SCAN_POLL_INTERVAL_MS } from '@/features/documents/upload/useScanWatch.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import {
  CLASSIFICATION_ID,
  DOCUMENT_TYPE_ID,
  documentDetail,
  documentSession,
  PROJECT_ID,
  version,
  withDocumentLookups,
} from '@/test/documentFixtures.ts';
import { type MockApi, mockApi, page, problem } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// MOD-050 Upload Document: the upload's own states (uploading, then the scan's) and its refusals.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const pending = () => version({ scanState: 'SCAN_PENDING', scanCompletedAt: null });

interface Scenario {
  api: MockApi;
  /** Lets the POST answer: until then the dialog shows the upload in flight. */
  release: () => void;
  /** What the next read of the version answers. */
  setScanState: (state: ScanState) => void;
}

function openLibrary(path = '/documents', { lookupsReadable = true } = {}): Scenario {
  let release: () => void = () => undefined;
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  let scanState: ScanState = 'SCAN_PENDING';
  const api = withDocumentLookups(mockApi(), { readable: lookupsReadable })
    .on('GET', /^\/documents$/, { body: page([]) })
    .on('POST', /^\/documents$/, async () => {
      await gate;
      return { status: 201, body: documentDetail({ latestVersion: pending() }) };
    })
    .on('GET', /^\/documents\/[^/]+\/versions\/[^/]+$/, () => ({
      body: version({ scanState }),
    }));
  renderApp({ path, session: documentSession() });
  return {
    api,
    release,
    setScanState: (state) => {
      scanState = state;
    },
  };
}

async function openDialog(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole('button', { name: en('documents.upload.action') }));
  return screen.getByRole('dialog', { name: en('documents.upload.title') });
}

async function fillForm(user: ReturnType<typeof userEvent.setup>, dialog: HTMLElement) {
  await user.upload(
    within(dialog).getByLabelText(/^File/),
    new File(['%PDF-1.7'], 'minutes.pdf', { type: 'application/pdf' }),
  );
  await user.type(within(dialog).getByLabelText(/^Title/), 'Design review minutes');
  await within(dialog).findByRole('option', { name: 'Meeting minutes' });
  await user.selectOptions(within(dialog).getByLabelText(/^Document type/), DOCUMENT_TYPE_ID);
  await user.selectOptions(within(dialog).getByLabelText(/^Classification/), CLASSIFICATION_ID);
}

function badge(dialog: HTMLElement, label: string) {
  return within(dialog).getByText(label, { selector: '.badge' }).className;
}

describe('MOD-050 Upload Document', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  test('shows uploading, then scanning, then clean, each distinctly', async () => {
    const scenario = openLibrary();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const dialog = await openDialog(user);
    await fillForm(user, dialog);
    await user.click(within(dialog).getByRole('button', { name: en('documents.upload.confirm') }));

    // Uploading: the share sent, as text and as a progress bar; the form is gone.
    const progress = await within(dialog).findByRole('progressbar', {
      name: en('documents.upload.progressLabel'),
    });
    expect(progress.getAttribute('value')).toBe('100');
    expect(within(dialog).getByText(en('documents.upload.sent', { percent: 100 }))).toBeTruthy();
    const uploading = badge(dialog, en('documents.upload.uploading'));
    expect(within(dialog).queryByRole('textbox', { name: /^Title/ })).toBeNull();
    expect(
      within(dialog).getByRole('button', { name: en('documents.upload.cancel') }),
    ).toBeTruthy();

    // Scanning: received, not yet usable.
    scenario.release();
    expect(
      await within(dialog).findByText(en('documents.scanNotice.SCAN_PENDING.title')),
    ).toBeTruthy();
    const scanning = badge(dialog, en('documents.scanState.SCAN_PENDING'));
    expect(within(dialog).queryByRole('progressbar')).toBeNull();
    expect(within(dialog).getByText(en('documents.upload.closeWhileScanning'))).toBeTruthy();

    // Clean: the next read of the version, one poll later.
    scenario.setScanState('CLEAN');
    await vi.advanceTimersByTimeAsync(SCAN_POLL_INTERVAL_MS);
    expect(await within(dialog).findByText(en('documents.scanNotice.CLEAN.title'))).toBeTruthy();
    const clean = badge(dialog, en('documents.scanState.CLEAN'));
    expect(
      within(dialog).getByRole('link', { name: en('documents.upload.openDocument') }),
    ).toBeTruthy();

    expect(new Set([uploading, scanning, clean]).size).toBe(3);
    expect(uploading).toBe('badge badge--info');
    expect(scanning).toBe('badge badge--neutral');
    expect(clean).toBe('badge badge--positive');
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('a quarantined upload is flagged as an alert, apart from every other state', async () => {
    const scenario = openLibrary();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const dialog = await openDialog(user);
    await fillForm(user, dialog);
    await user.click(within(dialog).getByRole('button', { name: en('documents.upload.confirm') }));
    scenario.release();
    await within(dialog).findByText(en('documents.scanNotice.SCAN_PENDING.title'));

    scenario.setScanState('QUARANTINED');
    await vi.advanceTimersByTimeAsync(SCAN_POLL_INTERVAL_MS);

    const alert = await within(dialog).findByRole('alert');
    expect(alert.textContent).toContain(en('documents.scanNotice.QUARANTINED.title'));
    expect(alert.textContent).toContain(en('documents.scanNotice.QUARANTINED.body'));
    expect(badge(dialog, en('documents.scanState.QUARANTINED'))).toBe('badge badge--negative');
    // The verdict is final: no further read of the version.
    const reads = scenario.api.requestsTo('GET', /\/versions\/[^/]+$/).length;
    await vi.advanceTimersByTimeAsync(SCAN_POLL_INTERVAL_MS * 3);
    expect(scenario.api.requestsTo('GET', /\/versions\/[^/]+$/)).toHaveLength(reads);
  });

  test('a file that could not be scanned says so', async () => {
    const scenario = openLibrary();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const dialog = await openDialog(user);
    await fillForm(user, dialog);
    await user.click(within(dialog).getByRole('button', { name: en('documents.upload.confirm') }));
    scenario.release();
    await within(dialog).findByText(en('documents.scanNotice.SCAN_PENDING.title'));

    scenario.setScanState('SCAN_FAILED');
    await vi.advanceTimersByTimeAsync(SCAN_POLL_INTERVAL_MS);

    expect(
      await within(dialog).findByText(en('documents.scanNotice.SCAN_FAILED.title')),
    ).toBeTruthy();
    expect(badge(dialog, en('documents.scanState.SCAN_FAILED'))).toBe('badge badge--warning');
  });

  test('sends the file and metadata as multipart fields, with an idempotency key', async () => {
    const scenario = openLibrary();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const dialog = await openDialog(user);
    await fillForm(user, dialog);
    await user.type(within(dialog).getByLabelText(/^Description/), '  Stage 2 review  ');
    await user.click(within(dialog).getByRole('button', { name: en('documents.upload.confirm') }));
    scenario.release();
    await within(dialog).findByText(en('documents.scanNotice.SCAN_PENDING.title'));

    const [request] = scenario.api.requestsTo('POST', /^\/documents$/);
    const form = request?.body as FormData;
    expect(form.get('title.text')).toBe('Design review minutes');
    expect(form.get('title.language')).toBe('en');
    expect(form.get('description.text')).toBe('Stage 2 review');
    expect(form.get('documentTypeItemId')).toBe(DOCUMENT_TYPE_ID);
    expect(form.get('dataClassificationItemId')).toBe(CLASSIFICATION_ID);
    expect(form.has('projectId')).toBe(false);
    expect((form.get('file') as File).name).toBe('minutes.pdf');
    expect(request?.headers.get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/);
    // The list behind the dialog reads again.
    expect(await screen.findByText(en('documents.done.uploaded'))).toBeTruthy();
    expect(scenario.api.requestsTo('GET', /^\/documents$/)).toHaveLength(2);
  });

  test('on a project’s page the upload belongs to the project', async () => {
    const scenario = openLibrary(`/documents/projects/${PROJECT_ID}`);
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const dialog = await openDialog(user);
    await fillForm(user, dialog);
    await user.click(within(dialog).getByRole('button', { name: en('documents.upload.confirm') }));
    scenario.release();
    await within(dialog).findByText(en('documents.scanNotice.SCAN_PENDING.title'));

    const form = scenario.api.requestsTo('POST', /^\/documents$/)[0]?.body as FormData;
    expect(form.get('projectId')).toBe(PROJECT_ID);
  });

  test('missing fields are refused before anything is sent', async () => {
    const scenario = openLibrary();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const dialog = await openDialog(user);
    await within(dialog).findByRole('option', { name: 'Meeting minutes' });

    await user.click(within(dialog).getByRole('button', { name: en('documents.upload.confirm') }));

    expect(within(dialog).getByRole('alert').textContent).toBe(
      en('common.form.fixErrors', { count: 4 }),
    );
    expect(within(dialog).getByLabelText(/^File/).getAttribute('aria-invalid')).toBe('true');
    expect(document.activeElement).toBe(within(dialog).getByLabelText(/^File/));
    expect(scenario.api.requestsTo('POST', /^\/documents$/)).toHaveLength(0);
  });

  test('a file too large or of a refused type is shown on the file, and the form comes back as it was', async () => {
    const scenario = openLibrary();
    scenario.api.on('POST', /^\/documents$/, problem(413, 'PAYLOAD_TOO_LARGE'));
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const dialog = await openDialog(user);
    await fillForm(user, dialog);
    await user.click(within(dialog).getByRole('button', { name: en('documents.upload.confirm') }));

    const fileInput = await within(dialog).findByLabelText(/^File/);
    expect(fileInput.getAttribute('aria-invalid')).toBe('true');
    expect(within(dialog).getAllByText(en('documents.problems.payloadTooLarge'))).toHaveLength(2);
    expect(within(dialog).getByLabelText(/^Title/)).toHaveProperty(
      'value',
      'Design review minutes',
    );

    scenario.api.on('POST', /^\/documents$/, problem(415, 'UNSUPPORTED_MEDIA_TYPE'));
    await user.click(within(dialog).getByRole('button', { name: en('documents.upload.confirm') }));
    expect(
      await within(dialog).findAllByText(en('documents.problems.unsupportedMediaType')),
    ).toHaveLength(2);
  });

  test('a type or classification no longer published lands on its field', async () => {
    const scenario = openLibrary();
    scenario.api.on(
      'POST',
      /^\/documents$/,
      problem(422, 'DOCUMENT_REFERENCE_INVALID', [
        { field: 'documentTypeItemId', code: 'NOT_FOUND' },
      ]),
    );
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const dialog = await openDialog(user);
    await fillForm(user, dialog);
    await user.click(within(dialog).getByRole('button', { name: en('documents.upload.confirm') }));

    expect((await within(dialog).findByRole('alert')).textContent).toBe(
      en('documents.problems.referenceInvalid'),
    );
    expect(
      within(dialog)
        .getByLabelText(/^Document type/)
        .getAttribute('aria-invalid'),
    ).toBe('true');
  });

  test('only published items are offered', async () => {
    openLibrary();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const dialog = await openDialog(user);
    await within(dialog).findByRole('option', { name: 'Meeting minutes' });
    expect(within(dialog).queryByRole('option', { name: 'Memo' })).toBeNull();
  });

  test('without access to the catalogues the form says why and cannot be sent', async () => {
    const scenario = openLibrary('/documents', { lookupsReadable: false });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const dialog = await openDialog(user);

    expect((await within(dialog).findByRole('alert')).textContent).toContain(
      en('documents.lookups.unavailable'),
    );
    const submit = within(dialog).getByRole('button', { name: en('documents.upload.confirm') });
    expect((submit as HTMLButtonElement).disabled).toBe(true);
    expect(scenario.api.requestsTo('POST', /^\/documents$/)).toHaveLength(0);
  });
});
