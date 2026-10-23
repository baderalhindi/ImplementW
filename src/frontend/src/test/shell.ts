import { screen, within } from '@testing-library/react';

import { translate } from '@/shared/i18n/i18n.ts';

// The signed-in shell's master navigation (Blueprint §20.1), as a test reads it: the main menu, a menu's own link (its
// name may carry a count), and the open menu's screens.

const en = (key: string) => translate('en', key);

function startingWith(text: string): RegExp {
  return new RegExp(`^${text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`);
}

export function mainMenu(): HTMLElement {
  return screen.getByRole('navigation', { name: en('common.menu.label') });
}

export function findMainMenu(): Promise<HTMLElement> {
  return screen.findByRole('navigation', { name: en('common.menu.label') });
}

/** The link of the menu labelled `menuKey`, or null when the session is not shown that menu. */
export function menuLink(menuKey: string): HTMLElement | null {
  return within(mainMenu()).queryByRole('link', { name: startingWith(en(menuKey)) });
}

/** The screens listed under the open menu `menuKey`; throws when that menu is not the open one. */
export function openMenu(menuKey: string): HTMLElement {
  return within(mainMenu()).getByRole('list', { name: en(menuKey) });
}

/** The open menu `menuKey`'s list, once it is the open one. */
export function findOpenMenu(menuKey: string): Promise<HTMLElement> {
  return within(mainMenu()).findByRole('list', { name: en(menuKey) });
}

/** The addresses the open menu `menuKey` lists, in order. */
export function openMenuTargets(menuKey: string): (string | null)[] {
  return within(openMenu(menuKey))
    .getAllByRole('link')
    .map((link) => link.getAttribute('href'));
}

/** The names of the screens the open menu `menuKey` lists, in order. */
export function openMenuNames(menuKey: string): (string | null)[] {
  return within(openMenu(menuKey))
    .getAllByRole('link')
    .map((link) => link.textContent);
}
