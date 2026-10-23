import { type ReactElement } from 'react';

import { type MenuId } from './menus.ts';

/** The shell's stroke icons: one per menu of the master navigation, and the controls of the rail and the top bar. */
export type ShellIconName =
  MenuId | 'create' | 'chevronDown' | 'globe' | 'collapse' | 'expand' | 'menu' | 'close';

const paths: Record<ShellIconName, ReactElement> = {
  home: <path d="M3 10.5 12 3l9 7.5M5.5 8.5V20h13V8.5M10 20v-5.5h4V20" />,
  portfolio: <path d="M12 3 3 7.5 12 12l9-4.5L12 3zM3 12l9 4.5 9-4.5M3 16.5 12 21l9-4.5" />,
  projects: (
    <path d="M3 6.5A1.5 1.5 0 0 1 4.5 5h4.6l2 2.5h8.4A1.5 1.5 0 0 1 21 9v9.5a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 18.5z" />
  ),
  myWork: (
    <>
      <rect x="5" y="4" width="14" height="17" rx="2" />
      <path d="M9 4V3h6v1M9 12.5l2 2 4-4" />
    </>
  ),
  risksIssues: (
    <path d="M12 3 4.5 6v5.5c0 4.6 3.1 8 7.5 9.5 4.4-1.5 7.5-4.9 7.5-9.5V6zM12 8.5V12M12 15.5h.01" />
  ),
  requestsApprovals: (
    <path d="M3 13.5 5.5 5h13l2.5 8.5V19a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1zM3 13.5h5l1.5 2.5h5l1.5-2.5h5" />
  ),
  documents: (
    <path d="M14 3H6.5A1.5 1.5 0 0 0 5 4.5v15A1.5 1.5 0 0 0 6.5 21h11a1.5 1.5 0 0 0 1.5-1.5V8zM14 3v5h5M8.5 13h7M8.5 16.5h7" />
  ),
  dashboardsReports: <path d="M4 20h16M6.5 20v-6M11 20V8M15.5 20v-9M20 20V5" />,
  notifications: <path d="M6 16v-5a6 6 0 1 1 12 0v5l1.5 2h-15zM10 20.5a2 2 0 0 0 4 0" />,
  administration: <path d="M4 7h10M18 7h2M4 17h4M12 17h8M14 4.5v5M8 14.5v5" />,
  create: <path d="M12 5v14M5 12h14" />,
  chevronDown: <path d="m6 9 6 6 6-6" />,
  globe: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M3 12h18M12 3c2.5 2.6 3.75 5.6 3.75 9S14.5 18.4 12 21c-2.5-2.6-3.75-5.6-3.75-9S9.5 5.6 12 3z" />
    </>
  ),
  collapse: <path d="M4 5h16v14H4zM9 5v14M15 10l-2 2 2 2" />,
  expand: <path d="M4 5h16v14H4zM9 5v14M13 10l2 2-2 2" />,
  menu: <path d="M4 7h16M4 12h16M4 17h16" />,
  close: <path d="M6 6l12 12M18 6 6 18" />,
};

export function ShellIcon({
  name,
  className,
}: {
  name: ShellIconName;
  className?: string;
}): ReactElement {
  return (
    <svg
      className={className ?? 'shell-icon'}
      viewBox="0 0 24 24"
      width="20"
      height="20"
      aria-hidden="true"
      focusable="false"
    >
      <g
        fill="none"
        stroke="currentColor"
        strokeWidth="1.75"
        strokeLinecap="round"
        strokeLinejoin="round"
      >
        {paths[name]}
      </g>
    </svg>
  );
}
