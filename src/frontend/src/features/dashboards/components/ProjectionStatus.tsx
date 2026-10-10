import { type ReactElement } from 'react';

import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { ValueStateFlag } from '@/shared/ui/ValueState.tsx';

import {
  type CoverageExclusionReason,
  type DashboardWidgetResult,
  type WidgetCoverage,
  type WidgetUnknownReason,
} from '../api/types.ts';
import { SEMANTIC_STATES, UNKNOWN_REASONS } from '../presentation.ts';

// Step 16's freshness and version banner (FE-CMP-008) on every widget: which semantic state the value is, how fresh
// the source says it is, when it was current, which source and projection version it comes from, and how much of the
// population it covers. "Never replace missing with zero or Unknown with Green" (acceptance criterion 2).

const EXCLUSIONS: Record<CoverageExclusionReason, TranslationKey> = {
  MISSING: 'dashboards.coverage.excluded.MISSING',
  MASKED: 'dashboards.coverage.excluded.MASKED',
  INCOMPATIBLE: 'dashboards.coverage.excluded.INCOMPATIBLE',
};

/** "2 of 3 projects counted · left out: 1 with no data", and how many counted values are out of date. */
export function CoverageLine({ coverage }: { coverage: WidgetCoverage }): ReactElement {
  const { t } = useI18n();
  const parts = [
    t('dashboards.coverage.counted', {
      included: coverage.includedCount,
      eligible: coverage.eligibleCount,
    }),
  ];
  if (coverage.exclusions.length > 0) {
    parts.push(
      t('dashboards.coverage.leftOut', {
        reasons: coverage.exclusions
          .map((exclusion) => t(EXCLUSIONS[exclusion.reason], { count: exclusion.count }))
          .join(', '),
      }),
    );
  }
  if (coverage.staleCount > 0) {
    parts.push(t('dashboards.coverage.stale', { count: coverage.staleCount }));
  }
  return (
    <p className="widget__coverage" data-coverage>
      {parts.join(' · ')}
    </p>
  );
}

export function ProjectionStatus({ widget }: { widget: DashboardWidgetResult }): ReactElement {
  const { t, formatDateTime } = useI18n();
  const { semanticState, freshness, asOf } = widget.projection;
  const semantic = SEMANTIC_STATES[semanticState];
  return (
    <div className="widget__status">
      <span className={`badge ${semantic.badge}`} data-semantic-state={semanticState}>
        {t(semantic.label)}
      </span>
      {freshness === 'STALE' && (
        <ValueStateFlag
          icon="stale"
          label={t('dashboards.freshness.STALE')}
          state="STALE"
          tone="warning"
        />
      )}
      <span className="widget__as-of">
        {asOf === null
          ? t('dashboards.asOf.none')
          : t('dashboards.asOf.at', { date: formatDateTime(asOf) })}
      </span>
      <span className="widget__source">
        {t('dashboards.source', {
          source: widget.sourceDomain === '' ? '—' : widget.sourceDomain,
          version: widget.projectionVersion === '' ? '—' : widget.projectionVersion,
        })}
      </span>
    </div>
  );
}

const ICONS = {
  MISSING: 'missing',
  NOT_APPLICABLE: 'notApplicable',
  RESTRICTED: 'restricted',
  SOURCE_UNAVAILABLE: 'unavailable',
} as const satisfies Record<WidgetUnknownReason, string>;

/** A widget with no data: its reason as a flag and a sentence, never a 0 or an empty chart (BR-DSH-013 to -015). */
export function UnknownValue({ reason }: { reason: WidgetUnknownReason }): ReactElement {
  const { t } = useI18n();
  const words = UNKNOWN_REASONS[reason];
  return (
    <div className="widget__unknown">
      <ValueStateFlag
        icon={ICONS[reason]}
        label={t(words.label)}
        state={reason}
        tone={reason === 'SOURCE_UNAVAILABLE' ? 'warning' : 'neutral'}
      />
      <p className="widget__explanation">{t(words.explanation)}</p>
    </div>
  );
}
