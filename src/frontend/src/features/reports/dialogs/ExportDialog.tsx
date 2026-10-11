import { type ReactElement, useEffect, useState } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { RadioGroupField } from '@/shared/ui/FormFields.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { reportsApi } from '../api/reportsApi.ts';
import { type ExportOptions, type ReportExportFormat, type ReportJobDetail } from '../api/types.ts';
import { JobDownloadButton } from '../components/JobDownloadButton.tsx';
import { failureMessage, isJobActive, jobTone } from '../presentation.ts';
import { reportProblemMessage } from '../problems.ts';

// MOD-060 Export Report (FG-02 §13.6, FE-CMP-010): PDF, XLSX or CSV only (ADR-005), started as a job (R-7) that runs in
// the background; the dialog follows it until it ends and offers its file, which is authorised again at download. View
// permission is not export permission: the screen offers this only when the API says the person may export.

/** How often a running job is read again. */
export const JOB_POLL_MS = 2000;

interface ExportFormProps {
  formats: ReportExportFormat[];
  /** A column the output would hold is sensitive (ADR-010): the file may be classified Sensitive. */
  sensitive: boolean;
  /** What is exported, for the download button's name. */
  name: string;
  start: (options: ExportOptions) => Promise<ReportJobDetail>;
  onClose: () => void;
}

function JobProgress({ job, name }: { job: ReportJobDetail; name: string }): ReactElement {
  const { t } = useI18n();
  return (
    <div className="report-job" role="status" data-job-status={job.status}>
      <p>
        {t('reports.export.jobStatus')}{' '}
        <StatusBadge label={t(`reports.jobStatus.${job.status}`)} tone={jobTone(job.status)} />
      </p>
      {isJobActive(job.status) && <p className="form__note">{t('reports.export.running')}</p>}
      {job.status === 'FAILED' && (
        <p className="form__note">{failureMessage(job.failureCode, t)}</p>
      )}
      {job.status === 'COMPLETED' && job.output !== null && (
        <>
          {job.output.sensitivity === 'SENSITIVE' && (
            <p className="sensitive-note">{t('reports.export.classifiedSensitive')}</p>
          )}
          <JobDownloadButton jobId={job.id} format={job.exportFormat} name={name} />
        </>
      )}
      <p>
        <Link to="/reports/exports">{t('reports.export.history')}</Link>
      </p>
    </div>
  );
}

function ExportForm({ formats, sensitive, name, start, onClose }: ExportFormProps): ReactElement {
  const { t, language } = useI18n();
  const [format, setFormat] = useState<ReportExportFormat>(formats[0] ?? 'PDF');
  const [outputLanguage, setOutputLanguage] = useState<'ar' | 'en'>(language);
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [job, setJob] = useState<ReportJobDetail | null>(null);

  // Follow the job until it ends; each read replaces the last.
  const jobId = job?.id ?? null;
  const active = job !== null && isJobActive(job.status);
  useEffect(() => {
    if (jobId === null || !active) {
      return;
    }
    const controller = new AbortController();
    const timer = setTimeout(() => {
      reportsApi.job(jobId, controller.signal).then(setJob, () => {
        // A failed read leaves the job as last read; a copy of it schedules the next read.
        if (!controller.signal.aborted) {
          setJob((current) => (current === null ? null : { ...current }));
        }
      });
    }, JOB_POLL_MS);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [jobId, active, job]);

  if (job !== null) {
    return (
      <div className="form">
        <JobProgress job={job} name={name} />
        <div className="form__actions">
          <button type="button" className="button" onClick={onClose}>
            {t('reports.export.close')}
          </button>
        </div>
      </div>
    );
  }

  return (
    <form
      className="form"
      onSubmit={(event) => {
        event.preventDefault();
        setBusy(true);
        setProblem(null);
        start({ format, language: outputLanguage }).then(setJob, (error: unknown) => {
          setProblem(reportProblemMessage(error, t));
          setBusy(false);
        });
      }}
    >
      <FormAlert message={problem} />
      <RadioGroupField
        label={t('reports.export.format')}
        name="format"
        value={format}
        onChange={(value) => {
          setFormat(formats.find((candidate) => candidate === value) ?? format);
        }}
        options={formats.map((candidate) => ({
          value: candidate,
          label: t(`reports.formats.${candidate}`),
        }))}
      />
      <RadioGroupField
        label={t('reports.export.language')}
        name="language"
        value={outputLanguage}
        onChange={(value) => {
          setOutputLanguage(value === 'ar' ? 'ar' : 'en');
        }}
        options={[
          { value: 'ar', label: t('common.languages.ar') },
          { value: 'en', label: t('common.languages.en') },
        ]}
      />
      {sensitive && <p className="sensitive-note">{t('reports.export.sensitiveWarning')}</p>}
      <p className="form__note">{t('reports.export.background')}</p>
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={busy}>
          {busy ? t('common.states.saving') : t('reports.export.start')}
        </button>
        <button type="button" className="button button--quiet" disabled={busy} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}

export function ExportDialog({
  open,
  onClose,
  ...form
}: Omit<ExportFormProps, 'onClose'> & { open: boolean; onClose: () => void }): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={open} title={t('reports.export.title')} onClose={onClose}>
      <ExportForm {...form} onClose={onClose} />
    </Dialog>
  );
}
