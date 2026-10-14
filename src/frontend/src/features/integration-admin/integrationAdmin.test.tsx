import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, type MockInstance, test, vi } from 'vitest';

import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import {
  ACKNOWLEDGED_ALERT_ID,
  connectionTest,
  CORRELATION_ID,
  DEAD_LETTER_ID,
  DIRECTORY_ID,
  FIRST_ATTEMPT_ID,
  internalSystem,
  type IntegrationState,
  OPEN_ALERT_ID,
  PLANTED_SECRETS,
  RETRY_ATTEMPT_ID,
  seededCatalogue,
  SIGN_IN_CALL_ID,
  SMS_ID,
  SSO_ID,
  withIntegrations,
} from '@/test/integrationAdminFixtures.ts';
import { type MockApi, mockApi, problem } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// TASK-076, the FG-05 Integration Administration UI over TASK-075's API. Acceptance criteria: (1) the Test Integration
// Connection action never displays or logs the underlying secret value in the UI or the browser console; (2) ADM-047
// shows Availability and Freshness as two separate indicators per Blueprint §17.1, not one combined health icon. The
// workbook's checks: trigger the test and inspect network and console output for a secret; see ADM-047 render the two
// indicators apart for a sample integration.

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

function open(path: string, state: IntegrationState = {}, session: Session = r01()): MockApi {
  const api = withIntegrations(mockApi(), state);
  renderApp({ path, session });
  return api;
}

/** Everything the console was given, flattened to text. */
type ConsoleSpy = MockInstance<(...data: unknown[]) => void>;

function consoleText(spies: ConsoleSpy[]): string {
  return spies
    .flatMap((spy) => spy.mock.calls.flat())
    .map((value) => (typeof value === 'string' ? value : JSON.stringify(value)))
    .join('\n');
}

function spyOnConsole(): ConsoleSpy[] {
  return (['log', 'info', 'debug', 'warn', 'error'] as const).map((method) =>
    vi.spyOn(console, method).mockImplementation(() => undefined),
  );
}

function expectNoPlantedSecret(text: string) {
  for (const secret of PLANTED_SECRETS) {
    expect(text).not.toContain(secret);
  }
}

async function expectAccessible() {
  const violations = await accessibilityViolations();
  expect(violations, describeViolations(violations)).toEqual([]);
}

const fg05Requests = (api: MockApi) =>
  api.requests.filter((request) =>
    /^\/(integrations|integration-|sync-runs|operational-alerts|identity-integration)/.test(
      request.path,
    ),
  );

afterEach(() => {
  cleanup();
});

