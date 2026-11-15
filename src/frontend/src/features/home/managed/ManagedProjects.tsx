import { type ReactElement, type ReactNode, useCallback, useState } from 'react';
import { Link } from 'react-router';

import { useMyUpdates } from '@/features/external-participation/monthly/myUpdatesStore.ts';
import { projectsApi } from '@/features/projects/api/projectsApi.ts';
import { type ProjectFollowUp, type ProjectSummary } from '@/features/projects/api/types.ts';
import { OnPlanBadge } from '@/features/projects/components/FollowUpBadges.tsx';
import { languageTag } from '@/features/projects/presentation.ts';
import { readInBatches } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { NoteList } from '@/shared/ui/NoteList.tsx';
import { SelectAllHeader, SelectRowCell } from '@/shared/ui/rowSelection.tsx';
import { SortHeader } from '@/shared/ui/SortHeader.tsx';
import { EmptyState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';
import { useRowSelection } from '@/shared/ui/useRowSelection.ts';

import {
  DEFAULT_MANAGED_SORT,
  filterRows,
  MANAGED_CHIPS,
  MANAGED_PAGE_SIZE,
  type ManagedChip,
  type ManagedRow,
  type ManagedSort,
  managedProjectsApi,
  needsAction,
  sortRows,
} from './managedProjects.ts';

/** Every project the person manages, with what waits on each: one list read and one attention read, whatever the number of projects. */
async function readManaged(userId: string, signal: AbortSignal) {
  const [projects, attention] = await Promise.all([
    projectsApi.listAll({ projectManagerUserId: userId }, signal),
    // The projects still list when the attention cannot be read; their "needs action" is then unknown, not zero.
    managedProjectsApi.attention(signal).catch(() => null),
  ]);
  return { projects, attention };
}

/** The next action's date for the rows on screen only: the list's summaries do not carry it, so each shown project's follow-up is read. */
async function readFollowUps(ids: readonly string[], signal: AbortSignal) {
  const read = await readInBatches([...ids], async (id) => {
    try {
      return [[id, (await projectsApi.get(id, signal)).data.followUp ?? null] as const];
    } catch {
      return [[id, null] as const];
    }
  });
  return new Map<string, ProjectFollowUp | null>(read);
}

/** The follow-ups of the rows on screen, named by their ids joined with commas; read again only when they change. */
function useFollowUps(key: string) {
  const load = useCallback(
    (signal: AbortSignal) => readFollowUps(key === '' ? [] : key.split(','), signal),
    [key],
  );
  return useApiResource(load, { keepPreviousResult: true });
}

function ProjectCell({ project }: { project: ProjectSummary }): ReactElement {
  return (
    <>
      <Link to={`/projects/${project.id}`} lang={languageTag(project.title.language)} dir="auto">
        {project.title.text}
      </Link>
      {project.formalProjectId !== null && (
        <span className="managed__number" dir="ltr">
          {project.formalProjectId}
        </span>
      )}
    </>
  );
}

/** What needs action on the project this month, each item opening its list (point 01); a dash when nothing does. */
function ActionItems({ row }: { row: ManagedRow }): ReactElement {
  const { t } = useI18n();
  const { project, attention, requestsWaiting } = row;
  if (attention === null && requestsWaiting === 0) {
    return <span className="value--unavailable">{t('home.managed.attentionUnknown')}</span>;
  }
  const items: ReactNode[] = [];
  if (attention?.updateDueDate != null) {
    items.push(
      <Link key="update" to={`/projects/${project.id}/progress`}>
        {t('home.managed.updateDue', { date: attention.updateDueDate })}
      </Link>,
    );
  }
  if ((attention?.overdueTasks ?? 0) > 0) {
    items.push(
      <Link key="tasks" to={`/projects/${project.id}/tasks`}>
        {t('home.managed.overdueTasks', { count: attention?.overdueTasks ?? 0 })}
      </Link>,
    );
  }
  if ((attention?.overdueMilestones ?? 0) > 0) {
    items.push(
      <Link key="milestones" to={`/projects/${project.id}/milestones`}>
        {t('home.managed.overdueMilestones', { count: attention?.overdueMilestones ?? 0 })}
      </Link>,
    );
  }
  return items.length === 0 ? (
    <span>{t('home.managed.nothingDue')}</span>
  ) : (
    <NoteList className="home__actions-due">{items}</NoteList>
  );
}

/**
 * The projects the person manages (the 5 Nov 2026 review §3 row 12; review round 8, point 13): decided by the data and no role code, shown when
 * any project names them as manager. Filter chips (needs action this month, late or stalled, no stage plan), a search by name or number,
 * every column sortable with "needs action first" by default, and pages of 20. Each row opens the project; each count opens its list.
 */
export function ManagedProjects({ userId }: { userId: string }): ReactElement | null {
  const { t, language } = useI18n();
  const { page: waiting } = useMyUpdates();
  const managed = useApiResource(
    useCallback((signal: AbortSignal) => readManaged(userId, signal), [userId]),
  );
  const [chips, setChips] = useState<ReadonlySet<ManagedChip>>(new Set());
  const [search, setSearch] = useState('');
  const [sort, setSort] = useState<ManagedSort>(DEFAULT_MANAGED_SORT);
  const [page, setPage] = useState(1);
  const selection = useRowSelection();

  // The React Compiler memoizes these derivations; no manual useMemo.
  const attention = new Map(
    (managed.data?.attention?.items ?? []).map((item) => [item.projectId, item]),
  );
  const waitingByProject = new Map<string, number>();
  (waiting?.items ?? []).forEach((item) => {
    waitingByProject.set(item.projectId, (waitingByProject.get(item.projectId) ?? 0) + 1);
  });
  const rows: ManagedRow[] = (managed.data?.projects ?? []).map((project) => ({
    project,
    attention: attention.get(project.id) ?? null,
    requestsWaiting: waitingByProject.get(project.id) ?? 0,
  }));

  const shown = sortRows(filterRows(rows, chips, search), sort, language);
  const pageCount = Math.max(1, Math.ceil(shown.length / MANAGED_PAGE_SIZE));
  const current = Math.min(page, pageCount);
  const visible = shown.slice((current - 1) * MANAGED_PAGE_SIZE, current * MANAGED_PAGE_SIZE);
  const visibleKey = visible.map((row) => row.project.id).join(',');
  const followUps = useFollowUps(visibleKey);

  if (managed.data === undefined || managed.data.projects.length === 0) {
    return null;
  }
  const notAvailable = t('projects.followUp.notAvailable');
  const counts = Object.fromEntries(
    MANAGED_CHIPS.map((chip) => [chip, filterRows(rows, new Set([chip]), '').length]),
  ) as Record<ManagedChip, number>;
  const toggle = (chip: ManagedChip) => {
    const next = new Set(chips);
    if (next.has(chip)) {
      next.delete(chip);
    } else {
      next.add(chip);
    }
    setChips(next);
    setPage(1);
  };
  const resort = (next: ManagedSort) => {
    setSort(next);
    setPage(1);
  };

  return (
    <section className="home__work managed" aria-labelledby="home-managed">
      <h2 id="home-managed">{t('home.managed.title')}</h2>
      <div className="managed__controls">
        <div className="managed__chips" role="group" aria-label={t('home.managed.chipsLabel')}>
          {MANAGED_CHIPS.map((chip) => (
            <button
              key={chip}
              type="button"
              className="chip-toggle"
              aria-pressed={chips.has(chip)}
              onClick={() => {
                toggle(chip);
              }}
            >
              {t(`home.managed.chips.${chip}`)}{' '}
              <span className="chip-toggle__count" dir="ltr">
                {counts[chip]}
              </span>
            </button>
          ))}
        </div>
        <label className="managed__search">
          <span className="visually-hidden">{t('home.managed.searchLabel')}</span>
          <input
            type="search"
            className="field__input"
            placeholder={t('home.managed.searchPlaceholder')}
            value={search}
            onChange={(event) => {
              setSearch(event.target.value);
              setPage(1);
            }}
          />
        </label>
      </div>
      {managed.data.attention === null && (
        <p className="form__note">{t('home.managed.attentionUnavailable')}</p>
      )}
      <p className="list-order" role="status">
        {t('home.managed.showing', { shown: shown.length, total: rows.length })}
      </p>
      {shown.length === 0 ? (
        <EmptyState
          title={t('home.managed.noMatch')}
          reason="filter"
          action={{
            label: t('common.filters.clear'),
            onClick: () => {
              setChips(new Set());
              setSearch('');
              setPage(1);
            },
          }}
        />
      ) : (
        <>
          <TableContainer
            caption={t('home.managed.title')}
            selection={{ selection, exportName: 'managed-projects' }}
          >
            <thead>
              <tr>
                <SelectAllHeader
                  ids={visible.map(({ project }) => project.id)}
                  selection={selection}
                />
                <SortHeader
                  label={t('projects.fields.title')}
                  column="title"
                  sort={sort}
                  onSort={resort}
                />
                <SortHeader
                  label={t('home.managed.needsAction')}
                  column="needsAction"
                  sort={sort}
                  onSort={resort}
                  firstDirection="descending"
                />
                <SortHeader
                  label={t('projects.followUp.onPlan')}
                  column="onPlan"
                  sort={sort}
                  onSort={resort}
                  firstDirection="descending"
                />
                <SortHeader
                  label={t('projects.followUp.daysRemaining')}
                  column="daysRemaining"
                  sort={sort}
                  onSort={resort}
                />
                <SortHeader
                  label={t('home.managed.nextActionDue')}
                  column="nextAction"
                  sort={sort}
                  onSort={resort}
                  firstDirection="descending"
                />
                <SortHeader
                  label={t('home.managed.requestsWaiting')}
                  column="requestsWaiting"
                  sort={sort}
                  onSort={resort}
                  firstDirection="descending"
                />
              </tr>
            </thead>
            <tbody>
              {visible.map((row) => {
                const { project, requestsWaiting } = row;
                const indicators = project.indicators;
                const followUp = followUps.data?.get(project.id);
                return (
                  <tr key={project.id} data-needs-action={needsAction(row) ? 'true' : undefined}>
                    <SelectRowCell
                      id={project.id}
                      name={project.title.text}
                      selection={selection}
                    />
                    <td>
                      <ProjectCell project={project} />
                    </td>
                    <td>
                      <ActionItems row={row} />
                    </td>
                    <td>
                      <OnPlanBadge status={indicators?.onPlan} />
                    </td>
                    <td>
                      {indicators?.daysRemaining == null
                        ? notAvailable
                        : t('projects.followUp.daysValue', { days: indicators.daysRemaining })}
                    </td>
                    <td>
                      <span dir="ltr">{followUp?.nextActionDueDate ?? notAvailable}</span>
                      {indicators?.actionDue === 'OVERDUE' && (
                        <>
                          {' '}
                          <StatusBadge
                            label={t('projects.followUp.actionOverdue')}
                            tone="negative"
                          />
                        </>
                      )}
                    </td>
                    <td className="cell--ltr">
                      {requestsWaiting === 0 ? (
                        <span dir="ltr">0</span>
                      ) : (
                        <Link
                          to={`/projects/${project.id}/external-requests`}
                          dir="ltr"
                          title={t('home.managed.requestsWaitingHint')}
                        >
                          {requestsWaiting}
                        </Link>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </TableContainer>
          <Pagination
            page={current}
            pageSize={MANAGED_PAGE_SIZE}
            totalCount={shown.length}
            onPageChange={setPage}
          />
        </>
      )}
    </section>
  );
}
