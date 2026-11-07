import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { sessionFor, withIdentityAccessData } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi, problem, SHELL_PERMISSIONS } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

import { type GlobalSearchHit, recordPath } from './globalSearchApi.ts';

const en = (key: string, params?: Record<string, string>) => translate('en', key, params);
/** The icon shows exactly while the box is closed. The stylesheet hides the box, and tests run without it. */
const toggle = () => screen.queryByRole('button', { name: en('common.globalSearch.label') });
const box = async () => {
  await userEvent.click(
    await screen.findByRole('button', { name: en('common.globalSearch.label') }),
  );
  return screen.findByRole('combobox', { name: en('common.globalSearch.label') });
};

const PROJECT: GlobalSearchHit = {
  type: 'PROJECT',
  id: 'project-1',
  projectId: 'project-1',
  title: { text: 'Corniche road' },
  projectTitle: null,
  reference: 'PRJ-7',
};
const RISK: GlobalSearchHit = {
  type: 'RISK',
  id: 'risk-1',
  projectId: 'project-1',
  title: { text: 'Corniche permit delay' },
  projectTitle: { text: 'Corniche road' },
  reference: null,
};
const searchRequests = (api: MockApi) => api.requestsTo('GET', /^\/search$/);

function renderSignedIn(api: MockApi, hits: GlobalSearchHit[] = [PROJECT, RISK]) {
  withIdentityAccessData(api).on('GET', /^\/search$/, { body: { items: hits } });
  return renderApp({ path: '/', session: sessionFor(['R01']) });
}

