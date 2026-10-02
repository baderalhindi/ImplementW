import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import { type MockApi, mockApi, problem } from '@/test/mockApi.ts';
import {
  DEPARTMENT_ID,
  entitySession,
  ENTITY_USER_ID,
  projectSummary,
  REVIEWER_ID,
  reviewerSession,
  withProjectLookups,
} from '@/test/projectFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// SCR-025 Project Register and SCR-026 My Projects.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const PROJECTS: ProjectSummary[] = [
  projectSummary({
    id: '4d4d4d4d-0000-4000-8000-000000000001',
    title: { text: 'Coastal road', language: 'EN' },
  }),
  projectSummary({
    id: '4d4d4d4d-0000-4000-8000-000000000002',
    title: { text: 'City park', language: 'EN' },
    status: 'ACTIVE',
    formalProjectId: 'PRJ-000042',
    projectManagerUserId: REVIEWER_ID,
  }),
  projectSummary({
    id: '4d4d4d4d-0000-4000-8000-000000000003',
    title: { text: 'Old museum', language: 'EN' },
    status: 'ACTIVE',
    legacyIntakeDate: '2025-01-15',
  }),
];

/** The register API over a set of projects, answering the page and filters it is asked for. */
function openRegister(
  path: string,
  session: Session,
  projects: ProjectSummary[] = PROJECTS,
  totalCount?: number,
): MockApi {
  const api = withProjectLookups(mockApi()).on('GET', /^\/projects$/, (request) => {
    const status = request.query.get('status');
    const q = request.query.get('q');
    const items = projects.filter(
      (p) => (status === null || p.status === status) && (q === null || p.title.text.includes(q)),
    );
    return {
      body: {
        items,
        page: Number(request.query.get('page') ?? '1'),
        pageSize: 25,
        totalCount: totalCount ?? items.length,
      },
    };
  });
  renderApp({ path, session });
  return api;
}

