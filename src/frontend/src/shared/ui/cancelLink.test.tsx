import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, Link, RouterProvider } from 'react-router';
import { describe, expect, test } from 'vitest';

import { I18nProvider } from '@/shared/i18n/I18nProvider.tsx';

import { CancelLink } from './CancelLink.tsx';

function renderAt(path: string) {
  const router = createMemoryRouter(
    [
      { path: '/list', element: <Link to="/edit">Edit</Link> },
      { path: '/edit', element: <CancelLink fallback="/detail" /> },
      { path: '/detail', element: <p>Detail page</p> },
    ],
    { initialEntries: [path] },
  );
  render(
    <I18nProvider initialLanguage="en">
      <RouterProvider router={router} />
    </I18nProvider>,
  );
  return router;
}

describe('a form Cancel', () => {
  test('goes back to the page the user came from', async () => {
    const router = renderAt('/list');
    const user = userEvent.setup();
    await user.click(screen.getByRole('link', { name: 'Edit' }));
    await user.click(screen.getByRole('link', { name: 'Cancel' }));
    expect(router.state.location.pathname).toBe('/list');
  });

  test('goes to its fallback when the form was opened directly', async () => {
    const router = renderAt('/edit');
    expect(screen.getByRole('link', { name: 'Cancel' }).getAttribute('href')).toBe('/detail');
    await userEvent.setup().click(screen.getByRole('link', { name: 'Cancel' }));
    expect(router.state.location.pathname).toBe('/detail');
  });
});