describe('acceptance criterion 1: the connection test never shows or logs a secret', () => {
  test('MOD-100 sends no value, shows each outcome and code, and nothing of the secrets the response carries', async () => {
    const consoleSpies = spyOnConsole();
    const api = open('/admin/sso-directory-integration');
    const user = userEvent.setup();

    await user.click(
      await screen.findByRole('button', { name: en('integrationAdmin.actions.testConnection') }),
    );
    const dialog = await screen.findByRole('dialog', {
      name: en('integrationAdmin.connectionTest.title'),
    });
    await user.click(
      within(dialog).getByRole('button', { name: en('integrationAdmin.connectionTest.run') }),
    );

    const results = await within(dialog).findByRole('list', {
      name: en('integrationAdmin.connectionTest.results'),
    });
    const target = (name: string) =>
      present(results.querySelector(`[data-connection-target="${name}"]`), name);
    expect(target('directory').textContent).toContain(
      en('integrationAdmin.connectionTest.outcome.FAILED'),
    );
    expect(target('directory').textContent).toContain(
      en('integrationAdmin.connectionTest.failures.BIND_REJECTED'),
    );
    expect(target('directory').querySelector('code')?.textContent).toBe('BIND_REJECTED');
    expect(target('singleSignOn').textContent).toContain(
      en('integrationAdmin.connectionTest.outcome.SUCCEEDED'),
    );
    expect(target('identityVerification').textContent).toContain(
      en('integrationAdmin.connectionTest.outcome.NOT_CONFIGURED'),
    );

    // Network: one test call, carrying no body and so no value of the browser's.
    const [call, ...others] = api.requestsTo('POST', /^\/identity-integration\/test$/);
    expect(others).toEqual([]);
    expect(present(call, 'test call').body).toBeUndefined();
    // UI and console: none of the planted secrets — the provider message, the bind password, the client secret.
    expectNoPlantedSecret(document.body.textContent);
    expectNoPlantedSecret(document.body.innerHTML);
    expectNoPlantedSecret(consoleText(consoleSpies));
    await expectAccessible();
  });

  test('a failure "code" that is a provider sentence is not shown, only that the connection failed', async () => {
    const leaked = `token rejected: ${PLANTED_SECRETS[2]}`;
    const consoleSpies = spyOnConsole();
    open('/admin/sso-directory-integration', {
      test: connectionTest({ singleSignOnFailure: leaked }),
    });
    const user = userEvent.setup();

    await user.click(
      await screen.findByRole('button', { name: en('integrationAdmin.actions.testConnection') }),
    );
    await user.click(
      await screen.findByRole('button', { name: en('integrationAdmin.connectionTest.run') }),
    );

    const sso = await waitFor(() =>
      present(document.querySelector('[data-connection-target="singleSignOn"]'), 'SSO result'),
    );
    expect(sso.textContent).toContain(en('integrationAdmin.connectionTest.failures.other'));
    expect(sso.querySelector('code')).toBeNull();
    expect(document.body.textContent).not.toContain('token rejected');
    expectNoPlantedSecret(document.body.innerHTML);
    expectNoPlantedSecret(consoleText(consoleSpies));
  });

  test('a refused test is told by its code alone; the refusal’s own text is never shown or logged', async () => {
    const consoleSpies = spyOnConsole();
    const api = open('/admin/sso-directory-integration');
    const refusal = problem(500, 'INTERNAL_ERROR');
    api.on('POST', /^\/identity-integration\/test$/, {
      ...refusal,
      body: {
        ...(refusal.body as object),
        title: `bind failed for password ${PLANTED_SECRETS[0]}`,
        detail: PLANTED_SECRETS[1],
      },
    });
    const user = userEvent.setup();

    await user.click(
      await screen.findByRole('button', { name: en('integrationAdmin.actions.testConnection') }),
    );
    await user.click(
      await screen.findByRole('button', { name: en('integrationAdmin.connectionTest.run') }),
    );

    const alert = await screen.findByText(
      plain(en('common.problems.unexpectedWithReference', { reference: 'test-correlation-id' })),
      { normalizer: (text) => plain(text) },
    );
    expect(alert).toBeTruthy();
    expectNoPlantedSecret(document.body.innerHTML);
    expectNoPlantedSecret(consoleText(consoleSpies));
  });

  test('ADM-041 and ADM-043 show each secret only as set or not set, and no other field of the status', async () => {
    open('/admin/sso-directory-integration');

    const directory = await waitFor(() =>
      present(
        document.querySelector<HTMLElement>('[data-configuration="directory"]'),
        'directory settings',
      ),
    );
    const row = (term: string) =>
      present(within(directory).getByText(term, { selector: 'dt' }).closest('.details__row'), term);
    expect(row(en('integrationAdmin.configuration.bindPassword')).textContent).toContain(
      en('integrationAdmin.configuration.set'),
    );
    const sso = present(
      document.querySelector<HTMLElement>('[data-configuration="single-sign-on"]'),
      'SSO settings',
    );
    expect(
      within(sso)
        .getByText(en('integrationAdmin.configuration.clientSecret'), { selector: 'dt' })
        .closest('.details__row')?.textContent,
    ).toContain(en('integrationAdmin.configuration.notSet'));
    expectNoPlantedSecret(document.body.innerHTML);

    cleanup();
    open('/admin/nafath-integration');
    await screen.findByRole('heading', {
      level: 2,
      name: en('integrationAdmin.configuration.nafath'),
    });
    expect(document.querySelector('[data-nafath-disabled="true"]')?.textContent).toBe(
      en('integrationAdmin.nafath.switchedOff'),
    );
    expectNoPlantedSecret(document.body.innerHTML);
  });
});

