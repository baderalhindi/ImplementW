import { useEffect, useState } from 'react';

import { documentsApi } from '../api/documentsApi.ts';
import { type DocumentVersionDetail } from '../api/types.ts';

/**
 * How often a version waiting for its scan is read again. The scan worker takes up to 20 versions every 30 s
 * (TASK-037 `DocumentManagement:Scan`), so a verdict appears within a pass or two of the upload.
 */
export const SCAN_POLL_INTERVAL_MS = 5000;

/**
 * The version as it is now: read again every SCAN_POLL_INTERVAL_MS while its scan is pending, and left alone once
 * the scan has decided (CLEAN, QUARANTINED, SCAN_FAILED). A failed read stops the polling; the version shown stays
 * the last one read, so the screen never claims a verdict it did not receive.
 */
export function useScanWatch(version: DocumentVersionDetail | null): {
  version: DocumentVersionDetail | null;
  error: unknown;
} {
  const [current, setCurrent] = useState<{
    watched: DocumentVersionDetail | null;
    latest: DocumentVersionDetail | null;
    error: unknown;
  }>({ watched: version, latest: version, error: null });

  // A new version to watch replaces whatever was read for the previous one.
  const state =
    current.watched === version ? current : { watched: version, latest: version, error: null };
  if (state !== current) {
    setCurrent(state);
  }

  const latest = state.latest;
  const pending = latest?.scanState === 'SCAN_PENDING' && state.error === null;

  useEffect(() => {
    if (!pending) {
      return;
    }
    const controller = new AbortController();
    const timer = setTimeout(() => {
      documentsApi.getVersion(latest.documentId, latest.id, controller.signal).then(
        (read) => {
          setCurrent((previous) =>
            previous.watched === version ? { ...previous, latest: read } : previous,
          );
        },
        (error: unknown) => {
          if (!controller.signal.aborted) {
            setCurrent((previous) =>
              previous.watched === version ? { ...previous, error } : previous,
            );
          }
        },
      );
    }, SCAN_POLL_INTERVAL_MS);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [pending, latest, version]);

  return { version: state.latest, error: state.error };
}
