import { type ReactElement, useCallback, useMemo, useState } from 'react';
import { Link, Navigate, useLocation, useNavigate, useParams } from 'react-router';

import { PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField } from '@/shared/ui/FormFields.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { reportsApi } from './api/reportsApi.ts';
import {
  type ReportCode,
  type ReportParameterValue,
  type ReportRunRequest,
  type ReportView,
  type SavedViewDetail,
} from './api/types.ts';
import { ResultGrid } from './components/ResultGrid.tsx';
import { SavedViewNotice } from './components/SavedViewNotice.tsx';
import {
  type Composition,
  defaultColumns,
  optionsFromReport,
  toColumns,
  toSort,
} from './composition.ts';
import { ColumnsDialog } from './dialogs/ColumnsDialog.tsx';
import { ExportDialog } from './dialogs/ExportDialog.tsx';
import { SaveViewDialog, type SaveViewValues } from './dialogs/SaveViewDialog.tsx';
import { FAMILIES, familyOfSlug, reportPath } from './presentation.ts';
import { isProblem, reportProblemMessage } from './problems.ts';
import { type SavedState, savedNotice, savedViewRequest, useOpenedView } from './useSavedView.ts';

// SCR-131 to SCR-137 (FG-02 §13.2): one report shell for the seven experiences — Report Header, Parameter Panel, Active
// Filter Summary, Coverage/Freshness Banner, Results Grid, Column Selector (MOD-061), sort, Export (MOD-060) and Save
// View (MOD-062). The API authorises each report independently: what this screen offers (parameter options, export,
// saving) is what the API said the person may do, and every run is authorised again on its data.

type Dialogs = 'columns' | 'export' | 'save' | 'saveAs' | null;

/** The ten reports of ADR-006: any other name in the address is no report. */
const REPORT_CODES = new Set<string>([
  'PORTFOLIO_SUMMARY',
  'PROJECT_REGISTER',
  'PROJECT_REPORT',
  'PROGRESS_REPORTING',
  'PROGRESS_HISTORY',
  'SCHEDULE_DELIVERY',
  'RISK_ISSUE',
  'FINANCIAL_PERFORMANCE',
  'KPI_PERFORMANCE',
  'GOVERNANCE_CHANGE',
]);

function isReportCode(value: string | undefined): value is ReportCode {
  return value !== undefined && REPORT_CODES.has(value);
}

/** The parameter values the panel holds that the report still has: a saved value it no longer takes is dropped. */
function initialParameters(
  report: ReportView,
  view: SavedViewDetail | null,
): Record<string, string> {
  const known = new Set(report.parameters.map((parameter) => parameter.code));
  return Object.fromEntries(
    (view?.compatibility === 'VALID' ? view.parameters : [])
      .filter((parameter) => known.has(parameter.code))
      .map((parameter) => [parameter.code, parameter.value]),
  );
}

function missingRequired(report: ReportView, values: Record<string, string>): string[] {
  return report.parameters
    .filter((parameter) => parameter.isRequired && (values[parameter.code] ?? '') === '')
    .map((parameter) => parameter.code);
}

