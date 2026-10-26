import { type ReactElement } from 'react';

import { Detail } from '@/features/projects/components/Detail.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type Decimal } from '../api/types.ts';
import { formatPercent, formatVariance, variance, varianceTone } from '../presentation.ts';

import { OverriddenBadge } from './ProgressBadges.tsx';

// ADR-009: planned and actual side by side, with the variance between them, and an override marked as one with the
// calculated value beside it. ADR-014: a legacy-intake project's variance runs from its intake, and its opening
// position, entered once, has none.

export interface FigureValues {
  plannedPercent: Decimal | null;
  /** The reported figure: the override when overridden. */
  actualPercent: Decimal | null;
  isOverridden: boolean;
  /** The roll-up, shown beside an override; absent where the API keeps none (a snapshot, the live value). */
  actualPercentCalculated?: Decimal | undefined;
  /** ADR-014's opening position: the intake's figure, which has no plan to vary from. */
  isOpeningPosition?: boolean;
}

/** The project's ADR-014 intake date, or null for a project registered before it started. */
export type IntakeDate = string | null;

export function PercentValue({ value }: { value: Decimal | null }): ReactElement {
  const { t } = useI18n();
  const text = formatPercent(value);
  return text === null ? (
    <span className="figure figure--none">{t('progress.figures.noPlan')}</span>
  ) : (
    <span className="figure" dir="ltr">
      {text}
    </span>
  );
}

export function ActualValue({ figures }: { figures: FigureValues }): ReactElement {
  const { t } = useI18n();
  const calculated =
    figures.isOverridden && figures.actualPercentCalculated !== undefined
      ? formatPercent(figures.actualPercentCalculated)
      : null;
  return (
    <span className="figure-group">
      <PercentValue value={figures.actualPercent} />
      {figures.isOverridden && <OverriddenBadge />}
      {calculated !== null && (
        <span className="details__aside">
          {t('progress.figures.calculated', { value: calculated })}
        </span>
      )}
    </span>
  );
}

export function VarianceValue({
  figures,
  intakeDate,
}: {
  figures: FigureValues;
  intakeDate: IntakeDate;
}): ReactElement {
  const { t } = useI18n();
  if (figures.isOpeningPosition === true) {
    return <span className="figure figure--none">{t('progress.intake.openingPosition')}</span>;
  }
  const points = variance(figures.actualPercent, figures.plannedPercent);
  if (points === null) {
    return <span className="figure figure--none">{t('progress.figures.noVariance')}</span>;
  }
  return (
    <span className="figure-group">
      <StatusBadge
        label={t('progress.figures.points', { value: formatVariance(points) })}
        tone={varianceTone(points)}
      />
      {intakeDate !== null && (
        <span className="details__aside">{t('progress.intake.since', { date: intakeDate })}</span>
      )}
    </span>
  );
}

/** Planned, actual and variance, in that order, as rows of a `.details` list. */
export function FigureDetails({
  figures,
  intakeDate,
}: {
  figures: FigureValues;
  intakeDate: IntakeDate;
}): ReactElement {
  const { t } = useI18n();
  return (
    <>
      <Detail term={t('progress.figures.planned')}>
        <PercentValue value={figures.plannedPercent} />
      </Detail>
      <Detail term={t('progress.figures.actual')}>
        <ActualValue figures={figures} />
      </Detail>
      <Detail term={t('progress.figures.variance')}>
        <VarianceValue figures={figures} intakeDate={intakeDate} />
      </Detail>
    </>
  );
}
