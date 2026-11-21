import { type Box, tenth } from './visuals.ts';

/**
 * Alignment guides while a visual is moved or sized (20 Nov 2026, "thin red lines like in Power BI"): where one of its edges or its centre
 * lines up with another visual's, a guide is drawn across the canvas; an edge within SNAP of another's is pulled onto it.
 */

/** An edge or centre line: across (`x`, a column) or down (`y`, a row), and which of the moving visual's lines it is. */
export interface Guide {
  axis: 'x' | 'y';
  /** In columns (x) or rows (y) from the grid's start. */
  at: number;
  line: 'start' | 'end' | 'center';
}

/** How close, in cells, an edge is pulled onto another's. */
export const SNAP = 0.3;
/** Lines this close, in cells, are drawn as aligned (positions are tenths, so this is "the same tenth"). */
const SAME = 0.05;

const LINES = ['start', 'end', 'center'] as const;

function lineOf(box: Box, axis: Guide['axis'], line: Guide['line']): number {
  const from = axis === 'x' ? box.layoutColumn : box.layoutRow;
  const length = axis === 'x' ? box.layoutWidth : box.layoutHeight;
  return line === 'start' ? from : line === 'end' ? from + length : from + length / 2;
}

/** The lines the other visuals draw on an axis. */
function linesOn(others: readonly Box[], axis: Guide['axis']): number[] {
  return others.flatMap((other) => LINES.map((line) => lineOf(other, axis, line)));
}

/** The nearest pull within SNAP that puts one of `lines` (of the moving visual) onto one of `targets`; 0 when none is near. */
function pull(lines: readonly number[], targets: readonly number[]): number {
  let best = 0;
  let distance = SNAP + SAME;
  for (const mine of lines) {
    for (const target of targets) {
      const delta = target - mine;
      if (Math.abs(delta) < distance) {
        distance = Math.abs(delta);
        best = delta;
      }
    }
  }
  return distance <= SNAP ? best : 0;
}

/** The place a moved visual takes once its edges or centre near another visual's are pulled onto them. */
export function snappedPlace(box: Box, others: readonly Box[]): { column: number; row: number } {
  const across = pull(
    LINES.map((line) => lineOf(box, 'x', line)),
    linesOn(others, 'x'),
  );
  const down = pull(
    LINES.map((line) => lineOf(box, 'y', line)),
    linesOn(others, 'y'),
  );
  return { column: tenth(box.layoutColumn + across), row: tenth(box.layoutRow + down) };
}

/** The size a sized visual takes once its end edges near another visual's lines are pulled onto them (its start stays where it is). */
export function snappedSize(box: Box, others: readonly Box[]): { width: number; height: number } {
  const across = pull([lineOf(box, 'x', 'end')], linesOn(others, 'x'));
  const down = pull([lineOf(box, 'y', 'end')], linesOn(others, 'y'));
  return { width: tenth(box.layoutWidth + across), height: tenth(box.layoutHeight + down) };
}

/** The guides to draw: each of the moving visual's lines that lines up with another visual's, once per place. */
export function guidesFor(box: Box, others: readonly Box[]): Guide[] {
  const guides: Guide[] = [];
  for (const axis of ['x', 'y'] as const) {
    const targets = linesOn(others, axis);
    for (const line of LINES) {
      const at = lineOf(box, axis, line);
      if (
        targets.some((target) => Math.abs(target - at) < SAME) &&
        !guides.some((guide) => guide.axis === axis && Math.abs(guide.at - at) < SAME)
      ) {
        guides.push({ axis, at: tenth(at), line });
      }
    }
  }
  return guides;
}
