import { type ReactElement } from 'react';

import { documentsApi } from '@/features/documents/api/documentsApi.ts';
import { AttachDocumentDialog } from '@/features/documents/dialogs/AttachDocumentDialog.tsx';
import { UploadDocumentDialog } from '@/features/documents/dialogs/UploadDocumentDialog.tsx';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';

import { milestonesApi } from '../api/milestonesApi.ts';
import { milestoneProblemMessage } from '../problems.ts';
import { type MilestoneLookups } from '../useMilestoneData.ts';

import { AchievementDialog } from './AchievementDialog.tsx';
import { type MilestoneDialog, returnTo } from './milestoneDialog.ts';
import { MilestoneFormDialog } from './MilestoneFormDialog.tsx';

interface MilestoneDialogsProps {
  dialog: MilestoneDialog | null;
  onDialogChange: (dialog: MilestoneDialog | null) => void;
  project: ProjectSummary;
  user: SessionUser;
  lookups: MilestoneLookups;
  /** Something was saved (or overtaken): the screen reads its milestones again and, given a message, says so. */
  onChanged: (message: TranslationKey | null, tone?: 'success' | 'warning') => void;
}

/** MOD-016, MOD-019 and the WF-12 modals MOD-019 opens (MOD-054 Attach, MOD-050 Upload), one at a time. */
export function MilestoneDialogs({
  dialog,
  onDialogChange,
  project,
  user,
  lookups,
  onChanged,
}: MilestoneDialogsProps): ReactElement | null {
  const { t } = useI18n();
  if (dialog === null) {
    return null;
  }
  const back = returnTo(dialog);
  const close = () => {
    onDialogChange(back === null ? null : { kind: 'achievement', milestoneId: back });
  };

  switch (dialog.kind) {
    case 'achievement':
      return (
        <AchievementDialog
          key={dialog.milestoneId}
          milestoneId={dialog.milestoneId}
          project={project}
          user={user}
          lookups={lookups}
          onOpen={onDialogChange}
          onClose={close}
          onChanged={() => {
            onChanged(null);
          }}
        />
      );
    case 'create':
    case 'edit':
      return (
        <MilestoneFormDialog
          milestoneId={dialog.kind === 'edit' ? dialog.milestoneId : null}
          project={project}
          lookups={lookups}
          onClose={close}
          onStale={() => {
            onChanged('milestones.done.stale', 'warning');
            close();
          }}
          onDone={(milestone) => {
            onChanged(
              dialog.kind === 'edit' ? 'milestones.done.updated' : 'milestones.done.created',
            );
            onDialogChange({ kind: 'achievement', milestoneId: milestone.id });
          }}
        />
      );
    case 'attach': {
      const { achievementId, etag, mandatory } = dialog;
      // The mandatory types first, then every other PUBLISHED one; a mandatory type is offered even unnamed.
      const evidenceTypes = [
        ...mandatory.map((id) => ({ value: id, label: lookups.itemLabel(id) })),
        ...lookups.evidenceTypeOptions.filter((option) => !mandatory.includes(option.value)),
      ];
      return (
        <AttachDocumentDialog
          open
          projectId={project.id}
          targetLabel={t('milestones.evidence.target')}
          evidenceTypes={evidenceTypes}
          describe={milestoneProblemMessage}
          onAttach={async (document, evidenceTypeItemId) => {
            // Evidence pins a version (TASK-050 D-8): the document's latest, which the list showed as clean.
            const { data } = await documentsApi.get(document.id);
            if (data.latestVersion === null || evidenceTypeItemId === null) {
              throw new ApiError(409, 'DOCUMENT_NOT_AVAILABLE', [], null);
            }
            return milestonesApi.attachEvidence(
              achievementId,
              {
                documentId: document.id,
                documentVersionId: data.latestVersion.id,
                evidenceTypeItemId,
              },
              etag,
            );
          }}
          onClose={close}
          onDone={() => {
            onChanged('milestones.done.evidenceAttached');
            close();
          }}
        />
      );
    }
    case 'upload':
      return (
        <UploadDocumentDialog
          open
          projectId={project.id}
          onClose={close}
          onUploaded={() => {
            onChanged(null);
          }}
        />
      );
  }
}
