import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type DocumentVersionDetail } from '@/features/documents/api/types.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import {
  DOCUMENT_ID,
  documentDetail,
  documentSession,
  OTHER_UPLOADER_ID,
  SELF_ID,
  shownAt,
  UPLOADER_ID,
  version,
  withDocumentLookups,
} from '@/test/documentFixtures.ts';
import { type MockApi, mockApi, problem } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// SCR-124 Version History: every version with its uploader and timestamp (acceptance criterion 3).

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

/** Newest first, as the API lists them; three uploaders, one of them the signed-in person. */
const VERSIONS: DocumentVersionDetail[] = [
  version({
    id: 'v4',
    versionNo: 4,
    uploadedByUserId: SELF_ID,
    uploadedAt: '2026-09-30T08:00:00Z',
    scanState: 'SCAN_PENDING',
    scanCompletedAt: null,
  }),
  version({
    id: 'v3',
    versionNo: 3,
    uploadedByUserId: OTHER_UPLOADER_ID,
    uploadedAt: '2026-09-29T14:30:00Z',
    scanState: 'SCAN_FAILED',
  }),
  version({
    id: 'v2',
    versionNo: 2,
    uploadedByUserId: OTHER_UPLOADER_ID,
    uploadedAt: '2026-09-29T11:05:00Z',
    scanState: 'QUARANTINED',
  }),
  version({ id: 'v1', versionNo: 1, uploadedByUserId: UPLOADER_ID }),
];

function openHistory(versions = VERSIONS): MockApi {
  const api = withDocumentLookups(mockApi())
    .on('GET', /^\/documents\/[^/]+$/, { body: documentDetail() })
    .on('GET', /^\/documents\/[^/]+\/versions$/, { body: versions });
  renderApp({ path: `/documents/${DOCUMENT_ID}/versions`, session: documentSession() });
  return api;
}

/** The row at `index`; a missing row fails the test here rather than as a null later. */
function at(rows: HTMLElement[], index: number): HTMLElement {
  const row = rows[index];
  if (row === undefined) {
    throw new Error(`missing row ${String(index)}`);
  }
  return row;
}

async function versionRows() {
  const table = await screen.findByRole('table', { name: en('documents.versions.caption') });
  return within(table).getAllByRole('row').slice(1);
}

describe('SCR-124 Version History', () => {
  test('every version is listed, newest first, with its uploader and upload time', async () => {
    openHistory();
    const rows = await versionRows();

    expect(rows).toHaveLength(VERSIONS.length);
    const expected = [
      [4, en('common.people.you'), '2026-09-30T08:00:00Z'],
      [3, 'Omar Reviewer', '2026-09-29T14:30:00Z'],
      [2, 'Omar Reviewer', '2026-09-29T11:05:00Z'],
      [1, 'Salma Uploader', '2026-09-28T09:15:00Z'],
    ] as const;
    for (const [index, [versionNo, uploader, uploadedAt]] of expected.entries()) {
      const row = rows[index];
      if (row === undefined) {
        throw new Error(`missing row ${String(index)}`);
      }
      const cells = within(row);
      expect(cells.getByRole('rowheader').textContent).toBe(
        en('documents.fields.versionNo', { number: versionNo }),
      );
      expect(await cells.findByText(uploader)).toBeTruthy();
      expect(cells.getByText(shownAt(uploadedAt))).toBeTruthy();
    }
    expect(screen.getByRole('heading', { level: 1 }).textContent).toBe(
      en('documents.versions.titleOf', { title: 'Design review minutes' }),
    );
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('an uploader whose name cannot be read is still identified', async () => {
    const api = openHistory([version({ uploadedByUserId: OTHER_UPLOADER_ID })]);
    api.on('GET', /^\/users\/[^/]+$/, problem(403, 'PERMISSION_DENIED'));
    const row = at(await versionRows(), 0);
    expect(
      await within(row).findByText(en('common.people.unknownUser', { id: '6b6b6b6b' })),
    ).toBeTruthy();
  });

  test('only a clean version can be downloaded; a quarantined one is flagged and says why', async () => {
    openHistory();
    const rows = await versionRows();
    const download = (row: HTMLElement) =>
      within(row).getByRole('button', { name: en('documents.download.action') });

    expect((download(at(rows, 3)) as HTMLButtonElement).disabled).toBe(false);
    for (const row of rows.slice(0, 3)) {
      expect((download(row) as HTMLButtonElement).disabled).toBe(true);
    }
    expect(rows[2]?.className).toBe('row--flagged');
    expect(within(at(rows, 2)).getByText(en('documents.unavailable.QUARANTINED'))).toBeTruthy();
    expect(within(at(rows, 2)).getByText(en('documents.scanState.QUARANTINED')).className).toBe(
      'badge badge--negative',
    );
  });

  test('a version whose scan failed can be queued again', async () => {
    const api = openHistory();
    api.on('POST', /\/rescan$/, { body: version({ id: 'v3', scanState: 'SCAN_PENDING' }) });
    const user = userEvent.setup();
    const rows = await versionRows();

    expect(screen.getAllByRole('button', { name: en('documents.rescan.action') })).toHaveLength(1);
    await user.click(
      within(at(rows, 1)).getByRole('button', { name: en('documents.rescan.action') }),
    );

    expect(await screen.findByText(en('documents.done.rescanQueued'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/rescan$/)[0]?.path).toBe(
      `/documents/${DOCUMENT_ID}/versions/v3/rescan`,
    );
    expect(api.requestsTo('GET', /\/versions$/)).toHaveLength(2);
  });
});
