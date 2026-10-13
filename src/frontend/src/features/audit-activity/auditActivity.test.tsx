import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, test } from 'vitest';

import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import {
  ADMINISTRATION_EVENT_ID,
  AUDIT_REVIEWER_ACCESS,
  type AuditState,
  BUSINESS_EVENT_ID,
  CORRELATION_ID,
  INTEGRATION_EVENT_ID,
  INTEGRATION_REVIEWER_ACCESS,
  NO_AUDIT_ACCESS,
  PROJECT_MANAGER_ACCESS,
  RISK_ID,
  SYSTEM_ADMINISTRATOR_ACCESS,
  withAudit,
} from '@/test/auditActivityFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi, problem } from '@/test/mockApi.ts';
import {
  projectDetail,
  PROJECT_ID,
  reviewerSession,
  withProject,
  withProjectLookups,
} from '@/test/projectFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// TASK-074, the FG-06 Audit & Activity UI over TASK-073's API. Acceptance criteria: (1) SCR-057 Activity and SCR-058
// Audit are visually and navigationally distinct, never one tab; (2) SCR-058 is reachable only with the explicit audit
// permission, not for every R01; (3) ADM-052 is labelled a narrow technical view, not a SIEM/observability console.
// The workbook's checks: reach SCR-058 as an R01 without the permission and see it denied; see the two tabs apart.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

/** Text with the bidi isolates translate() adds around parameters removed. */
const plain = (text: string | null | undefined) => (text ?? '').replace(/[⁨⁩]/g, '');

function present<T>(value: T | null | undefined, what: string): T {
  if (value === null || value === undefined) {
    throw new Error(`No ${what}.`);
  }
  return value;
}

const r01 = () => sessionFor(['R01']);
/** An internal reviewer over the project's department, holding no R01. */
const reviewer = () => reviewerSession();

function open(path: string, session: Session, state: AuditState, language: 'en' | 'ar' = 'en') {
  const api = withAudit(
    withProject(withProjectLookups(mockApi()), projectDetail({ status: 'ACTIVE' })),
    state,
  );
  renderApp({ path, session, language });
  return api;
}

/** An Activity line's visible sentence (its link repeats it for screen readers). */
const SUMMARY = '.activity-timeline__summary';

const workspace = (tab = '') => `/projects/${PROJECT_ID}${tab === '' ? '' : `/${tab}`}`;

async function tabLinks(): Promise<HTMLElement[]> {
  const tabs = await screen.findByRole('navigation', {
    name: en('projects.workspace.tabs.label'),
  });
  return within(tabs).getAllByRole('link');
}

const auditReads = (api: MockApi) => api.requestsTo('GET', /^\/audit-events/);
const activityReads = (api: MockApi) => api.requestsTo('GET', /^\/activity-entries$/);

async function expectAccessible() {
  const violations = await accessibilityViolations();
  expect(violations, describeViolations(violations)).toEqual([]);
}

afterEach(() => {
  cleanup();
});