describe('SCR-025 Project Register', () => {
  test('shows the projects the API returns, most recently changed first, with state, identifier and manager', async () => {
    const api = openRegister('/projects', reviewerSession());

    const table = await screen.findByRole('table', { name: en('projects.register.title') });
    expect(within(table).getAllByRole('row')).toHaveLength(PROJECTS.length + 1);
    expect(screen.getByText(en('projects.list.order'))).toBeTruthy();
    expect(api.requestsTo('GET', /^\/projects$/)[0]?.query.toString()).toBe('page=1&pageSize=25');

    const park = within(table).getByRole('link', { name: 'City park' }).closest('tr');
    expect(park).not.toBeNull();
    expect(within(park as HTMLElement).getByText('PRJ-000042')).toBeTruthy();
    expect(within(park as HTMLElement).getByText(en('projects.status.ACTIVE'))).toBeTruthy();
    // The signed-in reviewer manages it.
    expect(within(park as HTMLElement).getByText(en('common.people.you'))).toBeTruthy();

    const road = within(table).getByRole('link', { name: 'Coastal road' }).closest('tr');
    expect(
      within(road as HTMLElement).getByText(en('projects.formalProjectId.notIssued')),
    ).toBeTruthy();
    expect(
      within(road as HTMLElement).getByText(en('projects.projectManager.notNamed')),
    ).toBeTruthy();
    expect(within(road as HTMLElement).getByText('Roads Department')).toBeTruthy();

    // ADR-014: a legacy intake project is told apart wherever it is listed.
    const museum = within(table).getByRole('link', { name: 'Old museum' }).closest('tr');
    expect(within(museum as HTMLElement).getByText(en('projects.legacyIntake'))).toBeTruthy();
  });

  test('filters by status, department and text in the request and the URL, and returns to page 1', async () => {
    const user = userEvent.setup();
    const api = openRegister('/projects?page=2', reviewerSession(), PROJECTS, 60);
    await screen.findByRole('table');

    await user.selectOptions(screen.getByLabelText(en('projects.fields.status')), 'ACTIVE');
    await screen.findByRole('link', { name: 'City park' });
    expect(api.requests.at(-1)?.query.get('status')).toBe('ACTIVE');
    expect(api.requests.at(-1)?.query.get('page')).toBe('1');

    await user.selectOptions(
      screen.getByLabelText(en('projects.fields.department')),
      DEPARTMENT_ID,
    );
    await user.type(screen.getByLabelText(en('projects.filters.q')), 'park');
    await user.click(screen.getByRole('button', { name: en('common.actions.search') }));

    await screen.findByRole('link', { name: 'City park' });
    const last = api.requestsTo('GET', /^\/projects$/).at(-1);
    expect(last?.query.get('status')).toBe('ACTIVE');
    expect(last?.query.get('departmentId')).toBe(DEPARTMENT_ID);
    expect(last?.query.get('q')).toBe('park');
    expect(screen.queryByRole('link', { name: 'Coastal road' })).toBeNull();
  });

  test('pages through the API’s pages', async () => {
    const user = userEvent.setup();
    const api = openRegister('/projects', reviewerSession(), PROJECTS, 60);
    await screen.findByRole('table');

    await user.click(screen.getByRole('button', { name: en('common.pagination.next') }));

    await screen.findByText(
      en('common.pagination.status', { page: 2, pageCount: 3, totalCount: 60 }),
    );
    expect(
      api
        .requestsTo('GET', /^\/projects$/)
        .at(-1)
        ?.query.get('page'),
    ).toBe('2');
  });

  test('a role that reaches no project gets its own empty state, not the filtered one', async () => {
    openRegister('/projects', reviewerSession(), []);

    expect(await screen.findByText(en('projects.register.emptyForRole'))).toBeTruthy();
    expect(screen.getByText(en('projects.register.emptyForRoleDescription'))).toBeTruthy();
    expect(screen.queryByText(en('projects.list.emptyFiltered'))).toBeNull();
    expect(screen.queryByRole('table')).toBeNull();
  });

  test('a filter that matches nothing says so and offers to clear it', async () => {
    const user = userEvent.setup();
    openRegister('/projects?status=CLOSED', reviewerSession());

    expect(await screen.findByText(en('projects.list.emptyFiltered'))).toBeTruthy();
    expect(screen.queryByText(en('projects.register.emptyForRole'))).toBeNull();

    const empty = screen.getByText(en('projects.list.emptyFiltered')).parentElement;
    await user.click(
      within(empty ?? document.body).getByRole('link', { name: en('common.filters.clear') }),
    );
    expect(await screen.findByRole('table')).toBeTruthy();
  });

  test('an external user is offered no entity filter: they see their own entity only (ADR-013)', async () => {
    openRegister('/projects', entitySession());
    await screen.findByRole('table');

    expect(screen.queryByLabelText(en('projects.fields.externalEntity'))).toBeNull();
    expect(screen.getByLabelText(en('projects.fields.department'))).toBeTruthy();
  });

  test('a role without PROJECT_VIEW gets the same empty state as a scope that reaches nothing', async () => {
    withProjectLookups(mockApi()).on('GET', /^\/projects$/, problem(403, 'PERMISSION_DENIED'));
    renderApp({ path: '/projects', session: reviewerSession() });

    expect(await screen.findByText(en('projects.register.emptyForRole'))).toBeTruthy();
    expect(screen.queryByRole('button', { name: en('common.actions.retry') })).toBeNull();
  });

  test('any other refusal is explained, with a retry', async () => {
    withProjectLookups(mockApi()).on('GET', /^\/projects$/, problem(503, 'UNAVAILABLE'));
    renderApp({ path: '/projects', session: reviewerSession() });

    expect(await screen.findByText(en('common.problems.unavailable'))).toBeTruthy();
    expect(screen.getByRole('button', { name: en('common.actions.retry') })).toBeTruthy();
  });

  test('has no axe violation in English', async () => {
    openRegister('/projects', reviewerSession());
    await screen.findByRole('table');
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('has no axe violation in Arabic, right to left', async () => {
    withProjectLookups(mockApi()).on('GET', /^\/projects$/, {
      body: { items: PROJECTS, page: 1, pageSize: 25, totalCount: 3 },
    });
    renderApp({ path: '/projects', session: reviewerSession(), language: 'ar' });
    await screen.findByRole('table', { name: translate('ar', 'projects.register.title') });

    expect(document.documentElement.dir).toBe('rtl');
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });
});

describe('SCR-026 My Projects', () => {
  test('asks for the projects the signed-in person manages', async () => {
    const api = openRegister('/projects/mine', entitySession(), [
      projectSummary({ projectManagerUserId: ENTITY_USER_ID, status: 'SUBMITTED' }),
    ]);

    await screen.findByRole('table', { name: en('projects.mine.title') });
    expect(api.requestsTo('GET', /^\/projects$/)[0]?.query.get('projectManagerUserId')).toBe(
      ENTITY_USER_ID,
    );
    expect(screen.queryByLabelText(en('projects.fields.department'))).toBeNull();
  });

  test('managing nothing has its own empty state', async () => {
    openRegister('/projects/mine', entitySession(), []);

    expect(await screen.findByText(en('projects.mine.empty'))).toBeTruthy();
    expect(screen.queryByText(en('projects.register.emptyForRole'))).toBeNull();
    expect(screen.getByRole('link', { name: en('projects.mine.toRegister') })).toBeTruthy();
  });
});
