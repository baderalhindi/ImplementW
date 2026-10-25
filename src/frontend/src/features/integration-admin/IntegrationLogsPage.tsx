import { type ReactElement, type SyntheticEvent, useCallback, useRef, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';

import { ConfirmDialog } from '@/features/identity-access/components/ConfirmDialog.tsx';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useListParams } from '@/shared/api/useListParams.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField, TextField } from '@/shared/ui/FormFields.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { integrationAdminApi } from './api/integrationAdminApi.ts';
import { type IntegrationSummary, type InvocationDetail } from './api/types.ts';
import {
  INTEGRATION_LOGS_PATH,
  INVOCATION_STATUSES,
  invocationPath,
  LOG_FILTER_NAMES,
  type LogFilterName,
  type LogFilterProblem,
  logFilterProblems,
  type LogFilterValues,
  logFilterValues,
  logsFilteredBy,
  toInvocationQuery,
} from './catalogue.ts';
import { CataloguePage } from './components/CataloguePage.tsx';
import { Code, Moment, Section } from './components/Display.tsx';
import { InvocationList } from './components/InvocationList.tsx';
import { InvocationTable } from './components/InvocationTable.tsx';
import { invocationTone, labelIn } from './presentation.ts';
import { integrationProblemMessage } from './problems.ts';

const PROBLEM_MESSAGES: Record<LogFilterProblem, TranslationKey> = {
  malformed: 'integrationAdmin.filters.malformed',
  tooLong: 'integrationAdmin.filters.tooLong',
  dateBeforeStart: 'integrationAdmin.filters.dateBeforeStart',
};

/**
 * ADM-048's filters (FG-05 §13.2): integration, state, operation, correlation id, lineage key, business reference and a
 * day range — the API's allowlisted search, no free query. Applied together into the URL; keyed on the URL by the
 * caller, so Clear or Back resets the inputs.
 */
function LogFilters({
  values,
  integrations,
}: {
  values: LogFilterValues;
  integrations: readonly IntegrationSummary[];
}): ReactElement {
  const { t, language } = useI18n();
  const [params, setParams] = useSearchParams();
  const [draft, setDraft] = useState(values);
  const [problems, setProblems] = useState<Partial<Record<LogFilterName, LogFilterProblem>>>({});
  const set = (name: LogFilterName) => (value: string) => {
    setDraft({ ...draft, [name]: value });
  };
  const error = (name: LogFilterName) => {
    const problem = problems[name];
    return problem === undefined ? undefined : t(PROBLEM_MESSAGES[problem]);
  };

  const apply = (event: SyntheticEvent) => {
    event.preventDefault();
    const trimmed = Object.fromEntries(
      LOG_FILTER_NAMES.map((name) => [name, draft[name].trim()]),
    ) as unknown as LogFilterValues;
    const found = logFilterProblems(trimmed);
    setProblems(found);
    if (Object.keys(found).length > 0) {
      return;
    }
    const next = new URLSearchParams(params);
    next.delete('page');
    for (const name of LOG_FILTER_NAMES) {
      if (trimmed[name] === '') {
        next.delete(name);
      } else {
        next.set(name, trimmed[name]);
      }
    }
    setParams(next);
  };

  const clear = () => {
    setParams(new URLSearchParams());
  };

  const text = (name: LogFilterName, label: TranslationKey) => (
    <TextField
      label={t(label)}
      name={name}
      value={draft[name]}
      error={error(name)}
      dir="ltr"
      onChange={set(name)}
    />
  );

  return (
    <form className="filters" onSubmit={apply} aria-label={t('common.filters.label')} noValidate>
      <SelectField
        label={t('integrationAdmin.fields.integration')}
        name="integrationId"
        value={draft.integrationId}
        placeholder={t('common.filters.any')}
        error={error('integrationId')}
        options={integrations.map((integration) => ({
          value: integration.id,
          label: labelIn(integration.name, language),
        }))}
        onChange={set('integrationId')}
      />
      <SelectField
        label={t('integrationAdmin.fields.invocationStatus')}
        name="status"
        value={draft.status}
        placeholder={t('common.filters.any')}
        options={INVOCATION_STATUSES.map((status) => ({
          value: status,
          label: t(`integrationAdmin.invocationStatus.${status}`),
        }))}
        onChange={set('status')}
      />
      {text('operationCode', 'integrationAdmin.fields.operation')}
      {text('correlationId', 'integrationAdmin.fields.correlationId')}
      {text('idempotencyKey', 'integrationAdmin.fields.idempotencyKey')}
      {text('sourceReference', 'integrationAdmin.fields.sourceReference')}
      <TextField
        label={t('integrationAdmin.filters.from')}
        name="from"
        type="date"
        value={draft.from}
        error={error('from')}
        onChange={set('from')}
      />
      <TextField
        label={t('integrationAdmin.filters.to')}
        name="to"
        type="date"
        value={draft.to}
        error={error('to')}
        onChange={set('to')}
      />
      <div className="filters__actions">
        <button type="submit" className="button button--primary">
          {t('common.filters.apply')}
        </button>
        <button type="button" className="button" onClick={clear}>
          {t('common.filters.clear')}
        </button>
      </div>
    </form>
  );
}

