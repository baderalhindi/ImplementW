import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import { type DashboardWidgetType } from './api/types.ts';

// Which chart a distribution is drawn as, and in which fills. Colours come from two palettes validated for
// colour-vision deficiency (the dataviz method): the status fills keep a source's own RAG meaning; the categorical
// fills only tell values apart. Neither is ever chosen by a value's rank.

/** One drawn value: its key, its count, its words and its fill. */
export interface ChartBucket {
  key: string;
  count: number;
  text: string;
  color: string;
}

export type ChartForm = 'donut' | 'columns' | 'bars';

/** Up to this many values a donut or column chart stays readable; past it a ranked bar list does. */
const FEW = 6;
const CATEGORICAL = [1, 2, 3, 4, 5, 6].map((slot) => `var(--chart-category-${String(slot)})`);
const STATUS: Record<Exclude<StatusTone, 'neutral'>, string> = {
  positive: 'var(--chart-positive)',
  warning: 'var(--chart-warning)',
  negative: 'var(--chart-negative)',
  info: 'var(--chart-info)',
};
const NEUTRAL = ['var(--chart-neutral-1)', 'var(--chart-neutral-2)', 'var(--chart-neutral-3)'];

/** DONUT_PIE and a short STATUS_DISTRIBUTION are donuts, a short BAR_COLUMN columns; a long one of any is a bar list. */
export function chartForm(widgetType: DashboardWidgetType, count: number): ChartForm {
  if (count > FEW) {
    return 'bars';
  }
  return widgetType === 'BAR_COLUMN' ? 'columns' : 'donut';
}

/**
 * The fill of each value. A status scale (a health or RAG count) keeps its tones' own colours, with "no data" values in
 * greys; any other (a lifecycle, a rating the source gave no colour) takes categorical fills in the source's order;
 * past six values one fill, as length then carries the comparison.
 */
export function bucketColors(tones: StatusTone[], statusScale: boolean): string[] {
  if (statusScale) {
    let neutral = 0;
    return tones.map((tone) =>
      tone === 'neutral'
        ? (NEUTRAL[Math.min(neutral++, NEUTRAL.length - 1)] ?? 'var(--chart-neutral-1)')
        : STATUS[tone],
    );
  }
  return tones.length > CATEGORICAL.length
    ? tones.map(() => 'var(--chart-single)')
    : tones.map((_, index) => CATEGORICAL[index] ?? 'var(--chart-single)');
}
