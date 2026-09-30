import { type ReactElement, useCallback, useState } from 'react';
import { Link, useParams } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { documentsApi } from '../api/documentsApi.ts';
import { type DocumentDetail } from '../api/types.ts';
import { ScanStateNotice } from '../components/ScanState.tsx';
import { VersionDetails } from '../components/VersionDetails.tsx';
import { ArchiveDocumentDialog } from '../dialogs/ArchiveDocumentDialog.tsx';
import { EditMetadataDialog, type EditTarget } from '../dialogs/EditMetadataDialog.tsx';
import { ReplaceVersionDialog } from '../dialogs/ReplaceVersionDialog.tsx';
import { documentTone, languageTag, shortId } from '../presentation.ts';
import { documentProblemMessage } from '../problems.ts';
import { useScanWatch } from '../upload/useScanWatch.ts';
import { useDocumentLookups } from '../useDocumentLookups.ts';

import { DocumentLinks } from './DocumentLinks.tsx';

type OpenDialog = 'edit' | 'replace' | 'archive' | null;

/**
 * SCR-123 Document Detail: the metadata, the latest version with its scan state, and where the document is used.
 * A version that is not CLEAN is flagged above everything else and its download is disabled (TASK-038 D-4); replacing
 * it with a new version stays possible, since that is the remedy. An ARCHIVED document takes no change.
 */
export function DocumentDetailPage(): ReactElement {
  const { t } = useI18n();
  const { documentId = '' } = useParams();
  const load = useCallback(
    (signal: AbortSignal) => documentsApi.get(documentId, signal),
    [documentId],
  );
  const resource = useApiResource(load);
  const [dialog, setDialog] = useState<OpenDialog>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  // A version added through MOD-052: the page reads again when the dialog closes, so the dialog can show the scan.
  const [versionAdded, setVersionAdded] = useState(false);

  if (resource.loading) {
    return <LoadingState label={t('documents.detail.loading')} />;
  }
  if (resource.data === undefined) {
    return (
      <>
        <PageHeader title={t('documents.detail.fallbackTitle')} />
        <ErrorState message={documentProblemMessage(resource.error, t)} onRetry={resource.reload} />
      </>
    );
  }

  const { data: document, etag } = resource.data;
  const target: EditTarget = { document, etag };
  const archived = document.status === 'ARCHIVED';
  const closeDialog = () => {
    setDialog(null);
    if (versionAdded) {
      setVersionAdded(false);
      setNotice({ tone: 'success', message: t('documents.done.versionAdded') });
      resource.reload();
    }
  };
  const done = (message: string) => {
    setDialog(null);
    setNotice({ tone: 'success', message });
    resource.reload();
  };
  const open = (next: OpenDialog) => () => {
    setNotice(null);
    setDialog(next);
  };

  return (
    <>
      <PageHeader
        title={document.title.text}
        actions={
          <>
            <Link className="button" to={`/documents/${document.id}/versions`}>
              {t('documents.versions.action')}
            </Link>
            <button type="button" className="button" disabled={archived} onClick={open('replace')}>
              {t('documents.replace.action')}
            </button>
            <button type="button" className="button" disabled={archived} onClick={open('edit')}>
              {t('documents.edit.action')}
            </button>
            {!archived && (
              <button type="button" className="button button--danger" onClick={open('archive')}>
                {t('documents.archive.action')}
              </button>
            )}
          </>
        }
      />
      <PageNotice notice={notice} />
      {archived && <p className="notice notice--warning">{t('documents.detail.archived')}</p>}

      <DocumentBody document={document} />

      <ReplaceVersionDialog
        documentId={dialog === 'replace' ? document.id : null}
        onClose={closeDialog}
        onUploaded={() => {
          setVersionAdded(true);
        }}
      />
      <EditMetadataDialog
        target={dialog === 'edit' ? target : null}
        onClose={closeDialog}
        onDone={() => {
          done(t('documents.done.edited'));
        }}
      />
      <ArchiveDocumentDialog
        target={dialog === 'archive' ? target : null}
        onClose={closeDialog}
        onDone={() => {
          done(t('documents.done.archived'));
        }}
      />
    </>
  );
}

function DocumentBody({ document }: { document: DocumentDetail }): ReactElement {
  const { t, formatDateTime } = useI18n();
  const lookups = useDocumentLookups();
  const person = usePersonNames([
    document.ownerUserId,
    document.latestVersion?.uploadedByUserId ?? null,
  ]);
  // A version still being scanned is read again until the scan decides, so the page shows the verdict unprompted.
  const { version } = useScanWatch(document.latestVersion);

  return (
    <>
      {version !== null && version.scanState !== 'CLEAN' && (
        <ScanStateNotice state={version.scanState} />
      )}

      <section className="section" aria-labelledby="document-details">
        <h2 id="document-details">{t('documents.detail.details')}</h2>
        <dl className="details">
          <div className="details__row">
            <dt>{t('documents.fields.status')}</dt>
            <dd>
              <StatusBadge
                label={t(`documents.status.${document.status}`)}
                tone={documentTone(document.status)}
              />
            </dd>
          </div>
          <div className="details__row">
            <dt>{t('documents.fields.description')}</dt>
            <dd>
              {document.description === null ? (
                t('common.values.none')
              ) : (
                <span lang={languageTag(document.description.language)} dir="auto">
                  {document.description.text}
                </span>
              )}
            </dd>
          </div>
          <div className="details__row">
            <dt>{t('documents.fields.documentType')}</dt>
            <dd>{lookups.label(document.documentTypeItemId)}</dd>
          </div>
          <div className="details__row">
            <dt>{t('documents.fields.classification')}</dt>
            <dd>{lookups.label(document.dataClassificationItemId)}</dd>
          </div>
          <div className="details__row">
            <dt>{t('documents.fields.project')}</dt>
            <dd>
              {document.projectId === null ? (
                t('documents.values.noProject')
              ) : (
                <Link to={`/documents/projects/${document.projectId}`}>
                  {t('documents.values.project', { id: shortId(document.projectId) })}
                </Link>
              )}
            </dd>
          </div>
          <div className="details__row">
            <dt>{t('documents.fields.owner')}</dt>
            <dd>{person(document.ownerUserId)}</dd>
          </div>
          <div className="details__row">
            <dt>{t('documents.fields.createdAt')}</dt>
            <dd>{formatDateTime(document.createdAt)}</dd>
          </div>
          <div className="details__row">
            <dt>{t('documents.fields.updatedAt')}</dt>
            <dd>{formatDateTime(document.updatedAt)}</dd>
          </div>
        </dl>
      </section>

      <section className="section" aria-labelledby="document-latest-version">
        <h2 id="document-latest-version">{t('documents.detail.latestVersion')}</h2>
        {version === null ? (
          <p>{t('documents.detail.noVersion')}</p>
        ) : (
          <VersionDetails version={version} uploadedBy={person(version.uploadedByUserId)} />
        )}
      </section>

      <DocumentLinks documentId={document.id} lookups={lookups} />
    </>
  );
}
