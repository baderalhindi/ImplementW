import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest';

import { type DocumentDetail } from '@/features/documents/api/types.ts';
import { SCAN_POLL_INTERVAL_MS } from '@/features/documents/upload/useScanWatch.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import {
  DOCUMENT_ID,
  documentDetail,
  documentSession,
  link,
  RETIRED_TYPE_ID,
  version,
  VERSION_ID,
  withDocumentLookups,
} from '@/test/documentFixtures.ts';
import { type MockApi, mockApi, problem } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// SCR-123 Document Detail with MOD-051 Edit Metadata, MOD-052 Replace Version and MOD-053 Archive.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const CONTENT = /^\/documents\/[^/]+\/versions\/[^/]+\/content$/;

function openDetail(document: DocumentDetail = documentDetail(), links = [link()]): MockApi {
  const api = withDocumentLookups(mockApi())
    .on('GET', /^\/documents\/[^/]+$/, { body: document, headers: { ETag: '"7"' } })
    .on('GET', /^\/documents\/[^/]+\/links$/, { body: links });
  renderApp({ path: `/documents/${DOCUMENT_ID}`, session: documentSession() });
  return api;
}

const quarantined = () =>
  documentDetail({
    latestVersion: version({ scanState: 'QUARANTINED', scanCompletedAt: '2026-09-28T09:17:00Z' }),
  });

function downloadButton() {
  return screen.getByRole('button', { name: en('documents.download.action') });
}

describe('SCR-123 Document Detail', () => {
  test('a quarantined document is flagged and its content cannot be downloaded', async () => {
    const api = openDetail(quarantined());

    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain(en('documents.scanNotice.QUARANTINED.title'));
    expect(alert.className).toContain('scan-notice--negative');

    const download = downloadButton();
    expect((download as HTMLButtonElement).disabled).toBe(true);
    const reasonId = download.getAttribute('aria-describedby') ?? '';
    expect(document.getElementById(reasonId)?.textContent).toBe(
      en('documents.unavailable.QUARANTINED'),
    );
    // A new version is the remedy, so it stays possible.
    const replace = screen.getByRole('button', { name: en('documents.replace.action') });
    expect((replace as HTMLButtonElement).disabled).toBe(false);
    expect(api.requestsTo('GET', CONTENT)).toHaveLength(0);
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('a clean version is downloaded under its own name', async () => {
    const api = openDetail();
    api.on('GET', CONTENT, {
      body: new Blob(['%PDF-1.7']),
      headers: { 'Content-Disposition': "attachment; filename*=UTF-8''design-review.pdf" },
    });
    const createObjectURL = vi.fn(() => 'blob:test');
    vi.stubGlobal('URL', Object.assign(URL, { createObjectURL, revokeObjectURL: vi.fn() }));
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, 'click')
      .mockImplementation(() => undefined);
    const user = userEvent.setup();

    await screen.findByText('design-review.pdf');
    expect(screen.queryByRole('alert')).toBeNull();
    await user.click(downloadButton());

    await vi.waitFor(() => {
      expect(click).toHaveBeenCalledOnce();
    });
    expect(api.requestsTo('GET', CONTENT)[0]?.path).toBe(
      `/documents/${DOCUMENT_ID}/versions/${VERSION_ID}/content`,
    );
    expect((click.mock.contexts[0] as HTMLAnchorElement).download).toBe('design-review.pdf');
  });

  test('a download the server refuses says why', async () => {
    const api = openDetail();
    api.on('GET', CONTENT, problem(409, 'DOCUMENT_NOT_AVAILABLE'));
    const user = userEvent.setup();

    await screen.findByText('design-review.pdf');
    await user.click(downloadButton());

    expect((await screen.findByRole('alert')).textContent).toBe(
      en('documents.problems.notAvailable'),
    );
  });

  describe('while the latest version is being scanned', () => {
    beforeEach(() => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
    });
    afterEach(() => {
      vi.useRealTimers();
    });

    test('it is read again until the scan decides, then its content can be downloaded', async () => {
      const api = openDetail(
        documentDetail({
          latestVersion: version({ scanState: 'SCAN_PENDING', scanCompletedAt: null }),
        }),
      );
      api.on('GET', /^\/documents\/[^/]+\/versions\/[^/]+$/, { body: version() });

      expect(await screen.findByText(en('documents.scanNotice.SCAN_PENDING.title'))).toBeTruthy();
      expect((downloadButton() as HTMLButtonElement).disabled).toBe(true);

      await vi.advanceTimersByTimeAsync(SCAN_POLL_INTERVAL_MS);

      await vi.waitFor(() => {
        expect((downloadButton() as HTMLButtonElement).disabled).toBe(false);
      });
      expect(screen.queryByText(en('documents.scanNotice.SCAN_PENDING.title'))).toBeNull();
    });
  });

  test('where the document is used, with the evidence pinned on each link', async () => {
    const api = openDetail();
    const region = await screen.findByRole('region', { name: en('documents.links.title') });
    const row = (await within(region).findAllByRole('row'))[1];
    if (row === undefined) {
      throw new Error('no link row');
    }
    expect(row.textContent).toContain('Milestone · MILESTONE · e0000000');
    expect(row.textContent).toContain('Stage sign-off');
    expect(within(row).getByText(en('documents.evidence.satisfies')).className).toBe(
      'badge badge--positive',
    );

    api.on('GET', /^\/evidence-references\/[^/]+\/content$/, { body: new Blob(['x']) });
    vi.stubGlobal(
      'URL',
      Object.assign(URL, { createObjectURL: vi.fn(() => 'blob:x'), revokeObjectURL: vi.fn() }),
    );
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    await userEvent
      .setup()
      .click(within(row).getByRole('button', { name: en('documents.evidence.download') }));
    await vi.waitFor(() => {
      expect(api.requestsTo('GET', /^\/evidence-references\/[^/]+\/content$/)).toHaveLength(1);
    });
  });

  test('an unknown or unreadable document is an error, not an empty page', async () => {
    mockApi()
      .on('GET', /^\/documents\/[^/]+$/, problem(404, 'NOT_FOUND'))
      .on('GET', /^\/master-data-catalogues$/, { body: [] });
    renderApp({ path: `/documents/${DOCUMENT_ID}`, session: documentSession() });
    expect((await screen.findByRole('alert')).textContent).toContain(
      en('common.problems.notFound'),
    );
  });
});

