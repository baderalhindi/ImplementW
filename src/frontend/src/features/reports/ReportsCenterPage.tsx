import { type ReactElement, useCallback } from 'react';
import { Link } from 'react-router';

import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useListParams } from '@/shared/api/useListParams.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { FilterBar } from '@/shared/ui/FilterBar.tsx';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { reportsApi } from './api/reportsApi.ts';
import { type ReportCatalogueEntry } from './api/types.ts';
import { ReportList } from './components/ReportList.tsx';
import {
  entryLanguage,
  FAMILIES,
  FAMILY_ORDER,
  familyOfSlug,
  jobTone,
  viewPath,
} from './presentation.ts';
import { reportProblemMessage } from './problems.ts';

// SCR-130 Reports Center (FG-02 §13.1): the reports the person's roles may run — searched by name, code or description
// and narrowed by experience — with their latest saved views and exports. A catalogue entry shows what the report is
// for and how to open it, never business data. Scheduled reports are not built (reports.md F-7).

/** Latest saved views and exports shown on the landing. */
const RECENT_COUNT = 5;

function matches(entry: ReportCatalogueEntry, query: string): boolean {
  const needle = query.trim().toLowerCase();
  if (needle === '') {
    return true;
  }
  return [
    entry.code,
    entry.name.ar,
    entry.name.en,
    entry.description?.ar ?? '',
    entry.description?.en ?? '',
  ].some((text) => text.toLowerCase().includes(needle));
}

function Recent({ catalogue }: { catalogue: ReportCatalogueEntry[] }): ReactElement | null {
  const { t, formatDateTime } = useI18n();
  const compose = useCan('REPORT_COMPOSE');
  const load = useCallback(
    async (signal: AbortSignal) => {
      const [jobs, views] = await Promise.all([
        reportsApi.jobs(1, RECENT_COUNT, signal),
        compose ? reportsApi.savedViews(1, RECENT_COUNT, signal) : Promise.resolve(null),
      ]);
      return { jobs, views };
    },
    [compose],
  );
  const { data } = useApiResource(load);
  if (data === undefined) {
    return null;
  }
  return (
    <div className="columns report-recent">
      {data.views !== null && (
        <section className="section" aria-labelledby="recent-views">
          <h2 id="recent-views">{t('reports.center.recentViews')}</h2>
          {data.views.items.length === 0 ? (
            <p>{t('reports.savedViews.empty')}</p>
          ) : (
            <ul className="link-list">
              {data.views.items.map((view) => {
                const path = viewPath(view, catalogue);
                const name = (
                  <span lang={entryLanguage(view.name.language)} dir="auto">
                    {view.name.text}
                  </span>
                );
                return (
                  <li key={view.id}>{path === null ? name : <Link to={path}>{name}</Link>}</li>
                );
              })}
            </ul>
          )}
          <Link to="/reports/saved-views">{t('reports.center.allViews')}</Link>
        </section>
      )}
      <section className="section" aria-labelledby="recent-exports">
        <h2 id="recent-exports">{t('reports.center.recentExports')}</h2>
        {data.jobs.items.length === 0 ? (
          <p>{t('reports.history.empty')}</p>
        ) : (
          <ul className="link-list">
            {data.jobs.items.map((job) => (
              <li key={job.id}>
                {t(`reports.formats.${job.exportFormat}`)} · {formatDateTime(job.requestedAt)}{' '}
                <StatusBadge
                  label={t(`reports.jobStatus.${job.status}`)}
                  tone={jobTone(job.status)}
                />
              </li>
            ))}
          </ul>
        )}
        <Link to="/reports/exports">{t('reports.center.allExports')}</Link>
      </section>
    </div>
  );
}

export function ReportsCenterPage(): ReactElement {
  const { t } = useI18n();
  const compose = useCan('REPORT_COMPOSE');
  const { params, setFilter, setFilters } = useListParams();
  const query = params.get('q') ?? '';
  const family = familyOfSlug(params.get('family') ?? undefined);
  const catalogue = useApiResource(reportsApi.catalogue);
  const shown = (catalogue.data ?? []).filter(
    (entry) => matches(entry, query) && (family === null || entry.audienceFamily === family),
  );

  return (
    <>
      <PageHeader
        title={t('reports.center.title')}
        description={t('reports.center.description')}
        actions={
          compose ? (
            <Link className="button" to="/reports/explorer">
              {t('reports.nav.explorer')}
            </Link>
          ) : undefined
        }
      />
      <FilterBar
        fields={[
          {
            name: 'family',
            label: t('reports.center.family'),
            all: t('reports.center.allFamilies'),
            options: FAMILY_ORDER.map((candidate) => ({
              value: FAMILIES[candidate].slug,
              label: t(FAMILIES[candidate].label),
            })),
          },
        ]}
        values={{ family: family === null ? undefined : FAMILIES[family].slug }}
        onChange={(name, value) => {
          setFilter(name, value ?? '');
        }}
        onClearAll={() => {
          setFilters({ q: '', family: '' });
        }}
        chips={
          query === ''
            ? []
            : [
                {
                  name: 'q',
                  text: t('common.filters.inline', {
                    label: t('reports.center.search'),
                    value: query,
                  }),
                  onRemove: () => {
                    setFilter('q', '');
                  },
                },
              ]
        }
      >
        <form
          className="filters filters--inline"
          aria-label={t('reports.center.searchLabel')}
          onSubmit={(event) => {
            event.preventDefault();
          }}
        >
          <TextField
            label={t('reports.center.search')}
            name="q"
            type="search"
            value={query}
            onChange={(value) => {
              setFilter('q', value);
            }}
          />
        </form>
      </FilterBar>

      {catalogue.loading ? (
        <LoadingState />
      ) : catalogue.error !== null ? (
        <ErrorState
          error={catalogue.error}
          message={reportProblemMessage(catalogue.error, t)}
          onRetry={catalogue.reload}
        />
      ) : (catalogue.data ?? []).length === 0 ? (
        <EmptyState title={t('reports.center.none')} />
      ) : shown.length === 0 ? (
        <EmptyState title={t('reports.center.noMatch')} />
      ) : (
        FAMILY_ORDER.filter((candidate) =>
          shown.some((entry) => entry.audienceFamily === candidate),
        ).map((candidate) => (
          <section key={candidate} className="section" aria-labelledby={`family-${candidate}`}>
            <h2 id={`family-${candidate}`}>
              <Link to={`/reports/${FAMILIES[candidate].slug}`}>
                {t(FAMILIES[candidate].label)}
              </Link>
            </h2>
            <ReportList entries={shown.filter((entry) => entry.audienceFamily === candidate)} />
          </section>
        ))
      )}
      <Recent catalogue={catalogue.data ?? []} />
    </>
  );
}
