import { screen, waitFor, within } from '@testing-library/react';
import { type UserEvent } from '@testing-library/user-event';
import { expect } from 'vitest';

import { WORKSPACE_TABS } from '@/features/projects/access.ts';
import { translate } from '@/shared/i18n/i18n.ts';

/**
 * Every tab the project page offers, by name, in the order of its headings (review round 8, point 04): the page draws the headings, then only
 * the tabs of the open heading, so each heading is opened in turn. A heading with one tab draws no tab row; its tab is the one it links to.
 */
export async function offeredTabs(user: UserEvent): Promise<string[]> {
  const groups = await screen.findByRole('navigation', {
    name: translate('en', 'projects.workspace.groups.label'),
  });
  const headings = within(groups).getAllByRole('link');
  const names: string[] = [];
  for (const index of headings.keys()) {
    const heading = within(groups).getAllByRole('link')[index];
    if (heading === undefined) {
      continue;
    }
    await user.click(heading);
    await waitFor(() => {
      expect(within(groups).getAllByRole('link')[index]?.getAttribute('aria-current')).toBe('page');
    });
    const row = screen.queryByRole('navigation', {
      name: translate('en', 'projects.workspace.tabs.label'),
    });
    if (row === null) {
      const href = heading.getAttribute('href') ?? '';
      const path = href.split('/').slice(3).join('/');
      const tab = WORKSPACE_TABS.find((candidate) => candidate.path === path);
      if (tab !== undefined) {
        names.push(translate('en', `projects.workspace.tabs.${tab.key}`));
      }
    } else {
      names.push(
        ...within(row)
          .getAllByRole('link')
          .map((link) => link.textContent),
      );
    }
  }
  return names;
}
