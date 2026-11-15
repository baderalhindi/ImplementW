import { type TranslationKey } from '@/shared/i18n/i18n.ts';
import { DASHBOARDS_PATH, PORTFOLIO_DASHBOARD_PATH } from '@/features/dashboards/paths.ts';
import { EXTERNAL_REQUESTS_PATH } from '@/features/external-participation/paths.ts';
import { type PermissionCode } from '@/features/identity-access/permissions/permissionCodes.ts';

// The welcome banner's quick actions (review round 8, point 08): the catalogue each person chooses up to six from, grouped as the client asked, and
// each offered only to someone allowed to do it. Which role may have which action follows the RACI matrix later; the checks below are the ones the
// screens themselves make today.

/** Review round 8, point 08: up to six. */
export const MAX_QUICK_ACTIONS = 6;

export type QuickActionGroup = 'open' | 'create' | 'follow';

export const QUICK_ACTION_GROUPS: readonly QuickActionGroup[] = ['open', 'create', 'follow'];

/** What the person may do, read once for the whole catalogue. */
export interface QuickActionContext {
  can: (...codes: readonly PermissionCode[]) => boolean;
  external: boolean;
  /** Whether the dashboard catalogue offers this person the Portfolio Dashboard (its audience is its roles, ADR-006). */
  portfolioDashboard: boolean;
  /** The Executive has one dashboard, the Portfolio Dashboard named "اللوحة التنفيذية" (review round 8, point 14). */
  executive: boolean;
}

export interface QuickAction {
  code: string;
  group: QuickActionGroup;
  label: TranslationKey;
  /** A name that depends on who looks: the Executive's dashboard (point 14). */
  labelFor?: (context: QuickActionContext) => TranslationKey;
  /** A page to open; or, for an action done inside one project, the project's tab, opened after the person picks the project. */
  target:
    { to: string } | { projectTab: 'progress' | 'financials' | 'risks' | 'issues-challenges' };
  allowed: (context: QuickActionContext) => boolean;
}

const label = (code: string) => `home.quickActions.actions.${code}` as TranslationKey;

const action = (
  code: string,
  group: QuickActionGroup,
  target: QuickAction['target'],
  allowed: QuickAction['allowed'],
): QuickAction => ({ code, group, label: label(code), target, allowed });

/** The catalogue, in the order the picker lists it. */
export const QUICK_ACTIONS: readonly QuickAction[] = [
  action('projectRegister', 'open', { to: '/projects' }, ({ can }) => can('PROJECT_VIEW')),
  {
    ...action(
      'portfolioDashboard',
      'open',
      { to: PORTFOLIO_DASHBOARD_PATH },
      (c) => c.portfolioDashboard,
    ),
    labelFor: (c) => (c.executive ? 'dashboards.landing.R07' : label('portfolioDashboard')),
  },
  // The Executive's one dashboard is the one above (point 14).
  action(
    'dashboards',
    'open',
    { to: DASHBOARDS_PATH },
    (c) => !c.external && !c.executive && c.can('PROJECT_VIEW'),
  ),
  action('reports', 'open', { to: '/reports' }, ({ can }) => can('PROJECT_VIEW')),
  action('notificationPreferences', 'open', { to: '/notifications/preferences' }, () => true),
  action('createProject', 'create', { to: '/projects/new' }, ({ can }) => can('PROJECT_REGISTER')),
  action('importProjects', 'create', { to: '/projects/import' }, ({ can }) =>
    can('PROJECT_IMPORT', 'PROJECT_IMPORT_APPROVE'),
  ),
  action('raiseChangeRequest', 'create', { to: '/change-requests/new' }, ({ can }) =>
    can('CHANGE_REQUEST_RAISE'),
  ),
  action('registerRisk', 'create', { projectTab: 'risks' }, ({ can }) => can('RISK_MANAGE')),
  action('raiseIssue', 'create', { projectTab: 'issues-challenges' }, ({ can }) =>
    can('CONCERN_RAISE'),
  ),
  action('submitProgress', 'follow', { projectTab: 'progress' }, ({ can }) =>
    can('PROGRESS_SUBMIT'),
  ),
  action('submitFinancials', 'follow', { projectTab: 'financials' }, ({ can }) =>
    can('FINANCIAL_SUBMIT'),
  ),
  action('followUpList', 'follow', { to: '/projects/follow-up' }, ({ can }) => can('PROJECT_VIEW')),
  action('approvalsInbox', 'follow', { to: '/approvals/inbox' }, ({ can }) =>
    can('APPROVAL_DECIDE'),
  ),
  action('entityUpdateRequests', 'follow', { to: EXTERNAL_REQUESTS_PATH }, ({ can }) =>
    can('EXTERNAL_REQUEST_VIEW'),
  ),
  action('tasks', 'follow', { to: '/tasks' }, ({ can }) => can('TASK_UPDATE')),
  action('notifications', 'follow', { to: '/notifications' }, () => true),
];

/** The six buttons every role had before this point, in their order: what a person sees until they choose, and what "restore default" brings back. */
const ROLE_SET = [
  'createProject',
  'raiseChangeRequest',
  'projectRegister',
  'portfolioDashboard',
  'dashboards',
  'notificationPreferences',
];

const BY_CODE = new Map(QUICK_ACTIONS.map((entry) => [entry.code, entry]));

/** The actions this person may do, in the catalogue's order. */
export function availableQuickActions(context: QuickActionContext): QuickAction[] {
  return QUICK_ACTIONS.filter((entry) => entry.allowed(context));
}

/** The role's set: the actions of the set this person may do. */
export function defaultQuickActionCodes(context: QuickActionContext): string[] {
  return ROLE_SET.filter((code) => BY_CODE.get(code)?.allowed(context) === true);
}

/**
 * The buttons to show, in order: the person's choice, or the role's set while they have none. An action they may no longer do, or one the catalogue
 * no longer has, is left out, so its slot is free again.
 */
export function shownQuickActions(
  saved: readonly string[] | null,
  context: QuickActionContext,
): QuickAction[] {
  return (saved ?? defaultQuickActionCodes(context))
    .map((code) => BY_CODE.get(code))
    .filter((entry): entry is QuickAction => entry?.allowed(context) === true)
    .slice(0, MAX_QUICK_ACTIONS);
}

/** The action's name for this person. */
export function quickActionLabel(entry: QuickAction, context: QuickActionContext): TranslationKey {
  return entry.labelFor?.(context) ?? entry.label;
}
