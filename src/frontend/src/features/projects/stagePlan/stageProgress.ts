// Stage-weighted progress (review round 7, item 1), the API's StageProgress arithmetic, so a figure shown while it is typed is the figure the server
// records. Overall progress is Σ weight × % complete ÷ Σ weight over the stages that apply: a stage that does not apply takes its weight out.

const DAY_MS = 86_400_000;

function dayNumber(isoDate: string): number {
  return Math.floor(Date.parse(`${isoDate}T00:00:00Z`) / DAY_MS);
}

/** A stage's planned % on `asOf`: 0 before its start, 100 from its end, linear by day between. */
export function plannedPercent(start: string, end: string, asOf: string): number {
  const [from, to, on] = [dayNumber(start), dayNumber(end), dayNumber(asOf)];
  if (on < from) {
    return 0;
  }
  if (on >= to) {
    return 100;
  }
  return (100 * (on - from)) / (to - from);
}

export interface WeightedStage {
  weight: number;
  /** null when not known: then the overall figure is not known either. */
  percent: number | null;
}

/** Σ w·c ÷ Σ w over the stages given (the applicable ones); null when they weigh nothing or a % is unknown. */
export function weightedOverall(stages: readonly WeightedStage[]): number | null {
  let weights = 0;
  let weighted = 0;
  for (const stage of stages) {
    if (stage.percent === null) {
      return null;
    }
    weights += stage.weight;
    weighted += stage.weight * stage.percent;
  }
  return weights === 0 ? null : weighted / weights;
}

/** The current stage: the first applicable stage, in stage order, below 100%; the last one when all are complete. */
export function currentStage<T>(
  applicableInOrder: readonly T[],
  percentOf: (stage: T) => number,
): T | null {
  return (
    applicableInOrder.find((stage) => percentOf(stage) < 100) ?? applicableInOrder.at(-1) ?? null
  );
}

/** A figure to one decimal, as the client's reports show it. */
export function oneDecimal(value: number): string {
  return (Math.round(value * 10) / 10).toFixed(1);
}

/** Today in UTC, as an ISO date: the API's ReportingCalendar day. */
export function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}
