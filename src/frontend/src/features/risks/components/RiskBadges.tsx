import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import {
  type BilingualLabel,
  type RiskAcceptanceStatus,
  type RiskStatus,
  type RiskTreatmentActionStatus,
} from '../api/types.ts';
import { actionStatusTone, acceptanceStatusTone, riskStatusTone } from '../presentation.ts';
import { type RiskMatrix, severityStep } from '../riskRules.ts';

/** WF-06's lifecycle: Identified, Assessed, In treatment, Monitoring, Closed. */
export function RiskStatusBadge({ status }: { status: RiskStatus }): ReactElement {
  const { t } = useI18n();
  return <StatusBadge label={t(`risks.status.${status}`)} tone={riskStatusTone(status)} />;
}

export function ActionStatusBadge({ status }: { status: RiskTreatmentActionStatus }): ReactElement {
  const { t } = useI18n();
  return <StatusBadge label={t(`risks.actionStatus.${status}`)} tone={actionStatusTone(status)} />;
}

export function AcceptanceStatusBadge({ status }: { status: RiskAcceptanceStatus }): ReactElement {
  const { t } = useI18n();
  return (
    <StatusBadge
      label={t(`risks.acceptanceStatus.${status}`)}
      tone={acceptanceStatusTone(status)}
    />
  );
}

/**
 * A rating as the server gave it, in its own words, coloured by its step in the matrix in force. A rating the version
 * no longer defines, or one read without the matrix, is shown in words on a neutral ground.
 */
export function RatingBadge({
  code,
  label,
  matrix,
}: {
  code: string;
  label: BilingualLabel;
  matrix: RiskMatrix | null;
}): ReactElement {
  const { language } = useI18n();
  const step = matrix === null ? null : severityStep(matrix, code);
  return (
    <span
      className={`badge risk-rating risk-rating--${step === null ? 'none' : String(step)}`}
      data-rating={code}
    >
      {label[language]}
    </span>
  );
}

/** "Not assessed": an IDENTIFIED risk has no rating, which is said, never shown as the lowest one. */
export function UnratedBadge(): ReactElement {
  const { t } = useI18n();
  return <StatusBadge label={t('risks.rating.none')} tone="neutral" />;
}
