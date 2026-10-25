import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test, vi } from 'vitest';

import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi, problem } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';
import { findMainMenu, openMenuNames } from '@/test/shell.ts';
import {
  ALLOWLIST,
  COLLEAGUE_ID,
  composerSession,
  jobDetail,
  jobSummary,
  OWNER_ID,
  PROJECT_A,
  type ReportState,
  reportView,
  savedViewDetail,
  SHARED_VIEW,
  withReports,
} from '@/test/reportFixtures.ts';

// TASK-072, the FG-02 Reports Center UI over TASK-071's API (and TASK-072's sharing). Acceptance criteria: (1) the
// Custom Report Builder only ever offers the allowlisted fields from the backend configuration, never a free-text SQL
// box; (2) the Export History shows who generated each output and when; (3) a user cannot see another user's
// non-shared saved views. The workbook's checks: compare the builder's field picker with the backend configuration;
// a view created by one user is not visible to another unless explicitly shared.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const dateTime = (iso: string) =>
  new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(
    new Date(iso),
  );

function at(path: string, session: Session = composerSession(), state: ReportState = {}): MockApi {
  const api = withReports(mockApi(), state);
  renderApp({ path, session });
  return api;
}

/** The element, or a failed test saying what was missing. */
function present<T>(value: T | null | undefined, what: string): T {
  if (value === null || value === undefined) {
    throw new Error(`No ${what}.`);
  }
  return value;
}

/** Text with the bidi isolates translate() adds around parameters removed. */
const plain = (text: string | null | undefined) => (text ?? '').replace(/[\u2068\u2069]/g, '');

/** Opens a dialog from the first control of that name (the explorer offers it in its header and its empty state). */
async function openDialog(name: string) {
  const [control] = await screen.findAllByRole('button', { name });
  await userEvent.click(present(control, 'control'));
  return screen.findByRole('dialog');
}

