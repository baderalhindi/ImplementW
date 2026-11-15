import { type WorkspaceTab, type WorkspaceTabKey } from '../access.ts';

// Review round 8, point 04: the project page's tabs under six headings, each opening its own tabs. The tabs keep their addresses, so a link to
// /projects/{id}/progress still opens Progress; the heading that holds the open tab is the current one.

export type WorkspaceGroupKey =
  'overview' | 'planning' | 'performance' | 'risks' | 'requests' | 'records';

export const WORKSPACE_GROUPS: { key: WorkspaceGroupKey; tabs: WorkspaceTabKey[] }[] = [
  { key: 'overview', tabs: ['overview'] },
  { key: 'planning', tabs: ['schedule', 'tasks', 'milestones'] },
  { key: 'performance', tabs: ['progress', 'financials', 'kpis'] },
  { key: 'risks', tabs: ['risks', 'issuesChallenges'] },
  { key: 'requests', tabs: ['changeRequests', 'suspension', 'closeout', 'externalRequests'] },
  {
    key: 'records',
    tabs: ['registration', 'location', 'documents', 'reviews', 'activity', 'audit'],
  },
];

/** A tab chosen on the page, not arrived at by a link: the workspace keeps it rather than reopening the last tab used. */
export const CHOSEN_TAB = { chosenTab: true } as const;

/** The group a tab is under. */
export function groupOf(tab: WorkspaceTabKey): WorkspaceGroupKey {
  return WORKSPACE_GROUPS.find((group) => group.tabs.includes(tab))?.key ?? 'overview';
}

/** The tab an address opens: the first segment after /projects/{id}, the overview when there is none. */
export function tabOfPath(
  pathname: string,
  projectId: string,
  tabs: readonly WorkspaceTab[],
): WorkspaceTab | undefined {
  const rest = pathname.split(`/projects/${projectId}`)[1] ?? '';
  const segment = rest.split('/').find((part) => part !== '') ?? '';
  return tabs.find((tab) => tab.path === segment);
}

function storageKey(userId: string, projectId: string): string {
  return `pmplatform.projectTab.${userId}.${projectId}`;
}

/** The last tab this person opened on this project, kept in this browser only. */
export function rememberedTab(userId: string, projectId: string): string | null {
  try {
    return localStorage.getItem(storageKey(userId, projectId));
  } catch {
    return null;
  }
}

export function rememberTab(userId: string, projectId: string, tab: WorkspaceTabKey): void {
  try {
    localStorage.setItem(storageKey(userId, projectId), tab);
  } catch {
    // Storage may be unavailable (a private window); the project then opens on its overview.
  }
}
