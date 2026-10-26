import { type ReactElement } from 'react';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { type ApiResource } from '@/shared/api/useApiResource.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';

import { risksApi } from '../api/risksApi.ts';
import { isLiveAction } from '../riskRules.ts';
import { type MatrixState, type RiskLookups, type RiskRecord } from '../useRiskData.ts';

import { AcceptRiskDialog } from './AcceptRiskDialog.tsx';
import { AssessRiskDialog } from './AssessRiskDialog.tsx';
import { AssignOwnerDialog } from './AssignOwnerDialog.tsx';
import { CloseRiskDialog } from './CloseRiskDialog.tsx';
import { ConfirmCommandDialog } from './ConfirmCommandDialog.tsx';
import { type RiskDialog } from './riskDialog.ts';
import { RiskFormDialog } from './RiskFormDialog.tsx';
import { TreatmentActionDialog } from './TreatmentActionDialog.tsx';

interface RiskDialogsProps {
  dialog: RiskDialog | null;
  onDialogChange: (dialog: RiskDialog | null) => void;
  record: RiskRecord;
  project: ProjectSummary;
  user: SessionUser;
  lookups: RiskLookups;
  matrixState: ApiResource<MatrixState>;
  /** Something was saved (or overtaken): the screen reads the risk again and, given a message, says so. */
  onChanged: (message: TranslationKey, tone?: 'success' | 'warning') => void;
}

/** SCR-082's modals, MOD-031 to MOD-035 and the risk's commands, one at a time. */
export function RiskDialogs({
  dialog,
  onDialogChange,
  record,
  project,
  user,
  lookups,
  matrixState,
  onChanged,
}: RiskDialogsProps): ReactElement | null {
  const { t } = useI18n();
  if (dialog === null) {
    return null;
  }
  const { risk } = record;
  const close = () => {
    onDialogChange(null);
  };
  const stale = () => {
    onChanged('risks.done.stale', 'warning');
    close();
  };
  const done = (message: TranslationKey) => () => {
    onChanged(message);
    close();
  };
  const knownUserIds = record.actions.map((action) => action.ownerUserId);

  switch (dialog.kind) {
    case 'edit':
      return (
        <RiskFormDialog
          editing={risk}
          project={project}
          user={user}
          lookups={lookups}
          knownUserIds={knownUserIds}
          onClose={close}
          onDone={done('risks.done.updated')}
          onStale={stale}
        />
      );
    case 'assess':
      return (
        <AssessRiskDialog
          risk={risk}
          previous={record.assessments[0] ?? null}
          matrixState={matrixState}
          lookups={lookups}
          onClose={close}
          onDone={done('risks.done.assessed')}
          onStale={stale}
        />
      );
    case 'owner':
      return (
        <AssignOwnerDialog
          risk={risk}
          project={project}
          user={user}
          knownUserIds={knownUserIds}
          onClose={close}
          onDone={done('risks.done.ownerAssigned')}
          onStale={stale}
        />
      );
    case 'action':
      return (
        <TreatmentActionDialog
          risk={risk.data}
          actionId={dialog.actionId}
          project={project}
          user={user}
          knownUserIds={knownUserIds}
          onClose={close}
          onDone={done(
            dialog.actionId === null ? 'risks.done.actionCreated' : 'risks.done.actionUpdated',
          )}
          onStale={stale}
        />
      );
    case 'close':
      return (
        <CloseRiskDialog
          risk={risk}
          liveActions={record.actions.filter(isLiveAction).length}
          onClose={close}
          onDone={done('risks.done.closed')}
          onStale={stale}
        />
      );
    case 'accept':
      return (
        <AcceptRiskDialog
          risk={risk}
          onClose={close}
          onDone={done('risks.done.accepted')}
          onStale={stale}
        />
      );
    case 'command': {
      const { command } = dialog;
      return (
        <ConfirmCommandDialog
          title={t(`risks.command.${command}.title`)}
          consequence={t(`risks.command.${command}.consequence`)}
          confirmLabel={t(`risks.command.${command}.confirm`)}
          run={() => risksApi.command(risk.data.id, command, risk.etag)}
          onClose={close}
          onDone={done(`risks.command.${command}.done`)}
          onStale={stale}
        />
      );
    }
    case 'actionCommand': {
      const { actionId, command } = dialog;
      return (
        <ConfirmCommandDialog
          title={t(`risks.actionCommand.${command}.title`)}
          consequence={t(`risks.actionCommand.${command}.consequence`)}
          confirmLabel={t(`risks.actionCommand.${command}.confirm`)}
          run={async () => {
            // An action's ETag is its own, read now: the list carries none.
            const { etag } = await risksApi.action(actionId);
            return risksApi.actionCommand(actionId, command, etag);
          }}
          onClose={close}
          onDone={done(`risks.actionCommand.${command}.done`)}
          onStale={stale}
        />
      );
    }
  }
}
