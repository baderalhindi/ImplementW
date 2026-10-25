import { type ReactElement, useId, useState } from 'react';

import { problemMessage, type Translate } from '@/features/identity-access/problems.ts';
import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { saveFile } from '@/shared/api/saveFile.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { FileField } from '@/shared/ui/FormFields.tsx';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { importApi, type ImportIssue, type ImportReport } from './api.ts';

const XLSX_TYPES = '.xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

/** The most problems listed at once; the summary still counts them all. */
const SHOWN_ISSUES = 200;

const ISSUE_MESSAGES: Record<string, TranslationKey> = {
  REQUIRED: 'dataImport.issues.REQUIRED',
  MAX_LENGTH: 'dataImport.issues.MAX_LENGTH',
  MALFORMED: 'dataImport.issues.MALFORMED',
  ENUM_VALUE: 'dataImport.issues.ENUM_VALUE',
  OUT_OF_RANGE: 'dataImport.issues.OUT_OF_RANGE',
  INVALID_CHARACTER: 'dataImport.issues.INVALID_CHARACTER',
  COLUMN_MISSING: 'dataImport.issues.COLUMN_MISSING',
  COLUMN_UNKNOWN: 'dataImport.issues.COLUMN_UNKNOWN',
  COLUMN_DUPLICATE: 'dataImport.issues.COLUMN_DUPLICATE',
  NO_ROWS: 'dataImport.issues.NO_ROWS',
  DUPLICATE_IN_FILE: 'dataImport.issues.DUPLICATE_IN_FILE',
  REFERENCE_NOT_FOUND: 'dataImport.issues.REFERENCE_NOT_FOUND',
  ALREADY_EXISTS: 'dataImport.issues.ALREADY_EXISTS',
  NOT_FOUND: 'dataImport.issues.NOT_FOUND',
  INACTIVE: 'dataImport.issues.INACTIVE',
  DUPLICATE: 'dataImport.issues.DUPLICATE',
  NOT_ALLOWED: 'dataImport.issues.NOT_ALLOWED',
  DATE_BEFORE_START: 'dataImport.issues.DATE_BEFORE_START',
};

interface ImportActionProps {
  /** The register's collection, e.g. `/departments`: its template and imports live under it. */
  collectionPath: string;
  /** The register as the dialog's title names it, e.g. "Departments". */
  registerName: string;
  /** Called once an import has committed, so the page reads its list again. */
  onImported: () => void;
}

/**
 * TASK-102: a register's migration import — the template, a check of the filled workbook that writes nothing, and the import
 * itself, all rows or none. Offered to the holders of DATA_IMPORT, which is R01 alone (data-import.md D-2); the API decides.
 */
export function ImportAction({
  collectionPath,
  registerName,
  onImported,
}: ImportActionProps): ReactElement | null {
  const { t } = useI18n();
  const allowed = useCan('DATA_IMPORT');
  const [open, setOpen] = useState(false);
  if (!allowed) {
    return null;
  }
  return (
    <>
      <button
        type="button"
        className="button"
        onClick={() => {
          setOpen(true);
        }}
      >
        {t('dataImport.action')} <span className="visually-hidden">{registerName}</span>
      </button>
      <Dialog
        open={open}
        title={t('dataImport.title', { register: registerName })}
        onClose={() => {
          setOpen(false);
        }}
      >
        <ImportSteps
          collectionPath={collectionPath}
          onImported={onImported}
          onClose={() => {
            setOpen(false);
          }}
        />
      </Dialog>
    </>
  );
}

type Busy = 'template' | 'check' | 'commit' | null;

