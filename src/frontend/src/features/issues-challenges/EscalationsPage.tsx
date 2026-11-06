import { type ReactElement, useState } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { isForbidden } from '@/features/risks/problems.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { FilterBar } from '@/shared/ui/FilterBar.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { type ConcernType } from './api/types.ts';
import { EscalationTable } from './components/EscalationTable.tsx';
import {
  ANY,
  byEscalation,
  DEFAULT_ESCALATION_VIEW,
  ESCALATION_VIEWS,
  type EscalationView,
  matchesEscalationView,
  needsHistory,
} from './concernRules.ts';
import { concernProblemMessage } from './problems.ts';
import { useConcernLookups, useEscalationQueue, useRoleNames } from './useConcernData.ts';

const TYPES: readonly ConcernType[] = ['ISSUE', 'CHALLENGE'];

/**
 * SCR-087 Escalations: the issue and challenge escalations across the projects the person may see (WF-07's slice of
 * the cross-domain queue; WF-06 has no escalation yet). Acceptance criterion: it opens on the open escalations only,
 * and the status filter shows the resolved and withdrawn ones on request. Open and ended escalations are told apart
 * by their status in words, the count of open ones, open rows first and ended rows muted with when and by whom they
 * ended. A row opens SCR-088, where the escalation is resolved or withdrawn.
 */
export function EscalationsPage(): ReactElement {
  const { t } = useI18n();
  const [view, setView] = useState<EscalationView>(DEFAULT_ESCALATION_VIEW);
  const [type, setType] = useState(ANY);
  const queue = useEscalationQueue(needsHistory(view));
  const lookups = useConcernLookups();
  const roles = useRoleNames();
  const entries = queue.data ?? [];
  const personName = usePersonNames(
    entries.flatMap(({ escalation }) => [
      escalation.escalatedByUserId,
      escalation.resolvedByUserId,
    ]),
  );

  const filters = (
    <FilterBar
      fields={[
        {
          name: 'status',
          label: t('issuesChallenges.escalations.filter.status'),
          all: t(`issuesChallenges.escalations.filter.views.${DEFAULT_ESCALATION_VIEW}`),
          options: ESCALATION_VIEWS.filter((value) => value !== DEFAULT_ESCALATION_VIEW).map(
            (value) => ({
              value,
              label: t(`issuesChallenges.escalations.filter.views.${value}`),
            }),
          ),
        },
        {
          name: 'type',
          label: t('issuesChallenges.escalations.filter.type'),
          all: t('common.filters.any'),
          options: TYPES.map((value) => ({ value, label: t(`issuesChallenges.type.${value}`) })),
        },
      ]}
      values={{
        status: view === DEFAULT_ESCALATION_VIEW ? undefined : view,
        type: type === ANY ? undefined : type,
      }}
      onChange={(name, value) => {
        if (name === 'status') {
          setView(
            ESCALATION_VIEWS.find((candidate) => candidate === value) ?? DEFAULT_ESCALATION_VIEW,
          );
        } else {
          setType(value ?? ANY);
        }
      }}
      onClearAll={() => {
        setView(DEFAULT_ESCALATION_VIEW);
        setType(ANY);
      }}
    />
  );

  const header = (
    <PageHeader
      title={t('issuesChallenges.escalations.title')}
      description={t('issuesChallenges.escalations.description')}
    />
  );
  if (queue.data === undefined) {
    return (
      <>
        {header}
        {filters}
        {queue.loading ? (
          <LoadingState />
        ) : isForbidden(queue.error) ? (
          <p className="state">{t('issuesChallenges.forbidden')}</p>
        ) : (
          <ErrorState
            error={queue.error}
            message={concernProblemMessage(queue.error, t)}
            onRetry={queue.reload}
          />
        )}
      </>
    );
  }

  const open = entries.filter(({ escalation }) => escalation.status === 'OPEN').length;
  const shown = entries
    .filter(
      ({ escalation, concern }) =>
        matchesEscalationView(escalation, view) && (type === ANY || concern.concernType === type),
    )
    .sort(byEscalation);

  return (
    <>
      {header}
      <dl className="risk-summary">
        <div className="risk-summary__card" data-summary="open">
          <dt>{t('issuesChallenges.escalations.summary.open')}</dt>
          <dd className="figure">{open}</dd>
        </div>
      </dl>
      {filters}
      <p className="form__note" role="status">
        {t(`issuesChallenges.escalations.showing.${view}`, { count: shown.length })}
      </p>
      {shown.length === 0 ? (
        <EmptyState title={t(`issuesChallenges.escalations.empty.${view}`)}>
          {view === DEFAULT_ESCALATION_VIEW && (
            <p>{t('issuesChallenges.escalations.empty.hint')}</p>
          )}
        </EmptyState>
      ) : (
        <EscalationTable
          caption={t('issuesChallenges.escalations.caption')}
          entries={shown}
          roles={roles}
          itemLabel={lookups.itemLabel}
          personName={personName}
        />
      )}
    </>
  );
}