describe('acceptance criterion 2: ADM-047 shows Availability and Freshness as two indicators', () => {
  test('each integration has an availability and a freshness indicator, apart, named and in their own columns', async () => {
    open('/admin/integration-monitoring');

    const table = await screen.findByRole('table', {
      name: en('integrationAdmin.monitoring.health'),
    });
    const headers = within(table)
      .getAllByRole('columnheader')
      .map((header) => header.textContent);
    expect(headers).toContain(en('integrationAdmin.indicators.availability'));
    expect(headers).toContain(en('integrationAdmin.indicators.freshness'));

    // The sample integration: the directory answers, but its data is stale — the two indicators disagree.
    const directory = present(table.querySelector('tr[data-integration="DIRECTORY"]'), 'row');
    const availability = present(
      directory.querySelector('[data-indicator="availability"]'),
      'availability',
    );
    const freshness = present(directory.querySelector('[data-indicator="freshness"]'), 'freshness');
    expect(availability).not.toBe(freshness);
    expect(availability.closest('td')).not.toBe(freshness.closest('td'));
    expect(availability.getAttribute('data-state')).toBe('UP');
    expect(freshness.getAttribute('data-state')).toBe('STALE');
    expect(availability.textContent).toBe(
      `${en('integrationAdmin.indicators.availability')}${en('integrationAdmin.availability.UP')}`,
    );
    expect(freshness.textContent).toBe(
      `${en('integrationAdmin.indicators.freshness')}${en('integrationAdmin.freshness.STALE')}`,
    );
    // Different shapes, not one icon in two colours.
    expect(availability.querySelector('svg')?.innerHTML).not.toBe(
      freshness.querySelector('svg')?.innerHTML,
    );
    expect(availability.className).toContain('indicator--availability');
    expect(freshness.className).toContain('indicator--freshness');

    // Every row has exactly one of each; nothing combines them.
    for (const row of table.querySelectorAll('tbody tr')) {
      expect(row.querySelectorAll('[data-indicator="availability"]')).toHaveLength(1);
      expect(row.querySelectorAll('[data-indicator="freshness"]')).toHaveLength(1);
    }
    expect(document.querySelector('[data-indicator="health"]')).toBeNull();
    await expectAccessible();
  });

  test('the legend states what each means, and the summary counts each dimension on its own', async () => {
    open('/admin/integration-monitoring');

    await screen.findByRole('table', { name: en('integrationAdmin.monitoring.health') });
    const legendList = present(document.querySelector('.indicator-legend'), 'legend');
    expect(legendList.textContent).toContain(en('integrationAdmin.indicators.availabilityMeaning'));
    expect(legendList.textContent).toContain(en('integrationAdmin.indicators.freshnessMeaning'));
    expect(document.querySelector('[data-summary="availability"] dd')?.textContent).toBe('1');
    expect(document.querySelector('[data-summary="freshness"] dd')?.textContent).toBe('1');
  });
});