describe('the header search', () => {
  test('is in the top bar for a person holding nothing, and finds nothing they may not open', async () => {
    const api = mockApi().on('GET', /^\/search$/, { body: { items: [] } });
    renderApp({ path: '/notifications', session: sessionFor(['R08']) });

    await userEvent.type(await box(), 'zzz');

    expect(
      await screen.findByText(en('common.globalSearch.noResults', { text: 'zzz' })),
    ).toBeTruthy();
    expect(searchRequests(api).at(-1)?.query.get('q')).toBe('zzz');
  });

  test('groups the records by kind, naming the project a risk belongs to', async () => {
    renderSignedIn(mockApi());

    await userEvent.type(await box(), 'corn');

    const risks = await screen.findByRole('group', {
      name: en('common.globalSearch.groups.risks'),
    });
    expect(within(risks).getByRole('option').textContent).toContain('Corniche permit delay');
    expect(within(risks).getByRole('option').textContent).toContain('Corniche road');
    const projects = screen.getByRole('group', { name: en('common.globalSearch.groups.projects') });
    expect(within(projects).getByRole('option').textContent).toContain('PRJ-7');
  });

  test('asks the API once for what was typed in one go, and not at all for one character', async () => {
    const api = mockApi();
    renderSignedIn(api);
    const input = await box();

    await userEvent.type(input, 'c');
    await new Promise((resolve) => setTimeout(resolve, 250));
    expect(searchRequests(api)).toHaveLength(0);

    await userEvent.type(input, 'orn');
    await screen.findByRole('group', { name: en('common.globalSearch.groups.risks') });
    expect(searchRequests(api).map((request) => request.query.get('q'))).toEqual(['corn']);
  });

  test('matches a screen from the first letter, without asking the API', async () => {
    const api = mockApi();
    renderSignedIn(api);

    await userEvent.type(await box(), en('identityAccess.nav.users').slice(0, 1));

    expect(
      await screen.findByRole('group', { name: en('common.globalSearch.groups.screens') }),
    ).toBeTruthy();
    expect(searchRequests(api)).toHaveLength(0);
  });

  test('offers no screen the person may not open', async () => {
    mockApi().on('GET', /^\/search$/, { body: { items: [] } });
    renderApp({ path: '/notifications', session: sessionFor(['R08']) });

    await userEvent.type(await box(), en('identityAccess.nav.users'));

    await screen.findByText(
      en('common.globalSearch.noResults', { text: en('identityAccess.nav.users') }),
    );
    expect(
      screen.queryByRole('group', { name: en('common.globalSearch.groups.screens') }),
    ).toBeNull();
  });

  test('the arrow keys and Enter open a record, and the box is emptied', async () => {
    const { router } = renderSignedIn(mockApi());
    const input = await box();

    await userEvent.type(input, 'corn');
    await screen.findByRole('group', { name: en('common.globalSearch.groups.risks') });
    await userEvent.keyboard('{ArrowDown}{ArrowDown}');
    const active = document.getElementById(input.getAttribute('aria-activedescendant') ?? '');
    expect(active?.textContent).toContain('Corniche permit delay');
    await userEvent.keyboard('{Enter}');

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/projects/project-1/risks/risk-1');
    });
    expect((input as HTMLInputElement).value).toBe('');
  });

  test('a click opens a record', async () => {
    const { router } = renderSignedIn(mockApi());

    await userEvent.type(await box(), 'corn');
    await userEvent.click(await screen.findByRole('option', { name: /Corniche road.*PRJ-7/ }));

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/projects/project-1');
    });
  });

  test('Escape closes the results, and again clears and collapses the box', async () => {
    renderSignedIn(mockApi());
    const input = await box();

    await userEvent.type(input, 'corn');
    await screen.findByRole('group', { name: en('common.globalSearch.groups.risks') });
    await userEvent.keyboard('{Escape}');
    expect(input.getAttribute('aria-expanded')).toBe('false');
    await userEvent.keyboard('{Escape}');
    expect(toggle()).not.toBeNull();
  });

  test('Ctrl+K moves to the box from anywhere', async () => {
    renderSignedIn(mockApi());
    const input = await box();

    await userEvent.keyboard('{Control>}k{/Control}');

    expect(document.activeElement).toBe(input);
  });

  test('says so when the search fails', async () => {
    const api = mockApi();
    renderSignedIn(api);
    api.on('GET', /^\/search$/, problem(503, 'UNAVAILABLE'));

    await userEvent.type(await box(), 'corn');

    expect(await screen.findByText(en('common.globalSearch.failed'))).toBeTruthy();
  });

  test.each<[GlobalSearchHit['type'], string]>([
    ['PROJECT', '/projects/r'],
    ['PROJECT_TASK', '/projects/p/tasks'],
    ['RISK', '/projects/p/risks/r'],
    ['MANAGEMENT_CONCERN', '/projects/p/issues-challenges/r'],
    ['CHANGE_REQUEST', '/change-requests/r'],
    ['DOCUMENT', '/documents/r'],
  ])('%s opens at %s', (type, path) => {
    expect(recordPath({ ...RISK, type, id: 'r', projectId: 'p' })).toBe(path);
  });

  test('it shows as an icon, opens on a click or on "/" with the cursor in it, and closes empty', async () => {
    renderApp({ path: '/', session: sessionFor(['R02']) });
    const icon = await screen.findByRole('button', { name: en('common.globalSearch.label') });

    await userEvent.click(icon);
    const input = screen.getByRole('combobox', { name: en('common.globalSearch.label') });
    expect(toggle()).toBeNull();
    expect(document.activeElement).toBe(input);

    await userEvent.keyboard('{Escape}');
    expect(toggle()).not.toBeNull();

    await userEvent.keyboard('/');
    expect(document.activeElement).toBe(input);
    expect(toggle()).toBeNull();

    await userEvent.tab();
    expect(toggle()).not.toBeNull();
  });

  test('the icon sits immediately before Create, in the top bar actions', async () => {
    mockApi().on('GET', SHELL_PERMISSIONS, {
      body: {
        permissions: [{ code: 'PROJECT_REGISTER', scopes: ['ALL'] }],
        showCreateInHeader: true,
      },
    });
    renderApp({ path: '/notifications', session: sessionFor(['R02']) });
    await screen.findByRole('button', { name: en('common.shell.create') });

    const search = toggle()?.closest('.global-search');
    expect(search?.parentElement?.className).toBe('topbar__actions');
    expect(search?.nextElementSibling?.textContent).toContain(en('common.shell.create'));
  });

  test('Escape on the empty box returns the focus to the icon', async () => {
    renderSignedIn(mockApi());
    await box();

    await userEvent.keyboard('{Escape}');

    expect(document.activeElement).toBe(toggle());
  });

  test('choosing a record closes the box back to the icon', async () => {
    renderSignedIn(mockApi());

    await userEvent.type(await box(), 'corn');
    await userEvent.click(await screen.findByRole('option', { name: /Corniche road.*PRJ-7/ }));

    await screen.findByRole('button', { name: en('common.globalSearch.label') });
  });

  test('it stays open while it holds text', async () => {
    renderApp({ path: '/', session: sessionFor(['R02']) });
    await userEvent.type(await box(), 'ab');
    await userEvent.click(document.body);
    expect(toggle()).toBeNull();
  });
});
