import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, test } from 'vitest';

import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { WORKSPACE_TABS, type WorkspaceTabKey } from '@/features/projects/access.ts';
import { type ProjectStatus } from '@/features/projects/api/types.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import { withChangeRequests } from '@/test/changeRequestFixtures.ts';
import { concernProject, managerSession, withConcerns } from '@/test/concernFixtures.ts';
import { withFinancials, withKpis } from '@/test/financialKpiFixtures.ts';
import { withMilestones } from '@/test/milestoneFixtures.ts';
import { mockApi, page } from '@/test/mockApi.ts';
import { submitted, withProgress } from '@/test/progressFixtures.ts';
import {
  PROJECT_ID,
  reviewerSession,
  withProject,
  withProjectLookups,
} from '@/test/projectFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';
import { withRisks } from '@/test/riskFixtures.ts';
import { withSchedule } from '@/test/scheduleFixtures.ts';
import {
  closureCase,
  completionEffected,
  obligation,
  withSuspensionClosure,
} from '@/test/suspensionClosureFixtures.ts';
import { withTasks } from '@/test/taskFixtures.ts';

// Acceptance criterion 2 (TASK-064): a Closed Project's workspace renders in a clearly read-only mode with edit controls
// disabled or hidden. The workbook's check: open a Closed Project and confirm no edit control is present or enabled.
// Every tab is opened on a project whose modules hold records that offer writes while it is open — tasks, milestones,
// risks, issues, a change request, a submitted progress update, a submitted KPI value, a financial update, an
// obligation — signed in as the project's Project Manager (who raises and changes) and as the department's manager
// (who reviews, waives and decides). The same data on an ACTIVE project is the control: it must show write controls,
// or the sweep would prove nothing.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const CLOSED_AT = '2026-10-12T09:00:00Z';

function openWorkspace(path: string, session: Session, status: ProjectStatus): void {
  const closed = status === 'CLOSED';
  const api = mockApi();
  withProjectLookups(api);
  withProgress(api, { submissions: [submitted()] });
  withSchedule(api);
  withTasks(api);
  withMilestones(api);
  withFinancials(api);
  withKpis(api);
  withRisks(api);
  withConcerns(api);
  withChangeRequests(api);
  withSuspensionClosure(api, {
    completions: [completionEffected()],
    closures: closed ? [closureCase({ status: 'EFFECTED', effectedAt: CLOSED_AT })] : [],
    obligations: [obligation(closed ? { status: 'SATISFIED', satisfiedAt: CLOSED_AT } : {})],
  });
  api.on('GET', /^\/documents$/, { body: page([]) });
  // Registered last, so every module's read of the project answers this one.
  withProject(api, concernProject({ status, closedAt: closed ? CLOSED_AT : null }));
  renderApp({ path, session });
}

function tabPath(path: string): string {
  return `/projects/${PROJECT_ID}${path === '' ? '' : `/${path}`}`;
}

/** Waits until the tab has read what it shows: no loading state left in its panel. */
async function settledPanel(): Promise<HTMLElement> {
  const panel = await waitFor(() => {
    const found = document.querySelector<HTMLElement>('.tabs__panel');
    if (found === null) {
      throw new Error('No workspace panel yet');
    }
    return found;
  });
  await waitFor(() => {
    expect(panel.querySelector('.state--loading')).toBeNull();
    expect(panel.textContent).not.toBe('');
  });
  return panel;
}

/**
 * A record's title: in the first cell of its row, or a parent's or subtask's in a detail. It opens that record's
 * detail, which is swept in turn.
 */
const OPENERS = [
  'tbody tr > td:first-child > button.button--link',
  '.task-detail__list > li > button.button--link',
  '.details__row dd > button.button--link',
].join(', ');

function rowOpeners(container: HTMLElement): HTMLButtonElement[] {
  return [...container.querySelectorAll<HTMLButtonElement>(OPENERS)];
}

const DISMISS = new Set([
  en('common.actions.cancel'),
  en('tasks.actions.close'),
  en('milestones.actions.close'),
]);

/**
 * The controls that start a change: enabled buttons and button-styled links. A filter form's controls and a table's
 * paging only change what is shown; a record's title opens its detail; a dialog's close only closes it.
 */
