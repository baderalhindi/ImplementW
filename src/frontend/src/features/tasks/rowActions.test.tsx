import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { mockApi } from '@/test/mockApi.ts';
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
import { coastalTasks, withTasks } from '@/test/taskFixtures.ts';

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

  test('the Viewer sees the rows with no actions column at all', async () => {
    open(viewerSession());
    await screen.findByRole('button', { name: 'Site preparation' });
    expect(screen.queryByRole('columnheader', { name: en('common.table.actions') })).toBeNull();
    expect(
      screen.queryByRole('button', { name: new RegExp(`^${en('common.rowActions.delete')}`) }),
    ).toBeNull();
  });
});