describe('acceptance criterion 1: Activity and Audit are two distinct tabs', () => {
  test('the workspace offers them as separate tabs with their own labels and addresses', async () => {
    open(workspace(), reviewer(), { access: AUDIT_REVIEWER_ACCESS });

    await waitFor(async () => {
      expect((await tabLinks()).map((link) => link.textContent)).toContain(
        en('projects.workspace.tabs.audit'),
      );
    });
    const links = await tabLinks();
    const activity = present(
      links.find((link) => link.textContent === en('projects.workspace.tabs.activity')),
      'Activity tab',
    );
    const audit = present(
      links.find((link) => link.textContent === en('projects.workspace.tabs.audit')),
      'Audit tab',
    );
    expect(activity).not.toBe(audit);
    expect(activity.getAttribute('href')).toBe(workspace('activity-history'));
    expect(audit.getAttribute('href')).toBe(workspace('audit-history'));
  });

  test('SCR-057 is a plain-language timeline that reads Activity and never the audit store', async () => {
    const api = open(workspace('activity-history'), reviewer(), {
      access: AUDIT_REVIEWER_ACCESS,
    });

    const timeline = await screen.findByRole('list', { name: en('auditActivity.activity.title') });
    expect(within(timeline).getByText('Risk closed', { selector: SUMMARY })).toBeTruthy();
    expect(document.querySelector('[data-history="activity"]')).not.toBeNull();
    expect(document.querySelector('[data-history="audit"]')).toBeNull();
    expect(screen.queryByRole('table')).toBeNull();
    expect(auditReads(api)).toEqual([]);
    expect(activityReads(api)[0]?.query.get('projectId')).toBe(PROJECT_ID);
    // The current tab is Activity, and only Activity.
    const current = (await tabLinks()).filter(
      (link) => link.getAttribute('aria-current') === 'page',
    );
    expect(current.map((link) => link.textContent)).toEqual([
      en('projects.workspace.tabs.activity'),
    ]);
    await expectAccessible();
  });

  test('SCR-058 is formal evidence in a table, read from the audit store and never from Activity', async () => {
    const api = open(workspace('audit-history'), reviewer(), { access: AUDIT_REVIEWER_ACCESS });

    const table = await screen.findByRole('table', {
      name: en('auditActivity.projectAudit.caption'),
    });
    expect(within(table).getByText('Risk.RiskClosed', { selector: 'code' })).toBeTruthy();
    expect(document.querySelector('[data-history="audit"]')).not.toBeNull();
    expect(document.querySelector('.activity-timeline')).toBeNull();
    expect(activityReads(api)).toEqual([]);
    const current = (await tabLinks()).filter(
      (link) => link.getAttribute('aria-current') === 'page',
    );
    expect(current.map((link) => link.textContent)).toEqual([en('projects.workspace.tabs.audit')]);
    await expectAccessible();
  });
});

describe('acceptance criterion 2: SCR-058 needs the explicit audit permission, not R01', () => {
  test('R01 as shipped is offered no Audit tab', async () => {
    open(workspace(), r01(), { access: SYSTEM_ADMINISTRATOR_ACCESS });

    await screen.findByRole('heading', { level: 1, name: 'Coastal road upgrade' });
    // The audit-access answer has landed: R01's sidebar shows its audit trail entry.
    await screen.findByRole('navigation', { name: en('auditActivity.nav.label') });
    const names = (await tabLinks()).map((link) => link.textContent);
    expect(names).not.toContain(en('projects.workspace.tabs.audit'));
    expect(names).not.toContain(en('projects.workspace.tabs.activity'));
  });

  test('validation check: R01 reaching SCR-058 by its address is denied and nothing is read', async () => {
    const api = open(workspace('audit-history'), r01(), { access: SYSTEM_ADMINISTRATOR_ACCESS });

    expect(await screen.findByText(en('auditActivity.projectAudit.deniedTitle'))).toBeTruthy();
    expect(screen.getByText(en('auditActivity.denied.projectAudit'))).toBeTruthy();
    expect(screen.queryByRole('table')).toBeNull();
    expect(auditReads(api)).toEqual([]);
    await expectAccessible();
  });

  test('so is an event of SCR-058 reached by its address', async () => {
    const api = open(workspace(`audit-history/${BUSINESS_EVENT_ID}`), r01(), {
      access: SYSTEM_ADMINISTRATOR_ACCESS,
    });

    expect(await screen.findByText(en('auditActivity.projectAudit.deniedTitle'))).toBeTruthy();
    expect(auditReads(api)).toEqual([]);
  });

  test('a reviewer holding project audit without R01 reads it, asking only for their own project categories', async () => {
    const api = open(workspace('audit-history'), reviewer(), { access: AUDIT_REVIEWER_ACCESS });

    await screen.findByRole('table', { name: en('auditActivity.projectAudit.caption') });
    const [read] = auditReads(api);
    expect(read?.query.get('category')).toBe('FINANCIAL,BUSINESS');
    expect(read?.query.get('projectId')).toBe(PROJECT_ID);
  });

  test('the API decides: a permission withdrawn after the tab opened is refused and said so', async () => {
    const api = open(workspace('audit-history'), reviewer(), { access: AUDIT_REVIEWER_ACCESS });
    api.on('GET', /^\/audit-events$/, problem(403, 'PERMISSION_DENIED'));

    expect(await screen.findByText(en('auditActivity.problems.permissionDenied'))).toBeTruthy();
  });

  test('someone with Activity only is denied SCR-058 and pointed back to the Activity tab', async () => {
    open(workspace('audit-history'), reviewer(), { access: PROJECT_MANAGER_ACCESS });

    const back = await screen.findByRole('link', {
      name: en('auditActivity.projectAudit.toActivity'),
    });
    expect(back.getAttribute('href')).toBe(workspace('activity-history'));
  });

  test('an event opens inside the project, immutable, with no edit or delete', async () => {
    open(workspace('audit-history'), reviewer(), { access: AUDIT_REVIEWER_ACCESS });

    const table = await screen.findByRole('table', {
      name: en('auditActivity.projectAudit.caption'),
    });
    const row = present(
      table.querySelector<HTMLElement>(`[data-audit-event="${BUSINESS_EVENT_ID}"]`),
      'event row',
    );
    await userEvent.click(within(row).getByRole('link'));

    expect(await screen.findByText(en('auditActivity.detail.immutable'))).toBeTruthy();
    expect(screen.getByRole('heading', { level: 1, name: 'Coastal road upgrade' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: /edit|delete|remove/i })).toBeNull();
    await expectAccessible();
  });
});

