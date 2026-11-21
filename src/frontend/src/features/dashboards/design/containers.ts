import { type CSSProperties } from 'react';

import { translate } from '@/shared/i18n/i18n.ts';

import { type Visual, type VisualType } from './designApi.ts';
import {
  type Box,
  COLUMNS,
  DEFAULT_SIZE,
  freePlace,
  gridPlace,
  inReadingOrder,
  MAX_HEIGHT,
  MAX_VISUALS,
  overlaps,
  rowsOf,
  settled,
  tenth,
  withoutEmptyRows,
} from './visuals.ts';

// Containers (19 Nov 2026, docs/architecture/dashboard-designer-containers.md): a framed, titled box on the dashboard holding visuals, one level
// deep. A child is placed on its container's inner grid: the container's columns, and its rows under the title bar. Moving a container moves
// its children; what is dropped on a container's inner grid becomes its child. The same rules the API checks (PortfolioDesignService).

/** The rows a container's title bar takes, above its inner grid. */
export const TITLE_ROWS = 1;
/** The most visuals one container holds (contract rule 5). */
export const MAX_CHILDREN = 20;

/** A visual that may hold others: a container's children are of its own kind. */
export interface Nestable<T> extends Box {
  visualType: VisualType;
  children?: T[] | null | undefined;
}

/** Where a visual is: its index among the dashboard's visuals, and its index among that container's children when it is a child. */
export interface VisualPath {
  index: number;
  child: number | null;
}

/** A cell of the dashboard's grid, counted from 0 from the top and the reading start. */
export interface Cell {
  column: number;
  row: number;
}

export function isContainer(visual: { visualType: VisualType }): boolean {
  return visual.visualType === 'CONTAINER';
}

/** A container's children; none for any other visual. */
export function childrenOf<T extends Nestable<T>>(visual: T): T[] {
  return isContainer(visual) ? (visual.children ?? []) : [];
}

/** The rows of a container's inner grid. */
export function innerRows(container: Box): number {
  return tenth(container.layoutHeight - TITLE_ROWS);
}

/** How many visuals the design has, each container's children counted (contract rule 5). */
export function visualCount<T extends Nestable<T>>(visuals: readonly T[]): number {
  return visuals.reduce((count, visual) => count + 1 + childrenOf(visual).length, 0);
}

/** The design top to bottom, then from the reading start, and each container's children so within it: the order the API takes. */
export function nestedReadingOrder<T extends Nestable<T>>(visuals: readonly T[]): T[] {
  return inReadingOrder(
    visuals.map((visual) =>
      isContainer(visual) ? { ...visual, children: inReadingOrder(childrenOf(visual)) } : visual,
    ),
  );
}

/** The visual at `path`, or undefined when there is none. */
export function visualAt<T extends Nestable<T>>(
  visuals: readonly T[],
  path: VisualPath,
): T | undefined {
  const top = visuals[path.index];
  return top === undefined || path.child === null ? top : childrenOf(top)[path.child];
}

/** The path of the first visual (top-level, then each container's children) that `test` accepts; null when none does. */
export function pathWhere<T extends Nestable<T>>(
  visuals: readonly T[],
  test: (visual: T) => boolean,
): VisualPath | null {
  for (const [index, visual] of visuals.entries()) {
    if (test(visual)) {
      return { index, child: null };
    }
    const child = childrenOf(visual).findIndex(test);
    if (child >= 0) {
      return { index, child };
    }
  }
  return null;
}

/** A child's place on the dashboard's grid, as it is drawn: its container's place, under the title bar, plus its own inner place. */
export function onDashboard(container: Box, child: Box): Cell {
  return {
    column: tenth(container.layoutColumn + child.layoutColumn),
    row: tenth(container.layoutRow + TITLE_ROWS + child.layoutRow),
  };
}

/** The least height a container takes: its title bar and one inner row (contract rule 2). */
export const CONTAINER_MIN_HEIGHT = TITLE_ROWS + 1;

/**
 * The children laid out without overlapping, in reading order: each stays where it is unless it lands on one before it, then moves straight
 * down to just below it.
 */
