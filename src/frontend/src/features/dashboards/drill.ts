// A widget's drill target is a canonical screen id (TASK-069 D-4; FG-01 §5.2); the target re-authorises whoever
// arrives (BR-DSH-011). A screen is linked only where it is built and takes this context: a project's tabs need the
// project, the cross-project registers need none. SCR-027 Projects Requiring Attention and SCR-028 Delayed Projects
// are not built, and SCR-040 is the page the Project Dashboard is already on, so neither is linked.

const PROJECT_SCREENS: Readonly<Record<string, string>> = {
  'SCR-049': 'financials',
  'SCR-050': 'kpis',
  'SCR-080': 'risks',
};

const PORTFOLIO_SCREENS: Readonly<Record<string, string>> = {
  'SCR-025': '/projects',
  'SCR-080': '/risks',
};

/** The address a widget drills to in its context, or null when there is no built screen to open. */
export function drillPath(screenId: string | null, projectId: string | null): string | null {
  if (screenId === null) {
    return null;
  }
  if (projectId !== null) {
    const tab = PROJECT_SCREENS[screenId];
    return tab === undefined ? null : `/projects/${projectId}/${tab}`;
  }
  return PORTFOLIO_SCREENS[screenId] ?? null;
}
