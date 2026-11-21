import { type TranslationKey } from '@/shared/i18n/i18n.ts';
import { type MenuScreens } from '@/shared/navigation/menuScreens.ts';
import { DASHBOARDS_PATH, PORTFOLIO_DASHBOARD_PATH } from '@/features/dashboards/paths.ts';
import {
  EXTERNAL_REQUESTS_PATH,
  NEW_UPDATE_BATCH_PATH,
} from '@/features/external-participation/paths.ts';
import { type PermissionCode } from '@/features/identity-access/permissions/permissionCodes.ts';

// The welcome banner's quick actions (review round 8, point 08): the catalogue each person chooses up to six from, each offered only to someone
// allowed to do it. 21 Nov 2026 (the user: "add all the possible options, for each user all the buttons of their user, and organize them"): the
// catalogue is everything this person can do from the platform — first what they can create or submit, then every screen of every menu they
// have, grouped under the menu's name in the menus' order. The screens come from the master navigation (app/menus.ts, through MenuScreensContext),
// so a screen the person may not open is never offered and a new screen is offered as soon as it is in a menu.

/** Review round 8, point 08: up to six. */
export const MAX_QUICK_ACTIONS = 6;

/** What the person may do, read once for the whole catalogue. */
export interface QuickActionContext {
  can: (...codes: readonly PermissionCode[]) => boolean;
  /** The menus and screens the master navigation offers this person, in its order. */
  menus: readonly MenuScreens[];
}

/** A heading of the catalogue: "Create and submit", or a menu's name. */
export interface QuickActionGroup {
  key: string;
  label: TranslationKey;
}

export interface QuickAction {
  code: string;
  group: QuickActionGroup;
  label: TranslationKey;
  /** A page to open; or, for an action done inside one project, the project's tab, opened after the person picks the project. */
  target:
    { to: string } | { projectTab: 'progress' | 'financials' | 'risks' | 'issues-challenges' };
}

const CREATE_GROUP: QuickActionGroup = { key: 'create', label: 'home.quickActions.groups.create' };

/** What can be created or submitted, each with the permission its form checks: the first heading of the catalogue. */
const CREATE_ACTIONS: readonly {
  code: string;
  label: TranslationKey;
  target: QuickAction['target'];
  permissions: readonly PermissionCode[];
}[] = [
  {
    code: 'createProject',
    label: 'home.quickActions.actions.createProject',
    target: { to: '/projects/new' },
    permissions: ['PROJECT_REGISTER'],
  },
  {
    code: 'importProjects',
    label: 'home.quickActions.actions.importProjects',
    target: { to: '/projects/import' },
    permissions: ['PROJECT_IMPORT', 'PROJECT_IMPORT_APPROVE'],
  },
  {
    code: 'raiseChangeRequest',
    label: 'home.quickActions.actions.raiseChangeRequest',
    target: { to: '/change-requests/new' },
    permissions: ['CHANGE_REQUEST_RAISE'],
  },
  {
    code: 'registerRisk',
    label: 'home.quickActions.actions.registerRisk',
    target: { projectTab: 'risks' },
    permissions: ['RISK_MANAGE'],
  },
  {
    code: 'raiseIssue',
    label: 'home.quickActions.actions.raiseIssue',
    target: { projectTab: 'issues-challenges' },
    permissions: ['CONCERN_RAISE'],
  },
  {
    code: 'submitProgress',
    label: 'home.quickActions.actions.submitProgress',
    target: { projectTab: 'progress' },
    permissions: ['PROGRESS_SUBMIT'],
  },
  {
    code: 'submitFinancials',
    label: 'home.quickActions.actions.submitFinancials',
    target: { projectTab: 'financials' },
    permissions: ['FINANCIAL_SUBMIT'],
  },
  {
    code: 'newUpdateBatch',
    label: 'externalParticipation.batch.new',
    target: { to: NEW_UPDATE_BATCH_PATH },
    permissions: ['UPDATE_REQUEST_SEND'],
  },
  {
    code: 'createUser',
    label: 'home.health.createUser',
    target: { to: '/admin/users/new' },
    permissions: ['USER_MANAGE'],
  },
  {
    code: 'createRole',
    label: 'identityAccess.roles.list.create',
    target: { to: '/admin/roles/new' },
    permissions: ['ROLE_MANAGE'],
  },
];

/**
 * The codes the catalogue had before 21 Nov 2026, by the screen they open, so a choice saved then still names the same buttons. Every other screen's
 * code is made from its path (`screenCode`).
 */
