import { createContext, useContext } from 'react';

import { type TranslationKey } from '@/shared/i18n/i18n.ts';

/** One screen of a menu, as the master navigation offers it to this person. */
export interface MenuScreen {
  to: string;
  label: TranslationKey;
}

/** A menu of the master navigation, with the screens this person may open under it, in the menus' order. */
export interface MenuScreens {
  label: TranslationKey;
  screens: readonly MenuScreen[];
}

/**
 * The signed-in person's menus and screens (app/menus.ts menusFor), published by the app's layout so a feature can offer them (the Home banner's
 * quick actions, 21 Nov 2026) without reading the app's navigation itself. Empty outside the signed-in layout.
 */
export const MenuScreensContext = createContext<readonly MenuScreens[]>([]);

export function useMenuScreens(): readonly MenuScreens[] {
  return useContext(MenuScreensContext);
}