function unstacked<T extends Box>(children: T[]): T[] {
  const placed: T[] = [];
  for (const child of inReadingOrder(children)) {
    let box = child;
    for (let covered = placed.filter((other) => overlaps(box, other)); covered.length > 0;) {
      box = {
        ...box,
        layoutRow: tenth(Math.max(...covered.map((other) => other.layoutRow + other.layoutHeight))),
      };
      covered = placed.filter((other) => overlaps(box, other));
    }
    placed.push(box);
  }
  return placed;
}

/**
 * A container sized to `width` × `height` (the user, 20 Nov 2026: "free to minimize a visual or container, not fixed"): from one column and
 * its title bar over one inner row up to the grid's end edge and the tallest a visual is, never held at what its children need. Its children
 * scale with it: each one's place and size times the new over the old columns, and inner rows, each to a tenth, at least one column by one row
 * and within the container's columns; where rounding or that least size makes two overlap, the lower moves down, and the container grows to
 * hold what then reaches below its inner rows.
 */
export function containerResized<T extends Nestable<T>>(
  container: T,
  width: number,
  height: number,
): T {
  const layoutWidth = Math.max(1, Math.min(tenth(COLUMNS - container.layoutColumn), tenth(width)));
  const layoutHeight = Math.max(CONTAINER_MIN_HEIGHT, Math.min(MAX_HEIGHT, tenth(height)));
  const across = layoutWidth / container.layoutWidth;
  const down = (layoutHeight - TITLE_ROWS) / innerRows(container);
  const children = childrenOf(container);
  if (children.length === 0 || (across === 1 && down === 1)) {
    return { ...container, layoutWidth, layoutHeight };
  }
  const scaled = unstacked(
    children.map((child) => {
      const childWidth = Math.min(layoutWidth, Math.max(1, tenth(child.layoutWidth * across)));
      return {
        ...child,
        layoutWidth: childWidth,
        layoutHeight: Math.max(1, tenth(child.layoutHeight * down)),
        layoutColumn: Math.min(tenth(layoutWidth - childWidth), tenth(child.layoutColumn * across)),
        layoutRow: tenth(child.layoutRow * down),
      };
    }),
  );
  return {
    ...container,
    layoutWidth,
    layoutHeight: Math.min(MAX_HEIGHT, Math.max(layoutHeight, TITLE_ROWS + rowsOf(scaled))),
    children: scaled,
  };
}

/**
 * The container with `child` placed at the inner cell (`column`, `row`), in place of its child at `replacing` (null: added). The child is
 * clamped to the inner grid's columns (an added one to its rows too); a child it lands on moves down below it (`settled`), and the container
 * grows to hold its children: taller, and, for an added child wider than it, wider (moving toward the reading start when the grid's end edge
 * stops it). Null when it cannot fit: the child is a container, the container is full, or it would grow taller than a visual may be.
 */
export function placedInContainer<T extends Nestable<T>>(
  container: T,
  child: T,
  column: number,
  row: number,
  replacing: number | null,
): T | null {
  const others = childrenOf(container).filter((_, index) => index !== replacing);
  if (
    !isContainer(container) ||
    isContainer(child) ||
    (replacing !== null && child.layoutWidth > container.layoutWidth) ||
    (replacing === null && others.length >= MAX_CHILDREN)
  ) {
    return null;
  }
  // A visual wider than the container widens it (the user, 19 Nov 2026: a drop on a container always goes in).
  if (child.layoutWidth > container.layoutWidth) {
    const layoutWidth = Math.min(COLUMNS, child.layoutWidth);
    return placedInContainer(
      {
        ...container,
        layoutWidth,
        layoutColumn: Math.min(container.layoutColumn, tenth(COLUMNS - layoutWidth)),
      },
      { ...child, layoutWidth },
      column,
      row,
      replacing,
    );
  }
  const height = Math.max(1, Math.min(tenth(child.layoutHeight), MAX_HEIGHT - TITLE_ROWS));
  const placed: T = {
    ...child,
    layoutWidth: tenth(child.layoutWidth),
    layoutHeight: height,
    layoutColumn: Math.min(
      tenth(container.layoutWidth - child.layoutWidth),
      Math.max(0, tenth(column)),
    ),
    // A dropped visual is kept within the inner rows; a child moved or sized in place keeps its row, and the container grows to hold it.
    layoutRow: Math.max(
      0,
      replacing === null ? Math.min(tenth(innerRows(container) - height), tenth(row)) : tenth(row),
    ),
  };
  const children = settled([...others, placed], others.length);
  const layoutHeight = Math.max(container.layoutHeight, TITLE_ROWS + rowsOf(children));
  return layoutHeight > MAX_HEIGHT ? null : { ...container, children, layoutHeight };
}

