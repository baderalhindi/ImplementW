import { type ReactElement, type SyntheticEvent, useCallback, useState } from 'react';
import { Link } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { saveFile } from '@/shared/api/saveFile.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { FileField, SelectField, TextAreaField } from '@/shared/ui/FormFields.tsx';
import { PageHeader, TableContainer } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { projectImportApi } from '../api/followUpApi.ts';
import { type ImportBatch, type ImportMapping, type ImportPreview } from '../api/importTypes.ts';
import { projectProblemMessage } from '../problems.ts';
import { TEXT_LENGTH } from '../registration.ts';

import { ImportPreviewView } from './ImportPreviewView.tsx';

/** Projects → Import (the 2 Nov 2026 review, R3-4): the batches, and a new import for a holder of PROJECT_IMPORT. */
export function ProjectImportPage(): ReactElement {
  const { t } = useI18n();
  const mayImport = useCan('PROJECT_IMPORT');
  const [notice, setNotice] = useState<Notice | null>(null);
  const [loads, setLoads] = useState(0);
  return (
    <>
      <PageHeader
        title={t('projects.import.title')}
        description={t('projects.import.description')}
      />
      <PageNotice notice={notice} />
      {mayImport && (
        <NewImport
          onSubmitted={(batch) => {
            setNotice({
              tone: 'success',
              message: t('projects.import.submitted', { batchNo: batch.batchNo }),
            });
            setLoads((n) => n + 1);
          }}
        />
      )}
      <BatchList key={loads} />
    </>
  );
}

/** Download the template, upload, validate, map the names, check each row, then submit the batch with a note. */
function NewImport({ onSubmitted }: { onSubmitted: (batch: ImportBatch) => void }): ReactElement {
  const { t, language } = useI18n();
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [mappings, setMappings] = useState<ImportMapping[]>([]);
  const [skipped, setSkipped] = useState<string[]>([]);
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const plan = { mappings, skippedRows: skipped };
  const attempt = async <T,>(action: () => Promise<T>): Promise<T | null> => {
    setBusy(true);
    setError(null);
    try {
      return await action();
    } catch (caught) {
      setError(projectProblemMessage(caught, t));
      return null;
    } finally {
      setBusy(false);
    }
  };
  const validate = async (event?: SyntheticEvent) => {
    event?.preventDefault();
    if (file === null) {
      return;
    }
    const result = await attempt(() => projectImportApi.validate(file, plan));
    if (result !== null) {
      setPreview(result);
    }
  };
  const submit = async () => {
    if (file === null) {
      return;
    }
    const batch = await attempt(() => projectImportApi.submit(file, plan, note, language));
    if (batch !== null) {
      setFile(null);
      setPreview(null);
      setMappings([]);
      setSkipped([]);
      setNote('');
      onSubmitted(batch);
    }
  };
  const mapped = (dimension: string, sourceText: string) =>
    mappings.find((m) => m.dimension === dimension && m.sourceText === sourceText)?.targetValue ??
    '';

  return (
    <section className="section" aria-labelledby="new-import-heading">
      <h2 id="new-import-heading">{t('projects.import.steps.template')}</h2>
      <p>{t('projects.import.templateHint')}</p>
      <button
        type="button"
        className="button"
        disabled={busy}
        onClick={() => {
          void attempt(async () => {
            saveFile(await projectImportApi.template(), 'project-import-template.xlsx');
          });
        }}
      >
        {t('projects.import.download')}
      </button>

      <h2>{t('projects.import.steps.upload')}</h2>
      <form className="form" noValidate onSubmit={(event) => void validate(event)}>
        <FormAlert message={error} />
        <FileField
          label={t('projects.import.file')}
          name="file"
          required
          accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
          onChange={(chosen) => {
            setFile(chosen);
            setPreview(null);
          }}
        />
        <div className="form__actions">
          <button type="submit" className="button button--primary" disabled={file === null || busy}>
            {busy
              ? t('projects.import.validating')
              : preview === null
                ? t('projects.import.validate')
                : t('projects.import.revalidate')}
          </button>
        </div>
      </form>

      {preview !== null && preview.unmatched.length > 0 && (
        <>
          <h2>{t('projects.import.steps.map')}</h2>
          <p>{t('projects.import.unmatchedHint')}</p>
          <div className="filters">
            {preview.unmatched.map((name) => (
              <SelectField
                key={`${name.dimension}-${name.sourceText}`}
                label={`${t(`projects.import.dimensionName.${name.dimension}`)} · ${t('projects.import.mapTo', { name: name.sourceText === '' ? '—' : name.sourceText, rows: name.rowCount })}`}
                name={`map-${name.dimension}-${name.sourceText}`}
                value={mapped(name.dimension, name.sourceText)}
                placeholder={t('common.form.choose')}
                options={name.options.map((option) => ({
                  value: option.value,
                  label: option.label[language],
                }))}
                onChange={(value) => {
                  setMappings((current) => [
                    ...current.filter(
                      (m) => !(m.dimension === name.dimension && m.sourceText === name.sourceText),
                    ),
                    ...(value === ''
                      ? []
                      : [
                          {
                            dimension: name.dimension,
                            sourceText: name.sourceText,
                            targetValue: value,
                          },
                        ]),
                  ]);
                }}
              />
            ))}
          </div>
          <button type="button" className="button" disabled={busy} onClick={() => void validate()}>
            {t('projects.import.revalidate')}
          </button>
        </>
      )}

      {preview !== null && (
        <>
          <h2>{t('projects.import.steps.rows')}</h2>
          <ImportPreviewView
            preview={preview}
            skipped={skipped}
            onToggleSkip={(reference) => {
              setSkipped((current) =>
                current.includes(reference)
                  ? current.filter((r) => r !== reference)
                  : [...current, reference],
              );
            }}
          />
          <h2>{t('projects.import.steps.submit')}</h2>
          <TextAreaField
            label={t('projects.import.note')}
            name="note"
            maxLength={TEXT_LENGTH}
            value={note}
            onChange={setNote}
          />
          {!preview.submittable && (
            <p className="form__note">{t('projects.import.notSubmittable')}</p>
          )}
          <div className="form__actions">
            <button
              type="button"
              className="button button--primary"
              disabled={!preview.submittable || busy}
              onClick={() => void submit()}
            >
              {busy ? t('projects.import.submitting') : t('projects.import.submit')}
            </button>
          </div>
        </>
      )}
    </section>
  );
}

