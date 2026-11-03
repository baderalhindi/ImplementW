import { type ReactElement, type SyntheticEvent, useCallback, useState } from 'react';
import { Link, useNavigate } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useListParams } from '@/shared/api/useListParams.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { FilterBar, type FilterBarField } from '@/shared/ui/FilterBar.tsx';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { isInternal } from '../access.ts';
import { projectsApi } from '../api/projectsApi.ts';
import { type ProjectSummary } from '../api/types.ts';
import { GapValue, ImportedLabel, OnPlanBadge } from '../components/FollowUpBadges.tsx';
import { ProjectStatusBadge } from '../components/ProjectStatusBadge.tsx';
import { isProjectStatus, PROJECT_STATUSES } from '../presentation.ts';
import { isForbidden, projectProblemMessage } from '../problems.ts';
import {
  type ProjectCatalogueCode,
  type ProjectLookups,
  useProjectLookups,
} from '../useProjectLookups.ts';

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
] as const;

/** The register's filter bar parameters besides `q`, `status` and the two organization ones (the 2 Nov 2026 review, R3-1). */
const ITEM_FILTERS = [
  'classificationItemId',
  'strategicPortfolioItemId',
  'strategicInitiativeItemId',
  'priorityItemId',
  'regionItemId',
  'cityItemId',
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
  const q = params.get('q') ?? '';
  const statusParam = params.get('status') ?? '';
  const status = isProjectStatus(statusParam) ? statusParam : undefined;
  const departmentId = mine ? undefined : (params.get('departmentId') ?? undefined);
  const externalEntityId = mine ? undefined : (params.get('externalEntityId') ?? undefined);
  const projectManagerUserId = mine ? user.id : undefined;
  const itemFilters = Object.fromEntries(
    ITEM_FILTERS.map((name) => [name, mine ? undefined : (params.get(name) ?? undefined)]),
  ) as Record<(typeof ITEM_FILTERS)[number], string | undefined>;
  const itemKey = ITEM_FILTERS.map((name) => itemFilters[name] ?? '').join('|');

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
        <ProjectListEmpty filtered={false} mine={mine} basePath={basePath} />
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
  values: Partial<Record<string, string | undefined>>;
  organizationFilters: boolean;
  /** An external user sees one entity's projects only (ADR-013), so has nothing to filter by. */
  entityFilter: boolean;
  lookups: ProjectLookups;
  basePath: string;
  onSearch: (text: string) => void;
  onFilter: (changes: Record<string, string>) => void;
}

/**
 * The register's search and the one filter bar the dashboards use (the 2 Nov 2026 review, R3-1): sector, entity, department, region and
 * city in its first row, status, priority, strategic portfolio and initiative under More filters. A filter applies as it is chosen. Keyed on
 * the search text, so Back resets the search box to what the list is showing.
 */
function ProjectFilters({
  q,
  values,
  organizationFilters,
  entityFilter,
  lookups,
  basePath,
  onSearch,
  onFilter,
}: ProjectFiltersProps): ReactElement {
  const { t } = useI18n();
  const navigate = useNavigate();
  const [text, setText] = useState(q);

  const applySearch = (event: SyntheticEvent) => {
    event.preventDefault();
    onSearch(text.trim());
  };

  // A department or entity filter needs their names; without them it is not offered (F-1).
  const organizationReadable = lookups.organizationError === null;
  const items = (code: ProjectCatalogueCode, parent?: string) =>
    lookups
      .options(code, parent)
      .filter((option) => parent === undefined || lookups.parentOf(option.value) === parent);
  const fields: FilterBarField[] = organizationFilters
    ? [
        {
          name: 'classificationItemId',
          label: t('dashboards.filter.classificationItemId'),
          all: t('dashboards.filter.all.classificationItemId'),
          options: items('PROJECT_CLASSIFICATION'),
        },
        ...(entityFilter && organizationReadable
          ? [
              {
                name: 'externalEntityId',
                label: t('dashboards.filter.externalEntityId'),
                all: t('dashboards.filter.all.externalEntityId'),
                options: lookups.entityOptions,
              },
            ]
          : []),
        ...(organizationReadable
          ? [
              {
                name: 'departmentId',
                label: t('dashboards.filter.departmentId'),
                all: t('dashboards.filter.all.departmentId'),
                options: lookups.departmentOptions,
              },
            ]
          : []),
        {
          name: 'regionItemId',
          label: t('dashboards.filter.regionItemId'),
          all: t('dashboards.filter.all.regionItemId'),
          options: items('REGION'),
        },
        {
          name: 'cityItemId',
          label: t('dashboards.filter.cityItemId'),
          all: t('dashboards.filter.all.cityItemId'),
          options: items('CITY', values.regionItemId),
        },
        {
          name: 'status',
          more: true,
          label: t('dashboards.filter.status'),
          all: t('dashboards.filter.all.status'),
          options: STATUS_OPTIONS(t),
        },
        {
          name: 'priorityItemId',
          more: true,
          label: t('dashboards.filter.priorityItemId'),
          all: t('dashboards.filter.all.priorityItemId'),
          options: items('PRIORITY'),
        },
        {
          name: 'strategicPortfolioItemId',
          more: true,
          label: t('dashboards.filter.strategicPortfolioItemId'),
          all: t('dashboards.filter.all.strategicPortfolioItemId'),
          options: items('STRATEGIC_PORTFOLIO'),
        },
        {
          name: 'strategicInitiativeItemId',
          more: true,
          label: t('dashboards.filter.strategicInitiativeItemId'),
          all: t('dashboards.filter.all.strategicInitiativeItemId'),
          options:
            values.strategicPortfolioItemId === undefined
              ? []
              : items('STRATEGIC_INITIATIVE', values.strategicPortfolioItemId),
        },
      ]
    : [
        {
          name: 'status',
          label: t('dashboards.filter.status'),
          all: t('dashboards.filter.all.status'),
          options: STATUS_OPTIONS(t),
        },
      ];
  return (
    <>
      <form
        className="filters filters--inline"
        role="search"
        aria-label={t('common.actions.search')}
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
        <div className="filters__actions">
          <button type="submit" className="button">
            {t('common.actions.search')}
          </button>
        </div>
      </form>
      <FilterBar
        fields={fields}
        values={values}
        onChange={(name, value) => {
          // A city lies in a region, an initiative in a portfolio: another parent drops the child chosen under the last one.
          onFilter({
            [name]: value ?? '',
            ...(name === 'regionItemId' ? { cityItemId: '' } : {}),
            ...(name === 'strategicPortfolioItemId' ? { strategicInitiativeItemId: '' } : {}),
          });
        }}
        onClearAll={() => {
          void navigate(basePath);
        }}
      />
    </>
  );
}

function STATUS_OPTIONS(t: ReturnType<typeof useI18n>['t']) {
  return PROJECT_STATUSES.map((value) => ({ value, label: t(`projects.status.${value}`) }));
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
