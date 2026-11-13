import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { mockApi, problem } from '@/test/mockApi.ts';
import {
  ENTITY_USER_ID,
  PROJECT_ID,
  projectDetail,
  viewerSession,
  withProject,
  withProjectLookups,
} from '@/test/projectFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { renderApp } from '@/test/renderApp.tsx';
import { coastalTasks, DRAINAGE_ID, withTasks } from '@/test/taskFixtures.ts';

// Client review, item 8: a register's rows carry Edit and Delete for whoever manages it (here the Project Manager);
// the Viewer, and an external entity user without TASK_MANAGE, see neither.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const TASKS_PATH = `/projects/${PROJECT_ID}/tasks`;

function open(session = sessionFor(['R04']), status: 'ACTIVE' | 'CLOSED' = 'ACTIVE') {
  const project = projectDetail({
    status,
    projectManagerUserId: ENTITY_USER_ID,
    activatedAt: '2026-10-01T10:00:00Z',
  });
  const api = withTasks(withProject(withProjectLookups(mockApi()), project), {
    tasks: coastalTasks(),
  });
  renderApp({ path: TASKS_PATH, session });
  return api;
}

describe('row actions on a register', () => {
  test('the Project Manager edits from the row and deletes it once confirmed by name', async () => {
    const api = open();
    const row = (await screen.findByRole('button', { name: 'Site preparation' })).closest(
      'tr',
    ) as HTMLElement;
    const user = userEvent.setup();

    await user.click(
      within(row).getByRole('button', { name: `${en('common.actions.edit')} Site preparation` }),
    );
    expect(await screen.findByRole('dialog')).toBeTruthy();
    await user.keyboard('{Escape}');

    await user.click(
      within(row).getByRole('button', {
        name: `${en('common.rowActions.delete')} Site preparation`,
      }),
    );
    const dialog = await screen.findByRole('dialog', { name: en('common.rowActions.deleteTitle') });
    expect(within(dialog).getByText(/Site preparation/)).toBeTruthy();
    api.on('DELETE', /^\/project-tasks\//, { status: 204 });
    await user.click(within(dialog).getByRole('button', { name: en('common.rowActions.delete') }));
    await waitFor(() => {
      expect(api.requestsTo('DELETE', /^\/project-tasks\//)).toHaveLength(1);
    });
  });

  test('select all, then Delete for every chosen task; a refused one is said and stays chosen', async () => {
    const api = open();
    const user = userEvent.setup();

    await user.click(await screen.findByRole('checkbox', { name: en('common.table.selectAll') }));
    const count = screen.getAllByRole('checkbox', { name: /^Select (?!all)/ }).length;
    const bar = screen
      .getByText(en('common.table.selected', { count }))
      .closest<HTMLElement>('.selection-bar');
    if (bar === null) {
      throw new Error('No selection bar.');
    }
    await user.click(within(bar).getByRole('button', { name: en('common.rowActions.delete') }));
    const dialog = await screen.findByRole('dialog', { name: en('common.rowActions.deleteTitle') });
    expect(within(dialog).getByText(/Site preparation.*Lay drainage/)).toBeTruthy();
    api.on('DELETE', /^\/project-tasks\//, (request) =>
      request.path.endsWith(DRAINAGE_ID)
        ? problem(409, 'RECORD_APPROVAL_PENDING')
        : { status: 204 },
    );
    await user.click(within(dialog).getByRole('button', { name: en('common.rowActions.delete') }));

    expect(await within(dialog).findByText(en('common.problems.approvalPending'))).toBeTruthy();
    expect(api.requestsTo('DELETE', /^\/project-tasks\//)).toHaveLength(count);
    expect(screen.getByText(en('common.table.selected', { count: 1 }))).toBeTruthy();
    expect(
      screen.getByRole<HTMLInputElement>('checkbox', {
        name: en('common.table.selectRow', { name: 'Lay drainage' }),
      }).checked,
    ).toBe(true);
  });

  test('the Viewer sees the rows with no actions column at all', async () => {
    open(viewerSession());
    await screen.findByRole('button', { name: 'Site preparation' });
    expect(screen.queryByRole('columnheader', { name: en('common.table.actions') })).toBeNull();
    await userEvent
      .setup()
      .click(screen.getByRole('checkbox', { name: en('common.table.selectAll') }));
    expect(screen.queryByRole('button', { name: en('common.rowActions.delete') })).toBeNull();
    expect(
      screen.queryByRole('button', { name: new RegExp(`^${en('common.rowActions.delete')}`) }),
    ).toBeNull();
  });
});