describe('acceptance criterion 1: the builder offers the allowlist and nothing else', () => {
  test('its field picker is exactly the PUBLISHED allowlist the API returns, named in the interface language', async () => {
    at('/reports/explorer');
    expect(
      await screen.findByText(en('reports.explorer.allowlist', { count: 4, version: 2 })),
    ).toBeTruthy();
    const dialog = await openDialog(en('reports.actions.columnsAndFilters'));
    const picker = dialog.querySelector('[data-field-picker]');
    const offered = [...(picker?.querySelectorAll('label') ?? [])].map(
      (label) => label.childNodes[1]?.textContent,
    );
    expect(offered).toEqual(ALLOWLIST.map((entry) => entry.label.en));
  });

  test('there is no query box: every field is a checkbox, every filter field and condition a closed list', async () => {
    at('/reports/explorer');
    const dialog = await openDialog(en('reports.actions.columnsAndFilters'));
    expect(dialog.querySelectorAll('textarea')).toHaveLength(0);
    expect(document.querySelectorAll('textarea')).toHaveLength(0);
    const picker = dialog.querySelector('[data-field-picker]');
    expect(picker?.querySelectorAll('input:not([type="checkbox"])')).toHaveLength(0);

    await userEvent.click(
      within(dialog).getByRole('button', { name: en('reports.columns.addFilter') }),
    );
    const field = within(dialog).getByRole('combobox', { name: en('reports.columns.filterField') });
    expect([...field.querySelectorAll('option')].map((option) => option.textContent)).toEqual(
      ALLOWLIST.filter((entry) => entry.isFilterable).map((entry) => entry.label.en),
    );
    await userEvent.selectOptions(field, 'PROJECT_LIFECYCLE_STATE.LIFECYCLE_STATE');
    const operator = within(dialog).getByRole('combobox', { name: en('reports.columns.operator') });
    expect([...operator.querySelectorAll('option')].map((option) => option.value)).toEqual([
      'EQ',
      'NEQ',
      'IN',
    ]);
    // A closed set of values is chosen from, never typed.
    const value = within(dialog).getByRole('combobox', { name: en('reports.columns.value') });
    expect([...value.querySelectorAll('option')].map((option) => option.value)).toEqual([
      '',
      'ACTIVE',
      'SUSPENDED',
      'CLOSED',
    ]);
  });

  test('a composition is sent as the codes the allowlist gave, and its result runs as the caller', async () => {
    const api = at('/reports/explorer');
    const dialog = await openDialog(en('reports.actions.columnsAndFilters'));
    await userEvent.click(within(dialog).getByRole('checkbox', { name: 'Project ID' }));
    await userEvent.click(within(dialog).getByRole('checkbox', { name: /Lifecycle state/ }));
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('reports.columns.addFilter') }),
    );
    await userEvent.selectOptions(
      within(dialog).getByRole('combobox', { name: en('reports.columns.filterField') }),
      'PROJECT_LIFECYCLE_STATE.LIFECYCLE_STATE',
    );
    await userEvent.selectOptions(
      within(dialog).getByRole('combobox', { name: en('reports.columns.value') }),
      'SUSPENDED',
    );
    await userEvent.selectOptions(
      within(dialog).getByRole('combobox', { name: en('reports.columns.sortBy') }),
      'PROJECT.FORMAL_PROJECT_ID',
    );
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('reports.columns.apply') }),
    );

    await screen.findByRole('table');
    const [run] = api.requestsTo('POST', /^\/report-explorer\/run$/);
    expect(run?.body).toEqual({
      columns: [
        { sourceEntityCode: 'PROJECT', fieldCode: 'FORMAL_PROJECT_ID' },
        { sourceEntityCode: 'PROJECT_LIFECYCLE_STATE', fieldCode: 'LIFECYCLE_STATE' },
      ],
      filters: [
        {
          sourceEntityCode: 'PROJECT_LIFECYCLE_STATE',
          fieldCode: 'LIFECYCLE_STATE',
          operator: 'EQ',
          value: 'SUSPENDED',
        },
      ],
      sort: [{ sourceEntityCode: 'PROJECT', fieldCode: 'FORMAL_PROJECT_ID', direction: 'ASC' }],
    });
  });

  test('a range filter on a number takes two values, never an expression', async () => {
    const api = at('/reports/explorer');
    const dialog = await openDialog(en('reports.actions.columnsAndFilters'));
    await userEvent.click(within(dialog).getByRole('checkbox', { name: 'Project ID' }));
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('reports.columns.addFilter') }),
    );
    await userEvent.selectOptions(
      within(dialog).getByRole('combobox', { name: en('reports.columns.filterField') }),
      'RISK_RISK_EXPOSURE.OPEN_RISKS',
    );
    await userEvent.selectOptions(
      within(dialog).getByRole('combobox', { name: en('reports.columns.operator') }),
      'BETWEEN',
    );
    await userEvent.type(within(dialog).getByLabelText(en('reports.columns.from')), '1');
    await userEvent.type(within(dialog).getByLabelText(en('reports.columns.to')), '5,;DROP');
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('reports.columns.apply') }),
    );
    await screen.findByRole('table');
    const [run] = api.requestsTo('POST', /^\/report-explorer\/run$/);
    expect((run?.body as { filters: unknown[] }).filters).toEqual([
      {
        sourceEntityCode: 'RISK_RISK_EXPOSURE',
        fieldCode: 'OPEN_RISKS',
        operator: 'BETWEEN',
        value: '1,5;DROP',
      },
    ]);
  });

  test('with no PUBLISHED report rules the builder says it is closed and offers no field', async () => {
    at('/reports/explorer', composerSession(), {
      allowlist: problem(422, 'CONFIGURATION_MISSING'),
    });
    expect(await screen.findByText(en('reports.explorer.closed'))).toBeTruthy();
    expect(
      screen.queryByRole('button', { name: en('reports.actions.columnsAndFilters') }),
    ).toBeNull();
  });

  test('a role without REPORT_COMPOSE is told the builder is not for it', async () => {
    at('/reports/explorer', sessionFor(['R06']), { allowlist: problem(403, 'PERMISSION_DENIED') });
    expect(await screen.findByText(en('reports.explorer.notForRole'))).toBeTruthy();
  });
});

