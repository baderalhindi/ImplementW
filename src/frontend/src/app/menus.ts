import { approvalNavigation } from '@/features/approvals/routes.tsx';
import { AUDIT_TRAIL_PATH, auditNavigation } from '@/features/audit-activity/access.ts';
import { type AuditAccessView } from '@/features/audit-activity/api/types.ts';
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
import { PORTFOLIO_DASHBOARD_PATH } from '@/features/dashboards/paths.ts';
import { dashboardNavigation } from '@/features/dashboards/routes.tsx';
import { dataImportNavigation } from '@/features/data-import/routes.tsx';
import { type Permissions } from '@/features/identity-access/permissions/permissions.ts';
import {
  identityAccessNavigation,
  type NavigationItem,
} from '@/features/identity-access/routes.tsx';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { integrationAdminNavigation } from '@/features/integration-admin/catalogue.ts';
import { issuesChallengesNavigation } from '@/features/issues-challenges/routes.tsx';
import { milestoneNavigation } from '@/features/milestones/routes.tsx';
import { notificationNavigation } from '@/features/notifications/routes.tsx';
import { projectNavigation } from '@/features/projects/routes.tsx';
import { reportNavigation } from '@/features/reports/routes.tsx';
import { riskNavigation } from '@/features/risks/routes.tsx';
import { suspensionClosureNavigation } from '@/features/suspension-closure/routes.tsx';
import { taskNavigation } from '@/features/tasks/routes.tsx';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

import { helpNavigation } from './help/routes.tsx';

// The master navigation of Blueprint §20.1: eleven menus in the blueprint's order, each gathering the screens built so
// far. A screen is offered only to a holder of the permission it needs, and a menu with no screen left is not shown (§20:
// unauthorized modules are not shown; the 24 Oct 2026 review, C3). The API's check stays authoritative. Help and Knowledge
// is every signed-in person's (TASK-096).

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
  | 'administration'
  | 'help';

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

/** Whether a link leads to the page shown: a link with a query names one rendering of its path, so it must match whole. */
export function isCurrent(to: string, pathname: string, search: string): boolean {
  return to.includes('?') ? `${pathname}${search}` === to : pathname === to;
}

/**
 * The Portfolio Dashboard's audience (dashboards.md D-2): R02, R03, R06 and R07 land on it and see the portfolio
 * reports. A dashboard's audience is its roles (ADR-006), not a permission.
 */
const PORTFOLIO_ROLES: readonly string[] = ['R02', 'R03', 'R06', 'R07'];

/**
 * Blueprint §20.1's Portfolio menu as far as it is built: the Portfolio Dashboard, which has no route of its own and is
 * the Home's rendering of it (dashboards-ui.md D-1), the portfolio report family (SCR-132), and the KPI register — all
 * of it the portfolio audience's only, so a Project Manager is not given a Portfolio menu (the review, finding 1b).
 */
function portfolioNavigation(user: SessionUser): NavigationItem[] {
  const audience =
    user.userType !== 'EXTERNAL' &&
    user.roleAssignments.some((assignment) => PORTFOLIO_ROLES.includes(assignment.roleCode));
  return audience
    ? [
        { to: PORTFOLIO_DASHBOARD_PATH, label: 'common.menu.portfolioDashboard', end: true },
        {
          to: '/reports/portfolio',
          label: 'common.menu.portfolioReports',
          permissions: ['PROJECT_VIEW'],
        },
        ...kpiNavigation,
      ]
    : [];
}

/** The audit categories that are platform evidence (FG-06 §7.2): their holders read the audit trail under Administration. */
const ADMINISTRATIVE_AUDIT: readonly string[] = ['ADMINISTRATION', 'SECURITY', 'INTEGRATION'];

function menu(
  id: MenuId,
  label: TranslationKey,
  prefixes: string[],
  items: NavigationItem[],
): Menu {
  return { id, label, to: items[0]?.to ?? prefixes[0] ?? '/', prefixes, items };
}

/**
 * The menus this session sees: every screen whose permission `permissions` holds, in the menus left with one. `audit` is
 * GET /audit-access: the audit trail sits under Administration for a holder of an administrative audit category and under
 * Projects for one who reads business audit only, so a business role is never given an Administration menu.
 */
export function menusFor(
  user: SessionUser,
  permissions: Permissions,
  audit: AuditAccessView | null,
): Menu[] {
  const auditItems = auditNavigation(audit);
  const administrative =
    audit?.categories.some((category) => ADMINISTRATIVE_AUDIT.includes(category)) ?? false;
  const businessAudit = administrative
    ? []
    : auditItems.filter((item) => item.to === AUDIT_TRAIL_PATH);
  const administration = [
    ...identityAccessNavigation,
    ...integrationAdminNavigation,
    ...dataImportNavigation,
    ...(administrative ? auditItems : auditItems.filter((item) => item.to !== AUDIT_TRAIL_PATH)),
  ];
  const menus = [
    { id: 'home', label: 'common.menu.home', to: '/', prefixes: [], items: [] } satisfies Menu,
    menu(
      'portfolio',
      'common.menu.portfolio',
      ['/kpis', '/reports/portfolio'],
      portfolioNavigation(user),
    ),
    menu(
      'projects',
      'common.menu.projects',
      ['/projects', ...businessAudit.map((item) => item.to)],
      [...projectNavigation, ...businessAudit],
    ),
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
      ['/dashboards', '/reports'],
      [...(user.userType === 'EXTERNAL' ? [] : dashboardNavigation), ...reportNavigation(user)],
    ),
    menu('notifications', 'common.menu.notifications', ['/notifications'], notificationNavigation),
    menu('administration', 'common.menu.administration', ['/admin'], administration),
    menu('help', 'common.menu.help', ['/help'], helpNavigation),
  ].map((candidate) => {
    const items = candidate.items.filter((item) => permissions.can(...(item.permissions ?? [])));
    return { ...candidate, to: items[0]?.to ?? candidate.to, items };
  });
  return menus.filter((candidate) => candidate.id === 'home' || candidate.items.length > 0);
}

/**
 * The menu the page belongs to: the menu of an item naming this exact rendering (a link with a query), else Home for
 * `/`, otherwise the menu owning the longest matching prefix.
 */
export function activeMenu(menus: Menu[], pathname: string, search = ''): Menu | undefined {
  const rendering = menus.find((candidate) =>
    candidate.items.some((item) => item.to.includes('?') && isCurrent(item.to, pathname, search)),
  );
  if (rendering !== undefined) {
    return rendering;
  }
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
export function activeItem(
  menu: Menu | undefined,
  pathname: string,
  search = '',
): NavigationItem | undefined {
  let best: NavigationItem | undefined;
  for (const item of menu?.items ?? []) {
    const matches =
      item.end === true || item.to.includes('?')
        ? isCurrent(item.to, pathname, search)
        : underPath(pathname, item.to);
    if (matches && item.to.length > (best?.to.length ?? 0)) {
      best = item;
    }
  }
  return best;
}
