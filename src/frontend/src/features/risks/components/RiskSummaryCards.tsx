import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type RiskSummary } from '../riskRules.ts';

/**
 * SCR-080's summary cards, from the same inputs the Risk Dashboard composes (riskSummary). The critical count needs the
 * matrix in force: without it the card says it is unknown, never zero.
 */
export function RiskSummaryCards({ summary }: { summary: RiskSummary }): ReactElement {
  const { t } = useI18n();
  const cards: { key: string; label: string; value: string }[] = [
    { key: 'open', label: t('risks.summary.open'), value: String(summary.open) },
    {
      key: 'critical',
      label: t('risks.summary.critical'),
      value: summary.critical === null ? t('risks.summary.unknown') : String(summary.critical),
    },
    { key: 'reviewDue', label: t('risks.summary.reviewDue'), value: String(summary.reviewDue) },
    {
      key: 'unassessed',
      label: t('risks.summary.unassessed'),
      value: String(summary.unassessed),
    },
    { key: 'accepted', label: t('risks.summary.accepted'), value: String(summary.accepted) },
  ];
  return (
    <dl className="risk-summary">
      {cards.map((card) => (
        <div key={card.key} className="risk-summary__card" data-summary={card.key}>
          <dt>{card.label}</dt>
          <dd className="figure">{card.value}</dd>
        </div>
      ))}
    </dl>
  );
}
