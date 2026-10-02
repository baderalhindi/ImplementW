import { type ReactElement, type SyntheticEvent, useCallback, useState } from 'react';
import { Link } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useListParams } from '@/shared/api/useListParams.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField, TextField } from '@/shared/ui/FormFields.tsx';
import { Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { isInternal } from '../access.ts';
import { projectsApi } from '../api/projectsApi.ts';
import { type ProjectSummary } from '../api/types.ts';
import { ProjectStatusBadge } from '../components/ProjectStatusBadge.tsx';
import { isProjectStatus, PROJECT_STATUSES } from '../presentation.ts';
import { isForbidden, projectProblemMessage } from '../problems.ts';
import { type ProjectLookups, useProjectLookups } from '../useProjectLookups.ts';

/** The filters each list offers; `q` and `status` everywhere, the organization filters on the Register only. */
const FILTERS = ['q', 'status', 'departmentId', 'externalEntityId'] as const;

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
  const { params, page, setFilter, goToPage } = useListParams();
  const lookups = useProjectLookups();
  const q = params.get('q') ?? '';
  const statusParam = params.get('status') ?? '';
  const status = isProjectStatus(statusParam) ? statusParam : undefined;
  const departmentId = mine ? undefined : (params.get('departmentId') ?? undefined);
  const externalEntityId = mine ? undefined : (params.get('externalEntityId') ?? undefined);
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
          page,
          pageSize: PAGE_SIZE,
        },
        signal,
      ),
    [q, status, departmentId, externalEntityId, projectManagerUserId, page],
  );
  const projects = useApiResource(load);
  const managerName = usePersonNames(
    (projects.data?.items ?? []).map((project) => project.projectManagerUserId),
  );
  const filtered = FILTERS.some((name) => (params.get(name) ?? '') !== '');

  return (
    <>
      <ProjectFilters
        key={params.toString()}
        q={q}
        status={status ?? ''}
        departmentId={departmentId ?? ''}
        externalEntityId={externalEntityId ?? ''}
        organizationFilters={!mine}
        entityFilter={!mine && isInternal(user)}
        lookups={lookups}
        filtered={filtered}
        basePath={basePath}
        onSearch={(text) => {
          setFilter('q', text);
        }}
        onFilter={setFilter}
      />
      <p className="list-order">{t('projects.list.order')}</p>

      {projects.loading && <LoadingState label={t('projects.list.loading')} />}
      {/* A role without PROJECT_VIEW sees no project at all: the same answer as a scope that reaches none. */}
      {isForbidden(projects.error) && (
        <ProjectListEmpty filtered={false} mine={mine} basePath={basePath} />
      )}
      {projects.error !== null && !isForbidden(projects.error) && (
        <ErrorState message={projectProblemMessage(projects.error, t)} onRetry={projects.reload} />
      )}
      {projects.data !== undefined &&
        (projects.data.items.length === 0 ? (
          <ProjectListEmpty filtered={filtered} mine={mine} basePath={basePath} />
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
 * Three different empty answers. Filtered: nothing matches, clear the filters. Unfiltered on the Register: the
 * person's role reaches no project at all, which is said as such (acceptance criterion 2). My Projects: they manage
 * none.
 */
function ProjectListEmpty({
  filtered,
  mine,
  basePath,
}: {
  filtered: boolean;
  mine: boolean;
  basePath: string;
}): ReactElement {
  const { t } = useI18n();
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
  return (
    <EmptyState title={t('projects.register.emptyForRole')}>
      <p>{t('projects.register.emptyForRoleDescription')}</p>
    </EmptyState>
  );
}

interface ProjectFiltersProps {
  q: string;
  status: string;
  departmentId: string;
  externalEntityId: string;
  organizationFilters: boolean;
  /** An external user sees one entity's projects only (ADR-013), so has nothing to filter by. */
  entityFilter: boolean;
  lookups: ProjectLookups;
  filtered: boolean;
  basePath: string;
  onSearch: (text: string) => void;
  onFilter: (name: string, value: string) => void;
}

/** Keyed on the URL, so Clear filters or Back resets the search box to what the list is showing. */
function ProjectFilters({
  q,
  status,
  departmentId,
  externalEntityId,
  organizationFilters,
  entityFilter,
  lookups,
  filtered,
  basePath,
  onSearch,
  onFilter,
}: ProjectFiltersProps): ReactElement {
  const { t } = useI18n();
  const [text, setText] = useState(q);

  const applySearch = (event: SyntheticEvent) => {
    event.preventDefault();
    onSearch(text.trim());
  };

  // A department or entity filter needs their names; without ORGANIZATION_VIEW it is not offered (F-1).
  const organizationReadable = lookups.organizationError === null;
  return (
    <form
      className="filters"
      role="search"
      aria-label={t('common.filters.label')}
      onSubmit={applySearch}
    >
      <TextField
        label={t('projects.filters.q')}
        name="q"
        type="search"
        dir="auto"
        value={text}
        onChange={setText}
      />
      <SelectField
        label={t('projects.fields.status')}
        name="status"
        value={status}
        placeholder={t('common.filters.any')}
        options={PROJECT_STATUSES.map((value) => ({ value, label: t(`projects.status.${value}`) }))}
        onChange={(value) => {
          onFilter('status', value);
        }}
      />
      {organizationFilters && organizationReadable && (
        <SelectField
          label={t('projects.fields.department')}
          name="departmentId"
          value={departmentId}
          placeholder={t('common.filters.any')}
          options={lookups.departmentOptions}
          onChange={(value) => {
            onFilter('departmentId', value);
          }}
        />
      )}
      {entityFilter && organizationReadable && (
        <SelectField
          label={t('projects.fields.externalEntity')}
          name="externalEntityId"
          value={externalEntityId}
          placeholder={t('common.filters.any')}
          options={lookups.entityOptions}
          onChange={(value) => {
            onFilter('externalEntityId', value);
          }}
        />
      )}
      <div className="filters__actions">
        <button type="submit" className="button">
          {t('common.actions.search')}
        </button>
        {filtered && (
          <Link className="button" to={basePath}>
            {t('common.filters.clear')}
          </Link>
        )}
      </div>
    </form>
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
            </td>
            <td>
              <ProjectStatusBadge
                status={project.status}
                legacyIntakeDate={project.legacyIntakeDate}
              />
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
