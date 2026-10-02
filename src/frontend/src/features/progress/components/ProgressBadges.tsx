import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type HealthStatus, type ProgressSubmissionStatus } from '../api/types.ts';
import { healthTone, submissionTone } from '../presentation.ts';

/** PUBLISHED/OFFICIAL and CURRENT/LIVE (M-12, TASK-044 D-13). */
export type SemanticState = 'official' | 'live';

/**
 * Which of the two semantic states a value is in. They differ in word, fill and border, not colour alone: the
 * official value is solid, the live one outlined and dashed.
 */
export function SemanticStateBadge({ state }: { state: SemanticState }): ReactElement {
  const { t } = useI18n();
  return <span className={`badge badge--${state}`}>{t(`progress.semanticState.${state}`)}</span>;
}

export function HealthBadge({ health }: { health: HealthStatus }): ReactElement {
  const { t } = useI18n();
  return <StatusBadge label={t(`progress.health.${health}`)} tone={healthTone(health)} />;
}

export function SubmissionStatusBadge({
  status,
}: {
  status: ProgressSubmissionStatus;
}): ReactElement {
  const { t } = useI18n();
  return <StatusBadge label={t(`progress.status.${status}`)} tone={submissionTone(status)} />;
}

/** ADR-014's permanent intake marker: the project entered under way, and its figures run from that date. */
export function IntakeMarker({ intakeDate }: { intakeDate: string }): ReactElement {
  const { t } = useI18n();
  return <StatusBadge label={t('progress.intake.marker', { date: intakeDate })} tone="warning" />;
}

/** ADR-009: a reported figure that is an override, not the roll-up. */
export function OverriddenBadge(): ReactElement {
  const { t } = useI18n();
  return <StatusBadge label={t('progress.figures.overridden')} tone="warning" />;
}
