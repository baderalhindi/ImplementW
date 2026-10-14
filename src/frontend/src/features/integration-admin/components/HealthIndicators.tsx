import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import { type IntegrationAvailability, type IntegrationFreshness } from '../api/types.ts';
import { availabilityTone, freshnessTone } from '../presentation.ts';

// Blueprint §17.1: availability is reachability, "distinct from freshness"; freshness is how current the data is, and
// "a reachable source may still be stale". So they are two indicators, never one health icon (acceptance criterion
// 2): each names its dimension, has its own icon shape — a signal for availability, a clock for freshness — and its own
// value word. Colour only repeats the word (WCAG 1.4.1). `data-indicator` names the dimension for tests and reviewers.

function AvailabilityIcon(): ReactElement {
  return (
    <svg
      className="indicator__icon"
      viewBox="0 0 16 16"
      width="16"
      height="16"
      aria-hidden="true"
      focusable="false"
    >
      <path d="M2 13h2v-3H2zM7 13h2V7H7zM12 13h2V3h-2z" fill="currentColor" />
    </svg>
  );
}

function FreshnessIcon(): ReactElement {
  return (
    <svg
      className="indicator__icon"
      viewBox="0 0 16 16"
      width="16"
      height="16"
      aria-hidden="true"
      focusable="false"
    >
      <circle cx="8" cy="8" r="6.2" fill="none" stroke="currentColor" strokeWidth="1.6" />
      <path d="M8 4.2V8l2.6 1.8" fill="none" stroke="currentColor" strokeWidth="1.6" />
    </svg>
  );
}

interface IndicatorProps {
  dimension: 'availability' | 'freshness';
  name: string;
  value: string;
  state: string;
  tone: StatusTone;
}

function Indicator({ dimension, name, value, state, tone }: IndicatorProps): ReactElement {
  return (
    <span
      className={`indicator indicator--${dimension} indicator--${tone}`}
      data-indicator={dimension}
      data-state={state}
    >
      {dimension === 'availability' ? <AvailabilityIcon /> : <FreshnessIcon />}
      <span className="indicator__name">{name}</span>
      <span className="indicator__value">{value}</span>
    </span>
  );
}

export function AvailabilityIndicator({
  availability,
}: {
  availability: IntegrationAvailability;
}): ReactElement {
  const { t } = useI18n();
  return (
    <Indicator
      dimension="availability"
      name={t('integrationAdmin.indicators.availability')}
      value={t(`integrationAdmin.availability.${availability}`)}
      state={availability}
      tone={availabilityTone(availability)}
    />
  );
}

export function FreshnessIndicator({
  freshness,
}: {
  freshness: IntegrationFreshness;
}): ReactElement {
  const { t } = useI18n();
  return (
    <Indicator
      dimension="freshness"
      name={t('integrationAdmin.indicators.freshness')}
      value={t(`integrationAdmin.freshness.${freshness}`)}
      state={freshness}
      tone={freshnessTone(freshness)}
    />
  );
}

/** What each indicator means, stated before the figures so neither is read as the other. */
export function IndicatorLegend(): ReactElement {
  const { t } = useI18n();
  return (
    <dl className="indicator-legend" aria-label={t('integrationAdmin.indicators.legend')}>
      <div>
        <dt>
          <AvailabilityIcon /> {t('integrationAdmin.indicators.availability')}
        </dt>
        <dd>{t('integrationAdmin.indicators.availabilityMeaning')}</dd>
      </div>
      <div>
        <dt>
          <FreshnessIcon /> {t('integrationAdmin.indicators.freshness')}
        </dt>
        <dd>{t('integrationAdmin.indicators.freshnessMeaning')}</dd>
      </div>
    </dl>
  );
}