const SAVED_SCREEN_CODES: ReadonlyMap<string, string> = new Map([
  ['/projects', 'projectRegister'],
  [PORTFOLIO_DASHBOARD_PATH, 'portfolioDashboard'],
  [DASHBOARDS_PATH, 'dashboards'],
  ['/reports', 'reports'],
  ['/notifications/preferences', 'notificationPreferences'],
  ['/projects/follow-up', 'followUpList'],
  ['/approvals/inbox', 'approvalsInbox'],
  [EXTERNAL_REQUESTS_PATH, 'entityUpdateRequests'],
  ['/tasks', 'tasks'],
  ['/notifications', 'notifications'],
]);

/** The API keeps a code of letters only, starting lower-case, up to 40 (BannerService): a screen's path in camel case, as `adminOrganizationEntities`. */
const MAX_CODE_LENGTH = 40;

export function screenCode(path: string): string {
  const saved = SAVED_SCREEN_CODES.get(path);
  if (saved !== undefined) {
    return saved;
  }
  const words = path.split(/[^A-Za-z]+/).filter((word) => word.length > 0);
  const code = words
    .map((word, index) =>
      index === 0
        ? word.toLowerCase()
        : `${word.charAt(0).toUpperCase()}${word.slice(1).toLowerCase()}`,
    )
    .join('');
  return (code === '' ? 'home' : code).slice(0, MAX_CODE_LENGTH);
}

/** The six buttons every role had before review round 8, point 08, in their order: what a person sees until they choose, and what "restore default" brings back. */
const ROLE_SET = [
  'createProject',
  'raiseChangeRequest',
  'projectRegister',
  'portfolioDashboard',
  'dashboards',
  'notificationPreferences',
];

/** The administrator's set (someone who administers the platform and holds no business data, as the Home tells them apart): the ways in the banner had. */
const ADMINISTRATOR_SET = [
  'createUser',
  'adminRoleAssignments',
  'adminPermissionMatrix',
  'adminDataMigration',
  'notificationPreferences',
];

/**
 * The actions this person may do, in the catalogue's order: what they may create or submit, then each menu's screens under its name. A screen a
 * create action already opens (Create project, Import projects) is listed once, under Create and submit.
 */
export function availableQuickActions(context: QuickActionContext): QuickAction[] {
  const creates: QuickAction[] = CREATE_ACTIONS.filter((entry) =>
    context.can(...entry.permissions),
  ).map(({ code, label, target }) => ({ code, label, target, group: CREATE_GROUP }));
  const created = new Set(
    CREATE_ACTIONS.flatMap((entry) => ('to' in entry.target ? [entry.target.to] : [])),
  );
  const codes = new Set(creates.map((entry) => entry.code));
  const screens = context.menus.flatMap((menu) => {
    const group: QuickActionGroup = { key: menu.label, label: menu.label };
    return menu.screens.flatMap((screen): QuickAction[] => {
      const code = screenCode(screen.to);
      if (created.has(screen.to) || codes.has(code)) {
        return [];
      }
      codes.add(code);
      return [{ code, group, label: screen.label, target: { to: screen.to } }];
    });
  });
  return [...creates, ...screens];
}

/** The catalogue's headings in order, each with its actions. */
export function quickActionGroups(
  actions: readonly QuickAction[],
): { group: QuickActionGroup; actions: QuickAction[] }[] {
  const groups = new Map<string, { group: QuickActionGroup; actions: QuickAction[] }>();
  for (const entry of actions) {
    const at = groups.get(entry.group.key) ?? { group: entry.group, actions: [] };
    at.actions.push(entry);
    groups.set(entry.group.key, at);
  }
  return [...groups.values()];
}

/** The role's set: the actions of the set this person may do; an administrator with no business data has the administrator's set. */
export function defaultQuickActionCodes(context: QuickActionContext): string[] {
  const available = new Set(availableQuickActions(context).map((entry) => entry.code));
  return (context.can('PROJECT_VIEW') ? ROLE_SET : ADMINISTRATOR_SET).filter((code) =>
    available.has(code),
  );
}

/**
 * The buttons to show, in order: the person's choice, or the role's set while they have none. An action they may no longer do, or one the catalogue
 * no longer has, is left out, so its slot is free again.
 */
export function shownQuickActions(
  saved: readonly string[] | null,
  context: QuickActionContext,
): QuickAction[] {
  const byCode = new Map(availableQuickActions(context).map((entry) => [entry.code, entry]));
  return (saved ?? defaultQuickActionCodes(context))
    .flatMap((code) => byCode.get(code) ?? [])
    .slice(0, MAX_QUICK_ACTIONS);
}