describe('acceptance criterion 3: ADM-052 is a narrow technical view', () => {
  test('it says so before any figure, and shows the pipeline state, not an audit search', async () => {
    const api = open('/admin/system-event-logs', r01(), { access: SYSTEM_ADMINISTRATOR_ACCESS });

    expect(
      await screen.findByRole('heading', { level: 1, name: en('auditActivity.system.title') }),
    ).toBeTruthy();
    const scope = present(document.querySelector('[data-scope="narrow-technical"]'), 'scope');
    expect(
      within(scope as HTMLElement).getByText(en('auditActivity.system.scopeTitle')),
    ).toBeTruthy();
    expect(within(scope as HTMLElement).getByText(en('auditActivity.system.scope'))).toBeTruthy();
    expect(await screen.findByText('AUDIT_EVENT_INVALID')).toBeTruthy();
    expect(screen.queryByRole('form', { name: en('common.filters.label') })).toBeNull();
    expect(auditReads(api)).toEqual([]);
    await expectAccessible();
  });

  test('its checks are audited writes; findings link to the formal event', async () => {
    const api = open('/admin/system-event-logs', r01(), { access: SYSTEM_ADMINISTRATOR_ACCESS });

    await userEvent.click(
      await screen.findByRole('button', { name: en('auditActivity.system.verify') }),
    );
    expect(await screen.findByText(en('auditActivity.system.intact'))).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: en('auditActivity.system.check') }));
    expect(await screen.findByText(en('auditActivity.system.differs'))).toBeTruthy();
    const finding = screen.getByRole('link', { name: BUSINESS_EVENT_ID });
    expect(finding.getAttribute('href')).toBe(`/admin/audit-trail/${BUSINESS_EVENT_ID}`);
    for (const path of [/^\/audit-integrity-verifications$/, /^\/activity-projection-checks$/]) {
      const [write] = api.requestsTo('POST', path);
      expect(write?.headers.get('Idempotency-Key')).toBeTruthy();
    }
    expect(api.requestsTo('POST', /^\/activity-projection-rebuilds$/)).toEqual([]);
  });

  test('it needs the operations permission', async () => {
    open('/admin/system-event-logs', reviewer(), { access: AUDIT_REVIEWER_ACCESS });

    expect(await screen.findByText(en('auditActivity.denied.system'))).toBeTruthy();
  });
});