/** The batches, newest first, each opening its own page where it is approved, returned or rolled back. */
function BatchList(): ReactElement {
  const { t, formatDateTime } = useI18n();
  const load = useCallback(
    (signal: AbortSignal) => projectImportApi.list({ pageSize: 50 }, signal),
    [],
  );
  const batches = useApiResource(load);
  const submitter = usePersonNames((batches.data?.items ?? []).map((batch) => batch.submittedBy));
  return (
    <section className="section" aria-labelledby="batches-heading">
      <h2 id="batches-heading">{t('projects.import.batches')}</h2>
      {batches.loading && <LoadingState />}
      {batches.error !== null && (
        <ErrorState
          error={batches.error}
          message={projectProblemMessage(batches.error, t)}
          onRetry={batches.reload}
        />
      )}
      {batches.data !== undefined &&
        (batches.data.items.length === 0 ? (
          <EmptyState title={t('projects.import.noBatches')} />
        ) : (
          <TableContainer caption={t('projects.import.batchesCaption')}>
            <thead>
              <tr>
                <th scope="col">{t('projects.import.batch')}</th>
                <th scope="col">{t('projects.import.status')}</th>
                <th scope="col">{t('projects.import.fileName')}</th>
                <th scope="col">{t('projects.import.counts')}</th>
                <th scope="col">{t('projects.import.submittedBy')}</th>
                <th scope="col">{t('projects.import.submittedAt')}</th>
              </tr>
            </thead>
            <tbody>
              {batches.data.items.map((batch) => (
                <tr key={batch.id}>
                  <td>
                    <Link to={`/projects/import/${batch.id}`}>
                      {t('projects.import.batchNo', { batchNo: batch.batchNo })}
                    </Link>
                  </td>
                  <td>
                    <BatchStatus batch={batch} />
                  </td>
                  <td dir="auto">{batch.fileName}</td>
                  <td>
                    {t('projects.import.countsValue', {
                      create: batch.createCount,
                      update: batch.updateCount,
                      skip: batch.skipCount,
                    })}
                  </td>
                  <td>{submitter(batch.submittedBy)}</td>
                  <td>{formatDateTime(batch.submittedAt)}</td>
                </tr>
              ))}
            </tbody>
          </TableContainer>
        ))}
    </section>
  );
}

export function BatchStatus({ batch }: { batch: ImportBatch }): ReactElement {
  const { t } = useI18n();
  const tone =
    batch.status === 'APPROVED'
      ? 'positive'
      : batch.status === 'SUBMITTED'
        ? 'info'
        : batch.status === 'RETURNED'
          ? 'warning'
          : 'neutral';
  return <StatusBadge label={t(`projects.import.batchStatus.${batch.status}`)} tone={tone} />;
}