const SCREENS = [
  { id: 'ADM-040', path: '/admin/integrations-overview', title: 'integrationAdmin.overview.title' },
  {
    id: 'ADM-041',
    path: '/admin/sso-directory-integration',
    title: 'integrationAdmin.ssoDirectory.title',
  },
  {
    id: 'ADM-042',
    path: '/admin/email-exchange-integration',
    title: 'integrationAdmin.emailExchange.title',
  },
  { id: 'ADM-043', path: '/admin/nafath-integration', title: 'integrationAdmin.nafath.title' },
  {
    id: 'ADM-044',
    path: '/admin/internal-system-integrations',
    title: 'integrationAdmin.internalSystems.title',
  },
  {
    id: 'ADM-045',
    path: '/admin/api-integration-catalogue',
    title: 'integrationAdmin.apiCatalogue.title',
  },
  {
    id: 'ADM-046',
    path: `/admin/integration-detail-configuration/${DIRECTORY_ID}`,
    title: null,
  },
  {
    id: 'ADM-047',
    path: '/admin/integration-monitoring',
    title: 'integrationAdmin.monitoring.title',
  },
  { id: 'ADM-048', path: '/admin/integration-logs', title: 'integrationAdmin.logs.title' },
  {
    id: 'ADM-048 attempt',
    path: `/admin/integration-logs/${DEAD_LETTER_ID}`,
    title: 'integrationAdmin.invocation.title',
  },
] as const;

describe.each(['en', 'ar'] as const)('in %s', (language) => {
  test.each(SCREENS)(
    '$id renders for R01 with the environment shown and no axe violation',
    async ({ path, title }) => {
      withIntegrations(mockApi());
      renderApp({ path, language, session: r01() });

      const heading = await screen.findByRole('heading', { level: 1 });
      if (title !== null) {
        expect(heading.textContent).toBe(translate(language, title));
      }
      await waitFor(() => {
        expect(document.querySelector('[data-environment="Development"]')).not.toBeNull();
      });
      await waitFor(() => {
        expect(document.querySelector('.state--loading')).toBeNull();
      });
      await expectAccessible();
    },
  );
});

describe('access', () => {
  test('a role without FG-05 is denied every screen, and nothing of FG-05 is read', async () => {
    for (const { path } of SCREENS) {
      const api = open(path, {}, sessionFor(['R02']));
      await screen.findByRole('heading', { level: 1, name: en('common.forbidden.title') });
      expect(fg05Requests(api)).toEqual([]);
      cleanup();
    }
  });

  test('the sidebar offers the integrations group to R01 only', async () => {
    open('/admin/integrations-overview');
    const nav = await screen.findByRole('navigation', { name: en('integrationAdmin.nav.label') });
    expect(within(nav).getAllByRole('link')).toHaveLength(8);
    cleanup();

    open('/', {}, sessionFor(['R02']));
    await screen.findByRole('navigation', { name: en('projects.nav.label') });
    expect(screen.queryByRole('navigation', { name: en('integrationAdmin.nav.label') })).toBeNull();
  });

  test('an R01 whose integration grant was withdrawn is told so, not shown an empty catalogue', async () => {
    withIntegrations(mockApi()).on('GET', /^\/integrations$/, problem(403, 'PERMISSION_DENIED'));
    renderApp({ path: '/admin/integrations-overview', session: r01() });

    await screen.findByText(en('integrationAdmin.problems.permissionDenied'));
    expect(screen.queryByText(en('integrationAdmin.overview.empty'))).toBeNull();
  });
});