function writeControls(container: HTMLElement): string[] {
  const openers = new Set<Element>(rowOpeners(container));
  return [
    ...within(container).queryAllByRole('button'),
    ...container.querySelectorAll<HTMLElement>('a.button'),
  ]
    .filter(
      (element) =>
        !openers.has(element) &&
        element.closest('.filters, .pagination, nav') === null &&
        !DISMISS.has(element.textContent.trim()) &&
        !(element instanceof HTMLButtonElement && element.disabled),
    )
    .map((element) => `${element.tagName.toLowerCase()} "${element.textContent.trim()}"`);
}

/** The panel's write controls and, when its rows open a detail, the first detail's. */
async function sweep(): Promise<string[]> {
  const panel = await settledPanel();
  const found = writeControls(panel);
  const opener = panel.querySelector<HTMLButtonElement>(
    'tbody tr > td:first-child > button.button--link',
  );
  if (opener !== null) {
    await userEvent.setup().click(opener);
    const dialog = await screen.findByRole('dialog');
    await waitFor(() => {
      expect(dialog.querySelector('.state--loading')).toBeNull();
    });
    found.push(...writeControls(dialog).map((control) => `detail: ${control}`));
  }
  return found;
}

/** Who is swept, and the tabs where the same data offers them writes while the project is ACTIVE. */
const SESSIONS: [string, () => Session, WorkspaceTabKey[]][] = [
  [
    "the project's Project Manager",
    managerSession,
    [
      'progress',
      'schedule',
      'tasks',
      'milestones',
      'financials',
      'kpis',
      'risks',
      'issuesChallenges',
      'changeRequests',
      'closeout',
      'documents',
    ],
  ],
  ["the department's manager", reviewerSession, ['progress', 'kpis', 'documents']],
];

afterEach(() => {
  cleanup();
});

describe('a CLOSED project is read-only in every tab (acceptance criterion 2)', () => {
  test('the workspace says so on every tab, with the date it closed and the way to its closeout', async () => {
    openWorkspace(tabPath('tasks'), managerSession(), 'CLOSED');

    const banner = await screen.findByRole('note');
    expect(banner.getAttribute('data-read-only')).toBe('true');
    expect(within(banner).getByText(en('suspensionClosure.banner.CLOSED.title'))).toBeTruthy();
    expect(
      within(banner)
        .getByRole('link', { name: en('suspensionClosure.banner.CLOSED.link') })
        .getAttribute('href'),
    ).toBe(`/projects/${PROJECT_ID}/closeout`);
    expect(document.querySelector('.workspace--read-only')).not.toBeNull();
    await settledPanel();
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });

  for (const [who, session, writableTabs] of SESSIONS) {
    test.each(WORKSPACE_TABS.map((tab) => [tab.key, tab.path] as const))(
      `${who}: the %s tab offers no write control`,
      async (_key, path) => {
        openWorkspace(tabPath(path), session(), 'CLOSED');

        expect(await sweep()).toEqual([]);
        expect(document.querySelector('.tabs__panel .state--error')).toBeNull();
        // The header offers no lifecycle or registration command either.
        const header = document.querySelector<HTMLElement>('.page-header');
        expect(header === null ? [] : writeControls(header)).toEqual([]);
      },
    );

    test.each(writableTabs)(
      `control: on the same data while ACTIVE, ${who} is offered writes in the %s tab`,
      async (key) => {
        const tab = WORKSPACE_TABS.find((candidate) => candidate.key === key);
        openWorkspace(tabPath(tab?.path ?? ''), session(), 'ACTIVE');
        expect((await sweep()).length).toBeGreaterThan(0);
      },
    );
  }

  test('the closeout tab shows both stages done and nothing to raise; the obligation history stays readable', async () => {
    openWorkspace(tabPath('closeout'), managerSession(), 'CLOSED');

    await screen.findByRole('heading', { name: en('suspensionClosure.stages.title') });
    const stages = [...document.querySelectorAll<HTMLElement>('.closeout-stage')];
    expect(stages.map((stage) => stage.getAttribute('data-stage'))).toEqual([
      'completion',
      'closure',
    ]);
    for (const stage of stages) {
      expect(within(stage).getByText(en('suspensionClosure.stages.state.DONE'))).toBeTruthy();
    }
    expect(
      screen.queryByRole('button', { name: en('suspensionClosure.obligations.add') }),
    ).toBeNull();
    expect(screen.getByText('Defect liability period')).toBeTruthy();
  });
});
