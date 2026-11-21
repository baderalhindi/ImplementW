import { type BilingualLabel } from '@/features/identity-access/api/types.ts';

import { type Visual, type VisualAxes, type VisualType } from './designApi.ts';

// A bar, column or line chart's axes (docs/architecture/dashboard-designer-axes.md, 19 Nov 2026), Power BI style: the category axis (what the
// chart is grouped by) and the value axis, each shown or hidden and titled in both languages; the value axis also takes a range and gridlines.
// The designer keeps what is typed, half-written titles and a start not below the end included, and sends (and draws) only what the API takes.

/** The types that draw axes. */
const AXIS_TYPES: readonly VisualType[] = ['BAR', 'COLUMN', 'LINE'];

export function takesAxes(type: VisualType): boolean {
  return AXIS_TYPES.includes(type);
}

/** Both axes shown, no titles, the range from the figures, gridlines on: what null stands for. */
export const DEFAULT_AXES: VisualAxes = {
  category: { show: true, title: null },
  value: { show: true, title: null, min: null, max: null, gridlines: true },
};

/** The longest axis title, per language, after trimming (the API's rule). */
export const AXIS_TITLE_MAX = 60;
/** A range end lies within ±10¹² (the API's rule). */
const RANGE_LIMIT = 1e12;

/** The axes as the designer holds them: the visual's own, or the defaults. */
export function axesOf(visual: Pick<Visual, 'axes'>): VisualAxes {
  return visual.axes ?? DEFAULT_AXES;
}

/** The axis boxes' words: the category axis is X for a column or line chart and Y for a bar chart, the value axis the other. */
export function axisLabels(type: VisualType): {
  category: 'dashboards.designer.axisX' | 'dashboards.designer.axisY';
  value: 'dashboards.designer.axisX' | 'dashboards.designer.axisY';
} {
  return type === 'BAR'
    ? { category: 'dashboards.designer.axisY', value: 'dashboards.designer.axisX' }
    : { category: 'dashboards.designer.axisX', value: 'dashboards.designer.axisY' };
}

/** Whether a title is written in one language but not the other: the API takes both or none. */
export function titleIsHalf(title: BilingualLabel | null): boolean {
  return title !== null && (title.ar.trim() === '') !== (title.en.trim() === '');
}

/** A title as it is sent: both languages trimmed, or none when either is empty. */
function sentTitle(title: BilingualLabel | null): BilingualLabel | null {
  if (title === null) {
    return null;
  }
  const ar = title.ar.trim();
  const en = title.en.trim();
  return ar === '' || en === '' ? null : { ar, en };
}

export type RangeProblem = 'order' | 'limit';

/** What is wrong with a range: an end outside ±10¹², or a start not below the end; null when it is taken. */
export function rangeProblem(min: number | null, max: number | null): RangeProblem | null {
  if ([min, max].some((end) => end !== null && Math.abs(end) > RANGE_LIMIT)) {
    return 'limit';
  }
  return min !== null && max !== null && min >= max ? 'order' : null;
}

/**
 * The axes as the API takes them: null for a type without axes and for the defaults; a half-written title as none, and a range the API would
 * refuse as automatic. The charts draw the same.
 */
export function sentAxes(visual: Pick<Visual, 'visualType' | 'axes'>): VisualAxes | null {
  if (!takesAxes(visual.visualType) || visual.axes === undefined || visual.axes === null) {
    return null;
  }
  const { category, value } = visual.axes;
  const range = rangeProblem(value.min, value.max) === null;
  const axes: VisualAxes = {
    category: { show: category.show, title: sentTitle(category.title) },
    value: {
      show: value.show,
      title: sentTitle(value.title),
      min: range ? value.min : null,
      max: range ? value.max : null,
      gridlines: value.gridlines,
    },
  };
  return JSON.stringify(axes) === JSON.stringify(DEFAULT_AXES) ? null : axes;
}

/** The axes a chart draws: what is sent, or the defaults. */
export function drawnAxes(visual: Pick<Visual, 'visualType' | 'axes'>): VisualAxes {
  return sentAxes(visual) ?? DEFAULT_AXES;
}

/** A value axis's scale: its ends, its ticks, the base a bar starts from, and where a value falls on it (0 at the low end, 1 at the high). */
export interface Scale {
  low: number;
  high: number;
  ticks: number[];
  /** Zero where the scale holds it, else its low end. */
  base: number;
  at: (value: number) => number;
}

/** About this many steps between the ends. */
const INTERVALS = 5;

/** The smallest round step (1, 2 or 5 times a power of ten) at least `raw`. */
function roundStep(raw: number): number {
  const power = 10 ** Math.floor(Math.log10(raw));
  return (
    [1, 2, 5].map((factor) => factor * power).find((step) => step >= raw * 0.999) ?? 10 * power
  );
}

/** A multiple of a step without the float's tail (0.30000000000000004). */
const exact = (value: number) => Number(value.toPrecision(12));

/**
 * The scale of `values` in round steps: from zero (or the lowest value, below zero) to the highest, 0 to 100 at least for percentages, and from
 * the lowest value for dates (`zero` false). A given start or end is kept as it is; the figures outside it are clipped by the chart. Whole
 * figures (counts) take whole steps.
 */
export function valueScale(
  values: readonly number[],
  range: { min: number | null; max: number | null },
  { percent, zero }: { percent: boolean; zero: boolean },
): Scale {
  const finite = values.filter((value) => Number.isFinite(value));
  const lowest = finite.length === 0 ? 0 : Math.min(...finite);
  const highest = finite.length === 0 ? 0 : Math.max(...finite);
  let low = range.min ?? (zero ? Math.min(0, lowest) : lowest);
  let high =
    range.max ?? (percent ? Math.max(100, highest) : zero ? Math.max(0, highest) : highest);
  if (high <= low) {
    // One end given past the figures, or every figure the same: the other end steps away from it.
    const span = Math.abs(range.min ?? range.max ?? low) || 1;
    if (range.max === null) {
      high = low + span;
    } else {
      low = high - span;
    }
  }
  const whole =
    finite.every((value) => Number.isInteger(value)) &&
    [range.min, range.max].every((end) => end === null || Number.isInteger(end));
  const raw = roundStep((high - low) / INTERVALS);
  const step = whole ? Math.max(1, raw) : raw;
  if (range.min === null) {
    low = exact(Math.floor(low / step) * step);
  }
  if (range.max === null) {
    high = exact(Math.ceil(high / step) * step);
  }
  const ticks: number[] = [];
  for (let index = Math.ceil(exact(low / step)); exact(index * step) <= high; index++) {
    ticks.push(exact(index * step));
  }
  const clamp = (value: number) => Math.min(high, Math.max(low, value));
  return {
    low,
    high,
    ticks,
    base: zero ? clamp(0) : low,
    at: (value) => (clamp(value) - low) / (high - low),
  };
}