describe('acceptance criterion 2: the export history shows who generated each output and when', () => {
  test('each job names its requester and when it was requested and generated', async () => {
    at('/reports/exports', composerSession(), {
      jobs: [
        jobSummary(),
        jobSummary({
          id: 'job-2',
          requestedByUserId: COLLEAGUE_ID,
          status: 'FAILED',
          failureCode: 'REPORT_EXPORT_SIZE_LIMIT_EXCEEDED',
          completedAt: null,
          sensitivity: null,
          expiresAt: null,
        }),
      ],
    });
    const table = await screen.findByRole('table', { name: en('reports.history.title') });
    const [header, own, other] = within(table).getAllByRole('row');
    expect(
      within(present(header, 'header')).getByText(en('reports.history.generatedBy')),
    ).toBeTruthy();
    expect(
      within(present(header, 'header')).getByText(en('reports.history.generatedAt')),
    ).toBeTruthy();

    expect(own?.querySelector('[data-generated-by]')?.getAttribute('data-generated-by')).toBe(
      OWNER_ID,
    );
    expect(
      within(present(own, 'own')).getByText(en('reports.history.you', { name: 'Noura Portfolio' })),
    ).toBeTruthy();
    expect(within(present(own, 'own')).getByText(dateTime('2026-10-10T07:00:00Z'))).toBeTruthy();
    expect(within(present(own, 'own')).getByText(dateTime('2026-10-10T07:01:00Z'))).toBeTruthy();
    expect(within(present(own, 'own')).getByText(en('reports.sensitivity.SENSITIVE'))).toBeTruthy();

    // A requester the directory does not name for this person is shown by short id, never left blank.
    expect(
      within(present(other, 'other')).getByText(
        en('common.people.unknownUser', { id: '00000000' }),
      ),
    ).toBeTruthy();
    expect(
      within(present(other, 'other')).getByText(en('reports.history.notGenerated')),
    ).toBeTruthy();
    expect(
      within(present(other, 'other')).getByText(
        en('reports.failure.REPORT_EXPORT_SIZE_LIMIT_EXCEEDED'),
      ),
    ).toBeTruthy();
  });

  test('a download is authorised again: a refusal says the person may no longer see what it holds', async () => {
    const api = at('/reports/exports');
    api.on('GET', /^\/report-jobs\/job-1\/content$/, problem(403, 'PERMISSION_DENIED'));
    await userEvent.click(await screen.findByRole('button', { name: /^Download/ }));
    expect((await screen.findByRole('alert')).textContent).toBe(
      en('reports.problems.downloadDenied'),
    );
  });

  test('a completed file is saved under the name the API gives it', async () => {
    const api = at('/reports/exports');
    api.on('GET', /^\/report-jobs\/job-1\/content$/, {
      body: new Blob(['a,b']),
      headers: { 'Content-Disposition': "attachment; filename*=UTF-8''financial.xlsx" },
    });
    const createObjectURL = vi.fn(() => 'blob:report');
    vi.stubGlobal('URL', Object.assign(URL, { createObjectURL, revokeObjectURL: vi.fn() }));
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, 'click')
      .mockImplementation(() => undefined);
    await userEvent.click(await screen.findByRole('button', { name: /^Download/ }));
    await waitFor(() => {
      expect(click).toHaveBeenCalledTimes(1);
    });
    expect((click.mock.contexts[0] as HTMLAnchorElement).download).toBe('financial.xlsx');
    click.mockRestore();
  });

  test('a job not yet running can be cancelled', async () => {
    const api = at('/reports/exports', composerSession(), {
      jobs: [
        jobSummary({ status: 'QUEUED', completedAt: null, sensitivity: null, expiresAt: null }),
      ],
    });
    api.on('POST', /^\/report-jobs\/job-1\/cancel$/, {
      body: jobDetail({ id: 'job-1', status: 'CANCELLED' }),
    });
    await userEvent.click(await screen.findByRole('button', { name: /^Cancel/ }));
    await waitFor(() => {
      expect(api.requestsTo('POST', /^\/report-jobs\/job-1\/cancel$/)).toHaveLength(1);
    });
  });
});

