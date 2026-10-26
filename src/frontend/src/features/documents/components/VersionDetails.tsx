import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { documentsApi } from '../api/documentsApi.ts';
import { type DocumentVersionDetail } from '../api/types.ts';
import { formatFileSize } from '../presentation.ts';

import { DownloadButton } from './DownloadButton.tsx';
import { ScanStateBadge } from './ScanState.tsx';

interface VersionDetailsProps {
  version: DocumentVersionDetail;
  uploadedBy: string;
}

/** SCR-123's latest version: file, uploader, upload time, scan state and checksum, with its download. */
export function VersionDetails({ version, uploadedBy }: VersionDetailsProps): ReactElement {
  const { t, formatDateTime } = useI18n();
  return (
    <>
      <dl className="details">
        <div className="details__row">
          <dt>{t('documents.fields.version')}</dt>
          <dd>{t('documents.fields.versionNo', { number: version.versionNo })}</dd>
        </div>
        <div className="details__row">
          <dt>{t('documents.fields.fileName')}</dt>
          <dd dir="auto" className="break-all">
            {version.fileName}
          </dd>
        </div>
        <div className="details__row">
          <dt>{t('documents.fields.size')}</dt>
          <dd dir="ltr">{formatFileSize(version.sizeBytes)}</dd>
        </div>
        <div className="details__row">
          <dt>{t('documents.fields.uploadedBy')}</dt>
          <dd>{uploadedBy}</dd>
        </div>
        <div className="details__row">
          <dt>{t('documents.fields.uploadedAt')}</dt>
          <dd>{formatDateTime(version.uploadedAt)}</dd>
        </div>
        <div className="details__row">
          <dt>{t('documents.fields.scanState')}</dt>
          <dd>
            <ScanStateBadge state={version.scanState} />
            {version.scanCompletedAt !== null && (
              <span className="details__aside">
                {t('documents.fields.scannedAt', {
                  time: formatDateTime(version.scanCompletedAt),
                })}
              </span>
            )}
          </dd>
        </div>
        <div className="details__row">
          <dt>{t('documents.fields.checksum')}</dt>
          <dd dir="ltr" className="break-all" title={version.checksumSha256}>
            <code>{version.checksumSha256}</code>
          </dd>
        </div>
      </dl>
      <p>
        <DownloadButton
          label={t('documents.download.action')}
          scanState={version.scanState}
          fileName={version.fileName}
          download={() => documentsApi.downloadVersion(version.documentId, version.id)}
        />
      </p>
    </>
  );
}