describe('ADM-050 Audit Trail and ADM-051 Audit Event Detail', () => {
  test('R01 reads administration audit, says so, and is offered no category it does not hold', async () => {
    const api = open('/admin/audit-trail', r01(), { access: SYSTEM_ADMINISTRATOR_ACCESS });

    const table = await screen.findByRole('table', { name: en('auditActivity.trail.title') });
    expect(
      [...table.querySelectorAll('[data-audit-event]')].map((row) =>
        row.getAttribute('data-category'),
      ),
    ).toEqual(['ADMINISTRATION']);
    expect(
      document.querySelector('[data-held-categories]')?.getAttribute('data-held-categories'),
    ).toBe('ADMINISTRATION');
    expect(
      screen.queryByRole('combobox', { name: en('auditActivity.fields.category') }),
    ).toBeNull();
    expect(auditReads(api)[0]?.query.get('category')).toBeNull();
    await expectAccessible();
  });

  test('an audit reviewer without R01 opens it; a person with no audit category does not', async () => {
    open('/admin/audit-trail', reviewer(), { access: AUDIT_REVIEWER_ACCESS });
    expect(
      await screen.findByRole('table', { name: en('auditActivity.trail.title') }),
    ).toBeTruthy();
    cleanup();

    const api = open('/admin/audit-trail', reviewer(), { access: PROJECT_MANAGER_ACCESS });
    expect(await screen.findByText(en('auditActivity.denied.trail'))).toBeTruthy();
    expect(auditReads(api)).toEqual([]);
  });

  test('filters are sent as the API reads them, and a malformed id is refused before anything is sent', async () => {
    const api = open('/admin/audit-trail', reviewer(), { access: AUDIT_REVIEWER_ACCESS });
    await screen.findByRole('table', { name: en('auditActivity.trail.title') });

    await userEvent.selectOptions(
      screen.getByRole('combobox', { name: en('auditActivity.fields.category') }),
      'BUSINESS',
    );
    await userEvent.selectOptions(
      screen.getByRole('combobox', { name: en('auditActivity.fields.outcome') }),
      'DENIED',
    );
    const actor = screen.getByRole('textbox', { name: en('auditActivity.fields.actorUserId') });
    await userEvent.type(actor, 'someone');
    await userEvent.click(screen.getByRole('button', { name: en('common.filters.apply') }));
    expect(await screen.findByText(en('auditActivity.filters.malformed'))).toBeTruthy();
    expect(auditReads(api)).toHaveLength(1);

    await userEvent.clear(actor);
    await userEvent.type(actor, BUSINESS_EVENT_ID);
    await userEvent.click(screen.getByRole('button', { name: en('common.filters.apply') }));
    await waitFor(() => {
      expect(auditReads(api)).toHaveLength(2);
    });
    const query = auditReads(api)[1]?.query;
    expect(query?.get('category')).toBe('BUSINESS');
    expect(query?.get('outcome')).toBe('DENIED');
    expect(query?.get('actorUserId')).toBe(BUSINESS_EVENT_ID);
  });

  test('ADM-051 shows the immutable event: attributes, withheld request context, integrity, forwarding, correlation', async () => {
    open(`/admin/audit-trail/${BUSINESS_EVENT_ID}`, reviewer(), { access: AUDIT_REVIEWER_ACCESS });

    expect(await screen.findByText(en('auditActivity.detail.immutable'))).toBeTruthy();
    expect(screen.getByText('Risk.RiskClosed')).toBeTruthy();
    const attributes = screen.getByRole('table', { name: en('auditActivity.detail.attributes') });
    expect(within(attributes).getByText('CLOSED')).toBeTruthy();
    const masked = present(document.querySelector<HTMLElement>('[data-masked]'), 'masked');
    expect(within(masked).getByText('client_address')).toBeTruthy();
    expect(screen.getByText(en('auditActivity.forwarding.FORWARDED'))).toBeTruthy();
    expect(
      screen
        .getByRole('link', { name: en('auditActivity.detail.correlatedEvents') })
        .getAttribute('href'),
    ).toBe(`/admin/audit-trail?correlationId=${CORRELATION_ID}`);
    expect(
      screen
        .getByRole('link', { name: en('auditActivity.detail.openRecord') })
        .getAttribute('href'),
    ).toBe(`/projects/${PROJECT_ID}/risks/${RISK_ID}`);
    expect(screen.queryByRole('button', { name: /edit|delete|remove/i })).toBeNull();
    await expectAccessible();
  });

  test('ADM-051 for an event out of the reader’s categories says not found or not available', async () => {
    open(`/admin/audit-trail/${BUSINESS_EVENT_ID}`, r01(), { access: SYSTEM_ADMINISTRATOR_ACCESS });

    expect(await screen.findByText(en('auditActivity.problems.notFound'))).toBeTruthy();
  });
});

