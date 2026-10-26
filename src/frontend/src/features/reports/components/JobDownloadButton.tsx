import { type ReactElement, useState } from 'react';

import { saveFile } from '@/shared/api/saveFile.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { reportsApi } from '../api/reportsApi.ts';
import { type ReportExportFormat } from '../api/types.ts';
import { downloadProblemMessage } from '../problems.ts';

/**
 * A completed job's file. The API authorises the download again now (reports.md D-7): a requester who may no longer
 * see what the file holds is refused, and is told so beside the button.
 */
export function JobDownloadButton({
  jobId,
  format,
  name,
}: {
  jobId: string;
  format: ReportExportFormat;
  /** What is downloaded, for a screen reader when several rows each have a button. */
  name: string;
}): ReactElement {
  const { t } = useI18n();
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  return (
    <span className="download">
      <button
        type="button"
        className="button"
        disabled={busy}
        onClick={() => {
          setBusy(true);
          setProblem(null);
          reportsApi
            .download(jobId)
            .then(
              (file) => {
                saveFile(file, `report.${format.toLowerCase()}`);
              },
              (error: unknown) => {
                setProblem(downloadProblemMessage(error, t));
              },
            )
            .finally(() => {
              setBusy(false);
            });
        }}
      >
        {busy ? t('reports.download.busy') : t('reports.download.label')}{' '}
        <span className="visually-hidden">{name}</span>
      </button>
      {problem !== null && (
        <span className="field__error" role="alert">
          {problem}
        </span>
      )}
    </span>
  );
}
