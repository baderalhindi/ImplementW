import { type ReactElement, useRef } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { concernsApi } from '../api/concernsApi.ts';
import { type ConcernDetail } from '../api/types.ts';

import { NarrativeCommandDialog } from './NarrativeCommandDialog.tsx';

interface EscalateDialogProps {
  concern: Pick<ConcernDetail, 'id' | 'title' | 'concernType'>;
  onClose: () => void;
  onDone: () => void;
  onStale: () => void;
}

/**
 * MOD-039 Escalate Item: a reason only. Where it goes is the server's routing (WORKFLOW_POLICY), and the dialog says
 * that escalation is not approval and does not change the concern's status (ISS-GP-06).
 *
 * The dialog keeps one Idempotency-Key per reason: pressing "Escalate" again after an answer was lost resends the same
 * key, so the API finds the escalation already raised and nothing is notified twice (TASK-057 D-6). A different reason
 * is a different request and gets its own key; if the first one did reach the API, that one is refused as already open.
 */
export function EscalateDialog({
  concern,
  onClose,
  onDone,
  onStale,
}: EscalateDialogProps): ReactElement {
  const { t } = useI18n();
  const sent = useRef<{ reason: string; key: string } | null>(null);

  const keyFor = (reason: string): string => {
    if (sent.current?.reason !== reason) {
      sent.current = { reason, key: crypto.randomUUID() };
    }
    return sent.current.key;
  };

  return (
    <NarrativeCommandDialog
      title={t('issuesChallenges.escalate.title', { concern: concern.title.text })}
      notes={[
        t('issuesChallenges.escalate.notApproval'),
        t(
          concern.concernType === 'ISSUE'
            ? 'issuesChallenges.escalate.statusKeptIssue'
            : 'issuesChallenges.escalate.statusKeptChallenge',
        ),
        t('issuesChallenges.escalate.routing'),
      ]}
      field="reason"
      label={t('issuesChallenges.fields.reason')}
      hint={t('issuesChallenges.escalate.reasonHint')}
      confirmLabel={t('issuesChallenges.escalate.confirm')}
      run={(reason) =>
        concernsApi.escalate({ managementConcernId: concern.id, reason }, keyFor(reason.text))
      }
      onClose={onClose}
      onDone={onDone}
      onStale={onStale}
    />
  );
}
