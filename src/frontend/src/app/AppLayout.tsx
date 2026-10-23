import { type ReactElement, type RefObject, useEffect, useId, useRef, useState } from 'react';
import { Link, NavLink, Outlet, useLocation } from 'react-router';

import { useInboxCount } from '@/features/approvals/useInboxCount.ts';
import { auditNavigation } from '@/features/audit-activity/access.ts';
import { useAuditAccess } from '@/features/audit-activity/useAuditAccess.ts';
import { identityAccessNavigation } from '@/features/identity-access/routes.tsx';
import { integrationAdminNavigation } from '@/features/integration-admin/catalogue.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { sessionStore } from '@/features/identity-access/session/sessionStore.ts';
import { StepUpDialog } from '@/features/identity-access/session/StepUpDialog.tsx';
import {
  SYSTEM_ADMINISTRATOR_ROLE,
  useHasRole,
  useSession,
} from '@/features/identity-access/session/useSession.ts';
import { unreadCountStore, useUnreadCount } from '@/features/notifications/unreadCount.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { activeItem, activeMenu, type Menu, type MenuId, menusFor } from './menus.ts';
import { ShellIcon } from './ShellIcon.tsx';

/** Each language in its own script, so a reader finds theirs whichever is showing. */
const languageChoices = [
  { code: 'en', name: 'English' },
  { code: 'ar', name: 'العربية' },
] as const;

const COLLAPSED_KEY = 'pmplatform.shell.collapsed';

interface Popover {
  open: boolean;
  setOpen: (open: boolean | ((value: boolean) => boolean)) => void;
  containerRef: RefObject<HTMLDivElement | null>;
  buttonRef: RefObject<HTMLButtonElement | null>;
  menuId: string;
}