describe("acceptance criterion 3: another user's views are seen only when shared", () => {
  test('the list holds the person’s own views and those shared with them, as the API returns them', async () => {
    at('/reports/saved-views');
    const table = await screen.findByRole('table', { name: en('reports.savedViews.title') });
    const rows = within(table).getAllByRole('row').slice(1);
    expect(rows.map((row) => row.getAttribute('data-saved-view'))).toEqual([
      'view-own',
      'view-shared',
    ]);

    const [own, shared] = rows;
    expect(within(present(own, 'own')).getByText(en('common.people.you'))).toBeTruthy();
    expect(
      within(present(own, 'own')).getByText(
        en('reports.savedViews.sharedWith', { roles: 'Sector Managers' }),
      ),
    ).toBeTruthy();
    expect(
      within(present(own, 'own')).getByRole('button', { name: /^Change sharing/ }),
    ).toBeTruthy();
    expect(within(present(own, 'own')).getByRole('button', { name: /^Delete/ })).toBeTruthy();

    // Shared with the person: named as such, its owner shown, and theirs to open but not to change or delete.
    expect(
      within(present(shared, 'shared')).getByText(en('reports.savedViews.sharedWithYou')),
    ).toBeTruthy();
    expect(
      within(present(shared, 'shared')).getByText(
        en('common.people.unknownUser', { id: '00000000' }),
      ),
    ).toBeTruthy();
    expect(
      within(present(shared, 'shared')).queryByRole('button', { name: /^Change sharing/ }),
    ).toBeNull();
    expect(within(present(shared, 'shared')).queryByRole('button', { name: /^Delete/ })).toBeNull();
    // ADR-012: the name keeps the language it was entered in.
    const name = present(shared, 'shared').querySelector('td span[lang]');
    expect([name?.textContent, name?.getAttribute('lang')]).toEqual(['المشاريع المعلقة', 'ar']);
  });

  test('a view that is neither the person’s nor shared with them is not found when opened by address', async () => {
    at('/reports/explorer?view=someone-elses');
    expect(await screen.findByText(en('reports.problems.notFound'))).toBeTruthy();
    expect(document.querySelector('[data-saved-view]')).toBeNull();
  });

  test('a shared view opens with its owner named and runs with the opener’s own access', async () => {
    const api = at(`/reports/explorer?view=${SHARED_VIEW.id}`);
    const notice = await screen.findByText(en('reports.savedView.ownAccess'));
    expect(notice).toBeTruthy();
    await screen.findByRole('table');
    // The run is the caller's own, through the explorer; nothing of the owner's access is sent or reused.
    const [run] = api.requestsTo('POST', /^\/report-explorer\/run$/);
    expect(run?.headers.get('Authorization')).toBe('Bearer test-access-token');
    expect((run?.body as { filters: unknown[] }).filters).toEqual(SHARED_VIEW.filters);
    // Only its owner may save changes to it; the person may save it as their own.
    expect(screen.queryByRole('button', { name: en('reports.actions.saveChanges') })).toBeNull();
    expect(screen.getByRole('button', { name: en('reports.actions.saveAsNew') })).toBeTruthy();
  });

  test('MOD-062 shares only with ADR-019’s roles, and keeps a view private unless one is ticked', async () => {
    const api = at('/reports/financial/FINANCIAL_PERFORMANCE');
    await screen.findByRole('table');
    const dialog = await openDialog(en('reports.actions.saveView'));
    const share = within(dialog).getByRole('group', { name: en('reports.saveView.shareWith') });
    expect(
      within(share)
        .getAllByRole('checkbox')
        .map((box) => box.getAttribute('value')),
    ).toEqual(['R02', 'R03', 'R07']);
    await userEvent.type(within(dialog).getByRole('textbox', { name: /Name/ }), 'Private view');
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('reports.saveView.save') }),
    );
    await waitFor(() => {
      expect(api.requestsTo('POST', /^\/saved-views$/)).toHaveLength(1);
    });
    expect(api.requestsTo('POST', /^\/saved-views$/)[0]?.body).toMatchObject({
      viewType: 'REPORT_PARAMETERS',
      reportCode: 'FINANCIAL_PERFORMANCE',
      sharedWithRoleCodes: [],
    });
  });

  test('its owner changes who a view is shared with, sending the view whole under its ETag', async () => {
    const api = at('/reports/saved-views');
    const table = await screen.findByRole('table');
    const own = present(within(table).getAllByRole('row')[1], 'first row');
    await userEvent.click(within(own).getByRole('button', { name: /^Change sharing/ }));
    const dialog = await screen.findByRole('dialog');
    await userEvent.click(within(dialog).getByRole('checkbox', { name: 'Executives' }));
    await userEvent.click(within(dialog).getByRole('checkbox', { name: 'Sector Managers' }));
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('reports.saveView.save') }),
    );
    await waitFor(() => {
      expect(api.requestsTo('PUT', /^\/saved-views\/view-own$/)).toHaveLength(1);
    });
    const [put] = api.requestsTo('PUT', /^\/saved-views\/view-own$/);
    expect(put?.headers.get('If-Match')).toBe('"7"');
    expect(put?.body).toMatchObject({
      parameters: [{ code: 'FINANCIAL_CONDITION', value: 'RED' }],
      sharedWithRoleCodes: ['R07'],
    });
  });

  test('a view that no longer fits says what does not, and is not applied', async () => {
    at('/reports/financial/FINANCIAL_PERFORMANCE?view=view-own', composerSession(), {
      views: [
        savedViewDetail({
          compatibility: 'INCOMPATIBLE',
          issues: [{ field: 'parameters[0]', code: 'NOT_ALLOWED' }],
        }),
      ],
    });
    expect(await screen.findByText(en('reports.savedView.incompatible'))).toBeTruthy();
    expect(screen.getByText(en('reports.issues.NOT_ALLOWED'), { exact: false })).toBeTruthy();
    expect(
      screen.getByRole<HTMLSelectElement>('combobox', { name: 'Financial condition' }).value,
    ).toBe('');
  });
});

