import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type ScanState } from '../api/types.ts';
import { scanTone } from '../presentation.ts';

/** A version's scan state as a word with its tone (TASK-038 D-3). */
export function ScanStateBadge({ state }: { state: ScanState }): ReactElement {
  const { t } = useI18n();
  return <StatusBadge label={t(`documents.scanState.${state}`)} tone={scanTone(state)} />;
}

/**
 * What a scan state means for the file, spelled out: whether it can be downloaded or used as evidence and what to do
 * next. A quarantined file is an alert; the other states are announced politely.
 */
export function ScanStateNotice({ state }: { state: ScanState }): ReactElement {
  const { t } = useI18n();
  return (
    <div
      className={`scan-notice scan-notice--${scanTone(state)}`}
      role={state === 'QUARANTINED' ? 'alert' : 'status'}
    >
      <p className="scan-notice__title">
        <ScanStateBadge state={state} /> {t(`documents.scanNotice.${state}.title`)}
      </p>
      <p className="scan-notice__body">{t(`documents.scanNotice.${state}.body`)}</p>
    </div>
  );
}