/** A button's drop-down: a click elsewhere or Escape closes it, and Escape returns focus to the button. */
function usePopover(): Popover {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const menuId = useId();

  useEffect(() => {
    if (!open) {
      return undefined;
    }
    const closeOnOutsideClick = (event: PointerEvent): void => {
      if (!containerRef.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    const closeOnEscape = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') {
        setOpen(false);
        buttonRef.current?.focus();
      }
    };
    document.addEventListener('pointerdown', closeOnOutsideClick);
    document.addEventListener('keydown', closeOnEscape);
    return () => {
      document.removeEventListener('pointerdown', closeOnOutsideClick);
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [open]);

  return { open, setOpen, containerRef, buttonRef, menuId };
}

/**
 * The language list; the current one is ticked. Signed out it is a globe alone; in the top bar it is labelled
 * "Language" with a chevron, as the master navigation draws it.
 */
function LanguageSwitch({ labelled = false }: { labelled?: boolean }): ReactElement {
  const { t, language, setLanguage } = useI18n();
  const { open, setOpen, containerRef, buttonRef, menuId } = usePopover();

  return (
    <div className="popover" ref={containerRef}>
      <button
        ref={buttonRef}
        type="button"
        className={
          labelled
            ? 'button button--quiet topbar__control'
            : 'button button--quiet language-switch__button'
        }
        aria-expanded={open}
        aria-controls={menuId}
        title={t('common.language')}
        onClick={() => {
          setOpen((value) => !value);
        }}
      >
        <ShellIcon name="globe" className="language-switch__icon" />
        <span className={labelled ? undefined : 'visually-hidden'}>{t('common.language')}</span>
        {labelled && <ShellIcon name="chevronDown" className="topbar__chevron" />}
      </button>
      <ul className="popover__menu" id={menuId} hidden={!open}>
        {languageChoices.map((choice) => (
          <li key={choice.code}>
            <button
              type="button"
              className="popover__option"
              lang={choice.code}
              aria-pressed={language === choice.code}
              onClick={() => {
                setLanguage(choice.code);
                setOpen(false);
              }}
            >
              <span className="popover__check" aria-hidden="true">
                {language === choice.code ? '✓' : ''}
              </span>
              {choice.name}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}

/** The top bar's Create: the records that can be started from anywhere, without a project chosen first. */
function CreateMenu(): ReactElement {
  const { t } = useI18n();
  const { open, setOpen, containerRef, buttonRef, menuId } = usePopover();
  const close = (): void => {
    setOpen(false);
  };

  return (
    <div className="popover" ref={containerRef}>
      <button
        ref={buttonRef}
        type="button"
        className="button button--primary topbar__create"
        aria-expanded={open}
        aria-controls={menuId}
        onClick={() => {
          setOpen((value) => !value);
        }}
      >
        <ShellIcon name="create" />
        {t('common.shell.create')}
      </button>
      <ul className="popover__menu" id={menuId} hidden={!open}>
        <li>
          <Link className="popover__option" to="/projects/new" onClick={close}>
            {t('projects.nav.create')}
          </Link>
        </li>
        <li>
          <Link className="popover__option" to="/change-requests/new" onClick={close}>
            {t('common.shell.createChangeRequest')}
          </Link>
        </li>
      </ul>
    </div>
  );
}

/** Up to two initials of the display name, in the script it is written in. */
function initialsOf(displayName: string): string {
  return displayName
    .split(/[\s—–-]+/u)
    .filter((word) => word !== '')
    .slice(0, 2)
    .map((word) => word.charAt(0).toUpperCase())
    .join('');
}

/** The account button: the person's initials, opening their name and Sign out. */
function AccountMenu({ user }: { user: SessionUser }): ReactElement {
  const { t } = useI18n();
  const { open, setOpen, containerRef, buttonRef, menuId } = usePopover();

  return (
    <div className="popover" ref={containerRef}>
      <button
        ref={buttonRef}
        type="button"
        className="topbar__avatar"
        aria-expanded={open}
        aria-controls={menuId}
        title={user.displayName}
        onClick={() => {
          setOpen((value) => !value);
        }}
      >
        <span aria-hidden="true">{initialsOf(user.displayName)}</span>
        <span className="visually-hidden">{`${t('common.shell.account')}: ${user.displayName}`}</span>
      </button>
      <div className="popover__menu popover__menu--account" id={menuId} hidden={!open}>
        <p className="popover__name">{user.displayName}</p>
        <button
          type="button"
          className="popover__option"
          onClick={() => {
            sessionStore.signOut();
          }}
        >
          {t('common.signOut')}
        </button>
      </div>
    </div>
  );
}

/** The menu and the menu item the page sits under; the last crumb is the current one and is not a link. */
function Breadcrumb({
  menu,
  pathname,
}: {
  menu: Menu | undefined;
  pathname: string;
}): ReactElement {
  const { t } = useI18n();
  const item = activeItem(menu, pathname);
  const crumbs = [
    ...(menu === undefined ? [] : [{ to: menu.to, label: t(menu.label) }]),
    ...(item === undefined ? [] : [{ to: item.to, label: t(item.label) }]),
  ];

  return (
    <nav className="breadcrumb" aria-label={t('common.shell.breadcrumb')}>
      <ol>
        {crumbs.map((crumb, index) => (
          <li key={crumb.label}>
            {index === crumbs.length - 1 && crumb.to === pathname ? (
              <span aria-current="page">{crumb.label}</span>
            ) : (
              <Link to={crumb.to}>{crumb.label}</Link>
            )}
          </li>
        ))}
      </ol>
    </nav>
  );
}

/** A count on a menu: the number on the badge, and its meaning for a screen reader. */
function MenuCount({
  count,
  meaning,
}: {
  count: number | null;
  meaning: string;
}): ReactElement | null {
  if (count === null || count <= 0) {
    return null;
  }
  return (
    <span className="rail__count">
      <span aria-hidden="true">{count}</span>
      <span className="visually-hidden">{` ${meaning}`}</span>
    </span>
  );
}

function readCollapsed(): boolean {
  try {
    return window.localStorage.getItem(COLLAPSED_KEY) === 'true';
  } catch {
    return false;
  }
}

function writeCollapsed(collapsed: boolean): void {
  try {
    window.localStorage.setItem(COLLAPSED_KEY, String(collapsed));
  } catch {
    // A blocked store only forgets the choice.
  }
}

/**
 * The rail of the master navigation (Blueprint §20.1): the eleven menus in order, the open menu's screens under it and
 * no other's, counts on Requests and Approvals and on Notifications only. Collapsed, it keeps the icons and counts.
 */
function Rail({
  menus,
  current,
  pathname,
  collapsed,
  administrationShown,
  onToggleCollapsed,
}: {
  menus: Menu[];
  current: Menu | undefined;
  pathname: string;
  collapsed: boolean;
  administrationShown: boolean;
  onToggleCollapsed: () => void;
}): ReactElement {
  const { t } = useI18n();
  const unread = useUnreadCount().count;
  const awaiting = useInboxCount(pathname);

  useEffect(() => unreadCountStore.watch(), []);

  const counts: Partial<Record<MenuId, ReactElement | null>> = {
    requestsApprovals: (
      <MenuCount
        count={awaiting}
        meaning={t('common.menu.awaitingDecision', { count: awaiting ?? 0 })}
      />
    ),
    notifications: (
      <MenuCount count={unread} meaning={t('notifications.bell.unread', { count: unread ?? 0 })} />
    ),
  };

  return (
    <>
      {/* The link carries the name, so it has one whichever image the rail's width shows. */}
      <Link className="rail__brand" to="/" aria-label={t('common.organizationName')}>
        <img className="rail__logo" src="/brand/ahda-logo.png" alt="" width="596" height="232" />
        <img
          className="rail__emblem"
          src="/brand/ahda-emblem.png"
          alt=""
          width="196"
          height="196"
        />
      </Link>
      <nav className="rail__nav" aria-label={t('common.menu.label')}>
        <ul>
          {menus.map((menu) => {
            const open = menu === current;
            return (
              <li key={menu.id}>
                <Link
                  className={open ? 'rail__menu rail__menu--open' : 'rail__menu'}
                  to={menu.to}
                  aria-current={pathname === menu.to ? 'page' : undefined}
                  title={collapsed ? t(menu.label) : undefined}
                >
                  <ShellIcon name={menu.id} />
                  <span className="rail__label">{t(menu.label)}</span>
                  {counts[menu.id]}
                </Link>
                {open && !collapsed && menu.items.length > 0 && (
                  <ul className="rail__items" aria-label={t(menu.label)}>
                    {menu.items.map((item) => (
                      <li key={item.to}>
                        <NavLink className="rail__item" to={item.to} end={item.end ?? false}>
                          {t(item.label)}
                        </NavLink>
                      </li>
                    ))}
                  </ul>
                )}
              </li>
            );
          })}
        </ul>
      </nav>
      <div className="rail__footer">
        {!administrationShown && !collapsed && (
          <p className="rail__note">
            <ShellIcon name="administration" />
            {t('common.menu.administrationHidden')}
          </p>
        )}
        <button
          type="button"
          className="rail__menu rail__toggle"
          aria-pressed={collapsed}
          title={collapsed ? t('common.menu.expand') : undefined}
          onClick={onToggleCollapsed}
        >
          <ShellIcon
            name={collapsed ? 'expand' : 'collapse'}
            className="shell-icon shell-icon--mirrored"
          />
          <span className="rail__label">
            {t(collapsed ? 'common.menu.expand' : 'common.menu.collapse')}
          </span>
        </button>
      </div>
    </>
  );
}

/** Whether the window has scrolled past `offset` pixels; the signed-out header floats once it has. */
function useScrolledPast(offset: number): boolean {
  const [scrolled, setScrolled] = useState(() => window.scrollY > offset);
  useEffect(() => {
    const update = (): void => {
      setScrolled(window.scrollY > offset);
    };
    update();
    window.addEventListener('scroll', update, { passive: true });
    return () => {
      window.removeEventListener('scroll', update);
    };
  }, [offset]);
  return scrolled;
}

/** Signed out (the sign-in page): the logo and the language switch over the page. */
function SignedOutLayout(): ReactElement {
  const { t } = useI18n();
  const floating = useScrolledPast(8);
  return (
    <div className="shell">
      <a className="skip-link" href="#main">
        {t('common.skipToContent')}
      </a>
      <header className={floating ? 'shell__header shell__header--floating' : 'shell__header'}>
        <Link className="shell__brand" to="/">
          <img
            className="shell__logo"
            src="/brand/ahda-logo.png"
            alt={t('common.organizationName')}
            width="596"
            height="232"
          />
        </Link>
        <LanguageSwitch />
      </header>
      <main id="main" className="shell__main" tabIndex={-1}>
        <Outlet />
      </main>
    </div>
  );
}

/**
 * Signed in: the master navigation's rail and a top bar (menu toggle on a narrow window, breadcrumb, Create, Language,
 * account) over the page. On a narrow window the rail is a drawer, closed again by a choice, Escape or the backdrop.
 */
function SignedInLayout({ user }: { user: SessionUser }): ReactElement {
  const { t } = useI18n();
  const { pathname } = useLocation();
  const administrator = useHasRole(SYSTEM_ADMINISTRATOR_ROLE);
  const auditItems = auditNavigation(useAuditAccess().data ?? null);
  const menus = menusFor(user, [
    ...(administrator ? [...identityAccessNavigation, ...integrationAdminNavigation] : []),
    ...auditItems,
  ]);
  const current = activeMenu(menus, pathname);
  const [collapsed, setCollapsed] = useState(readCollapsed);
  // The drawer is open on the page it was opened on, so choosing a screen closes it.
  const [drawerOpenOn, setDrawerOpenOn] = useState<string | null>(null);
  const drawerOpen = drawerOpenOn === pathname;
  const setDrawerOpen = (open: boolean): void => {
    setDrawerOpenOn(open ? pathname : null);
  };
  const drawerButtonRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!drawerOpen) {
      return undefined;
    }
    const closeOnEscape = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') {
        setDrawerOpenOn(null);
        drawerButtonRef.current?.focus();
      }
    };
    document.addEventListener('keydown', closeOnEscape);
    return () => {
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [drawerOpen]);

  // The drawer (a narrow window) always shows the full rail; the collapsed choice is for a wide one.
  const railCollapsed = collapsed && !drawerOpen;
  const railClass = [
    'rail',
    railCollapsed ? 'rail--collapsed' : '',
    drawerOpen ? 'rail--drawer-open' : '',
  ]
    .filter((name) => name !== '')
    .join(' ');

  return (
    <div className={collapsed ? 'app app--collapsed' : 'app'}>
      <a className="skip-link" href="#main">
        {t('common.skipToContent')}
      </a>
      <aside className={railClass} id="main-menu">
        <Rail
          menus={menus}
          current={current}
          pathname={pathname}
          collapsed={railCollapsed}
          administrationShown={menus.some((menu) => menu.id === 'administration')}
          onToggleCollapsed={() => {
            setCollapsed((value) => {
              writeCollapsed(!value);
              return !value;
            });
          }}
        />
      </aside>
      {drawerOpen && (
        <button
          type="button"
          className="rail__backdrop"
          aria-label={t('common.menu.close')}
          onClick={() => {
            setDrawerOpen(false);
          }}
        />
      )}
      <div className="app__column">
        <header className="topbar">
          <button
            ref={drawerButtonRef}
            type="button"
            className="button button--quiet topbar__drawer"
            aria-controls="main-menu"
            aria-expanded={drawerOpen}
            onClick={() => {
              setDrawerOpen(!drawerOpen);
            }}
          >
            <ShellIcon name={drawerOpen ? 'close' : 'menu'} />
            <span className="visually-hidden">
              {t(drawerOpen ? 'common.menu.close' : 'common.menu.open')}
            </span>
          </button>
          <Breadcrumb menu={current} pathname={pathname} />
          <div className="topbar__actions">
            <CreateMenu />
            <LanguageSwitch labelled />
            <AccountMenu user={user} />
          </div>
        </header>
        <main id="main" className="app__main" tabIndex={-1}>
          <Outlet />
        </main>
      </div>
      <StepUpDialog />
    </div>
  );
}

/** The application shell: the sign-in page's header while signed out, the master navigation once signed in. */
export function AppLayout(): ReactElement {
  const { session } = useSession();
  return session === null ? <SignedOutLayout /> : <SignedInLayout user={session.user} />;
}
