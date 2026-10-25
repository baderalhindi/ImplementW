import { type ReactElement, type SyntheticEvent, useCallback, useId, useState } from 'react';
import { Link, useSearchParams } from 'react-router';

import { useWorkspace } from '@/features/projects/workspace/workspaceContext.ts';
import { PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useListParams } from '@/shared/api/useListParams.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField, TextField } from '@/shared/ui/FormFields.tsx';
import { Pagination } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { auditActivityApi } from '../api/auditActivityApi.ts';
import { type ActivityEntryView } from '../api/types.ts';
import {
  activityFilterValues,
  type ActivityFilterValues,
  filterProblems,
  toActivityQuery,
} from '../filters.ts';
import {
  ACTIVITY_KINDS,
  activitySummary,
  isActivityKind,
  sourceRecordPath,
} from '../presentation.ts';
import { auditProblemMessage } from '../problems.ts';

function ActivityFilters({ values }: { values: ActivityFilterValues }): ReactElement {
  const { t } = useI18n();
  const [params, setParams] = useSearchParams();
  const [draft, setDraft] = useState(values);
  const [toError, setToError] = useState<string | undefined>(undefined);

  const apply = (event: SyntheticEvent) => {
    event.preventDefault();
    const problem = filterProblems(draft).to;
    setToError(problem === undefined ? undefined : t('auditActivity.filters.dateBeforeStart'));
    if (problem !== undefined) {
      return;
    }
    const next = new URLSearchParams(params);
    next.delete('page');
    for (const name of ['kind', 'from', 'to'] as const) {
      const value = draft[name];
      if (value === '') {
        next.delete(name);
      } else {
        next.set(name, value);
      }
    }
    setParams(next);
  };

  return (
    <form className="filters" onSubmit={apply} aria-label={t('common.filters.label')} noValidate>
      <SelectField
        label={t('auditActivity.activity.domain')}
        name="kind"
        value={draft.kind}
        placeholder={t('common.filters.any')}
        options={ACTIVITY_KINDS.map((value) => ({
          value,
          label: t(`auditActivity.activityKind.${value}`),
        }))}
        onChange={(kind) => {
          setDraft({ ...draft, kind });
        }}
      />
      <TextField
        label={t('auditActivity.filters.from')}
        name="from"
        type="date"
        value={draft.from}
        onChange={(from) => {
          setDraft({ ...draft, from });
        }}
      />
      <TextField
        label={t('auditActivity.filters.to')}
        name="to"
        type="date"
        value={draft.to}
        error={toError}
        onChange={(to) => {
          setDraft({ ...draft, to });
        }}
      />
      <div className="filters__actions">
        <button type="submit" className="button button--primary">
          {t('common.filters.apply')}
        </button>
        <button
          type="button"
          className="button"
          onClick={() => {
            setParams(new URLSearchParams());
          }}
        >
          {t('common.filters.clear')}
        </button>
      </div>
    </form>
  );
}

function ActivityLine({ entry }: { entry: ActivityEntryView }): ReactElement {
  const { t, language, formatDateTime } = useI18n();
  const summary = activitySummary(entry, language);
  const recordPath = sourceRecordPath(entry, entry.projectId);
  return (
    <li className="activity-timeline__entry" data-activity-entry={entry.id}>
      <time className="activity-timeline__time" dateTime={entry.occurredAt}>
        {formatDateTime(entry.occurredAt)}
      </time>
      <p className="activity-timeline__summary">{summary}</p>
      <p className="activity-timeline__meta">
        <span className="activity-timeline__domain">
          {isActivityKind(entry.activityKind)
            ? t(`auditActivity.activityKind.${entry.activityKind}`)
            : entry.activityKind}
        </span>
        <span>
          {entry.actorDisplayName === null
            ? t('auditActivity.activity.actorNotShown')
            : t('auditActivity.activity.by', { name: entry.actorDisplayName })}
        </span>
        {recordPath !== null && (
          <Link to={recordPath}>
            {t('auditActivity.activity.openRecord')}
            <span className="visually-hidden">{` ${summary}`}</span>
          </Link>
        )}
      </p>
    </li>
  );
}

/**
 * SCR-057 Project Activity History (FG-06 §7): the project's business-readable timeline — what happened, when, in
 * which domain and, where the reader may know, by whom — derived from the audit store and kept apart from it. It is
 * the default history, for ACTIVITY_VIEW; the formal audit is SCR-058, a separate tab. A line never carries a value,
 * name or note of the record (§7.4): its source link opens the record under that screen's own authorisation.
 */
export function ProjectActivityTab(): ReactElement {
  const { t } = useI18n();
  const { project } = useWorkspace();
  const { params, page, goToPage } = useListParams();
  const values = activityFilterValues(params);
  const key = JSON.stringify(toActivityQuery(values, project.id, ACTIVITY_KINDS));
  const load = useCallback(
    (signal: AbortSignal) =>
      auditActivityApi.activity(
        JSON.parse(key) as ReturnType<typeof toActivityQuery>,
        {
          page,
          pageSize: PAGE_SIZE,
        },
        signal,
      ),
    [key, page],
  );
  const activity = useApiResource(load);
  const titleId = useId();

  return (
    <section className="activity-history" data-history="activity" aria-labelledby={titleId}>
      <h2 id={titleId}>{t('auditActivity.activity.title')}</h2>
      <p className="activity-history__intro">{t('auditActivity.activity.intro')}</p>
      <ActivityFilters key={params.toString()} values={values} />
      {activity.loading ? (
        <LoadingState />
      ) : activity.error !== null || activity.data === undefined ? (
        <ErrorState error={activity.error} message={auditProblemMessage(activity.error, t)} onRetry={activity.reload} />
      ) : activity.data.items.length === 0 ? (
        <EmptyState title={t('auditActivity.activity.empty')} />
      ) : (
        <>
          <ol className="activity-timeline" aria-label={t('auditActivity.activity.title')}>
            {activity.data.items.map((entry) => (
              <ActivityLine key={entry.id} entry={entry} />
            ))}
          </ol>
          <Pagination
            page={activity.data.page}
            pageSize={activity.data.pageSize}
            totalCount={activity.data.totalCount}
            onPageChange={goToPage}
          />
        </>
      )}
    </section>
  );
}
