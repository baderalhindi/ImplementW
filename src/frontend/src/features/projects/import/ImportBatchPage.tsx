import { type ReactElement, useCallback, useState } from 'react';
import { Link, useParams } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { saveFile } from '@/shared/api/saveFile.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { TextAreaField } from '@/shared/ui/FormFields.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { projectImportApi } from '../api/followUpApi.ts';
import { type ImportBatch } from '../api/importTypes.ts';
import { Detail } from '../components/Detail.tsx';
import { projectProblemMessage } from '../problems.ts';
import { TEXT_LENGTH } from '../registration.ts';

import { ImportPreviewView } from './ImportPreviewView.tsx';
import { BatchStatus } from './ProjectImportPage.tsx';

type Decision = 'approve' | 'return' | 'rollback';

/**
 * One import batch (R3-4): who submitted it and why, what it will do — its plan validated again now — and, for a holder of
 * PROJECT_IMPORT_APPROVE who did not submit it, the decision: approve and apply, or return with a reason. An applied batch whose projects are
 * untouched can be rolled back.
 */
export function ImportBatchPage(): ReactElement {
  const { t, formatDateTime } = useI18n();
  const { batchId = '' } = useParams();
  const loadBatch = useCallback(
    (signal: AbortSignal) => projectImportApi.get(batchId, signal),
    [batchId],
  );
  const loadPreview = useCallback(
    (signal: AbortSignal) => projectImportApi.preview(batchId, signal),
    [batchId],
  );
  const batch = useApiResource(loadBatch, { keepWhileReloading: true });
  const preview = useApiResource(loadPreview);
  const [deciding, setDeciding] = useState<Decision | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const people = usePersonNames([
    batch.data?.data.submittedBy ?? null,
    batch.data?.data.decidedByUserId ?? null,
  ]);

  if (batch.data === undefined) {
    return (
      <>
        <PageHeader title={t('projects.import.batch')} />
        {batch.loading ? (
          <LoadingState />
        ) : (
          <ErrorState
            error={batch.error}
            message={projectProblemMessage(batch.error, t)}
            onRetry={batch.reload}
          />
        )}
      </>
    );
  }
  const current = batch.data.data;
  const etag = batch.data.etag;

  return (
    <>
      <PageHeader
        title={t('projects.import.batchNo', { batchNo: current.batchNo })}
        actions={
          <>
            <Link className="button" to="/projects/import">
              {t('projects.import.batches')}
            </Link>
            <button
              type="button"
              className="button"
              onClick={() => {
                void projectImportApi.file(current.id).then((file) => {
                  saveFile(file, current.fileName);
                });
              }}
            >
              {t('projects.import.downloadFile')}
            </button>
            {current.mayDecide && (
              <>
                <button
                  type="button"
                  className="button button--primary"
                  onClick={() => {
                    setDeciding('approve');
                  }}
                >
                  {t('projects.import.approve')}
                </button>
                <button
                  type="button"
                  className="button"
                  onClick={() => {
                    setDeciding('return');
                  }}
                >
                  {t('projects.import.return')}
                </button>
              </>
            )}
            {current.mayRollBack && (
              <button
                type="button"
                className="button button--danger"
                onClick={() => {
                  setDeciding('rollback');
                }}
              >
                {t('projects.import.rollback')}
              </button>
            )}
          </>
        }
      />
      <PageNotice notice={notice} />
      <dl className="details">
        <Detail term={t('projects.import.status')}>
          <BatchStatus batch={current} />
        </Detail>
        <Detail term={t('projects.import.fileName')}>
          <span dir="auto">{current.fileName}</span>
        </Detail>
        <Detail term={t('projects.import.submittedBy')}>
          {people(current.submittedBy)}
          <span className="details__aside">{formatDateTime(current.submittedAt)}</span>
        </Detail>
        {current.submissionNote !== null && (
          <Detail term={t('projects.import.note')}>
            <span dir="auto">{current.submissionNote.text}</span>
          </Detail>
        )}
        <Detail term={t('projects.import.counts')}>
          {t('projects.import.countsValue', {
            create: current.createCount,
            update: current.updateCount,
            skip: current.skipCount,
          })}
        </Detail>
        {current.decidedByUserId !== null && current.decidedAt !== null && (
          <Detail term={t('projects.import.decidedBy')}>
            {people(current.decidedByUserId)}
            <span className="details__aside">{formatDateTime(current.decidedAt)}</span>
          </Detail>
        )}
        {current.returnReason !== null && (
          <Detail term={t('projects.import.returnReason')}>
            <span dir="auto">{current.returnReason.text}</span>
          </Detail>
        )}
      </dl>
      {current.status === 'SUBMITTED' && !current.mayDecide && (
        <p>{t('projects.import.ownBatch')}</p>
      )}
      {current.status === 'SUBMITTED' && (
        <section className="section" aria-labelledby="preview-heading">
          <h2 id="preview-heading">{t('projects.import.preview')}</h2>
          {preview.loading && <LoadingState />}
          {preview.error !== null && (
            <ErrorState
              error={preview.error}
              message={projectProblemMessage(preview.error, t)}
              onRetry={preview.reload}
            />
          )}
          {preview.data !== undefined && <ImportPreviewView preview={preview.data} />}
        </section>
      )}
      <DecisionDialog
        decision={deciding}
        batch={current}
        etag={etag}
        onClose={() => {
          setDeciding(null);
        }}
        onDone={(message) => {
          setDeciding(null);
          setNotice({ tone: 'success', message });
          batch.reload();
        }}
      />
    </>
  );
}

function DecisionDialog({
  decision,
  batch,
  etag,
  onClose,
  onDone,
}: {
  decision: Decision | null;
  batch: ImportBatch;
  etag: string | null;
  onClose: () => void;
  onDone: (message: string) => void;
}): ReactElement {
  const { t, language } = useI18n();
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const title =
    decision === 'approve'
      ? t('projects.import.approve')
      : decision === 'return'
        ? t('projects.import.return')
        : t('projects.import.rollback');
  const run = async () => {
    setBusy(true);
    setError(null);
    try {
      if (decision === 'approve') {
        await projectImportApi.approve(batch.id, etag);
        onDone(t('projects.import.approved'));
      } else if (decision === 'return') {
        await projectImportApi.returnBatch(batch.id, { text: reason.trim(), language }, etag);
        onDone(t('projects.import.returned'));
      } else if (decision === 'rollback') {
        await projectImportApi.rollBack(batch.id, etag);
        onDone(t('projects.import.rolledBack'));
      }
    } catch (caught) {
      setError(projectProblemMessage(caught, t));
    } finally {
      setBusy(false);
    }
  };
  return (
    <Dialog open={decision !== null} title={title} onClose={onClose}>
      <FormAlert message={error} />
      {decision === 'approve' && (
        <p>
          {t('projects.import.approveConfirm', {
            batchNo: batch.batchNo,
            create: batch.createCount,
            update: batch.updateCount,
          })}
        </p>
      )}
      {decision === 'rollback' && (
        <p>{t('projects.import.rollbackConfirm', { count: batch.createCount })}</p>
      )}
      {decision === 'return' && (
        <TextAreaField
          label={t('projects.import.reason')}
          name="reason"
          required
          maxLength={TEXT_LENGTH}
          value={reason}
          onChange={setReason}
        />
      )}
      <div className="form__actions">
        <button
          type="button"
          className="button button--primary"
          disabled={busy || (decision === 'return' && reason.trim() === '')}
          onClick={() => void run()}
        >
          {busy ? t('common.states.saving') : title}
        </button>
        <button type="button" className="button" disabled={busy} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </Dialog>
  );
}