/** The container with its child at `index` changed to `next` (a place, a size, its settings), never wider than the container; unchanged when it cannot fit. */
export function withChildChanged<T extends Nestable<T>>(container: T, index: number, next: T): T {
  const child = {
    ...next,
    layoutWidth: Math.max(1, Math.min(container.layoutWidth, tenth(next.layoutWidth))),
  };
  return (
    placedInContainer(container, child, child.layoutColumn, child.layoutRow, index) ?? container
  );
}

/**
 * The design with the visual at `path` changed to `next`. A top-level visual stays where it is put and what it covers moves down (`settled`);
 * a container sized scales its children with it (`containerResized`). A child changes within its container, which grows to hold it, and what the grown
 * container covers moves down.
 */
export function changedAt<T extends Nestable<T>>(
  visuals: readonly T[],
  path: VisualPath,
  next: T,
): T[] {
  const top = visuals[path.index];
  if (top === undefined) {
    return [...visuals];
  }
  // A container resized scales its children from the size it had (`top`).
  const changed =
    path.child === null
      ? isContainer(next)
        ? containerResized(
            { ...next, layoutWidth: top.layoutWidth, layoutHeight: top.layoutHeight },
            next.layoutWidth,
            next.layoutHeight,
          )
        : next
      : withChildChanged(top, path.child, next);
  return settled(
    visuals.map((visual, index) => (index === path.index ? changed : visual)),
    path.index,
  );
}

/** The design without the visual at `path`; a container it leaves keeps its size. */
export function withoutVisual<T extends Nestable<T>>(visuals: readonly T[], path: VisualPath): T[] {
  if (path.child === null) {
    return visuals.filter((_, index) => index !== path.index);
  }
  return visuals.map((visual, index) =>
    index === path.index
      ? { ...visual, children: childrenOf(visual).filter((_, child) => child !== path.child) }
      : visual,
  );
}

/** The index of the container that holds `cell`, its title bar included; null when no container does. */
export function containerAt<T extends Nestable<T>>(
  visuals: readonly T[],
  cell: Cell,
): number | null {
  const index = visuals.findIndex(
    (visual) =>
      isContainer(visual) &&
      cell.column >= visual.layoutColumn &&
      cell.column < visual.layoutColumn + visual.layoutWidth &&
      cell.row >= visual.layoutRow &&
      cell.row < visual.layoutRow + visual.layoutHeight,
  );
  return index < 0 ? null : index;
}

/**
 * The container a visual dragged to `box` drops into: the one under the pointer, or else the one the visual reaches into while the pointer is
 * over its columns (a tall visual dragged down touches a container before the pointer does; it goes in then, instead of pushing the container
 * away); null for none.
 */
export function containerUnder<T extends Nestable<T>>(
  visuals: readonly T[],
  box: Box,
  pointer: Cell,
): number | null {
  const under = containerAt(visuals, pointer);
  if (under !== null) {
    return under;
  }
  const touched = visuals.findIndex(
    (visual) =>
      isContainer(visual) &&
      overlaps(box, visual) &&
      pointer.column >= visual.layoutColumn &&
      pointer.column < visual.layoutColumn + visual.layoutWidth,
  );
  return touched < 0 ? null : touched;
}

