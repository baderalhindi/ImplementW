import { type ReactElement, useId } from 'react';
import { Link, useOutletContext } from 'react-router';

import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';

import { type Menu, type MenuGroup } from './menus.ts';
import { ShellIcon, type ShellIconName } from './ShellIcon.tsx';

const GROUP_ICONS: Record<string, ShellIconName> = {
  usersRoles: 'usersRoles',
  organization: 'organization',
  data: 'data',
  integrations: 'integrations',
  auditLogs: 'auditLogs',
};

/** The last part of a heading's key (`common.menu.adminGroups.usersRoles` → `usersRoles`): its icon and description. */
const groupKey = (group: MenuGroup) => group.label.split('.').at(-1) ?? '';

/**
 * The administrator's map of the platform on the Home (21 Nov 2026, "make it better"): Administration first, one card per heading of its menu
 * (review round 8, point 15), each with what it covers and its screens as rows; then the other menus the administrator has, smaller, their
 * screens as chips. The menus are the shell's, so a card offers exactly what this person's permissions open.
 */
export function MenuOverview(): ReactElement {
  const { t } = useI18n();
  const titleId = useId();
  const adminTitleId = useId();
  const menus = useOutletContext<Menu[]>().filter((menu) => menu.id !== 'home');
  const administration = menus.find((menu) => menu.id === 'administration');
  const others = menus.filter((menu) => menu !== administration);
  const groups: MenuGroup[] =
    administration === undefined
      ? []
      : (administration.groups ?? [{ label: administration.label, items: administration.items }]);

  return (
    <section className="home__menus" aria-labelledby={titleId}>
      <h2 id={titleId}>{t('home.menus.title')}</h2>
      {administration !== undefined && (
        <section className="admin-map" aria-labelledby={adminTitleId}>
          <header className="admin-map__header">
            <span className="admin-map__badge" aria-hidden="true">
              <ShellIcon name="administration" />
            </span>
            <div>
              <h3 id={adminTitleId} className="admin-map__title">
                {t(administration.label)}
              </h3>
              <p className="admin-map__lead">{t('home.menus.adminDescription')}</p>
            </div>
          </header>
          <ul className="admin-map__groups">
            {groups.map((group) => {
              const key = groupKey(group);
              return (
                <li key={group.label} className="admin-map__group">
                  <div className="admin-map__group-head">
                    <span className="admin-map__icon" aria-hidden="true">
                      <ShellIcon name={GROUP_ICONS[key] ?? 'administration'} />
                    </span>
                    <div>
                      <h4 className="admin-map__group-title">{t(group.label)}</h4>
                      {key in GROUP_ICONS && (
                        <p className="admin-map__group-lead">
                          {t(`home.menus.groups.${key}` as TranslationKey)}
                        </p>
                      )}
                    </div>
                    <span className="admin-map__count" aria-hidden="true">
                      {group.items.length}
                    </span>
                  </div>
                  <ul className="admin-map__links" aria-label={t(group.label)}>
                    {group.items.map((item) => (
                      <li key={item.to}>
                        <Link to={item.to}>
                          <span>{t(item.label)}</span>
                          <ShellIcon name="chevronEnd" className="admin-map__chevron" />
                        </Link>
                      </li>
                    ))}
                  </ul>
                </li>
              );
            })}
          </ul>
        </section>
      )}
      {others.length > 0 && (
        <ul className="home__menu-strip">
          {others.map((menu) => (
            <li key={menu.id} className="home__menu-card">
              <h3 className="home__menu-title">
                <span className="home__menu-icon">
                  <ShellIcon name={menu.id} />
                </span>
                {t(menu.label)}
              </h3>
              <ul className="home__menu-chips" aria-label={t(menu.label)}>
                {menu.items.map((item) => (
                  <li key={item.to}>
                    <Link to={item.to}>{t(item.label)}</Link>
                  </li>
                ))}
              </ul>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