describe('MOD-101 Enable / Disable Integration', () => {
  test('disabling says the integration keeps working, sends the ETag, and the detail is read again', async () => {
    const api = open(`/admin/integration-detail-configuration/${DIRECTORY_ID}`);
    const user = userEvent.setup();

    await user.click(
      await screen.findByRole('button', { name: en('integrationAdmin.actions.disable') }),
    );
    const dialog = await screen.findByRole('dialog');
    expect(dialog.textContent).toContain(en('integrationAdmin.activation.disableBody'));
    await user.click(
      within(dialog).getByRole('button', { name: en('integrationAdmin.actions.disable') }),
    );

    await screen.findByText(en('integrationAdmin.activation.disabled'));
    const [command] = api.requestsTo('POST', /\/disable$/);
    expect(command?.path).toBe(`/integrations/${DIRECTORY_ID}/disable`);
    expect(command?.headers.get('If-Match')).toBe('"4"');
    expect(command?.headers.get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/);
    await screen.findByRole('button', { name: en('integrationAdmin.actions.enable') });
    expect(
      screen.getAllByText(en('integrationAdmin.activationStatus.DISABLED')).length,
    ).toBeGreaterThan(0);
  });

  test('a disabled integration is offered enabling; a change made meanwhile is refused, not overwritten', async () => {
    const api = open(`/admin/integration-detail-configuration/${SMS_ID}`);
    api.on('POST', /\/enable$/, problem(412, 'PRECONDITION_FAILED'));
    const user = userEvent.setup();

    await user.click(
      await screen.findByRole('button', { name: en('integrationAdmin.actions.enable') }),
    );
    const dialog = await screen.findByRole('dialog');
    await user.click(
      within(dialog).getByRole('button', { name: en('integrationAdmin.actions.enable') }),
    );

    expect(await within(dialog).findByText(en('common.problems.preconditionFailed'))).toBeTruthy();
  });

  test('a retired integration is offered no activation command', async () => {
    const items = seededCatalogue().map((item) =>
      item.id === SSO_ID ? { ...item, status: 'RETIRED' as const } : item,
    );
    open(`/admin/integration-detail-configuration/${SSO_ID}`, { items });

    await screen.findByRole('heading', { level: 1, name: 'Single sign-on (OpenID Connect)' });
    expect(
      screen.queryByRole('button', { name: en('integrationAdmin.actions.disable') }),
    ).toBeNull();
    expect(
      screen.queryByRole('button', { name: en('integrationAdmin.actions.enable') }),
    ).toBeNull();
    // MOD-100 is still offered: the connection itself can be tested.
    expect(
      screen.getByRole('button', { name: en('integrationAdmin.actions.testConnection') }),
    ).toBeTruthy();
  });
});

describe('ADM-046 Integration Detail', () => {
  test('a reconciliation is offered where FG-05 runs one, says it repairs nothing, and opens its attempt', async () => {
    const api = open(`/admin/integration-detail-configuration/${DIRECTORY_ID}`);
    const user = userEvent.setup();

    await user.click(
      await screen.findByRole('button', { name: en('integrationAdmin.actions.reconcile') }),
    );
    const dialog = await screen.findByRole('dialog');
    expect(dialog.textContent).toContain(en('integrationAdmin.reconcile.body'));
    await user.click(
      within(dialog).getByRole('button', { name: en('integrationAdmin.actions.reconcile') }),
    );

    await screen.findByRole('heading', { level: 1, name: en('integrationAdmin.invocation.title') });
    expect(api.requestsTo('POST', /^\/integration-reconciliations$/)[0]?.body).toEqual({
      integrationId: DIRECTORY_ID,
    });
  });

  test('an integration FG-05 runs nothing for offers no reconciliation and no connection test it lacks', async () => {
    open(`/admin/integration-detail-configuration/${SMS_ID}`);

    await screen.findByRole('heading', { level: 1, name: 'SMS provider' });
    expect(
      screen.queryByRole('button', { name: en('integrationAdmin.actions.reconcile') }),
    ).toBeNull();
    expect(
      screen.queryByRole('button', { name: en('integrationAdmin.actions.testConnection') }),
    ).toBeNull();
    expect(screen.getByText(en('integrationAdmin.detail.noOperations'))).toBeTruthy();
  });

  test('a longer measures window is sent as its bounds; the default sends none', async () => {
    const api = open(`/admin/integration-detail-configuration/${DIRECTORY_ID}`);
    const user = userEvent.setup();

    const window = await screen.findByRole('combobox', {
      name: en('integrationAdmin.measures.windowChoice'),
    });
    const [first] = api.requestsTo('GET', /^\/integrations\/[^/]+$/);
    expect(first?.query.get('from')).toBeNull();
    await user.selectOptions(window, '7');

    await waitFor(() => {
      expect(
        api
          .requestsTo('GET', /^\/integrations\/[^/]+$/)
          .at(-1)
          ?.query.get('from'),
      ).not.toBeNull();
    });
    const last = present(api.requestsTo('GET', /^\/integrations\/[^/]+$/).at(-1), 'read');
    const span =
      new Date(present(last.query.get('to'), 'to')).getTime() -
      new Date(present(last.query.get('from'), 'from')).getTime();
    expect(span).toBe(7 * 24 * 60 * 60 * 1000);
  });
});

