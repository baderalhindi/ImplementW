import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, test, vi } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { sessionFor, withIdentityAccessData } from '@/test/identityAccessFixtures.ts';
import { mockApi, problem } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

import { type ImportReport } from './api.ts';

// TASK-102: the migration import on a register's page — template, check, import — shown with ADM-012 Departments.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const workbook = () =>
  new File(['PK'], 'departments.xlsx', {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  });

function report(overrides: Partial<ImportReport>): ImportReport {
  return {
    registerCode: 'DEPARTMENT',
    committed: false,
    batchId: null,
    rowCount: 2,
    createdCount: 2,
    skippedCount: 0,
    issues: [],
    rows: [],
    ...overrides,
  };
}

async function openImport(roles = ['R01']) {
  const api = withIdentityAccessData(mockApi());
  renderApp({ path: '/admin/organization/departments', session: sessionFor(roles) });
  await screen.findByRole('heading', { level: 1, name: en('identityAccess.departments.title') });
  const user = userEvent.setup();
  await user.click(screen.getByRole('button', { name: /^Import/ }));
  const dialog = screen.getByRole('dialog', {
    name: en('dataImport.title', { register: en('identityAccess.departments.title') }),
  });
  return { api, user, dialog };
}

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('TASK-102 migration import', () => {
  test('the template downloads under the name the API gives it', async () => {
    const { api, user, dialog } = await openImport();
    api.on('GET', /^\/departments\/import-template\/content$/, {
      body: new Blob(['PK']),
      headers: {
        'Content-Disposition': "attachment; filename*=UTF-8''department-import-template.xlsx",
      },
    });
    vi.stubGlobal(
      'URL',
      Object.assign(URL, {
        createObjectURL: vi.fn(() => 'blob:template'),
        revokeObjectURL: vi.fn(),
      }),
    );
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, 'click')
      .mockImplementation(() => undefined);

    await user.click(
      within(dialog).getByRole('button', { name: en('dataImport.template.download') }),
    );

    await waitFor(() => {
      expect(click).toHaveBeenCalledTimes(1);
    });
    expect((click.mock.contexts[0] as HTMLAnchorElement).download).toBe(
      'department-import-template.xlsx',
    );
  });

  test('a check with problems lists each one by row and column and offers no import', async () => {
    const { api, user, dialog } = await openImport();
    api.on('POST', /^\/departments\/imports$/, {
      body: report({
        createdCount: 1,
        issues: [
          { row: 1, column: 'name_en', code: 'COLUMN_MISSING' },
          { row: 3, column: 'parent_code', code: 'REFERENCE_NOT_FOUND' },
          { row: 4, column: '', code: 'IDENTITY_ACCESS_SOMETHING_NEW' },
        ],
      }),
    });

    await user.upload(within(dialog).getByLabelText(/^Workbook/), workbook());
    await user.click(within(dialog).getByRole('button', { name: en('dataImport.check.run') }));

    expect(
      await within(dialog).findByText(en('dataImport.report.issues', { issues: 3 })),
    ).toBeTruthy();
    const rows = within(within(dialog).getByRole('table')).getAllByRole('row').slice(1);
    expect(
      rows.map((row) =>
        within(row)
          .getAllByRole('cell')
          .map((cell) => cell.textContent),
      ),
    ).toEqual([
      ['1', 'name_en', en('dataImport.issues.COLUMN_MISSING')],
      ['3', 'parent_code', en('dataImport.issues.REFERENCE_NOT_FOUND')],
      [
        '4',
        en('dataImport.report.wholeRow'),
        en('dataImport.issues.other', { code: 'IDENTITY_ACCESS_SOMETHING_NEW' }),
      ],
    ]);
    expect(within(dialog).queryByRole('button', { name: /^Import \d/ })).toBeNull();
    const [request] = api.requestsTo('POST', /^\/departments\/imports$/);
    expect(request?.query.get('commit')).toBeNull();
    expect((request?.body as FormData).get('file')).toBeInstanceOf(File);
  });

  test('a clean check offers the import, which commits and reloads the register', async () => {
    const { api, user, dialog } = await openImport();
    api.on('POST', /^\/departments\/imports$/, (request) =>
      request.query.get('commit') === 'true'
        ? {
            body: report({ committed: true, batchId: 'batch-1', createdCount: 1, skippedCount: 1 }),
          }
        : { body: report({ createdCount: 1, skippedCount: 1 }) },
    );

    await user.upload(within(dialog).getByLabelText(/^Workbook/), workbook());
    await user.click(within(dialog).getByRole('button', { name: en('dataImport.check.run') }));
    expect(
      await within(dialog).findByText(
        en('dataImport.report.ready', { rows: 2, created: 1, skipped: 1 }),
      ),
    ).toBeTruthy();
    const listReads = api.requestsTo('GET', /^\/departments$/).length;

    await user.click(
      within(dialog).getByRole('button', { name: en('dataImport.commit.run', { count: 1 }) }),
    );

    expect(await within(dialog).findByText(new RegExp(`Imported .*1.* records`))).toBeTruthy();
    expect(within(dialog).getByText(/batch-1/)).toBeTruthy();
    const commits = api
      .requestsTo('POST', /^\/departments\/imports$/)
      .filter((r) => r.query.get('commit') === 'true');
    expect(commits).toHaveLength(1);
    expect(commits[0]?.headers.get('Idempotency-Key')).not.toBeNull();
    await waitFor(() => {
      expect(api.requestsTo('GET', /^\/departments$/).length).toBeGreaterThan(listReads);
    });
    expect(within(dialog).queryByRole('button', { name: en('dataImport.check.run') })).toBeNull();
  });

  test('a file the API cannot read as a workbook says so', async () => {
    const { api, user, dialog } = await openImport();
    api.on('POST', /^\/departments\/imports$/, problem(415, 'UNSUPPORTED_MEDIA_TYPE'));

    await user.upload(within(dialog).getByLabelText(/^Workbook/), workbook());
    await user.click(within(dialog).getByRole('button', { name: en('dataImport.check.run') }));

    expect(await within(dialog).findByText(en('dataImport.problems.notWorkbook'))).toBeTruthy();
  });

  test('checking with no file chosen asks for one and sends nothing', async () => {
    const { api, user, dialog } = await openImport();

    await user.click(within(dialog).getByRole('button', { name: en('dataImport.check.run') }));

    expect(within(dialog).getByText(en('dataImport.check.noFile'))).toBeTruthy();
    expect(api.requestsTo('POST', /imports/)).toHaveLength(0);
  });
});
