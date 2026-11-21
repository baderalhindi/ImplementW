import { type ReactElement } from 'react';
import { Link, NavLink } from 'react-router';

import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';

import { type WorkspaceTab, type WorkspaceTabKey } from '../access.ts';

import { CHOSEN_TAB, groupOf, rememberedTab, WORKSPACE_GROUPS } from './tabGroups.ts';

/**
 * The project page's tabs (review round 8, point 04): six headings on one row, each shown when one of its tabs is offered, then the tabs of the heading
 * that holds the open one. A heading opens the tab of it this person last used on this project, else its first. Every tab keeps its address.
 */
export function WorkspaceTabs({
  projectId,
  userId,
  tabs,
  current,
  labels,
}: {
  projectId: string;
  userId: string;
  /** The tabs this person is offered on this project. */
  tabs: WorkspaceTab[];
  current: WorkspaceTabKey;
  labels: Record<WorkspaceTabKey, TranslationKey>;
}): ReactElement {
  const { t } = useI18n();
  const pathOf = (tab: WorkspaceTab) =>
    `/projects/${projectId}${tab.path === '' ? '' : `/${tab.path}`}`;
  const currentGroup = groupOf(current);
  const remembered = rememberedTab(userId, projectId);
  const groups = WORKSPACE_GROUPS.map((group) => ({
    ...group,
    offered: group.tabs
      .map((key) => tabs.find((tab) => tab.key === key))
      .filter((tab): tab is WorkspaceTab => tab !== undefined),
  })).filter((group) => group.offered.length > 0);
  const subTabs = groups.find((group) => group.key === currentGroup)?.offered ?? [];

  return (
    <>
      <nav className="tabs tabs--groups" aria-label={t('projects.workspace.groups.label')}>
        <ul>
          {groups.map((group) => {
            const target = group.offered.find((tab) => tab.key === remembered) ?? group.offered[0];
            return (
              <li key={group.key}>
                <Link
                  to={target === undefined ? `/projects/${projectId}` : pathOf(target)}
                  state={CHOSEN_TAB}
                  aria-current={group.key === currentGroup ? 'page' : undefined}
                >
                  {t(`projects.workspace.groups.${group.key}`)}
                </Link>
              </li>
            );
          })}
        </ul>
      </nav>
      {subTabs.length > 1 && (
        <nav className="tabs tabs--sub" aria-label={t('projects.workspace.tabs.label')}>
          <ul>
            {subTabs.map((tab) => (
              <li key={tab.key}>
                {/* Only the overview matches exactly: a tab with pages of its own (progress history) stays current. */}
                <NavLink to={pathOf(tab)} end={tab.path === ''} state={CHOSEN_TAB}>
                  {t(labels[tab.key])}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
      )}
    </>
  );
}
