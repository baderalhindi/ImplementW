import { type ReactElement } from 'react';

import { ConfirmCommandDialog } from '@/features/risks/dialogs/ConfirmCommandDialog.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { concernsApi } from '../api/concernsApi.ts';
import { type ConcernEscalationDetail } from '../api/types.ts';
import { concernProblemMessage, isStale } from '../problems.ts';

import { NarrativeCommandDialog } from './NarrativeCommandDialog.tsx';

interface EscalationCommandProps {
  escalation: Pick<ConcernEscalationDetail, 'id' | 'escalationNo'>;
  onClose: () => void;
  onDone: () => void;
  onStale: () => void;
}

/** OPEN → RESOLVED with the management direction, by the role it is addressed to. The concern's status stays. */
export function ResolveEscalationDialog({
  escalation,
  onClose,
  onDone,
  onStale,
}: EscalationCommandProps): ReactElement {
  const { t } = useI18n();
  return (
    <NarrativeCommandDialog
      title={t('issuesChallenges.resolveEscalation.title', { number: escalation.escalationNo })}
      notes={[t('issuesChallenges.resolveEscalation.statusKept')]}
      field="resolution"
      label={t('issuesChallenges.fields.direction')}
      hint={t('issuesChallenges.resolveEscalation.hint')}
      confirmLabel={t('issuesChallenges.resolveEscalation.confirm')}
      run={(resolution) => concernsApi.resolveEscalation(escalation.id, { resolution })}
      onClose={onClose}
      onDone={onDone}
      onStale={onStale}
    />
  );
}

/** OPEN → WITHDRAWN, by its escalator. */
export function WithdrawEscalationDialog({
  escalation,
  onClose,
  onDone,
  onStale,
}: EscalationCommandProps): ReactElement {
  const { t } = useI18n();
  return (
    <ConfirmCommandDialog
      title={t('issuesChallenges.withdrawEscalation.title', { number: escalation.escalationNo })}
      consequence={t('issuesChallenges.withdrawEscalation.consequence')}
      confirmLabel={t('issuesChallenges.withdrawEscalation.confirm')}
      run={() => concernsApi.withdrawEscalation(escalation.id)}
      describe={concernProblemMessage}
      isStale={isStale}
      onClose={onClose}
      onDone={onDone}
      onStale={onStale}
    />
  );
}
