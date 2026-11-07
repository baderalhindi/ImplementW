import { within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { type MockApi } from './mockApi.ts';

/**
 * Finds a person in the UserPicker inside `scope`, by typing, as a person does. The lookup answers with that one person. `index` names which
 * picker when the screen shows several.
 */
export async function pickPerson(
  api: MockApi,
  scope: HTMLElement,
  id: string,
  index = 0,
): Promise<void> {
  const displayName = 'Picked Person';
  api.on('GET', /^\/users$/, {
    body: {
      items: [{ id, displayName, username: 'picked.person' }],
      page: 1,
      pageSize: 10,
      totalCount: 1,
    },
  });
  await userEvent.type(within(scope).getAllByRole('searchbox')[index] ?? scope, 'picked');
  await userEvent.click(
    await within(scope).findByRole('button', { name: new RegExp(displayName) }),
  );
}
