import { useCallback, useEffect, useRef, useState } from 'react';

import {
  type SaveAction,
  type SaveResult,
  useSaveAction,
} from '@/features/identity-access/forms.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { type UploadProgress } from '../api/documentsApi.ts';
import { type DocumentVersionDetail } from '../api/types.ts';
import { documentProblemMessage, isFileProblem } from '../problems.ts';

import { useScanWatch } from './useScanWatch.ts';

export type UploadSender = (
  onProgress: UploadProgress,
  signal: AbortSignal,
) => Promise<DocumentVersionDetail>;

export interface FileUpload {
  save: SaveAction;
  /** The share sent while the request is in flight; null before and after. */
  progress: number | null;
  /** The version the API created, re-read while its scan is pending; null until the API answers. */
  version: DocumentVersionDetail | null;
  /** A failed re-read of the version: the scan result is then unknown, not assumed. */
  watchError: unknown;
  /** The refusal of the file itself (413, 415, or the API's `file` field error), for the file input. */
  fileError: string | undefined;
  run: (send: UploadSender) => Promise<SaveResult<DocumentVersionDetail>>;
}

/**
 * One upload of MOD-050 or MOD-052: the request with its progress, then the new version watched until its scan
 * decides. Leaving the dialog cancels a request still in flight; a file already received keeps its scan.
 */
export function useFileUpload(): FileUpload {
  const { t } = useI18n();
  const save = useSaveAction(documentProblemMessage);
  const [progress, setProgress] = useState<number | null>(null);
  const [created, setCreated] = useState<DocumentVersionDetail | null>(null);
  const [fileProblem, setFileProblem] = useState<string | undefined>(undefined);
  const watch = useScanWatch(created);
  const controller = useRef<AbortController | null>(null);

  useEffect(
    () => () => {
      controller.current?.abort();
    },
    [],
  );

  const { run: runSave } = save;
  const run = useCallback(
    async (send: UploadSender) => {
      controller.current = new AbortController();
      const { signal } = controller.current;
      setFileProblem(undefined);
      setProgress(0);
      const result = await runSave(() => send(setProgress, signal));
      setProgress(null);
      if (result.ok) {
        setCreated(result.value);
      } else if (isFileProblem(result.error)) {
        setFileProblem(documentProblemMessage(result.error, t));
      }
      return result;
    },
    [runSave, t],
  );

  return {
    save,
    progress,
    version: watch.version,
    watchError: watch.error,
    fileError: save.fieldErrors.file ?? fileProblem,
    run,
  };
}
