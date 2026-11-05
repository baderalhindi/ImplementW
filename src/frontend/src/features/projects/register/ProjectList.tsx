import { type ReactElement, useCallback } from 'react';
import { Link } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useListParams } from '@/shared/api/useListParams.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { isInternal } from '../access.ts';
import { projectsApi } from '../api/projectsApi.ts';
import { type ProjectSummary } from '../api/types.ts';
import { GapValue, ImportedLabel, OnPlanBadge } from '../components/FollowUpBadges.tsx';
import { ProjectStatusBadge } from '../components/ProjectStatusBadge.tsx';
import { isForbidden, projectProblemMessage } from '../problems.ts';
import { type ProjectLookups, useProjectLookups } from '../useProjectLookups.ts';
import { CreateProjectLink, ImportProjectsLink } from './ProjectActions.tsx';
import { ProjectFilters } from './ProjectFilters.tsx';
import { useProjectFilterValues } from './projectFilterValues.ts';

/** The filters each list offers; `q` and `status` everywhere, the rest of the filter bar on the Register only. */
const FILTERS = [
  'q',
  'status',
  'departmentId',
  'externalEntityId',
  'classificationItemId',
  'strategicPortfolioItemId',
  'strategicInitiativeItemId',
  'priorityItemId',
  'regionItemId',
  'cityItemId',
  'period',
] as const;

interface ProjectListProps {
  user: SessionUser;
  /** SCR-026: only the projects the signed-in person manages (I-04). */
  mine: boolean;
  /** Where Clear filters returns to. */
  basePath: string;
}

/**
 * SCR-025 Project Register and SCR-026 My Projects: the projects the caller may see, in the API's one order (most
 * recently changed first, indexing-strategy P-2), filtered and paged in the URL (R-29, R-31). The API lists only what
 * the caller's scope reaches (TASK-041, I-01 to I-05); the screen adds nothing and hides nothing.
 */
export function ProjectList({ user, mine, basePath }: ProjectListProps): ReactElement {
  const { t } = useI18n();
  const { params, page, setFilter, setFilters, goToPage } = useListParams();
  const lookups = useProjectLookups();
  const { q, status, departmentId, externalEntityId, itemFilters, itemKey } =
    useProjectFilterValues(mine);
  const projectManagerUserId = mine ? user.id : undefined;

  const load = useCallback(
    (signal: AbortSignal) =>
      projectsApi.list(
        {
          q: q === '' ? undefined : q,
          status,
          departmentId,
          externalEntityId,
          projectManagerUserId,
          ...itemFilters,
          page,
          pageSize: PAGE_SIZE,
        },
        signal,
      ),
    // itemKey stands for itemFilters, rebuilt on every render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [q, status, departmentId, externalEntityId, projectManagerUserId, itemKey, page],
  );
  const projects = useApiResource(load);
  const managerName = usePersonNames(
    (projects.data?.items ?? []).map((project) => project.projectManagerUserId),
  );
  const filtered = FILTERS.some((name) => (params.get(name) ?? '') !== '');

  return (
    <>
      <ProjectFilters
        key={q}
        q={q}
        values={{ status, departmentId, externalEntityId, ...itemFilters }}
        organizationFilters={!mine}
        entityFilter={!mine && isInternal(user)}
        lookups={lookups}
        basePath={basePath}
        onSearch={(text) => {
          setFilter('q', text);
        }}
        onFilter={setFilters}
      />
      <p className="list-order">{t('projects.list.order')}</p>

      {projects.loading && <LoadingState label={t('projects.list.loading')} />}
      {/* A role without PROJECT_VIEW sees no project at all: the same answer as a scope that reaches none. */}
      {isForbidden(projects.error) && (
        <ProjectListEmpty filtered={false} forbidden mine={mine} basePath={basePath} />
      )}
      {projects.error !== null && !isForbidden(projects.error) && (
        <ErrorState
          error={projects.error}
          message={projectProblemMessage(projects.error, t)}
          onRetry={projects.reload}
        />
      )}
      {projects.data !== undefined &&
        (projects.data.items.length === 0 ? (
          <ProjectListEmpty filtered={filtered} forbidden={false} mine={mine} basePath={basePath} />
        ) : (
          <>
            <ProjectTable
              caption={mine ? t('projects.mine.title') : t('projects.register.title')}
              projects={projects.data.items}
              lookups={lookups}
              managerName={managerName}
            />
            <Pagination
              page={projects.data.page}
              pageSize={projects.data.pageSize}
              totalCount={projects.data.totalCount}
              onPageChange={goToPage}
            />
          </>
        ))}
    </>
  );
}

