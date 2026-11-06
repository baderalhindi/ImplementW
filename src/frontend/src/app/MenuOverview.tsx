import { type ReactElement, useId } from 'react';
import { Link, useOutletContext } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type Menu } from './menus.ts';
import { ShellIcon } from './ShellIcon.tsx';

/** A menu with this many screens takes a whole row, its links in columns, so it does not tower over the cards beside it. */
const WIDE_MENU_SCREENS = 7;

/**
 * The Home's map of the platform: one card per menu of the rail with that menu's screens, the rail itself naming the menus only.
 * The menus are the shell's, so a card offers exactly what this person's permissions open.
 */
export function MenuOverview(): ReactElement {
  const { t } = useI18n();
  const titleId = useId();
  const menus = useOutletContext<Menu[]>().filter((menu) => menu.id !== 'home');

  return (
    <section className="home__menus" aria-labelledby={titleId}>
      <h2 id={titleId}>{t('home.menus.title')}</h2>
      <ul className="home__menu-cards">
        {menus.map((menu) => (
          <li
            key={menu.id}
            className={
              menu.items.length >= WIDE_MENU_SCREENS
                ? 'home__menu-card home__menu-card--wide'
                : 'home__menu-card'
            }
          >
            <h3 className="home__menu-title">
              <span className="home__menu-icon">
                <ShellIcon name={menu.id} />
              </span>
              {t(menu.label)}
            </h3>
            <ul className="home__menu-links" aria-label={t(menu.label)}>
              {menu.items.map((item) => (
                <li key={item.to}>
                  <Link to={item.to}>{t(item.label)}</Link>
                </li>
              ))}
            </ul>
          </li>
        ))}
      </ul>
    </section>
  );
}