/**
 * ADM-048 Integration Logs (FG-05 §13.2): redacted, structured attempts with controlled search. A correlation id finds a
 * request's whole technical lineage — its sign-in calls, a reconciliation's attempts and the directory reads they made
 * (TASK-075 D-14). The formal audit of operator actions is FG-06's ADM-053, not this.
 */
export function IntegrationLogsPage(): ReactElement {
  const { t } = useI18n();
  const { params, page, goToPage } = useListParams();
  const values = logFilterValues(params);
  const title = t('integrationAdmin.logs.title');
  return (
    <CataloguePage title={title} description={t('integrationAdmin.logs.description')}>
      {(catalogue) => (
        <>
          <LogFilters key={params.toString()} values={values} integrations={catalogue.items} />
          <InvocationList
            query={toInvocationQuery(values)}
            caption={title}
            emptyTitle={t('integrationAdmin.logs.empty')}
            page={page}
            onPageChange={goToPage}
          />
        </>
      )}
    </CataloguePage>
  );
}

/**
 * One attempt with every attempt of its lineage, oldest first, and the operator's retry where the API permits it
 * (`retryPermitted`: an FG-05 lineage that is dead-lettered or waiting; another module retries its own, D-2). A retry
 * is a new attempt under the lineage's key and correlation id; the attempts before it are never rewritten (D-7).
 */
export function InvocationDetailPage(): ReactElement {
  const { t } = useI18n();
  const { invocationId = '' } = useParams();
  const load = useCallback(
    (signal: AbortSignal) => integrationAdminApi.invocation(invocationId, signal),
    [invocationId],
  );
  const detail = useApiResource(load, { keepWhileReloading: true });
  const title = t('integrationAdmin.invocation.title');
  return (
    <CataloguePage
      title={title}
      actions={
        <Link className="button" to={INTEGRATION_LOGS_PATH}>
          {t('integrationAdmin.invocation.backToLogs')}
        </Link>
      }
    >
      {() =>
        detail.data === undefined ? (
          detail.error === null ? (
            <LoadingState />
          ) : (
            <ErrorState
              error={detail.error}
              message={integrationProblemMessage(detail.error, t)}
              onRetry={detail.reload}
            />
          )
        ) : (
          <AttemptDetail detail={detail.data} reload={detail.reload} />
        )
      }
    </CataloguePage>
  );
}