describe('ADM-053 Integration Audit Logs', () => {
  test('it reads integration audit only and says runtime logs are elsewhere', async () => {
    const api = open('/admin/integration-audit-logs', reviewer(), {
      access: INTEGRATION_REVIEWER_ACCESS,
    });

    const table = await screen.findByRole('table', { name: en('auditActivity.integration.title') });
    expect(table.querySelector(`[data-audit-event="${INTEGRATION_EVENT_ID}"]`)).not.toBeNull();
    expect(table.querySelector(`[data-audit-event="${ADMINISTRATION_EVENT_ID}"]`)).toBeNull();
    expect(screen.getByText(en('auditActivity.integration.boundary'))).toBeTruthy();
    expect(auditReads(api)[0]?.query.get('category')).toBe('INTEGRATION');
    await expectAccessible();
  });

  test('R01 as shipped does not hold integration audit', async () => {
    const api = open('/admin/integration-audit-logs', r01(), {
      access: SYSTEM_ADMINISTRATOR_ACCESS,
    });

    expect(await screen.findByText(en('auditActivity.denied.integration'))).toBeTruthy();
    expect(auditReads(api)).toEqual([]);
  });
});

describe('SCR-057 Activity', () => {
  test('lines read in the interface language, withhold an actor the API withheld, and link their record', async () => {
    open(workspace('activity-history'), reviewer(), { access: PROJECT_MANAGER_ACCESS }, 'ar');

    const timeline = await screen.findByRole('list', {
      name: translate('ar', 'auditActivity.activity.title'),
    });
    expect(within(timeline).getByText('تم إغلاق الخطر', { selector: SUMMARY })).toBeTruthy();
    expect(within(timeline).queryByText('Risk closed', { selector: SUMMARY })).toBeNull();
    expect(
      within(timeline).getByText(translate('ar', 'auditActivity.activity.actorNotShown')),
    ).toBeTruthy();
    const links = within(timeline).getAllByRole('link');
    expect(links.map((link) => link.getAttribute('href'))).toEqual([
      `/projects/${PROJECT_ID}/risks/${RISK_ID}`,
      `/projects/${PROJECT_ID}`,
    ]);
    await expectAccessible();
  });

  test('the domain filter asks the API for that domain', async () => {
    const api = open(workspace('activity-history'), reviewer(), { access: PROJECT_MANAGER_ACCESS });
    await screen.findByRole('list', { name: en('auditActivity.activity.title') });

    await userEvent.selectOptions(
      screen.getByRole('combobox', { name: en('auditActivity.activity.domain') }),
      'RISK',
    );
    await userEvent.click(screen.getByRole('button', { name: en('common.filters.apply') }));

    await waitFor(() => {
      expect(activityReads(api).at(-1)?.query.get('activityKind')).toBe('RISK');
    });
    await waitFor(() => {
      expect(screen.queryByText('Project activated', { selector: SUMMARY })).toBeNull();
    });
  });

  test('without ACTIVITY_VIEW the tab is not offered and its address reads nothing', async () => {
    const api = open(workspace('activity-history'), reviewer(), { access: NO_AUDIT_ACCESS });

    expect(await screen.findByText(en('projects.workspace.tabUnavailable'))).toBeTruthy();
    expect(activityReads(api)).toEqual([]);
  });
});

describe('the sidebar', () => {
  test('lists the audit screens the permissions open, and none for a project manager', async () => {
    open(workspace(), r01(), { access: SYSTEM_ADMINISTRATOR_ACCESS });
    const navigation = await screen.findByRole('navigation', {
      name: en('auditActivity.nav.label'),
    });
    expect(
      within(navigation)
        .getAllByRole('link')
        .map((link) => plain(link.textContent)),
    ).toEqual([en('auditActivity.nav.auditTrail'), en('auditActivity.nav.systemEvents')]);
    cleanup();

    const api = open(workspace(), reviewer(), { access: PROJECT_MANAGER_ACCESS });
    await screen.findByRole('heading', { level: 1, name: 'Coastal road upgrade' });
    await waitFor(() => {
      expect(api.requestsTo('GET', /^\/audit-access$/).length).toBeGreaterThan(0);
    });
    expect(screen.queryByRole('navigation', { name: en('auditActivity.nav.label') })).toBeNull();
  });
});