function ReportWorkspace({
  report,
  view,
  viewEtag,
  isOwnView,
}: {
  report: ReportView;
  view: SavedViewDetail | null;
  viewEtag: string | null;
  isOwnView: boolean;
}): ReactElement {
  const { t, language } = useI18n();
  const navigate = useNavigate();
  const options = useMemo(() => optionsFromReport(report.columns), [report.columns]);
  const [values, setValues] = useState(() => initialParameters(report, view));
  const [composition, setComposition] = useState<Composition>(() => ({
    columns: defaultColumns(options),
    sort: null,
    filters: [],
  }));
  const [missing, setMissing] = useState<string[]>([]);
  const [request, setRequest] = useState<ReportRunRequest | null>(() =>
    missingRequired(report, values).length > 0
      ? null
      : {
          parameters: toParameters(values),
          columns: toColumns(composition, options),
          sort: toSort(composition, options),
        },
  );
  const [page, setPage] = useState(1);
  const [dialog, setDialog] = useState<Dialogs>(null);

  const load = useCallback(
    (signal: AbortSignal) =>
      request === null
        ? Promise.resolve(null)
        : reportsApi.run(report.code, request, { page, pageSize: PAGE_SIZE }, signal),
    [report.code, request, page],
  );
  const result = useApiResource(load);
  const name = report.name[language];

  const run = (nextComposition: Composition = composition) => {
    const required = missingRequired(report, values);
    setMissing(required);
    if (required.length > 0) {
      return;
    }
    setPage(1);
    setRequest({
      parameters: toParameters(values),
      columns: toColumns(nextComposition, options),
      sort: toSort(nextComposition, options),
    });
  };

  const optionLabel = (code: string, value: string) =>
    report.parameters
      .find((parameter) => parameter.code === code)
      ?.options.find((option) => option.value === value)?.label[language] ?? value;

  const saveView = async (saved: SaveViewValues, update: boolean) => {
    const body = savedViewRequest(saved, {
      viewType: 'REPORT_PARAMETERS',
      reportCode: report.code,
      parameters: toParameters(values),
      columns: [],
      filters: [],
    });
    const { data } =
      update && view !== null
        ? await reportsApi.updateSavedView(view.id, body, viewEtag)
        : await reportsApi.createSavedView(body);
    setDialog(null);
    void navigate(`${reportPath(report.audienceFamily, report.code)}?view=${data.id}`, {
      replace: true,
      state: { savedViewName: data.name.text } satisfies SavedState,
    });
  };

  const shownSensitive = composition.columns.some(
    (key) => options.find((option) => option.key === key)?.isSensitive === true,
  );
  const canSave = report.maySaveView && report.allowsSavedViews;

  return (
    <>
      <PageHeader
        title={name}
        description={report.description?.[language]}
        actions={
          <>
            <button
              type="button"
              className="button"
              onClick={() => {
                setDialog('columns');
              }}
            >
              {t('reports.actions.columns')}
            </button>
            {report.mayExport && (
              <button
                type="button"
                className="button"
                disabled={result.data === null || result.data === undefined}
                onClick={() => {
                  setDialog('export');
                }}
              >
                {t('reports.actions.export')}
              </button>
            )}
            {canSave && view !== null && isOwnView && (
              <button
                type="button"
                className="button"
                onClick={() => {
                  setDialog('save');
                }}
              >
                {t('reports.actions.saveChanges')}
              </button>
            )}
            {canSave && (
              <button
                type="button"
                className="button"
                onClick={() => {
                  setDialog('saveAs');
                }}
              >
                {view === null ? t('reports.actions.saveView') : t('reports.actions.saveAsNew')}
              </button>
            )}
          </>
        }
      />
      <p className="report-header__meta">
        <Link to={`/reports/${FAMILIES[report.audienceFamily].slug}`}>
          {t(FAMILIES[report.audienceFamily].label)}
        </Link>
        {' · '}
        {t('reports.header.version', { version: report.versionNo })}
        {!report.mayExport && <> · {t('reports.header.noExport')}</>}
      </p>
      {view !== null && <SavedViewNotice view={view} isOwn={isOwnView} />}

      <form
        className="filters report-parameters"
        aria-label={t('reports.parameters.label')}
        noValidate
        onSubmit={(event) => {
          event.preventDefault();
          run();
        }}
      >
        {report.parameters.length === 0 && (
          <p className="form__note">{t('reports.parameters.none')}</p>
        )}
        {report.parameters.map((parameter) => (
          <SelectField
            key={parameter.code}
            label={parameter.label[language]}
            name={parameter.code}
            required={parameter.isRequired}
            value={values[parameter.code] ?? ''}
            placeholder={
              parameter.isRequired ? t('common.form.choose') : t('reports.parameters.all')
            }
            error={missing.includes(parameter.code) ? t('common.fieldErrors.required') : undefined}
            onChange={(value) => {
              setValues((current) => ({ ...current, [parameter.code]: value }));
            }}
            options={parameter.options.map((option) => ({
              value: option.value,
              label: option.label[language],
            }))}
          />
        ))}
        <div className="filters__actions">
          <button type="submit" className="button button--primary">
            {t('reports.actions.run')}
          </button>
        </div>
      </form>

      {request !== null && (
        <section className="report-summary" aria-label={t('reports.summary.label')}>
          <p>
            {request.parameters.length === 0
              ? t('reports.summary.noParameters')
              : request.parameters
                  .map(
                    (parameter) =>
                      `${report.parameters.find((p) => p.code === parameter.code)?.label[language] ?? parameter.code}: ${optionLabel(parameter.code, parameter.value)}`,
                  )
                  .join(' · ')}
          </p>
          <p>
            {t('reports.summary.columns', { count: request.columns?.length ?? 0 })}
            {request.sort[0] !== undefined &&
              ` · ${t('reports.summary.sortedBy', {
                field:
                  options.find(
                    (option) =>
                      option.ref.sourceEntityCode === request.sort[0]?.sourceEntityCode &&
                      option.ref.fieldCode === request.sort[0].fieldCode,
                  )?.label[language] ?? '',
                direction: t(
                  request.sort[0].direction === 'ASC'
                    ? 'reports.columns.ascending'
                    : 'reports.columns.descending',
                ),
              })}`}
          </p>
        </section>
      )}

      {request === null ? (
        <EmptyState title={t('reports.result.chooseParameters')} />
      ) : result.loading && result.data === undefined ? (
        <LoadingState label={t('reports.result.running')} />
      ) : result.error !== null ? (
        <ErrorState
          error={result.error}
          message={reportProblemMessage(result.error, t)}
          onRetry={result.reload}
        />
      ) : result.data === null || result.data === undefined ? null : (
        <ResultGrid result={result.data} caption={name} onPageChange={setPage} />
      )}

      <ColumnsDialog
        open={dialog === 'columns'}
        options={options}
        initial={composition}
        allowFilters={false}
        onClose={() => {
          setDialog(null);
        }}
        onApply={(next) => {
          setComposition(next);
          setDialog(null);
          if (request !== null) {
            run(next);
          }
        }}
      />
      {report.mayExport && request !== null && (
        <ExportDialog
          open={dialog === 'export'}
          formats={report.exportFormats}
          sensitive={shownSensitive}
          name={name}
          start={(exportOptions) => reportsApi.export(report.code, request, exportOptions)}
          onClose={() => {
            setDialog(null);
          }}
        />
      )}
      {canSave && (
        <SaveViewDialog
          open={dialog === 'save' || dialog === 'saveAs'}
          title={
            dialog === 'save' ? t('reports.saveView.titleChanges') : t('reports.saveView.title')
          }
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
      )}
    </>
  );
}

