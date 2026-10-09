import { type ReactElement } from 'react';

import { useListParams } from '@/shared/api/useListParams.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { CheckboxField, SelectField } from '@/shared/ui/FormFields.tsx';
import { EmptyState } from '@/shared/ui/States.tsx';

import { type ExternalUpdateRequestDetail } from '../api/types.ts';
import { DUE_CONDITIONS, REQUEST_STATUSES } from '../rules.ts';

import { RequestTable } from './RequestTable.tsx';

interface RequestRegisterViewProps {
  requests: ExternalUpdateRequestDetail[];
  audience: 'internal' | 'external';
  caption: string;
  /** The signed-in person: "mine" is the requests they review (AHDA) or answer (an entity). */
  userId: string;
  showProject?: boolean;
}

/**
 * A request list's filters — status, due condition, and the person's own (to review, or to answer) — kept in the
 * address, and its table. The statuses offered are those the rows carry. Nothing is counted beyond the rows the API
 * returned to this person (WF-13 §12.3 SCR-163: no unauthorized counts).
 */
export function RequestRegisterView({
  requests,
  audience,
  caption,
  userId,
  showProject = true,
}: RequestRegisterViewProps): ReactElement {
  const { t } = useI18n();
  const { params, setFilter } = useListParams();
  const status = params.get('status') ?? '';
  const due = params.get('due') ?? '';
  const mine = params.get('mine') === 'true';
  const present = REQUEST_STATUSES.filter((candidate) =>
    requests.some((request) => request.status === candidate),
  );
  const shown = requests.filter(
    (request) =>
      (status === '' || request.status === status) &&
      (due === '' || request.dueCondition === due) &&
      (!mine ||
        (audience === 'internal'
          ? request.reviewerUserId === userId
          : request.responsibleUserId === userId)),
  );

  return (
    <>
      <div className="filters">
        <SelectField
          label={t('externalParticipation.filter.status')}
          name="status"
          value={status}
          placeholder={t('externalParticipation.filter.allStatuses')}
          options={present.map((value) => ({
            value,
            label: t(`externalParticipation.requestStatus.${value}`),
          }))}
          onChange={(value) => {
            setFilter('status', value);
          }}
        />
        <SelectField
          label={t('externalParticipation.filter.due')}
          name="due"
          value={due}
          placeholder={t('externalParticipation.filter.anyDue')}
          options={DUE_CONDITIONS.map((value) => ({
            value,
            label: t(`externalParticipation.dueCondition.${value}`),
          }))}
          onChange={(value) => {
            setFilter('due', value);
          }}
        />
        <CheckboxField
          label={t(
            audience === 'internal'
              ? 'externalParticipation.filter.mineToReview'
              : 'externalParticipation.filter.mineToAnswer',
          )}
          name="mine"
          checked={mine}
          onChange={(checked) => {
            setFilter('mine', checked ? 'true' : '');
          }}
        />
      </div>
      <p className="list-order" role="status">
        {t('externalParticipation.filter.showing', { shown: shown.length, total: requests.length })}
      </p>
      {shown.length === 0 ? (
        <EmptyState title={t('externalParticipation.filter.noMatch')} />
      ) : (
        <RequestTable
          requests={shown}
          caption={caption}
          audience={audience}
          showProject={showProject}
        />
      )}
    </>
  );
}
