import { type ReactElement } from 'react';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { ConfirmCommandDialog } from '@/features/risks/dialogs/ConfirmCommandDialog.tsx';
import { type MatrixState } from '@/features/risks/useRiskData.ts';
import { type ApiResource } from '@/shared/api/useApiResource.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';

import { type ConcernCommand, concernsApi } from '../api/concernsApi.ts';
import { isReturned } from '../concernRules.ts';
import { concernProblemMessage, isStale } from '../problems.ts';
import { type ConcernLookups, type ConcernRecord } from '../useConcernData.ts';

import { AssessConcernDialog } from './AssessConcernDialog.tsx';
import { AssignConcernDialog } from './AssignConcernDialog.tsx';
import { ConcernFormDialog } from './ConcernFormDialog.tsx';
import { EscalateDialog } from './EscalateDialog.tsx';
import { ResolveEscalationDialog, WithdrawEscalationDialog } from './EscalationCommandDialogs.tsx';
import { NarrativeCommandDialog } from './NarrativeCommandDialog.tsx';

/** The modal open on SCR-084 or SCR-086, one at a time; each acts on the concern the screen shows. */
export type ConcernDialog =
  | { kind: 'edit' } // MOD-037 Edit Issue
  | { kind: 'assess' }
  | { kind: 'assign' }
  | { kind: 'submit' }
  | { kind: 'escalate' } // MOD-039 Escalate Item
  | { kind: 'command'; command: ConcernCommand }
  | { kind: 'resolveEscalation'; escalationId: string }
  | { kind: 'withdrawEscalation'; escalationId: string };

interface ConcernDialogsProps {
  dialog: ConcernDialog | null;
  onDialogChange: (dialog: ConcernDialog | null) => void;
  record: ConcernRecord;
  project: ProjectSummary;
  user: SessionUser;
  lookups: ConcernLookups;
  matrixState: ApiResource<MatrixState>;
  /** Something was saved (or overtaken): the screen reads the concern again and, given a message, says so. */
  onChanged: (message: TranslationKey, tone?: 'success' | 'warning') => void;
}

export function ConcernDialogs({
  dialog,
  onDialogChange,
  record,
  project,
  user,
  lookups,
  matrixState,
  onChanged,
}: ConcernDialogsProps): ReactElement | null {
  const { t } = useI18n();
  if (dialog === null) {
    return null;
  }
  const { concern } = record;
  const close = () => {
    onDialogChange(null);
  };
  const stale = () => {
    onChanged('issuesChallenges.done.stale', 'warning');
    close();
  };
  const done = (message: TranslationKey) => () => {
    onChanged(message);
    close();
  };

  switch (dialog.kind) {
    case 'edit':
      return (
        <ConcernFormDialog
          editing={concern}
          concernType={concern.data.concernType}
          project={project}
          lookups={lookups}
          onClose={close}
          onDone={done('issuesChallenges.done.updated')}
          onStale={stale}
        />
      );
    case 'assess':
      return (
        <AssessConcernDialog
          concern={concern}
          matrixState={matrixState}
          lookups={lookups}
          onClose={close}
          onDone={done('issuesChallenges.done.assessed')}
          onStale={stale}
        />
      );
    case 'assign':
      return (
        <AssignConcernDialog
          concern={concern}
          project={project}
          user={user}
          onClose={close}
          onDone={done('issuesChallenges.done.assigned')}
          onStale={stale}
        />
      );
    case 'submit':
      return (
        <NarrativeCommandDialog
          title={t('issuesChallenges.submit.title', { concern: concern.data.title.text })}
          notes={[
            t('issuesChallenges.submit.validation'),
            t('issuesChallenges.submit.frozen'),
            t('issuesChallenges.submit.ownDecision'),
          ]}
          field="resolution"
          label={t('issuesChallenges.fields.resolution')}
          hint={t('issuesChallenges.submit.hint')}
          initialText={isReturned(concern.data) ? (concern.data.resolution?.text ?? '') : ''}
          confirmLabel={t('issuesChallenges.submit.confirm')}
          run={(resolution) =>
            concernsApi.submitResolution(concern.data.id, { resolution }, concern.etag)
          }
          onClose={close}
          onDone={done('issuesChallenges.done.submitted')}
          onStale={stale}
        />
      );
    case 'escalate':
      return (
        <EscalateDialog
          concern={concern.data}
          onClose={close}
          onDone={done('issuesChallenges.done.escalated')}
          onStale={stale}
        />
      );
    case 'command': {
      const { command } = dialog;
      return (
        <ConfirmCommandDialog
          title={t(`issuesChallenges.command.${command}.title`)}
          consequence={t(`issuesChallenges.command.${command}.consequence`)}
          confirmLabel={t(`issuesChallenges.command.${command}.confirm`)}
          run={() => concernsApi.command(concern.data.id, command, concern.etag)}
          describe={concernProblemMessage}
          isStale={isStale}
          onClose={close}
          onDone={done(`issuesChallenges.command.${command}.done`)}
          onStale={stale}
        />
      );
    }
    case 'resolveEscalation':
    case 'withdrawEscalation': {
      const escalation = record.escalations.find((entry) => entry.id === dialog.escalationId);
      if (escalation === undefined) {
        return null;
      }
      const Command =
        dialog.kind === 'resolveEscalation' ? ResolveEscalationDialog : WithdrawEscalationDialog;
      return (
        <Command
          escalation={escalation}
          onClose={close}
          onDone={done(
            dialog.kind === 'resolveEscalation'
              ? 'issuesChallenges.done.escalationResolved'
              : 'issuesChallenges.done.escalationWithdrawn',
          )}
          onStale={stale}
        />
      );
    }
  }
}
