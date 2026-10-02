import { type ReactElement } from 'react';

import { ConfirmDialog } from '@/features/identity-access/components/ConfirmDialog.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { projectsApi } from '../api/projectsApi.ts';
import { type ProjectDetail } from '../api/types.ts';
import { projectProblemMessage } from '../problems.ts';

interface DeleteDraftDialogProps {
  open: boolean;
  project: ProjectDetail;
  etag: string | null;
  onClose: () => void;
  onDeleted: () => void;
}

/**
 * MOD-001 Delete Draft: a DRAFT is deleted outright (HARD_DRAFT) by the person who created it (TASK-041 D-11). It is
 * confirmed first because nothing brings it back. A draft another record names is refused (409 PROJECT_IN_USE).
 */
export function DeleteDraftDialog({
  open,
  project,
  etag,
  onClose,
  onDeleted,
}: DeleteDraftDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <ConfirmDialog
      confirmation={
        open
          ? {
              title: t('projects.delete.title'),
              body: t('projects.delete.body', { title: project.title.text }),
              confirmLabel: t('projects.delete.confirm'),
              destructive: true,
              action: () => projectsApi.remove(project.id, etag),
              describeProblem: projectProblemMessage,
            }
          : null
      }
      onClose={onClose}
      onDone={onDeleted}
    />
  );
}
