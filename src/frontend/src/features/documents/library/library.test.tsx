import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type ScanState } from '@/features/documents/api/types.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import {
  documentSession,
  documentSummary,
  PROJECT_ID,
  withDocumentLookups,
} from '@/test/documentFixtures.ts';
import { type MockApi, mockApi, page, problem } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// SCR-120 Document Library, SCR-121 Project Documents and SCR-122 Recent Documents.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const STATES: ScanState[] = ['SCAN_PENDING', 'CLEAN', 'QUARANTINED', 'SCAN_FAILED'];

const DOCUMENTS = STATES.map((state, index) =>
  documentSummary({
    id: `8d8d8d8d-0000-4000-8000-00000000010${String(index)}`,
    title: { text: `Document ${state}`, language: 'EN' },
    latestScanState: state,
  }),
);

function openList(path: string, items = DOCUMENTS, { lookupsReadable = true } = {}): MockApi {
  const api = withDocumentLookups(mockApi(), { readable: lookupsReadable }).on(
    'GET',
    /^\/documents$/,
    (request) => {
      const q = request.query.get('q');
      return {
        body: page(items.filter((item) => q === null || item.title.text.includes(q))),
      };
    },
  );
  renderApp({ path, session: documentSession() });
  return api;
}

describe('SCR-120 Document Library', () => {
  test('shows exactly the documents the API returns, each with its scan state', async () => {
    const api = openList('/documents');

    const table = await screen.findByRole('table', { name: en('documents.library.title') });
    expect(within(table).getAllByRole('row')).toHaveLength(DOCUMENTS.length + 1);
    expect(api.requestsTo('GET', /^\/documents$/)).toHaveLength(1);

    const tone = (state: ScanState) =>
      within(table).getByText(en(`documents.scanState.${state}`)).className;
    expect(new Set(STATES.map(tone)).size).toBe(4);
    expect(tone('QUARANTINED')).toBe('badge badge--negative');

    // The quarantined row is flagged, not only its badge.
    const quarantinedRow = within(table).getByText('Document QUARANTINED').closest('tr');
    expect(quarantinedRow?.className).toBe('row--flagged');
    expect(await within(table).findAllByText('Meeting minutes')).toHaveLength(DOCUMENTS.length);
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('each document links to its page and its project’s documents', async () => {
    openList('/documents', [documentSummary()]);
    const table = await screen.findByRole('table');
    expect(
      within(table).getByRole('link', { name: 'Design review minutes' }).getAttribute('href'),
    ).toBe(`/documents/${documentSummary().id}`);
    expect(
      within(table)
        .getByRole('link', { name: en('documents.values.project', { id: '7c7c7c7c' }) })
        .getAttribute('href'),
    ).toBe(`/documents/projects/${PROJECT_ID}`);
  });

  test('the title and status filters are sent to the API and kept in the URL', async () => {
    const api = openList('/documents');
    const user = userEvent.setup();
    await screen.findByRole('table');

    await user.type(
      screen.getByRole('searchbox', { name: en('documents.filters.title') }),
      'CLEAN',
    );
    await user.click(screen.getByRole('button', { name: en('common.filters.apply') }));
    await screen.findByText('Document CLEAN');
    await user.selectOptions(
      screen.getByRole('combobox', { name: en('documents.fields.status') }),
      'ARCHIVED',
    );

    const last = api.requestsTo('GET', /^\/documents$/).at(-1);
    expect(last?.query.get('q')).toBe('CLEAN');
    expect(last?.query.get('status')).toBe('ARCHIVED');
    expect(last?.query.has('projectId')).toBe(false);
  });

  test('loading, empty and filtered-empty are distinct states', async () => {
    openList('/documents', []);
    expect(screen.getByText(en('documents.list.loading'))).toBeTruthy();
    expect(await screen.findByText(en('documents.list.empty'))).toBeTruthy();
    expect(screen.queryByText(en('documents.list.loading'))).toBeNull();
  });

  test('a filter that matches nothing says so', async () => {
    openList('/documents?q=nothing', []);
    expect(await screen.findByText(en('documents.list.emptyFiltered'))).toBeTruthy();
  });

  test('no document permission is an error, never an empty library', async () => {
    withDocumentLookups(mockApi()).on('GET', /^\/documents$/, problem(403, 'PERMISSION_DENIED'));
    renderApp({ path: '/documents', session: documentSession() });
    expect((await screen.findByRole('alert')).textContent).toContain(
      en('documents.problems.permissionDenied'),
    );
    expect(screen.queryByText(en('documents.list.empty'))).toBeNull();
  });

  test('without the catalogues and user names, values are still identified', async () => {
    openList('/documents', [documentSummary()], { lookupsReadable: false });
    const table = await screen.findByRole('table');
    expect(
      await within(table).findByText(en('documents.lookups.unknownItem', { id: 'af000000' })),
    ).toBeTruthy();
    expect(
      within(table).getByText(en('common.people.unknownUser', { id: '5a5a5a5a' })),
    ).toBeTruthy();
  });

  test('the shell offers the documents navigation to anyone signed in', async () => {
    openList('/documents', []);
    const navigation = await screen.findByRole('navigation', { name: en('documents.nav.label') });
    expect(
      within(navigation)
        .getByRole('link', { name: en('documents.nav.library') })
        .getAttribute('aria-current'),
    ).toBe('page');
  });
});

describe('SCR-121 Project Documents', () => {
  test('asks for the project’s documents only and leaves out the project column', async () => {
    const api = openList(`/documents/projects/${PROJECT_ID}`, [documentSummary()]);
    const table = await screen.findByRole('table');
    expect(api.requestsTo('GET', /^\/documents$/)[0]?.query.get('projectId')).toBe(PROJECT_ID);
    expect(
      within(table).queryByRole('columnheader', { name: en('documents.fields.project') }),
    ).toBeNull();
    expect(screen.getByRole('heading', { level: 1 }).textContent).toBe(
      en('documents.project.title', { id: '7c7c7c7c' }),
    );
  });
});

describe('SCR-122 Recent Documents', () => {
  test('reads the first page of most recently changed documents, unfiltered', async () => {
    const api = openList('/documents/recent', [documentSummary()]);
    await screen.findByRole('table', { name: en('documents.recent.title') });
    const [request] = api.requestsTo('GET', /^\/documents$/);
    expect(request?.query.get('pageSize')).toBe('10');
    expect([...(request?.query.keys() ?? [])]).toEqual(['pageSize']);
    expect(
      screen.getByRole('link', { name: en('documents.recent.toLibrary') }).getAttribute('href'),
    ).toBe('/documents');
  });
});
