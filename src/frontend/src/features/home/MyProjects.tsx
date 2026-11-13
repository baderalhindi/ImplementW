import { type ReactElement, useCallback } from 'react';
import { Link } from 'react-router';

import { useCounted } from '@/features/external-participation/monthly/counts.ts';
import {
  myUpdatesStore,
  useMyUpdates,
} from '@/features/external-participation/monthly/myUpdatesStore.ts';
import { projectsApi } from '@/features/projects/api/projectsApi.ts';
import {
  type ProjectFollowUp,
  type ProjectQuery,
  type ProjectSummary,
} from '@/features/projects/api/types.ts';
import { OnPlanBadge } from '@/features/projects/components/FollowUpBadges.tsx';
import { languageTag } from '@/features/projects/presentation.ts';
import { readInBatches } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { SelectAllHeader, SelectRowCell } from '@/shared/ui/rowSelection.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';
import { useRowSelection } from '@/shared/ui/useRowSelection.ts';

/** How many projects a Home view lists; each costs a read for its follow-up. */
const SHOWN = 25;

interface ProjectRow {
  project: ProjectSummary;
  /** Null when the project's detail cannot be read. */
  followUp: ProjectFollowUp | null;
}

/** The list's summaries carry no follow-up dates, so each shown project's detail is read for them. */
async function readProjectRows(
  query: Omit<ProjectQuery, 'page' | 'pageSize'>,
  signal: AbortSignal,
): Promise<{ rows: ProjectRow[]; total: number }> {
  const projects = await projectsApi.listAll(query, signal);
  const rows = await readInBatches(projects.slice(0, SHOWN), async (project) => {
    try {
      const { data } = await projectsApi.get(project.id, signal);
      return [{ project, followUp: data.followUp ?? null }];
    } catch {
      return [{ project, followUp: null }];
    }
  });
  return { rows, total: projects.length };
}

function ProjectLink({ project }: { project: ProjectSummary }): ReactElement {
  return (
    <Link to={`/projects/${project.id}`} lang={languageTag(project.title.language)} dir="auto">
      {project.title.text}
    </Link>
  );
}

/**
 * The projects the person manages (the 5 Nov 2026 review §3 row 12), decided by the data and no role code: shown when any project names them
 * as its manager, each with its on-plan label, days remaining, next action due and the update requests waiting on them.
 */
export function ManagedProjects({ userId }: { userId: string }): ReactElement | null {
  const { t } = useI18n();
  const { page } = useMyUpdates();
  const managed = useApiResource(
    useCallback(
      (signal: AbortSignal) => readProjectRows({ projectManagerUserId: userId }, signal),
      [userId],
    ),
  );
  const selection = useRowSelection();
  if (managed.data === undefined || managed.data.rows.length === 0) {
    return null;
  }
  const notAvailable = t('projects.followUp.notAvailable');
  return (
    <section className="home__work">
      <h2>{t('home.managed.title')}</h2>
      <TableContainer
        caption={t('home.managed.title')}
        selection={{ selection, exportName: 'managed-projects' }}
      >
        <thead>
          <tr>
            <SelectAllHeader
              ids={managed.data.rows.map(({ project }) => project.id)}
              selection={selection}
            />
            <th scope="col">{t('projects.fields.title')}</th>
            <th scope="col">{t('projects.followUp.onPlan')}</th>
            <th scope="col">{t('projects.followUp.daysRemaining')}</th>
            <th scope="col">{t('home.managed.nextActionDue')}</th>
            <th scope="col">{t('home.managed.requestsWaiting')}</th>
          </tr>
        </thead>
        <tbody>
          {managed.data.rows.map(({ project, followUp }) => {
            const indicators = project.indicators;
            const waiting = (page?.items ?? []).filter(
              (item) => item.projectId === project.id,
            ).length;
            return (
              <tr key={project.id}>
                <SelectRowCell id={project.id} name={project.title.text} selection={selection} />
                <td>
                  <ProjectLink project={project} />
                </td>
                <td>
                  <OnPlanBadge status={indicators?.onPlan} />
                </td>
                <td>
                  {indicators?.daysRemaining === null || indicators?.daysRemaining === undefined
                    ? notAvailable
                    : t('projects.followUp.daysValue', { days: indicators.daysRemaining })}
                </td>
                <td>
                  <span dir="ltr">{followUp?.nextActionDueDate ?? notAvailable}</span>
                  {indicators?.actionDue === 'OVERDUE' && (
                    <>
                      {' '}
                      <StatusBadge label={t('projects.followUp.actionOverdue')} tone="negative" />
                    </>
                  )}
                </td>
                <td dir="ltr" className="cell--ltr">
                  {waiting}
                </td>
              </tr>
            );
          })}
        </tbody>
      </TableContainer>
      {managed.data.total > managed.data.rows.length && (
        <Link className="home__more" to="/projects/mine">
          {t('home.managed.all', { count: managed.data.total })}
        </Link>
      )}
    </section>
  );
}

async function readOwnProjects(signal: AbortSignal) {
  return readProjectRows({}, signal);
}

/**
 * An entity user's Home (the 5 Nov 2026 review §3 row 13): their projects, when each last had an update accepted from them, when the next is
 * due, and the way back into the monthly window.
 */
export function EntityProjects(): ReactElement | null {
  const { t } = useI18n();
  const { page } = useMyUpdates();
  const counted = useCounted();
  const own = useApiResource(readOwnProjects);
  const selection = useRowSelection();
  const due = page?.totalCount ?? 0;
  if (own.data === undefined) {
    return null;
  }
  const none = t('home.entity.none');
  return (
    <section className="home__work">
      <h2>{t('home.entity.title')}</h2>
      {due > 0 && (
        <p>
          <button
            type="button"
            className="button button--primary"
            onClick={() => {
              myUpdatesStore.openWindow();
            }}
          >
            {t('home.entity.openWindow', { projects: counted('dueProjects', due) })}
          </button>
        </p>
      )}
      {own.data.rows.length === 0 ? (
        <p className="home__empty">{t('home.entity.noProjects')}</p>
      ) : (
        <TableContainer
          caption={t('home.entity.title')}
          selection={{ selection, exportName: 'my-projects' }}
        >
          <thead>
            <tr>
              <SelectAllHeader
                ids={own.data.rows.map(({ project }) => project.id)}
                selection={selection}
              />
              <th scope="col">{t('projects.fields.title')}</th>
              <th scope="col">{t('home.entity.lastUpdate')}</th>
              <th scope="col">{t('home.entity.nextDue')}</th>
            </tr>
          </thead>
          <tbody>
            {own.data.rows.map(({ project, followUp }) => {
              const lastUpdate =
                followUp?.declaredSource === 'ENTITY_UPDATE' ? followUp.declaredAsOf : null;
              const next = (page?.items ?? []).find(
                (item) => item.projectId === project.id && item.dueDate !== null,
              );
              return (
                <tr key={project.id}>
                  <SelectRowCell id={project.id} name={project.title.text} selection={selection} />
                  <td>
                    <ProjectLink project={project} />
                  </td>
                  <td dir="ltr" className="cell--ltr">
                    {lastUpdate ?? none}
                  </td>
                  <td dir="ltr" className="cell--ltr">
                    {next?.dueDate ?? none}
                    {next?.overdue === true && (
                      <>
                        {' '}
                        <StatusBadge
                          label={t('externalParticipation.monthly.overdue')}
                          tone="negative"
                        />
                      </>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </TableContainer>
      )}
    </section>
  );
}
