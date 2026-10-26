import { type ReactElement } from 'react';

import { SEMANTIC_STATES, UNKNOWN_REASONS } from '@/features/dashboards/presentation.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { ValueStateFlag } from '@/shared/ui/ValueState.tsx';

import { type ReportColumnView, type ReportSection } from '../api/types.ts';
import { REASON_ICONS } from '../presentation.ts';

// FG-02 §13.2's Coverage/Freshness Banner, one per projection the result reads (FE-CMP-008): which columns it fills,
// whether those values are live, published or historical, how current the source says they are, from which source and
// version, and how many of the projects it expected a value for it shows, leaves out, or shows stale.

function Section({
  section,
  columns,
}: {
  section: ReportSection;
  columns: ReportColumnView[];
}): ReactElement {
  const { t, language, formatDateTime } = useI18n();
  const semantic = SEMANTIC_STATES[section.projection.semanticState];
  const { coverage } = section;
  const filled = columns
    .filter((column) => column.projectionCode === section.projectionCode)
    .map((column) => column.label[language]);
  return (
    <li
      className="report-section"
      data-projection={section.projectionCode}
      data-semantic-state={section.projection.semanticState}
      data-freshness={section.projection.freshness}
    >
      <p className="report-section__title">
        {filled.length === 0 ? section.projectionCode : filled.join(' · ')}
      </p>
      <div className="widget__status">
        <span className={`badge ${semantic.badge}`}>{t(semantic.label)}</span>
        {section.projection.freshness === 'STALE' && (
          <ValueStateFlag
            icon="stale"
            label={t('dashboards.freshness.STALE')}
            state="STALE"
            tone="warning"
          />
        )}
        {section.unknownReason !== null && (
          <ValueStateFlag
            icon={REASON_ICONS[section.unknownReason]}
            label={t(UNKNOWN_REASONS[section.unknownReason].label)}
            state={section.unknownReason}
            tone={section.unknownReason === 'SOURCE_UNAVAILABLE' ? 'warning' : 'neutral'}
          />
        )}
        <span>
          {section.projection.asOf === null
            ? t('reports.banner.noAsOf')
            : t('reports.banner.asOf', { date: formatDateTime(section.projection.asOf) })}
        </span>
        <span>
          {t('reports.banner.source', {
            source: section.sourceDomain,
            version: section.projectionVersion,
          })}
        </span>
      </div>
      <p className="widget__coverage" data-coverage>
        {[
          t('reports.banner.included', {
            included: coverage.includedCount,
            eligible: coverage.eligibleCount,
          }),
          ...(coverage.excludedCount > 0
            ? [t('reports.banner.excluded', { count: coverage.excludedCount })]
            : []),
          ...(coverage.staleCount > 0
            ? [t('reports.banner.stale', { count: coverage.staleCount })]
            : []),
        ].join(' · ')}
      </p>
    </li>
  );
}

export function CoverageBanner({
  sections,
  columns,
}: {
  sections: ReportSection[];
  columns: ReportColumnView[];
}): ReactElement | null {
  const { t } = useI18n();
  if (sections.length === 0) {
    return null;
  }
  return (
    <section className="report-banner" aria-label={t('reports.banner.label')}>
      <ul className="report-banner__sections">
        {sections.map((section) => (
          <Section key={section.projectionCode} section={section} columns={columns} />
        ))}
      </ul>
    </section>
  );
}
