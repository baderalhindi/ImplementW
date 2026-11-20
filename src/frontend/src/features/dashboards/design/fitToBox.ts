import { type BoxSize } from './useBoxSize.ts';

// Fit to box (the user, 20 Nov 2026: "I want to be free to minimize a visual or container, not fixed"): a visual sized smaller than what it
// holds draws it smaller, whole, instead of cutting it off or scrolling it.

/** A pixel of rounding is not an overflow. */
const TOLERANCE_PX = 1;

/**
 * The scale that fits content of its `natural` size into `box`: the smaller of the two ratios, so it shrinks evenly; 1 when it fits, never
 * larger than 1, and 1 while either size is unknown or empty.
 */
/**
 * The smallest a body is drawn to fit (20 Nov 2026): below it text is too small to read, so a body that would need less is drawn at this
 * scale and scrolls inside its visual instead (a long table, say).
 */
export const MIN_FIT_SCALE = 0.5;

export function fitScale(natural: BoxSize, box: BoxSize): number {
  if (natural.width <= 0 || natural.height <= 0 || box.width <= 0 || box.height <= 0) {
    return 1;
  }
  const across = natural.width <= box.width + TOLERANCE_PX ? 1 : box.width / natural.width;
  const down = natural.height <= box.height + TOLERANCE_PX ? 1 : box.height / natural.height;
  return Math.min(1, across, down);
}

/**
 * Whether a visual's body is fitted to its box: all but the charts that draw at their box's size already (bars, columns, lines), a text box and the map,
 * whose panning and zooming read the pointer on an unscaled map.
 */
export function fitsToBox(visual: {
  visualType: string;
  section?: string | null | undefined;
}): boolean {
  return (
    !DRAWN_AT_BOX_SIZE.has(visual.visualType) &&
    (visual.section === undefined || visual.section === null || !SCROLLED.has(visual.section))
  );
}

/**
 * Visuals that draw at their box's size already: the charts, a text box, whose words wrap in its box and scroll when longer, and a map visual, as
 * the map section is not fitted.
 */
const DRAWN_AT_BOX_SIZE: ReadonlySet<string> = new Set([
  'BAR',
  'COLUMN',
  'LINE',
  'TABLE',
  'TEXT',
  'MAP',
]);

/**
 * Sections never drawn smaller (20 Nov 2026: a table at half size was too small to read): the tables and lists, which scroll in their visual
 * at full size, and the map, whose panning and zooming read the pointer on an unscaled map.
 */
const SCROLLED: ReadonlySet<string> = new Set([
  'PORTFOLIO_TABLE',
  'INITIATIVE_TABLE',
  'SECTOR_TABLE',
  'STATUS_TABLE',
  'CURRENT_STAGE_TABLE',
  'TOP_FOLLOW_UP',
  'ENTITY_COMPLETION_TABLE',
  'PROJECT_LOCATIONS',
]);

/** How many halvings the search for the largest scale makes: within 1/128 of the range, finer than a pixel at a visual's size. */
const SEARCH_STEPS = 7;

/**
 * The largest scale from `least` to 1 at which `fitsAt` holds, by halving: content laid out wider as it is drawn smaller (at its room's
 * width over the scale) takes fewer lines, so a long table or a grid of boxes uses the room's whole width rather than a corner of it.
 * `least` is the scale known to fit (`fitScale` at the room's own width); 1 when `fitsAt(1)`.
 */
export function largestFit(least: number, fitsAt: (scale: number) => boolean): number {
  if (fitsAt(1)) {
    return 1;
  }
  let fits = least;
  let fails = 1;
  for (let step = 0; step < SEARCH_STEPS; step++) {
    const middle = (fits + fails) / 2;
    if (fitsAt(middle)) {
      fits = middle;
    } else {
      fails = middle;
    }
  }
  return fits;
}
