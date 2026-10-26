import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type DocumentVersionDetail } from '../api/types.ts';
import { ScanStateNotice } from '../components/ScanState.tsx';

interface UploadStatusProps {
  /** The share of the file sent, 0 to 1, while the request is in flight; null once the API has answered. */
  progress: number | null;
  /** The version the API created, as last read; null until it answers. */
  version: DocumentVersionDetail | null;
}

/**
 * MOD-050 and MOD-052's progress: uploading (with the share sent), then the scan's state — scanning, clean,
 * quarantined or scan failed — each with its own word, tone and explanation (TASK-038 acceptance criterion 1).
 */
export function UploadStatus({ progress, version }: UploadStatusProps): ReactElement | null {
  const { t } = useI18n();
  if (version !== null) {
    return <ScanStateNotice state={version.scanState} />;
  }
  if (progress === null) {
    return null;
  }
  const percent = Math.round(progress * 100);
  return (
    <div className="scan-notice scan-notice--info" role="status">
      <p className="scan-notice__title">
        <StatusBadge label={t('documents.upload.uploading')} tone="info" />{' '}
        {t('documents.upload.sent', { percent })}
      </p>
      <progress
        className="upload-progress"
        max={100}
        value={percent}
        aria-label={t('documents.upload.progressLabel')}
      />
    </div>
  );
}