function ImportSteps({
  collectionPath,
  onImported,
  onClose,
}: {
  collectionPath: string;
  onImported: () => void;
  onClose: () => void;
}): ReactElement {
  const { t } = useI18n();
  const [file, setFile] = useState<File | null>(null);
  const [report, setReport] = useState<ImportReport | null>(null);
  const [busy, setBusy] = useState<Busy>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const id = useId();

  const run = async (step: Exclude<Busy, null>, action: () => Promise<void>) => {
    setBusy(step);
    setProblem(null);
    try {
      await action();
    } catch (error) {
      setProblem(importProblemMessage(error, t));
    } finally {
      setBusy(null);
    }
  };

  const downloadTemplate = () =>
    run('template', async () => {
      saveFile(await importApi.template(collectionPath), 'import-template.xlsx');
    });

  const check = () => {
    if (file === null) {
      setProblem(t('dataImport.check.noFile'));
      return;
    }
    void run('check', async () => {
      setReport(await importApi.validate(collectionPath, file));
    });
  };

  const commit = (chosen: File) =>
    run('commit', async () => {
      const imported = await importApi.commit(collectionPath, chosen);
      setReport(imported);
      if (imported.committed) {
        onImported();
      }
    });

  const committed = report?.committed === true;
  return (
    <div className="form import">
      <p>{t('dataImport.intro')}</p>
      <FormAlert message={problem} />

      <section className="import__step" aria-labelledby={`${id}-template`}>
        <h3 id={`${id}-template`}>{t('dataImport.template.heading')}</h3>
        <p className="field__hint">{t('dataImport.template.hint')}</p>
        <button
          type="button"
          className="button"
          disabled={busy !== null}
          onClick={() => void downloadTemplate()}
        >
          {busy === 'template'
            ? t('dataImport.template.downloading')
            : t('dataImport.template.download')}
        </button>
      </section>

      <section className="import__step" aria-labelledby={`${id}-file`}>
        <h3 id={`${id}-file`}>{t('dataImport.file.heading')}</h3>
        <FileField
          label={t('dataImport.file.label')}
          name="file"
          accept={XLSX_TYPES}
          hint={t('dataImport.file.hint')}
          disabled={busy !== null || committed}
          onChange={(chosen) => {
            setFile(chosen);
            setReport(null);
            setProblem(null);
          }}
        />
      </section>

      <section className="import__step" aria-labelledby={`${id}-check`}>
        <h3 id={`${id}-check`}>{t('dataImport.check.heading')}</h3>
        {report !== null && <ReportSummary report={report} />}
        <div className="form__actions">
          {!committed && (
            <button
              type="button"
              className="button button--primary"
              disabled={busy !== null}
              onClick={check}
            >
              {busy === 'check' ? t('dataImport.check.running') : t('dataImport.check.run')}
            </button>
          )}
          {file !== null &&
            report !== null &&
            !committed &&
            report.issues.length === 0 &&
            report.createdCount > 0 && (
              <button
                type="button"
                className="button button--primary"
                disabled={busy !== null}
                onClick={() => void commit(file)}
              >
                {busy === 'commit'
                  ? t('dataImport.commit.running')
                  : t('dataImport.commit.run', { count: report.createdCount })}
              </button>
            )}
          <button type="button" className="button" disabled={busy !== null} onClick={onClose}>
            {t('dataImport.close')}
          </button>
        </div>
      </section>
    </div>
  );
}

function ReportSummary({ report }: { report: ImportReport }): ReactElement {
  const { t } = useI18n();
  if (report.issues.length > 0) {
    return (
      <>
        <p className="notice notice--warning" role="status">
          {t('dataImport.report.issues', { issues: report.issues.length })}
        </p>
        <IssueTable issues={report.issues} />
      </>
    );
  }
  if (report.committed) {
    return (
      <p className="notice" role="status">
        {t('dataImport.report.committed', {
          created: report.createdCount,
          skipped: report.skippedCount,
        })}{' '}
        {report.batchId !== null && t('dataImport.report.batch', { batchId: report.batchId })}
      </p>
    );
  }
  return (
    <p className="notice" role="status">
      {report.createdCount === 0
        ? t('dataImport.report.nothingNew')
        : t('dataImport.report.ready', {
            rows: report.rowCount,
            created: report.createdCount,
            skipped: report.skippedCount,
          })}
    </p>
  );
}

function IssueTable({ issues }: { issues: ImportIssue[] }): ReactElement {
  const { t } = useI18n();
  const shown = issues.slice(0, SHOWN_ISSUES);
  return (
    <>
      <TableContainer caption={t('dataImport.report.caption')}>
        <thead>
          <tr>
            <th scope="col">{t('dataImport.report.row')}</th>
            <th scope="col">{t('dataImport.report.column')}</th>
            <th scope="col">{t('dataImport.report.problem')}</th>
          </tr>
        </thead>
        <tbody>
          {shown.map((issue, index) => (
            <tr key={`${String(issue.row)}-${issue.column}-${String(index)}`}>
              <td>{issue.row}</td>
              <td dir="ltr" className="cell--ltr">
                {issue.column === '' ? t('dataImport.report.wholeRow') : issue.column}
              </td>
              <td>{issueMessage(issue.code, t)}</td>
            </tr>
          ))}
        </tbody>
      </TableContainer>
      {shown.length < issues.length && (
        <p className="field__hint">
          {t('dataImport.report.truncated', { shown: shown.length, total: issues.length })}
        </p>
      )}
    </>
  );
}

function issueMessage(code: string, t: Translate): string {
  const key = ISSUE_MESSAGES[code];
  return key === undefined ? t('dataImport.issues.other', { code }) : t(key);
}

function importProblemMessage(error: unknown, t: Translate): string {
  if (error instanceof ApiError && error.code === 'UNSUPPORTED_MEDIA_TYPE') {
    return t('dataImport.problems.notWorkbook');
  }
  if (error instanceof ApiError && error.code === 'PAYLOAD_TOO_LARGE') {
    return t('dataImport.problems.tooLarge');
  }
  return problemMessage(error, t);
}
