import { type ReactElement, useId, useState } from 'react';

import { type DownloadedFile } from '@/shared/api/httpClient.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { type ScanState } from '../api/types.ts';
import { isUsable } from '../presentation.ts';
import { documentProblemMessage } from '../problems.ts';

/** Hands the bytes to the browser as a file to save; the object URL is released once the click is dispatched. */
function saveFile({ content, fileName }: DownloadedFile, fallbackName: string): void {
  const url = URL.createObjectURL(content);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName ?? fallbackName;
  anchor.rel = 'noopener';
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

interface DownloadButtonProps {
  label: string;
  /** The version's scan state: only CLEAN content is served (TASK-037 D-4). */
  scanState: ScanState;
  fileName: string;
  download: () => Promise<DownloadedFile>;
  /** Names what is downloaded when the label alone is ambiguous, e.g. a row's version. */
  describedBy?: string | undefined;
}

/**
 * A download of a version's content, disabled unless the version is CLEAN, with the reason beside it. The API refuses
 * the rest with 409 DOCUMENT_NOT_AVAILABLE; that refusal, or any other, is shown beside the button.
 */
export function DownloadButton({
  label,
  scanState,
  fileName,
  download,
  describedBy,
}: DownloadButtonProps): ReactElement {
  const { t } = useI18n();
  const id = useId();
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);

  const run = async () => {
    setBusy(true);
    setProblem(null);
    try {
      saveFile(await download(), fileName);
    } catch (error) {
      setProblem(documentProblemMessage(error, t));
    } finally {
      setBusy(false);
    }
  };

  const usable = isUsable(scanState);
  const reason = scanState === 'CLEAN' ? null : t(`documents.unavailable.${scanState}`);
  const describedByIds = [describedBy, reason === null ? null : `${id}-reason`]
    .filter((part) => part !== null && part !== undefined)
    .join(' ');
  return (
    <span className="download">
      <button
        type="button"
        className="button"
        disabled={!usable || busy}
        aria-describedby={describedByIds === '' ? undefined : describedByIds}
        onClick={() => void run()}
      >
        {busy ? t('documents.download.busy') : label}
      </button>
      {reason !== null && (
        <span id={`${id}-reason`} className="download__reason">
          {reason}
        </span>
      )}
      {problem !== null && (
        <span className="field__error" role="alert">
          {problem}
        </span>
      )}
    </span>
  );
}
