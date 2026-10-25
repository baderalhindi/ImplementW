import { type ReactElement, useCallback, useMemo, useState } from 'react';
import { useLocation, useNavigate } from 'react-router';

import { labelOfValue } from '@/features/dashboards/presentation.ts';

import { PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { reportsApi } from './api/reportsApi.ts';
import {
  type ExplorerRunRequest,
  type ReportAllowlist,
  type SavedViewDetail,
} from './api/types.ts';
import { ResultGrid } from './components/ResultGrid.tsx';
import { SavedViewNotice } from './components/SavedViewNotice.tsx';
import {
  type Composition,
  compositionOfView,
  EMPTY_COMPOSITION,
  type FieldOption,
  optionsFromAllowlist,
  toExplorerRequest,
  toSavedColumns,
  valueParts,
} from './composition.ts';
import { ColumnsDialog } from './dialogs/ColumnsDialog.tsx';
import { ExportDialog } from './dialogs/ExportDialog.tsx';
import { SaveViewDialog, type SaveViewValues } from './dialogs/SaveViewDialog.tsx';
import { OPERATOR_LABELS } from './presentation.ts';
import { isProblem, reportProblemMessage } from './problems.ts';
import { type SavedState, savedNotice, savedViewRequest, useOpenedView } from './useSavedView.ts';

// SCR-138 Custom Report / Filter Builder, as FG-02 §11.3 reinterprets it: a controlled explorer over the PUBLISHED
// REPORT_RULES allowlist (acceptance criterion 1). Its field picker is the allowlist the API returns and nothing else;
// a filter's operators and values are each entry's own; there is no SQL box, no join to choose and no formula. The
// API checks every field again and refuses anything outside the allowlist before reading a source.

type Dialogs = 'columns' | 'export' | 'save' | 'saveAs' | null;

const EXPORT_FORMATS = ['PDF', 'XLSX', 'CSV'] as const;

function Summary({
  request,
  options,
}: {
  request: ExplorerRunRequest;
  options: FieldOption[];
}): ReactElement {
  const { t, language } = useI18n();
  const optionOf = (sourceEntityCode: string, fieldCode: string) =>
    options.find(
      (option) =>
        option.ref.sourceEntityCode === sourceEntityCode && option.ref.fieldCode === fieldCode,
    );
  const nameOf = (sourceEntityCode: string, fieldCode: string) =>
    optionOf(sourceEntityCode, fieldCode)?.label[language] ?? fieldCode;
  /** A closed-set value in its source's words; a typed value as it was typed. */
  const valueOf = (filter: ExplorerRunRequest['filters'][number]) => {
    const option = optionOf(filter.sourceEntityCode, filter.fieldCode);
    return option === undefined || option.values.length === 0
      ? filter.value
      : valueParts(filter.value)
          .map((part) => labelOfValue(option.projectionCode ?? '', part, null, language, t).text)
          .join(', ');
  };
  return (
    <section className="report-summary" aria-label={t('reports.summary.label')}>
      <p>
        {t('reports.explorer.columns', {
          columns: request.columns
            .map((column) => nameOf(column.sourceEntityCode, column.fieldCode))
            .join(', '),
        })}
      </p>
      <p>
        {request.filters.length === 0
          ? t('reports.explorer.noFilters')
          : t('reports.explorer.filters', {
              filters: request.filters
                .map(
                  (filter) =>
                    `${nameOf(filter.sourceEntityCode, filter.fieldCode)} ${t(OPERATOR_LABELS[filter.operator])} ${valueOf(filter)}`,
                )
                .join(' · '),
            })}
      </p>
      {request.sort[0] !== undefined && (
        <p>
          {t('reports.summary.sortedBy', {
            field: nameOf(request.sort[0].sourceEntityCode, request.sort[0].fieldCode),
            direction: t(
              request.sort[0].direction === 'ASC'
                ? 'reports.columns.ascending'
                : 'reports.columns.descending',
            ),
          })}
        </p>
      )}
    </section>
  );
}

function ExplorerWorkspace({
  allowlist,
  view,
  viewEtag,
  isOwnView,
}: {
  allowlist: ReportAllowlist;
  view: SavedViewDetail | null;
  viewEtag: string | null;
  isOwnView: boolean;
}): ReactElement {
  const { t, language } = useI18n();
  const navigate = useNavigate();
  const options = useMemo(() => optionsFromAllowlist(allowlist.items), [allowlist.items]);
  const [composition, setComposition] = useState<Composition>(() =>
    view?.compatibility === 'VALID' ? compositionOfView(view) : EMPTY_COMPOSITION,
  );
  const [request, setRequest] = useState<ExplorerRunRequest | null>(() =>
    composition.columns.length === 0 ? null : toExplorerRequest(composition, options),
  );
  const [page, setPage] = useState(1);
  const [dialog, setDialog] = useState<Dialogs>(null);

  const load = useCallback(
    (signal: AbortSignal) =>
      request === null
        ? Promise.resolve(null)
        : reportsApi.runExplorer(request, { page, pageSize: PAGE_SIZE }, signal),
    [request, page],
  );
  const result = useApiResource(load);
  const title = t('reports.explorer.title');

  const saveView = async (saved: SaveViewValues, update: boolean) => {
    const explorer = toExplorerRequest(composition, options);
    const body = savedViewRequest(saved, {
      viewType: 'EXPLORER_COMPOSITION',
      reportCode: null,
      parameters: [],
      columns: toSavedColumns(composition, options),
      filters: explorer.filters,
    });
    const { data } =
      update && view !== null
        ? await reportsApi.updateSavedView(view.id, body, viewEtag)
        : await reportsApi.createSavedView(body);
    setDialog(null);
    void navigate(`/reports/explorer?view=${data.id}`, {
      replace: true,
      state: { savedViewName: data.name.text } satisfies SavedState,
    });
  };

  const sensitive = composition.columns.some(
    (key) => options.find((option) => option.key === key)?.isSensitive === true,
  );
  const ran = result.data !== null && result.data !== undefined;

  return (
    <>
      <PageHeader
        title={title}
        description={t('reports.explorer.description')}
        actions={
          <>
            <button
              type="button"
              className="button button--primary"
              onClick={() => {
                setDialog('columns');
              }}
            >
              {t('reports.actions.columnsAndFilters')}
            </button>
            <button
              type="button"
              className="button"
              disabled={!ran}
              onClick={() => {
                setDialog('export');
              }}
            >
              {t('reports.actions.export')}
            </button>
            {view !== null && isOwnView && (
              <button
                type="button"
                className="button"
                disabled={composition.columns.length === 0}
                onClick={() => {
                  setDialog('save');
                }}
              >
                {t('reports.actions.saveChanges')}
              </button>
            )}
            <button
              type="button"
              className="button"
              disabled={composition.columns.length === 0}
              onClick={() => {
                setDialog('saveAs');
              }}
            >
              {view === null ? t('reports.actions.saveView') : t('reports.actions.saveAsNew')}
            </button>
          </>
        }
      />
      <p className="report-header__meta" data-allowlist-version={allowlist.configurationVersionNo}>
        {t('reports.explorer.allowlist', {
          count: options.length,
          version: allowlist.configurationVersionNo,
        })}
      </p>
      {view !== null && <SavedViewNotice view={view} isOwn={isOwnView} />}
      {request !== null && <Summary request={request} options={options} />}

      {request === null ? (
        <EmptyState title={t('reports.explorer.start')}>
          <button
            type="button"
            className="button"
            onClick={() => {
              setDialog('columns');
            }}
          >
            {t('reports.actions.columnsAndFilters')}
          </button>
        </EmptyState>
      ) : result.loading && result.data === undefined ? (
        <LoadingState label={t('reports.result.running')} />
      ) : result.error !== null ? (
        <ErrorState error={result.error} message={reportProblemMessage(result.error, t)} onRetry={result.reload} />
      ) : result.data === null || result.data === undefined ? null : (
        <ResultGrid result={result.data} caption={title} onPageChange={setPage} />
      )}

      <ColumnsDialog
        open={dialog === 'columns'}
        options={options}
        initial={composition}
        allowFilters
        onClose={() => {
          setDialog(null);
        }}
        onApply={(next) => {
          setComposition(next);
          setDialog(null);
          setPage(1);
          setRequest(toExplorerRequest(next, options));
        }}
      />
      {request !== null && (
        <ExportDialog
          open={dialog === 'export'}
          formats={[...EXPORT_FORMATS]}
          sensitive={sensitive}
          name={title}
          start={(exportOptions) => reportsApi.exportExplorer(request, exportOptions)}
          onClose={() => {
            setDialog(null);
          }}
        />
      )}
      <SaveViewDialog
        open={dialog === 'save' || dialog === 'saveAs'}
        title={dialog === 'save' ? t('reports.saveView.titleChanges') : t('reports.saveView.title')}
        initial={
          dialog === 'save' && view !== null
            ? {
                name: view.name.text,
                language: view.name.language.toLowerCase() === 'ar' ? 'ar' : 'en',
                sharedWithRoleCodes: view.sharedWithRoleCodes,
              }
            : { name: '', language, sharedWithRoleCodes: [] }
        }
        save={(saved) => saveView(saved, dialog === 'save')}
        onClose={() => {
          setDialog(null);
        }}
      />
    </>
  );
}

export function ExplorerPage(): ReactElement {
  const { t } = useI18n();
  const location = useLocation();
  const allowlist = useApiResource(reportsApi.allowlist);
  const opened = useOpenedView();
  const title = t('reports.explorer.title');

  if (isProblem(allowlist.error, 'CONFIGURATION_MISSING')) {
    return (
      <>
        <PageHeader title={title} />
        <EmptyState title={t('reports.explorer.closed')}>
          <p>{t('reports.explorer.closedDescription')}</p>
        </EmptyState>
      </>
    );
  }
  if (isProblem(allowlist.error, 'PERMISSION_DENIED')) {
    return (
      <>
        <PageHeader title={title} />
        <EmptyState title={t('reports.explorer.notForRole')} />
      </>
    );
  }
  if (allowlist.loading || opened.loading) {
    return <LoadingState />;
  }
  if (allowlist.error !== null || allowlist.data === undefined) {
    return (
      <ErrorState error={allowlist.error} message={reportProblemMessage(allowlist.error, t)} onRetry={allowlist.reload} />
    );
  }
  const view =
    opened.response?.data.viewType === 'EXPLORER_COMPOSITION' ? opened.response.data : null;
  return (
    <>
      <PageNotice notice={savedNotice(location.state, t)} />
      {opened.id !== null && opened.error !== null && (
        <PageNotice notice={{ tone: 'warning', message: reportProblemMessage(opened.error, t) }} />
      )}
      <ExplorerWorkspace
        key={`${view?.id ?? ''}:${opened.response?.etag ?? ''}`}
        allowlist={allowlist.data}
        view={view}
        viewEtag={opened.response?.etag ?? null}
        isOwnView={opened.isOwn}
      />
    </>
  );
}
