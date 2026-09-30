import { type ReactElement } from 'react';

import { ConfirmDialog } from '@/features/identity-access/components/ConfirmDialog.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { documentsApi } from '../api/documentsApi.ts';
import { documentProblemMessage } from '../problems.ts';

import { type EditTarget } from './EditMetadataDialog.tsx';

interface ArchiveDocumentDialogProps {
  target: EditTarget | null;
  onClose: () => void;
  onDone: () => void;
}

/**
 * MOD-053 Archive: ACTIVE → ARCHIVED. Nothing is deleted; versions, links and evidence stay readable, and the document
 * takes no new version or change.
 */
export function ArchiveDocumentDialog({
  target,
  onClose,
  onDone,
}: ArchiveDocumentDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <ConfirmDialog
      confirmation={
        target === null
          ? null
          : {
              title: t('documents.archive.title'),
              body: t('documents.archive.body'),
              confirmLabel: t('documents.archive.confirm'),
              destructive: true,
              action: () => documentsApi.archive(target.document.id, target.etag),
              describeProblem: documentProblemMessage,
            }
      }
      onClose={onClose}
      onDone={onDone}
    />
  );
}
