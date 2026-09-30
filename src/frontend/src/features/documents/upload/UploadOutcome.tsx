import { type ReactElement, type ReactNode } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { FormAlert } from '@/shared/ui/States.tsx';

import { documentProblemMessage } from '../problems.ts';

import { type FileUpload } from './useFileUpload.ts';
import { UploadStatus } from './UploadStatus.tsx';

interface UploadOutcomeProps {
  upload: FileUpload;
  onClose: () => void;
  /** Where to go once the file is received, e.g. the new document. */
  link: ReactNode;
}

/**
 * The dialog while a file is sent and scanned. Closing while it is sent cancels the upload; closing after leaves the
 * scan running, and the document's page shows its result.
 */
export function UploadOutcome({ upload, onClose, link }: UploadOutcomeProps): ReactElement {
  const { t } = useI18n();
  const sending = upload.progress !== null;
  return (
    <div className="form">
      <UploadStatus progress={upload.progress} version={upload.version} />
      <FormAlert
        message={
          upload.watchError === null
            ? null
            : `${t('documents.upload.watchFailed')} ${documentProblemMessage(upload.watchError, t)}`
        }
      />
      {upload.version?.scanState === 'SCAN_PENDING' && (
        <p className="form__note">{t('documents.upload.closeWhileScanning')}</p>
      )}
      <div className="form__actions">
        {!sending && link}
        <button type="button" className="button" onClick={onClose}>
          {sending ? t('documents.upload.cancel') : t('documents.upload.close')}
        </button>
      </div>
    </div>
  );
}
