import { type ReactElement } from 'react';

import { UNKNOWN_REASONS } from '@/features/dashboards/presentation.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';
import { ValueStateFlag } from '@/shared/ui/ValueState.tsx';

import { type ReportCell, type ReportColumnView } from '../api/types.ts';
import { cellContent, REASON_ICONS } from '../presentation.ts';

/**
 * One cell (FG-02 §7.1): the value in its unit, a code in its source's words and tone, free text in the direction it was
 * entered in (ADR-012) — or, with no value, the reason as a flag, never blank or 0. A STALE value keeps its value and
 * says so, with its own as-of (BR-RPT-020). `data-freshness` and `data-value-state` name the state in the DOM.
 */
export function CellValue({
  cell,
  column,
}: {
  cell: ReportCell;
  column: ReportColumnView;
}): ReactElement {
  const { t, language, formatDateTime } = useI18n();
  const content = cellContent(cell, column, language, t, formatDateTime);
  if (content.kind === 'unknown') {
    return (
      <ValueStateFlag
        icon={REASON_ICONS[content.reason]}
        label={t(UNKNOWN_REASONS[content.reason].label)}
        state={content.reason}
        tone={content.reason === 'SOURCE_UNAVAILABLE' ? 'warning' : 'neutral'}
      />
    );
  }
  return (
    <span className="report-cell" data-freshness={cell.freshness}>
      {content.tone === null ? (
        <span dir={content.isText ? 'auto' : undefined}>{content.text}</span>
      ) : (
        <StatusBadge label={content.text} tone={content.tone} />
      )}
      {cell.freshness === 'STALE' && (
        <ValueStateFlag
          icon="stale"
          label={
            cell.asOf === null
              ? t('dashboards.freshness.STALE')
              : t('reports.cell.staleAsOf', { date: formatDateTime(cell.asOf) })
          }
          state="STALE"
          tone="warning"
        />
      )}
    </span>
  );
}