describe('SCR-130 and SCR-131 to SCR-137', () => {
  test('the Reports Center lists the catalogue by experience and finds a report by its words', async () => {
    at('/reports');
    expect(await screen.findByRole('heading', { name: 'Financial Reports' })).toBeTruthy();
    expect(
      screen
        .getAllByRole('link', { name: /Performance$/ })
        .map((link) => link.getAttribute('href')),
    ).toEqual(['/reports/financial/FINANCIAL_PERFORMANCE', '/reports/financial/KPI_PERFORMANCE']);
    await userEvent.type(
      screen.getByRole('searchbox', { name: en('reports.center.search') }),
      'register',
    );
    await waitFor(() => {
      expect(screen.queryByRole('heading', { name: 'Financial Reports' })).toBeNull();
    });
    expect(screen.getByRole('link', { name: 'Project Register' })).toBeTruthy();
  });

  test('an empty catalogue says no report is published for the person’s roles', async () => {
    at('/reports', sessionFor(['R01']), { catalogue: [] });
    expect(await screen.findByText(en('reports.center.none'))).toBeTruthy();
  });

  test('each experience lists its own reports only', async () => {
    at('/reports/project');
    expect(await screen.findByRole('heading', { name: 'Project Reports', level: 1 })).toBeTruthy();
    expect(
      [...document.querySelectorAll('[data-report]')].map((item) =>
        item.getAttribute('data-report'),
      ),
    ).toEqual(['PROJECT_REPORT', 'SCHEDULE_DELIVERY']);
  });

  test('a report opened under another experience’s address moves to its own', async () => {
    at('/reports/portfolio/FINANCIAL_PERFORMANCE');
    await waitFor(() => {
      expect(screen.getByRole('heading', { level: 1 }).textContent).toBe('Financial Performance');
    });
  });

  test('a report outside the person’s audience is not found', async () => {
    at('/reports/executive/GOVERNANCE_CHANGE');
    expect(await screen.findByText(en('reports.notFound.description'))).toBeTruthy();
  });
});

