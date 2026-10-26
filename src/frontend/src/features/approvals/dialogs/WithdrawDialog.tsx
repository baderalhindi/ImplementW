import { type ReactElement } from 'react';

import { ConfirmDialog } from '@/features/identity-access/components/ConfirmDialog.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { approvalInstancesApi } from '../api/approvalsApi.ts';
import { approvalProblemMessage } from '../problems.ts';

interface WithdrawDialogProps {
  /** The PENDING run to withdraw; null while closed. */
  instanceId: string | null;
  onClose: () => void;
  onDone: () => void;
}

/** MOD-044 Withdraw, by the requester: the run ends WITHDRAWN and its open tasks are cancelled. */
export function WithdrawDialog({ instanceId, onClose, onDone }: WithdrawDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <ConfirmDialog
      confirmation={
        instanceId === null
          ? null
          : {
              title: t('approvals.withdraw.title'),
              body: t('approvals.withdraw.body'),
              confirmLabel: t('approvals.withdraw.confirm'),
              destructive: true,
              action: () => approvalInstancesApi.withdraw(instanceId),
              describeProblem: approvalProblemMessage,
            }
      }
      onClose={onClose}
      onDone={onDone}
    />
  );
}