describe('MOD-051 Edit Metadata', () => {
  test('sends the whole editable set with the ETag it was read with', async () => {
    const api = openDetail(documentDetail({ documentTypeItemId: RETIRED_TYPE_ID }));
    api.on('PUT', /^\/documents\/[^/]+$/, { body: documentDetail() });
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: en('documents.edit.action') }));
    const dialog = screen.getByRole('dialog', { name: en('documents.edit.title') });
    // A retired current value still reads as itself.
    expect(await within(dialog).findByRole('option', { name: 'Memo' })).toBeTruthy();
    const description = within(dialog).getByLabelText(/^Description/);
    await user.clear(description);
    await user.type(description, 'Revised');
    await user.click(within(dialog).getByRole('button', { name: en('common.actions.save') }));

    expect(await screen.findByText(en('documents.done.edited'))).toBeTruthy();
    const [request] = api.requestsTo('PUT', /^\/documents\/[^/]+$/);
    expect(request?.headers.get('If-Match')).toBe('"7"');
    expect(request?.body).toEqual({
      title: { text: 'Design review minutes', language: 'en' },
      description: { text: 'Revised', language: 'en' },
      documentTypeItemId: RETIRED_TYPE_ID,
      dataClassificationItemId: documentDetail().dataClassificationItemId,
    });
  });

  test('someone else’s change in between is explained', async () => {
    const api = openDetail();
    api.on('PUT', /^\/documents\/[^/]+$/, problem(412, 'PRECONDITION_FAILED'));
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: en('documents.edit.action') }));
    const dialog = screen.getByRole('dialog', { name: en('documents.edit.title') });
    await user.click(within(dialog).getByRole('button', { name: en('common.actions.save') }));

    expect((await within(dialog).findByRole('alert')).textContent).toBe(
      en('common.problems.preconditionFailed'),
    );
  });
});

describe('MOD-052 Replace Version', () => {
  test('uploads the file as the next version and shows its scan', async () => {
    const api = openDetail();
    api.on('POST', /^\/documents\/[^/]+\/versions$/, {
      status: 201,
      body: version({ id: 'v2', versionNo: 2, scanState: 'SCAN_PENDING', scanCompletedAt: null }),
    });
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: en('documents.replace.action') }));
    const dialog = screen.getByRole('dialog', { name: en('documents.replace.title') });
    await user.upload(
      within(dialog).getByLabelText(/^File/),
      new File(['v2'], 'design-review-v2.pdf', { type: 'application/pdf' }),
    );
    await user.click(within(dialog).getByRole('button', { name: en('documents.replace.confirm') }));

    expect(
      await within(dialog).findByText(en('documents.scanNotice.SCAN_PENDING.title')),
    ).toBeTruthy();
    const form = api.requestsTo('POST', /\/versions$/)[0]?.body as FormData;
    expect((form.get('file') as File).name).toBe('design-review-v2.pdf');
    // The page reads again once the dialog is closed, not under it.
    expect(api.requestsTo('GET', /^\/documents\/[^/]+$/)).toHaveLength(1);
    await user.click(within(dialog).getByRole('button', { name: en('documents.upload.close') }));
    expect(await screen.findByText(en('documents.done.versionAdded'))).toBeTruthy();
    expect(api.requestsTo('GET', /^\/documents\/[^/]+$/)).toHaveLength(2);
  });
});

describe('MOD-053 Archive', () => {
  test('archives with the ETag, and an archived document takes no change', async () => {
    const api = openDetail();
    api.on('POST', /\/archive$/, { body: documentDetail({ status: 'ARCHIVED' }) });
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: en('documents.archive.action') }));
    const dialog = screen.getByRole('dialog', { name: en('documents.archive.title') });
    api.on('GET', /^\/documents\/[^/]+$/, { body: documentDetail({ status: 'ARCHIVED' }) });
    await user.click(within(dialog).getByRole('button', { name: en('documents.archive.confirm') }));

    expect(await screen.findByText(en('documents.done.archived'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/archive$/)[0]?.headers.get('If-Match')).toBe('"7"');
    expect(await screen.findByText(en('documents.detail.archived'))).toBeTruthy();
    for (const name of ['documents.replace.action', 'documents.edit.action']) {
      expect(screen.getByRole('button', { name: en(name) })).toHaveProperty('disabled', true);
    }
    expect(screen.queryByRole('button', { name: en('documents.archive.action') })).toBeNull();
  });
});