describe('the report shell', () => {
  test('a missing value is never 0 and a stale one keeps its value, flagged with its own as-of', async () => {
    at('/reports/financial/FINANCIAL_PERFORMANCE');
    const table = await screen.findByRole('table', { name: 'Financial Performance' });
    const [, rowA, rowB] = within(table).getAllByRole('row');
    expect(
      within(present(rowA, 'rowA')).getByText(en('reports.units.sar', { amount: '1,250,000.00' })),
    ).toBeTruthy();
    expect(within(present(rowA, 'rowA')).getByText('Strategy Department')).toBeTruthy();

    const restricted = present(rowB, 'rowB').querySelector('[data-value-state="RESTRICTED"]');
    expect(plain(restricted?.textContent)).toBe(plain(en('dashboards.unknown.RESTRICTED.label')));
    expect(present(rowB, 'rowB').textContent).not.toMatch(/\b0\b/);
    const stale = present(rowB, 'rowB').querySelector('[data-value-state="STALE"]');
    expect(plain(stale?.textContent)).toBe(
      plain(en('reports.cell.staleAsOf', { date: dateTime('2026-09-01T09:00:00Z') })),
    );
    expect(present(rowB, 'rowB').querySelector('[data-freshness="STALE"]')?.textContent).toContain(
      en('financialKpi.financialStatus.AMBER'),
    );
    // Free text keeps the direction it was entered in.
    expect(within(present(rowB, 'rowB')).getByText('طريق الملك').getAttribute('dir')).toBe('auto');
  });

  test('the banner states each source’s semantic state, freshness and coverage', async () => {
    at('/reports/financial/FINANCIAL_PERFORMANCE');
    const banner = await screen.findByRole('region', { name: en('reports.banner.label') });
    const section = banner.querySelector('[data-projection]');
    expect(section?.getAttribute('data-semantic-state')).toBe('PUBLISHED_OFFICIAL');
    expect(section?.getAttribute('data-freshness')).toBe('STALE');
    expect(plain(section?.querySelector('[data-coverage]')?.textContent)).toBe(
      plain(
        [
          en('reports.banner.included', { included: 2, eligible: 3 }),
          en('reports.banner.excluded', { count: 1 }),
          en('reports.banner.stale', { count: 1 }),
        ].join(' · '),
      ),
    );
    expect(plain(screen.getByText(/^Run at/).textContent)).toContain(
      dateTime('2026-10-11T08:00:00Z'),
    );
  });

  test('parameters come from the API’s options and a required one must be chosen before anything runs', async () => {
    const api = at('/reports/project/PROJECT_REPORT');
    expect(await screen.findByText(en('reports.result.chooseParameters'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/run$/)).toHaveLength(0);
    await userEvent.click(screen.getByRole('button', { name: en('reports.actions.run') }));
    expect(screen.getByText(en('common.fieldErrors.required'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/run$/)).toHaveLength(0);

    await userEvent.selectOptions(screen.getByRole('combobox', { name: /Project/ }), PROJECT_A);
    await userEvent.click(screen.getByRole('button', { name: en('reports.actions.run') }));
    await waitFor(() => {
      expect(api.requestsTo('POST', /^\/reports\/PROJECT_REPORT\/run$/)).toHaveLength(1);
    });
    expect(api.requestsTo('POST', /^\/reports\/PROJECT_REPORT\/run$/)[0]?.body).toMatchObject({
      parameters: [{ code: 'PROJECT', value: PROJECT_A }],
    });
  });

  test('MOD-061 offers the report’s own columns and sends the choice and its order', async () => {
    const api = at('/reports/financial/FINANCIAL_PERFORMANCE');
    await screen.findByRole('table');
    const dialog = await openDialog(en('reports.actions.columns'));
    expect(within(dialog).queryByText(en('reports.columns.filters'))).toBeNull();
    await userEvent.click(within(dialog).getByRole('checkbox', { name: /Actual expenditure/ }));
    await userEvent.click(
      within(dialog).getByRole('button', { name: /^Move up Actual expenditure/ }),
    );
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('reports.columns.apply') }),
    );
    await waitFor(() => {
      expect(api.requestsTo('POST', /\/run$/)).toHaveLength(2);
    });
    const columns = (
      api.requestsTo('POST', /\/run$/)[1]?.body as { columns: { fieldCode: string }[] }
    ).columns.map((column) => column.fieldCode);
    expect(columns).toEqual([
      'FORMAL_PROJECT_ID',
      'TITLE',
      'DEPARTMENT',
      'FINANCIAL_STATUS',
      'ACTUAL_EXPENDITURE_TO_DATE',
      'APPROVED_BUDGET',
    ]);
  });

  test('export is offered only when the API says the person may export', async () => {
    at('/reports/financial/FINANCIAL_PERFORMANCE', composerSession(), {
      reports: [reportView({ mayExport: false })],
    });
    await screen.findByRole('table');
    expect(screen.queryByRole('button', { name: en('reports.actions.export') })).toBeNull();
    expect(screen.getByText(en('reports.header.noExport'), { exact: false })).toBeTruthy();
  });
});

