import { type ReactElement, useCallback, useState } from 'react';
import { Link, useParams } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader, TableContainer } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { documentsApi } from '../api/documentsApi.ts';
import { type DocumentVersionDetail } from '../api/types.ts';
import { DownloadButton } from '../components/DownloadButton.tsx';
import { ScanStateBadge } from '../components/ScanState.tsx';
import { formatFileSize, shortChecksum } from '../presentation.ts';
import { documentProblemMessage } from '../problems.ts';

/**
 * SCR-124 Version History: every version, newest first, as the API lists it (unpaged, TASK-037 F-11), each with its
 * uploader, upload time, scan state and checksum. Only a CLEAN version can be downloaded; a SCAN_FAILED one can be
 * queued for scanning again (DOCUMENT_MANAGE).
 */
export function VersionHistoryPage(): ReactElement {
  const { t } = useI18n();
  const { documentId = '' } = useParams();
  const load = useCallback(
    async (signal: AbortSignal) => {
      const [document, versions] = await Promise.all([
        documentsApi.get(documentId, signal),
        documentsApi.listVersions(documentId, signal),
      ]);
      return { document: document.data, versions };
    },
    [documentId],
  );
  const history = useApiResource(load);
  const [notice, setNotice] = useState<Notice | null>(null);

  const title =
    history.data === undefined
      ? t('documents.versions.title')
      : t('documents.versions.titleOf', { title: history.data.document.title.text });
  return (
    <>
      <PageHeader
        title={title}
        description={t('documents.versions.description')}
        actions={
          <Link className="button" to={`/documents/${documentId}`}>
            {t('documents.versions.backToDocument')}
          </Link>
        }
      />
      <PageNotice notice={notice} />
      {history.loading && <LoadingState label={t('documents.versions.loading')} />}
      {history.error !== null && (
        <ErrorState message={documentProblemMessage(history.error, t)} onRetry={history.reload} />
      )}
      {history.data !== undefined &&
        (history.data.versions.length === 0 ? (
          <EmptyState title={t('documents.versions.empty')} />
        ) : (
          <VersionTable
            versions={history.data.versions}
            onRescanned={() => {
              setNotice({ tone: 'success', message: t('documents.done.rescanQueued') });
              history.reload();
            }}
          />
        ))}
    </>
  );
}

function VersionTable({
  versions,
  onRescanned,
}: {
  versions: DocumentVersionDetail[];
  onRescanned: () => void;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  const person = usePersonNames(versions.map((version) => version.uploadedByUserId));
  return (
    <TableContainer caption={t('documents.versions.caption')}>
      <thead>
        <tr>
          <th scope="col">{t('documents.fields.version')}</th>
          <th scope="col">{t('documents.fields.fileName')}</th>
          <th scope="col">{t('documents.fields.size')}</th>
          <th scope="col">{t('documents.fields.uploadedBy')}</th>
          <th scope="col">{t('documents.fields.uploadedAt')}</th>
          <th scope="col">{t('documents.fields.scanState')}</th>
          <th scope="col">{t('documents.fields.checksum')}</th>
          <th scope="col">{t('common.table.actions')}</th>
        </tr>
      </thead>
      <tbody>
        {versions.map((version) => {
          const versionId = `version-${version.id}`;
          return (
            <tr
              key={version.id}
              className={version.scanState === 'QUARANTINED' ? 'row--flagged' : undefined}
            >
              <th scope="row" id={versionId}>
                {t('documents.fields.versionNo', { number: version.versionNo })}
              </th>
              <td dir="auto" className="break-all">
                {version.fileName}
              </td>
              <td dir="ltr">{formatFileSize(version.sizeBytes)}</td>
              <td>{person(version.uploadedByUserId)}</td>
              <td>{formatDateTime(version.uploadedAt)}</td>
              <td>
                <ScanStateBadge state={version.scanState} />
                {version.scanCompletedAt !== null && (
                  <span className="cell__aside">{formatDateTime(version.scanCompletedAt)}</span>
                )}
              </td>
              <td dir="ltr" title={version.checksumSha256}>
                <code>{shortChecksum(version.checksumSha256)}</code>
              </td>
              <td className="cell--actions cell--actions-wrap">
                <DownloadButton
                  label={t('documents.download.action')}
                  scanState={version.scanState}
                  fileName={version.fileName}
                  download={() => documentsApi.downloadVersion(version.documentId, version.id)}
                  describedBy={versionId}
                />
                {version.scanState === 'SCAN_FAILED' && (
                  <RescanButton version={version} describedBy={versionId} onDone={onRescanned} />
                )}
              </td>
            </tr>
          );
        })}
      </tbody>
    </TableContainer>
  );
}

function RescanButton({
  version,
  describedBy,
  onDone,
}: {
  version: DocumentVersionDetail;
  describedBy: string;
  onDone: () => void;
}): ReactElement {
  const { t } = useI18n();
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const rescan = async () => {
    setBusy(true);
    setProblem(null);
    try {
      await documentsApi.rescan(version.documentId, version.id);
      onDone();
    } catch (error) {
      setProblem(documentProblemMessage(error, t));
    } finally {
      setBusy(false);
    }
  };
  return (
    <>
      <button
        type="button"
        className="button"
        disabled={busy}
        aria-describedby={describedBy}
        onClick={() => void rescan()}
      >
        {busy ? t('common.states.saving') : t('documents.rescan.action')}
      </button>
      {problem !== null && (
        <span className="field__error" role="alert">
          {problem}
        </span>
      )}
    </>
  );
}