/**
 * The design with the visual at `from` dropped with the pointer on the cell `pointer` and its top-start corner on the cell `place` (both on the
 * dashboard's grid). The pointer's cell decides: on a container's inner grid the visual becomes (or stays) that container's child at the inner
 * cell under `place` (`placedInContainer`); anywhere else it becomes (or stays) a top-level visual at `place`, within the grid's columns, and
 * what it lands on moves down. A container is always placed as a top-level visual, its children with it. When the visual cannot fit in the
 * container under the pointer, the design is returned as it was, with `noRoom`.
 */
export function moved<T extends Nestable<T>>(
  visuals: readonly T[],
  from: VisualPath,
  pointer: Cell,
  place: Cell,
): { visuals: T[]; noRoom: boolean } {
  const visual = visualAt(visuals, from);
  if (visual === undefined) {
    return { visuals: [...visuals], noRoom: false };
  }
  const rest = withoutVisual(visuals, from);
  const target = isContainer(visual)
    ? null
    : containerUnder(
        rest,
        { ...visual, layoutColumn: place.column, layoutRow: place.row },
        pointer,
      );
  const container = target === null ? undefined : rest[target];
  if (target !== null && container !== undefined) {
    const inside = placedInContainer(
      container,
      visual,
      tenth(place.column - container.layoutColumn),
      tenth(place.row - container.layoutRow - TITLE_ROWS),
      null,
    );
    return inside === null
      ? { visuals: [...visuals], noRoom: true }
      : {
          visuals: settled(
            rest.map((other, index) => (index === target ? inside : other)),
            target,
          ),
          noRoom: false,
        };
  }
  const placed: T = {
    ...visual,
    layoutColumn: Math.min(tenth(COLUMNS - visual.layoutWidth), Math.max(0, tenth(place.column))),
    layoutRow: Math.max(0, tenth(place.row)),
  };
  return { visuals: settled([...rest, placed], rest.length), noRoom: false };
}

/**
 * The design without the container at `index`. With `keepVisuals`, each of its children moves to the dashboard at the place it had on screen
 * (`onDashboard`), and what it lands on moves down (`settled`); without, they are removed with it.
 */
export function withoutContainer<T extends Nestable<T>>(
  visuals: readonly T[],
  index: number,
  keepVisuals: boolean,
): T[] {
  const container = visuals[index];
  const rest = visuals.filter((_, at) => at !== index);
  if (container === undefined || !keepVisuals) {
    return rest;
  }
  return childrenOf(container).reduce<T[]>((all, child) => {
    const cell = onDashboard(container, child);
    return settled(
      [...all, { ...child, layoutColumn: cell.column, layoutRow: cell.row }],
      all.length,
    );
  }, inReadingOrder(rest));
}

/**
 * A container on the dashboard with only `children` (those the caller may see) at their inner places, the inner rows nothing covers taken out
 * and the container as tall as what is left (as `withoutEmptyRows` does on the dashboard).
 */
export function withoutEmptyInnerRows<T extends Nestable<T>>(container: T, children: T[]): T {
  const kept = withoutEmptyRows(children);
  return { ...container, children: kept, layoutHeight: TITLE_ROWS + rowsOf(kept) };
}

/**
 * A child's place on its container's inner grid, under the title bar, its first column and row counted from 1 (styles.css positions it on
 * columns and rows of the dashboard's own size).
 */
export function innerGridPlace(child: Box): CSSProperties {
  return gridPlace({ ...child, layoutRow: child.layoutRow + TITLE_ROWS });
}

/** A new, empty container titled "Container" at the first free place; null when the grid has no room left or the design is full. */
export function newContainer(visuals: Visual[]): Visual | null {
  const { width, height } = DEFAULT_SIZE.CONTAINER;
  const place = visualCount(visuals) >= MAX_VISUALS ? null : freePlace(visuals, width, height);
  return place === null
    ? null
    : {
        title: {
          ar: translate('ar', 'dashboards.designer.containerDefaultTitle'),
          en: translate('en', 'dashboards.designer.containerDefaultTitle'),
        },
        visualType: 'CONTAINER',
        measures: [],
        groupBy: 'NONE',
        layoutColumn: place.column,
        layoutRow: place.row,
        layoutWidth: width,
        layoutHeight: height,
        children: [],
      };
}