describe('MOD-060 Export Report', () => {
  test('PDF, XLSX or CSV in Arabic or English, warned when sensitive, followed until the file is ready', async () => {
    const api = at('/reports/financial/FINANCIAL_PERFORMANCE');
    let reads = 0;
    api.on('GET', /^\/report-jobs\/job-new$/, () => {
      reads += 1;
      return {
        body:
          reads === 1
            ? jobDetail({ status: 'RUNNING' })
            : jobDetail({
                status: 'COMPLETED',
                completedAt: '2026-10-11T08:00:05Z',
                output: {
                  fileName: 'financial.csv',
                  contentType: 'text/csv',
                  sizeBytes: 120,
                  checksumSha256: 'ab',
                  sensitivity: 'SENSITIVE',
                  rowCount: 2,
                  sourceAsOf: null,
                  expiresAt: '2026-10-12T08:00:05Z',
                  status: 'AVAILABLE',
                },
              }),
      };
    });
    await screen.findByRole('table');
    const dialog = await openDialog(en('reports.actions.export'));
    expect(within(dialog).getAllByRole('radio', { name: /PDF|Excel|CSV/ })).toHaveLength(3);
    expect(within(dialog).getByText(en('reports.export.sensitiveWarning'))).toBeTruthy();
    await userEvent.click(within(dialog).getByRole('radio', { name: 'CSV' }));
    await userEvent.click(within(dialog).getByRole('radio', { name: 'Arabic' }));
    await userEvent.click(within(dialog).getByRole('button', { name: en('reports.export.start') }));

    const [request] = api.requestsTo('POST', /^\/reports\/FINANCIAL_PERFORMANCE\/export$/);
    expect(request?.body).toMatchObject({ format: 'CSV', language: 'ar', parameters: [] });
    expect(request?.headers.get('Idempotency-Key')).not.toBeNull();
    await waitFor(
      () => {
        expect(within(dialog).getByRole('button', { name: /^Download/ })).toBeTruthy();
      },
      { timeout: 8000 },
    );
    expect(within(dialog).getByText(en('reports.export.classifiedSensitive'))).toBeTruthy();
  }, 15000);
});

describe('navigation, accessibility and language', () => {
  test('the builder and saved views are offered to ADR-019’s composers, the export history to whoever may export (the review, H3)', async () => {
    at('/reports', sessionFor(['R06']));
    await findMainMenu();
    expect(openMenuNames('common.menu.dashboardsReports')).toEqual([
      en('common.menu.dashboards'),
      en('reports.nav.center'),
    ]);
    cleanup();

    at('/reports', sessionFor(['R04']));
    await findMainMenu();
    expect(openMenuNames('common.menu.dashboardsReports')).toEqual([
      en('common.menu.dashboards'),
      en('reports.nav.center'),
      en('reports.nav.exports'),
    ]);
  });

  test.each([
    ['/reports'],
    ['/reports/financial/FINANCIAL_PERFORMANCE'],
    ['/reports/explorer'],
    ['/reports/saved-views'],
    ['/reports/exports'],
  ])('%s has no axe violations', async (path) => {
    at(path);
    await screen.findByRole('heading', { level: 1 });
    await waitFor(() => {
      expect(document.querySelector('[role="status"].state--loading')).toBeNull();
    });
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toHaveLength(0);
  });

  test('in Arabic the screens read in Arabic and the report names are the API’s Arabic ones', async () => {
    withReports(mockApi());
    renderApp({ path: '/reports', session: composerSession(), language: 'ar' });
    expect(await screen.findByRole('heading', { name: 'مركز التقارير', level: 1 })).toBeTruthy();
    expect(screen.getByRole('link', { name: 'الأداء المالي' })).toBeTruthy();
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toHaveLength(0);
  });
});