function toParameters(values: Record<string, string>): ReportParameterValue[] {
  return Object.entries(values)
    .filter(([, value]) => value !== '')
    .map(([code, value]) => ({ code, value }));
}

/**
 * The report at /reports/:family/:reportCode. A report outside the person's audience, or not one of the ten, is not
 * found (R-47). A report opened under another experience's address is moved to its own.
 */
export function ReportPage(): ReactElement {
  const { t } = useI18n();
  const { family: slug, reportCode } = useParams();
  const location = useLocation();
  const code = isReportCode(reportCode) ? reportCode : null;
  const load = useCallback(
    (signal: AbortSignal) => (code === null ? Promise.resolve(null) : reportsApi.get(code, signal)),
    [code],
  );
  const report = useApiResource(load);
  const opened = useOpenedView();

  if (code === null || familyOfSlug(slug) === null || isProblem(report.error, 'NOT_FOUND')) {
    return (
      <>
        <PageHeader title={t('reports.notFound.title')} />
        <EmptyState title={t('reports.notFound.description')}>
          <Link to="/reports">{t('reports.notFound.back')}</Link>
        </EmptyState>
      </>
    );
  }
  if (report.loading || opened.loading) {
    return <LoadingState />;
  }
  if (report.error !== null || report.data === null || report.data === undefined) {
    return (
      <ErrorState
        error={report.error}
        message={reportProblemMessage(report.error, t)}
        onRetry={report.reload}
      />
    );
  }
  if (FAMILIES[report.data.audienceFamily].slug !== slug) {
    return (
      <Navigate to={`${reportPath(report.data.audienceFamily, code)}${location.search}`} replace />
    );
  }
  const view = opened.response?.data.reportCode === code ? opened.response.data : null;
  return (
    <>
      <PageNotice notice={savedNotice(location.state, t)} />
      {opened.id !== null && opened.error !== null && (
        <PageNotice notice={{ tone: 'warning', message: reportProblemMessage(opened.error, t) }} />
      )}
      <ReportWorkspace
        key={`${code}:${view?.id ?? ''}:${opened.response?.etag ?? ''}`}
        report={report.data}
        view={view}
        viewEtag={opened.response?.etag ?? null}
        isOwnView={opened.isOwn}
      />
    </>
  );
}