describe('ADM-042 and ADM-044', () => {
  test('a measure over no sample reads "No sample", never 0', async () => {
    open('/admin/email-exchange-integration');

    const sms = await screen.findByRole('heading', {
      level: 2,
      name: en('integrationAdmin.measures.titleFor', { name: 'SMS provider' }),
    });
    const section = present(sms.closest('section'), 'SMS measures');
    await waitFor(() => {
      expect(section.querySelector('[data-value-state="no-sample"]')).not.toBeNull();
    });
    const rate = within(section)
      .getByText(en('integrationAdmin.measures.deliveryRate'), { selector: 'dt' })
      .closest('.details__row');
    expect(rate?.textContent).toContain(en('integrationAdmin.values.noSample'));
    expect(rate?.textContent).not.toContain('0%');
  });

  test('ADM-044 lists an internal system registered later, and says so when there is none', async () => {
    open('/admin/internal-system-integrations');
    await screen.findByText(en('integrationAdmin.internalSystems.empty'));
    cleanup();

    open('/admin/internal-system-integrations', {
      items: [...seededCatalogue(), internalSystem()],
    });
    const table = await screen.findByRole('table');
    expect(
      [...table.querySelectorAll('tbody tr')].map((row) => row.getAttribute('data-integration')),
    ).toEqual(['FINANCE_LEDGER']);
  });
});