/**
 * Four different empty answers. Filtered: nothing matches, clear the filters. My Projects: they manage none. A role without PROJECT_VIEW:
 * it reaches no project at all, which is said as such (acceptance criterion 2). Otherwise the Register has no project yet (the 5 Nov 2026
 * review §5), with the ways to add one for a role that has them, and otherwise where projects come from.
 */
function ProjectListEmpty({
  filtered,
  forbidden,
  mine,
  basePath,
}: {
  filtered: boolean;
  forbidden: boolean;
  mine: boolean;
  basePath: string;
}): ReactElement {
  const { t } = useI18n();
  const mayAdd = useCan('PROJECT_REGISTER', 'PROJECT_IMPORT', 'PROJECT_IMPORT_APPROVE');
  if (filtered) {
    return (
      <EmptyState title={t('projects.list.emptyFiltered')}>
        <Link to={basePath}>{t('common.filters.clear')}</Link>
      </EmptyState>
    );
  }
  if (mine) {
    return (
      <EmptyState title={t('projects.mine.empty')}>
        <p>{t('projects.mine.emptyDescription')}</p>
        <Link to="/projects">{t('projects.mine.toRegister')}</Link>
      </EmptyState>
    );
  }
  if (forbidden) {
    return (
      <EmptyState title={t('projects.register.emptyForRole')}>
        <p>{t('projects.register.emptyForRoleDescription')}</p>
      </EmptyState>
    );
  }
  return (
    <EmptyState title={t('projects.register.empty')}>
      {mayAdd ? (
        <div className="form__actions">
          <ImportProjectsLink />
          <CreateProjectLink />
        </div>
      ) : (
        <p>{t('projects.register.emptyForRoleDescription')}</p>
      )}
    </EmptyState>
  );
}

function ProjectTable({
  caption,
  projects,
  lookups,
  managerName,
}: {
  caption: string;
  projects: ProjectSummary[];
  lookups: ProjectLookups;
  managerName: (id: string | null) => string;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  return (
    <TableContainer caption={caption}>
      <thead>
        <tr>
          <th scope="col">{t('projects.fields.title')}</th>
          <th scope="col">{t('projects.fields.formalProjectId')}</th>
          <th scope="col">{t('projects.fields.status')}</th>
          <th scope="col">{t('projects.followUpList.onPlan')}</th>
          <th scope="col">{t('projects.followUpList.gap')}</th>
          <th scope="col">{t('projects.fields.department')}</th>
          <th scope="col">{t('projects.fields.participation')}</th>
          <th scope="col">{t('projects.fields.projectManager')}</th>
          <th scope="col">{t('projects.fields.updatedAt')}</th>
        </tr>
      </thead>
      <tbody>
        {projects.map((project) => (
          <tr key={project.id}>
            <td>
              <Link
                to={`/projects/${project.id}`}
                lang={project.title.language.toLowerCase()}
                dir="auto"
              >
                {project.title.text}
              </Link>
            </td>
            <td dir="ltr" className="cell--ltr">
              {project.formalProjectId ?? (
                <span className="cell__aside">{t('projects.formalProjectId.notIssued')}</span>
              )}
              {project.legacyReference !== null && project.legacyReference !== undefined && (
                <span className="cell__aside">{project.legacyReference}</span>
              )}
            </td>
            <td>
              <ProjectStatusBadge
                status={project.status}
                legacyIntakeDate={project.legacyIntakeDate}
              />{' '}
              <ImportedLabel batchId={project.projectImportBatchId} />
            </td>
            <td>
              <OnPlanBadge status={project.indicators?.onPlan} />
            </td>
            <td>
              <GapValue points={project.indicators?.gapPoints} />
            </td>
            <td>{lookups.departmentName(project.departmentId)}</td>
            <td>
              {t(`projects.participationMode.${project.participationMode}`)}
              {project.externalEntityId !== null && (
                <span className="cell__aside">{lookups.entityName(project.externalEntityId)}</span>
              )}
            </td>
            <td>
              {project.projectManagerUserId === null
                ? t('projects.projectManager.notNamed')
                : managerName(project.projectManagerUserId)}
            </td>
            <td>{formatDateTime(project.updatedAt)}</td>
          </tr>
        ))}
      </tbody>
    </TableContainer>
  );
}
