import { approvalNavigation } from '@/features/approvals/routes.tsx';
import { changeRequestNavigation } from '@/features/change-requests/routes.tsx';
import { documentNavigation } from '@/features/documents/routes.tsx';
import { externalParticipationNavigation } from '@/features/external-participation/routes.tsx';
import {
  APPLICATION_MONITOR_PATH,
  EXTERNAL_REQUESTS_PATH,
  MY_CONTRIBUTIONS_PATH,
  MY_EXTERNAL_REQUESTS_PATH,
} from '@/features/external-participation/paths.ts';
import { kpiNavigation } from '@/features/financial-kpi/routes.tsx';
import { type NavigationItem } from '@/features/identity-access/routes.tsx';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { issuesChallengesNavigation } from '@/features/issues-challenges/routes.tsx';
import { milestoneNavigation } from '@/features/milestones/routes.tsx';
import { notificationNavigation } from '@/features/notifications/routes.tsx';
import { projectNavigation } from '@/features/projects/routes.tsx';
import { reportNavigation } from '@/features/reports/routes.tsx';
import { riskNavigation } from '@/features/risks/routes.tsx';
import { suspensionClosureNavigation } from '@/features/suspension-closure/routes.tsx';
import { taskNavigation } from '@/features/tasks/routes.tsx';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

// The master navigation of Blueprint §20.1: eleven menus in the blueprint's order, each gathering the screens built so
// far. Help and Knowledge has no screen yet, so it is not listed; Administration is left out for a session with no
// administration or audit screen (§20: unauthorized modules are not shown; the API's check stays authoritative).

export type MenuId =
  | 'home'
  | 'portfolio'
  | 'projects'
  | 'myWork'
  | 'risksIssues'
  | 'requestsApprovals'
  | 'documents'
  | 'dashboardsReports'
  | 'notifications'
  | 'administration';

export interface Menu {
  id: MenuId;
  label: TranslationKey;
  /** Where the menu itself leads: its first screen. */
  to: string;
  /** The path prefixes whose screens belong to the menu, so a record's page keeps its menu open. */
  prefixes: string[];
  items: NavigationItem[];
}

/** Whether `pathname` is `prefix` or one of its sub-paths. */
export function underPath(pathname: string, prefix: string): boolean {
  return pathname === prefix || pathname.startsWith(`${prefix}/`);
}

function menu(
  id: MenuId,
  label: TranslationKey,
  prefixes: string[],
  items: NavigationItem[],
): Menu {
  return { id, label, to: items[0]?.to ?? prefixes[0] ?? '/', prefixes, items };
}

/** The menus this session sees. `administration` is the administration and audit screens the session may open. */
export function menusFor(user: SessionUser, administration: NavigationItem[]): Menu[] {
  const menus = [
    { id: 'home', label: 'common.menu.home', to: '/', prefixes: [], items: [] } satisfies Menu,
    menu('portfolio', 'common.menu.portfolio', ['/kpis'], kpiNavigation),
    menu('projects', 'common.menu.projects', ['/projects'], projectNavigation),
    menu(
      'myWork',
      'common.menu.myWork',
      ['/tasks', '/milestones'],
      [...taskNavigation, ...milestoneNavigation],
    ),
    menu(
      'risksIssues',
      'common.menu.risksIssues',
      ['/risks', '/issues-challenges'],
      [...riskNavigation, ...issuesChallengesNavigation],
    ),
    menu(
      'requestsApprovals',
      'common.menu.requestsApprovals',
      [
        '/approvals',
        '/change-requests',
        '/suspension-requests',
        '/closure-requests',
        EXTERNAL_REQUESTS_PATH,
        APPLICATION_MONITOR_PATH,
        '/external-contributions',
        MY_EXTERNAL_REQUESTS_PATH,
        MY_CONTRIBUTIONS_PATH,
      ],
      [
        ...approvalNavigation,
        ...changeRequestNavigation,
        ...suspensionClosureNavigation,
        ...externalParticipationNavigation(user),
      ],
    ),
    menu('documents', 'common.menu.documents', ['/documents'], documentNavigation),
    menu(
      'dashboardsReports',
      'common.menu.dashboardsReports',
      ['/reports'],
      reportNavigation(user),
    ),
    menu('notifications', 'common.menu.notifications', ['/notifications'], notificationNavigation),
    menu('administration', 'common.menu.administration', ['/admin'], administration),
  ];
  return menus.filter((candidate) => candidate.id === 'home' || candidate.items.length > 0);
}

/** The menu the page belongs to: Home for `/`, otherwise the menu owning the longest matching prefix. */
export function activeMenu(menus: Menu[], pathname: string): Menu | undefined {
  if (pathname === '/') {
    return menus.find((candidate) => candidate.id === 'home');
  }
  let best: { menu: Menu; length: number } | undefined;
  for (const candidate of menus) {
    for (const prefix of candidate.prefixes) {
      if (underPath(pathname, prefix) && prefix.length > (best?.length ?? 0)) {
        best = { menu: candidate, length: prefix.length };
      }
    }
  }
  return best?.menu;
}

/** The menu item the page sits under: the longest item path matching it, honouring an item's exact-match flag. */
export function activeItem(menu: Menu | undefined, pathname: string): NavigationItem | undefined {
  let best: NavigationItem | undefined;
  for (const item of menu?.items ?? []) {
    const matches = item.end === true ? pathname === item.to : underPath(pathname, item.to);
    if (matches && item.to.length > (best?.to.length ?? 0)) {
      best = item;
    }
  }
  return best;
}