describe('ADM-047 alerts', () => {
  test('an open alert is acknowledged after confirming, and the list is read again', async () => {
    const api = open('/admin/integration-monitoring');
    const user = userEvent.setup();

    const row = await waitFor(() =>
      present(document.querySelector(`tr[data-alert="${OPEN_ALERT_ID}"]`), 'open alert'),
    );
    await user.click(
      within(row as HTMLElement).getByRole('button', {
        name: `${en('integrationAdmin.actions.acknowledge')} ${en('integrationAdmin.alertType.DEAD_LETTER')}`,
      }),
    );
    const dialog = await screen.findByRole('dialog');
    expect(dialog.textContent).toContain(en('integrationAdmin.alerts.acknowledgeBody'));
    await user.click(
      within(dialog).getByRole('button', { name: en('integrationAdmin.actions.acknowledge') }),
    );

    await screen.findByText(en('integrationAdmin.alerts.acknowledged'));
    expect(api.requestsTo('POST', /\/acknowledge$/)[0]?.path).toBe(
      `/operational-alerts/${OPEN_ALERT_ID}/acknowledge`,
    );
    // The first read asked for the live alerts, comma-joined as the API parses a set.
    expect(api.requestsTo('GET', /^\/operational-alerts$/)[0]?.query.get('status')).toBe(
      'OPEN,ACKNOWLEDGED',
    );
  });

  test('resolving needs a note: none is refused in the form, one is sent with its language', async () => {
    const api = open('/admin/integration-monitoring');
    const user = userEvent.setup();

    const row = await waitFor(() =>
      present(document.querySelector(`tr[data-alert="${ACKNOWLEDGED_ALERT_ID}"]`), 'alert'),
    );
    expect(
      within(row as HTMLElement).queryByRole('button', {
        name: new RegExp(en('integrationAdmin.actions.acknowledge')),
      }),
    ).toBeNull();
    await user.click(
      within(row as HTMLElement).getByRole('button', {
        name: `${en('integrationAdmin.actions.resolve')} ${en('integrationAdmin.alertType.RECONCILIATION_MISMATCH')}`,
      }),
    );
    const dialog = await screen.findByRole('dialog');
    await user.click(
      within(dialog).getByRole('button', { name: en('integrationAdmin.actions.resolve') }),
    );
    expect(await within(dialog).findByText(en('common.fieldErrors.required'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/resolve$/)).toEqual([]);

    await user.type(
      within(dialog).getByRole('textbox', { name: new RegExp(en('integrationAdmin.fields.note')) }),
      'Directory corrected by IT',
    );
    await user.click(
      within(dialog).getByRole('button', { name: en('integrationAdmin.actions.resolve') }),
    );

    await screen.findByText(en('integrationAdmin.alerts.resolved'));
    expect(api.requestsTo('POST', /\/resolve$/)[0]?.body).toEqual({
      note: { text: 'Directory corrected by IT', language: 'EN' },
    });
  });
});

describe('ADM-048 Integration Logs', () => {
  test('filters are sent as the API reads them, and a malformed id is refused before any request', async () => {
    const api = open('/admin/integration-logs');
    const user = userEvent.setup();

    await screen.findByRole('table');
    const reads = () => api.requestsTo('GET', /^\/integration-invocations$/);
    const before = reads().length;
    const correlation = screen.getByRole('textbox', {
      name: en('integrationAdmin.fields.correlationId'),
    });
    await user.type(correlation, 'not-a-uuid');
    await user.click(screen.getByRole('button', { name: en('common.filters.apply') }));
    expect(await screen.findByText(en('integrationAdmin.filters.malformed'))).toBeTruthy();
    expect(reads()).toHaveLength(before);

    await user.clear(correlation);
    await user.type(correlation, CORRELATION_ID);
    await user.selectOptions(
      screen.getByRole('combobox', { name: en('integrationAdmin.fields.invocationStatus') }),
      'DEAD_LETTER',
    );
    await user.click(screen.getByRole('button', { name: en('common.filters.apply') }));

    await waitFor(() => {
      expect(reads().at(-1)?.query.get('correlationId')).toBe(CORRELATION_ID);
    });
    expect(reads().at(-1)?.query.get('status')).toBe('DEAD_LETTER');
    const rows = (await screen.findByRole('table')).querySelectorAll('tbody tr');
    expect([...rows].map((row) => row.getAttribute('data-invocation'))).toEqual([DEAD_LETTER_ID]);
  });

  test('a dead letter shows its lineage and is retried as a new attempt under the same correlation id', async () => {
    const api = open(`/admin/integration-logs/${DEAD_LETTER_ID}`);
    const user = userEvent.setup();

    const lineage = await screen.findByRole('table', {
      name: en('integrationAdmin.invocation.lineage'),
    });
    expect(
      [...lineage.querySelectorAll('tbody tr')].map((row) => row.getAttribute('data-invocation')),
    ).toEqual([FIRST_ATTEMPT_ID, '7d000000-0000-4000-8000-000000000002', DEAD_LETTER_ID]);
    expect(lineage.querySelector('tr[aria-current="true"]')?.getAttribute('data-invocation')).toBe(
      DEAD_LETTER_ID,
    );

    await user.click(screen.getByRole('button', { name: en('integrationAdmin.actions.retry') }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog.textContent).toContain(en('integrationAdmin.retry.body'));
    await user.click(
      within(dialog).getByRole('button', { name: en('integrationAdmin.actions.retry') }),
    );

    await waitFor(() => {
      expect(
        document.querySelector(`article[data-invocation="${RETRY_ATTEMPT_ID}"]`),
      ).not.toBeNull();
    });
    expect(api.requestsTo('POST', /\/retry$/)[0]?.path).toBe(
      `/integration-invocations/${DEAD_LETTER_ID}/retry`,
    );
    expect(document.body.textContent).toContain(CORRELATION_ID);
  });

  test('an attempt another module made offers no retry, and says why', async () => {
    open(`/admin/integration-logs/${SIGN_IN_CALL_ID}`);

    await screen.findByText(en('integrationAdmin.invocation.notRetryable'));
    expect(screen.queryByRole('button', { name: en('integrationAdmin.actions.retry') })).toBeNull();
    // The redacted response code is shown as the API stored it.
    expect(screen.getAllByText('[REDACTED]').length).toBeGreaterThan(0);
  });
});