function AttemptDetail({
  detail,
  reload,
}: {
  detail: InvocationDetail;
  reload: () => void;
}): ReactElement {
  const { t } = useI18n();
  const navigate = useNavigate();
  const [confirming, setConfirming] = useState(false);
  const [notice, setNotice] = useState<Notice | null>(null);
  const retried = useRef<InvocationDetail | null>(null);
  const { invocation, lineage, retryPermitted } = detail;
  const none = t('common.values.none');

  return (
    <article data-invocation={invocation.id}>
      <PageNotice notice={notice} />
      <Section
        title={t('integrationAdmin.invocation.attempt')}
        actions={
          retryPermitted ? (
            <button
              type="button"
              className="button button--primary"
              onClick={() => {
                setNotice(null);
                setConfirming(true);
              }}
            >
              {t('integrationAdmin.actions.retry')}
            </button>
          ) : undefined
        }
      >
        <dl className="details">
          <Detail term={t('integrationAdmin.fields.integration')}>
            <Code>{invocation.integrationCode}</Code>
          </Detail>
          <Detail term={t('integrationAdmin.fields.operation')}>
            <Code>{invocation.operationCode}</Code>
          </Detail>
          <Detail term={t('integrationAdmin.fields.attempt')}>{invocation.attemptNo}</Detail>
          <Detail term={t('integrationAdmin.fields.invocationStatus')}>
            <StatusBadge
              label={t(`integrationAdmin.invocationStatus.${invocation.status}`)}
              tone={invocationTone(invocation.status)}
            />
          </Detail>
          <Detail term={t('integrationAdmin.fields.failure')}>
            {invocation.failureCategory === null
              ? none
              : t(`integrationAdmin.failureCategory.${invocation.failureCategory}`)}
          </Detail>
          <Detail term={t('integrationAdmin.fields.responseCode')}>
            {invocation.responseCode === null ? none : <Code>{invocation.responseCode}</Code>}
          </Detail>
          <Detail term={t('integrationAdmin.fields.errorSummary')}>
            {invocation.errorSummary === null ? none : <Code>{invocation.errorSummary}</Code>}
          </Detail>
          <Detail term={t('integrationAdmin.fields.startedAt')}>
            <Moment value={invocation.startedAt} />
          </Detail>
          <Detail term={t('integrationAdmin.fields.completedAt')}>
            <Moment value={invocation.completedAt} absent={t('integrationAdmin.values.running')} />
          </Detail>
          {invocation.nextAttemptAt !== null && (
            <Detail term={t('integrationAdmin.invocations.nextAttempt')}>
              <Moment value={invocation.nextAttemptAt} />
            </Detail>
          )}
          <Detail term={t('integrationAdmin.fields.sourceModule')}>
            {invocation.sourceModule ?? none}
          </Detail>
          <Detail term={t('integrationAdmin.fields.sourceReference')}>
            {invocation.sourceReference === null ? none : <Code>{invocation.sourceReference}</Code>}
          </Detail>
          <Detail term={t('integrationAdmin.fields.correlationId')}>
            <Code>{invocation.correlationId}</Code>{' '}
            <Link to={logsFilteredBy('correlationId', invocation.correlationId)}>
              {t('integrationAdmin.invocation.sameCorrelation')}
            </Link>
          </Detail>
          <Detail term={t('integrationAdmin.fields.idempotencyKey')}>
            {invocation.idempotencyKey === null ? none : <Code>{invocation.idempotencyKey}</Code>}
          </Detail>
        </dl>
        {!retryPermitted && invocation.status !== 'SUCCEEDED' && (
          <p className="integration-note">{t('integrationAdmin.invocation.notRetryable')}</p>
        )}
      </Section>

      <Section title={t('integrationAdmin.invocation.lineage')}>
        <InvocationTable
          items={lineage}
          caption={t('integrationAdmin.invocation.lineage')}
          current={invocation.id}
        />
      </Section>

      <ConfirmDialog
        confirmation={
          confirming
            ? {
                title: t('integrationAdmin.retry.title'),
                body: t('integrationAdmin.retry.body'),
                confirmLabel: t('integrationAdmin.actions.retry'),
                destructive: false,
                action: async () => {
                  retried.current = await integrationAdminApi.retryInvocation(invocation.id);
                },
                describeProblem: integrationProblemMessage,
              }
            : null
        }
        onClose={() => {
          setConfirming(false);
        }}
        onDone={() => {
          setConfirming(false);
          const next = retried.current;
          if (next === null || next.invocation.id === invocation.id) {
            setNotice({ tone: 'success', message: t('integrationAdmin.retry.done') });
            reload();
          } else {
            void navigate(invocationPath(next.invocation.id));
          }
        }}
      />
    </article>
  );
}
