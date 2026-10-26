import { type ReactElement, type ReactNode } from 'react';

import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { ValueStateFlag as Flag, type ValueStateIcon } from '@/shared/ui/ValueState.tsx';

import { type Provenance } from '../api/types.ts';
import { type FigureState, type UnknownStatus } from '../financialKpiRules.ts';
import { formatAmount, formatKpiValue } from '../presentation.ts';

// Acceptance criterion 1: a figure that is Missing, Stale or N/A renders as that state, with an icon and its word, in
// a neutral or warning style — never blank, never 0, never green. A withheld one says it is restricted (ADR-010).
// `data-value-state` names the state for tests and for the eye of a reviewer reading the DOM.

const UNKNOWN_LABELS: Record<UnknownStatus, TranslationKey> = {
  MISSING: 'financialKpi.value.MISSING',
  STALE: 'financialKpi.value.STALE',
  NOT_APPLICABLE: 'financialKpi.value.NOT_APPLICABLE',
};

const ICONS: Record<UnknownStatus, ValueStateIcon> = {
  MISSING: 'missing',
  STALE: 'stale',
  NOT_APPLICABLE: 'notApplicable',
};

/** "No data", "Stale", "Not applicable": a bordered flag with its icon. Stale is a warning; the others neutral. */
export function ValueStateFlag({ status }: { status: UnknownStatus }): ReactElement {
  const { t } = useI18n();
  return (
    <Flag
      icon={ICONS[status]}
      label={t(UNKNOWN_LABELS[status])}
      state={status}
      tone={status === 'STALE' ? 'warning' : 'neutral'}
    />
  );
}

/** A figure withheld from the caller's audience: shown as such, never as missing or as 0. */
export function RestrictedFlag(): ReactElement {
  const { t } = useI18n();
  return <Flag icon="restricted" label={t('financialKpi.value.restricted')} state="RESTRICTED" />;
}

/**
 * No figure exists at all ("No approved budget", "No forecast given", "No data for this period"): the same flag, its
 * own words.
 */
export function AbsentFlag({ label }: { label: string }): ReactElement {
  return <Flag icon="missing" label={label} state="ABSENT" />;
}

interface FigureProps {
  state: FigureState;
  /** What "no figure at all" means here. */
  absent: TranslationKey;
}

/** A SAR amount: "SAR 1,250,000.00", or its state. No currency is ever chosen (ADR-008). */
export function AmountFigure({ state, absent }: FigureProps): ReactElement {
  const { t } = useI18n();
  return (
    <Figure state={state} absent={absent}>
      {(value) => t('financialKpi.figures.sar', { amount: formatAmount(value) })}
    </Figure>
  );
}

/** A KPI value to the API's four places, or its state. */
export function KpiFigure({ state, absent }: FigureProps): ReactElement {
  return (
    <Figure state={state} absent={absent}>
      {formatKpiValue}
    </Figure>
  );
}

function Figure({
  state,
  absent,
  children,
}: FigureProps & {
  children: (value: Extract<FigureState, { kind: 'measured' }>['value']) => ReactNode;
}): ReactElement {
  const { t } = useI18n();
  switch (state.kind) {
    case 'measured':
      return (
        <span className="figure" dir="ltr" data-value-state="MEASURED">
          {children(state.value)}
        </span>
      );
    case 'unknown':
      return <ValueStateFlag status={state.status} />;
    case 'restricted':
      return <RestrictedFlag />;
    case 'absent':
      return <AbsentFlag label={t(absent)} />;
  }
}

const SOURCE_LABELS = {
  MANUAL: 'financialKpi.source.MANUAL',
  ETIMAD: 'financialKpi.source.ETIMAD',
  OTHER: 'financialKpi.source.OTHER',
} as const satisfies Record<string, TranslationKey>;

/**
 * ADR-008 extended: where a figure came from and the day it is true as of, under the figure. A manual figure carries a
 * bordered "Manual entry" marker, so it is visibly manual. A withheld source says so.
 */
export function ProvenanceLine({
  provenance,
  maskedFields,
  personName,
}: {
  provenance: Provenance;
  maskedFields: string[];
  personName?: ((id: string | null) => string) | undefined;
}): ReactElement {
  const { t } = useI18n();
  const masked = (field: string) => maskedFields.includes(field);
  const source = provenance.sourceType;
  const parts: string[] = [];
  if (masked('asOfDate')) {
    parts.push(t('financialKpi.provenance.asOfRestricted'));
  } else if (provenance.asOfDate !== undefined) {
    parts.push(t('financialKpi.provenance.asOf', { date: provenance.asOfDate }));
  }
  if (!masked('sourceReference') && provenance.sourceReference) {
    parts.push(t('financialKpi.provenance.reference', { reference: provenance.sourceReference }));
  }
  if (personName !== undefined && !masked('enteredByUserId') && provenance.enteredByUserId) {
    parts.push(
      t('financialKpi.provenance.enteredBy', { name: personName(provenance.enteredByUserId) }),
    );
  }
  return (
    <span className="provenance">
      {masked('sourceType') || source === undefined ? (
        <span className="provenance__source">{t('financialKpi.provenance.sourceRestricted')}</span>
      ) : (
        <span
          className={`provenance__source provenance__source--${source.toLowerCase()}`}
          data-source={source}
        >
          {source === 'MANUAL' && (
            <svg
              className="value-state__icon"
              viewBox="0 0 16 16"
              width="14"
              height="14"
              aria-hidden="true"
              focusable="false"
            >
              <path
                d="M2.5 13.5 3 10.6 10.8 2.8l2.4 2.4-7.8 7.8z"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.5"
              />
            </svg>
          )}
          {t(SOURCE_LABELS[source])}
        </span>
      )}
      {parts.length > 0 && <span className="provenance__details">{parts.join(' · ')}</span>}
    </span>
  );
}

/** A figure with its provenance below it. */
export function FigureWithSource({
  figure,
  provenance,
}: {
  figure: ReactNode;
  provenance: ReactNode;
}): ReactElement {
  return (
    <span className="figure-stack">
      {figure}
      {provenance}
    </span>
  );
}
