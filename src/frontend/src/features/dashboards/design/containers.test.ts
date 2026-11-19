import { describe, expect, test } from 'vitest';

import {
  changedAt,
  containerMinSize,
  containerResized,
  innerGridPlace,
  moved,
  nestedReadingOrder,
  newContainer,
  onDashboard,
  visualCount,
  withoutContainer,
  withoutEmptyInnerRows,
} from './containers.ts';
import { type Visual } from './designApi.ts';

// Containers (19 Nov 2026, docs/architecture/dashboard-designer-containers.md): the pure rules behind dragging a visual in and out of a
// container, moving and sizing a container, removing one and drawing one on the dashboard. A pointer drag in jsdom has no layout, so the
// designer's drags are held to the contract here, on the helpers the designer calls.

type Place = [number, number, number, number];

function card(title: string, [layoutColumn, layoutRow, layoutWidth, layoutHeight]: Place): Visual {
  return {
    title: { ar: title, en: title },
    visualType: 'CARD',
    measures: ['PROJECTS'],
    groupBy: 'NONE',
    layoutColumn,
    layoutRow,
    layoutWidth,
    layoutHeight,
  };
}

function container(
  title: string,
  [layoutColumn, layoutRow, layoutWidth, layoutHeight]: Place,
  children: Visual[] = [],
): Visual {
  return {
    title: { ar: title, en: title },
    visualType: 'CONTAINER',
    measures: [],
    groupBy: 'NONE',
    layoutColumn,
    layoutRow,
    layoutWidth,
    layoutHeight,
    children,
  };
}

const placeOf = (visual: Visual | undefined): Place | undefined =>
  visual === undefined
    ? undefined
    : [visual.layoutColumn, visual.layoutRow, visual.layoutWidth, visual.layoutHeight];

const named = (visuals: readonly Visual[] | null | undefined, title: string) =>
  (visuals ?? []).find((visual) => visual.title.en === title);

const top = { index: 0, child: null } as const;

describe('dragging a visual into a container', () => {
  // The container's inner grid starts one row under its top (its title bar): dashboard rows 1 to 5 here, columns 4 to 11.
  const box = container('Box', [4, 0, 8, 6]);
  const kpi = card('KPI', [0, 0, 4, 2]);

  test('a visual dropped on an inner cell becomes the container child there, and leaves the dashboard', () => {
    const { visuals, noRoom } = moved(
      [kpi, box],
      top,
      { column: 6, row: 2 },
      { column: 6, row: 2 },
    );
    expect(noRoom).toBe(false);
    expect(visuals.map((visual) => visual.title.en)).toEqual(['Box']);
    const into = named(visuals, 'Box');
    expect(placeOf(into)).toEqual([4, 0, 8, 6]);
    // Dashboard column 6 is the container's third column; dashboard row 2 its second inner row.
    expect(into?.children?.map(placeOf)).toEqual([[2, 1, 4, 2]]);
  });

  test('a visual dropped too far toward the end or the bottom is clamped inside the inner grid', () => {
    const { visuals } = moved([kpi, box], top, { column: 11, row: 5 }, { column: 10, row: 5 });
    // Eight columns, a card of four: inner column 4 at most; five inner rows, a card of two: inner row 3 at most.
    expect(named(visuals, 'Box')?.children?.map(placeOf)).toEqual([[4, 3, 4, 2]]);
  });

  test('a child it lands on inside moves down below it, within the container', () => {
    const held = container('Box', [4, 0, 8, 6], [card('Held', [0, 0, 4, 2])]);
    const { visuals } = moved([kpi, held], top, { column: 4, row: 1 }, { column: 4, row: 1 });
    const children = named(visuals, 'Box')?.children;
    expect(placeOf(named(children, 'KPI'))).toEqual([0, 0, 4, 2]);
    expect(placeOf(named(children, 'Held'))).toEqual([0, 2, 4, 2]);
    // Both still fit in the five inner rows: the container keeps its size.
    expect(placeOf(named(visuals, 'Box'))).toEqual([4, 0, 8, 6]);
  });

  test('a visual wider than the container widens it and goes in (19 Nov 2026)', () => {
    const wide = card('Wide', [0, 8, 10, 2]);
    const result = moved(
      [box, wide],
      { index: 1, child: null },
      { column: 5, row: 2 },
      { column: 4, row: 2 },
    );
    expect(result.noRoom).toBe(false);
    const widened = named(result.visuals, 'Box');
    expect(widened?.layoutWidth).toBe(10);
    // Widened toward the reading start where the grid's end edge stops it.
    expect((widened?.layoutColumn ?? 0) + (widened?.layoutWidth ?? 0)).toBeLessThanOrEqual(12);
    expect(widened?.children?.map((child) => child.title.en)).toEqual(['Wide']);
  });

  test('a tall visual dragged down onto a container goes in when it reaches it, instead of pushing it away', () => {
    const tall = card('Tall', [0, 0, 6, 5]);
    const target = container('Target', [0, 6, 6, 6]);
    // The pointer (on the visual's handle) is still above the container, but the visual reaches into it.
    const { visuals } = moved(
      [tall, target],
      { index: 0, child: null },
      { column: 1, row: 3 },
      { column: 0, row: 3 },
    );
    expect(named(visuals, 'Target')?.layoutRow).toBe(6);
    expect(named(visuals, 'Target')?.children?.map((child) => child.title.en)).toEqual(['Tall']);
  });

  test('a container dropped on another container is placed as a top-level visual, not nested', () => {
    const other = container('Other', [0, 8, 4, 4]);
    const { visuals } = moved(
      [box, other],
      { index: 1, child: null },
      { column: 6, row: 2 },
      { column: 6, row: 2 },
    );
    expect(
      visuals.every((visual) => (visual.children ?? []).every((c) => c.visualType !== 'CONTAINER')),
    ).toBe(true);
    expect(placeOf(named(visuals, 'Other'))).toEqual([6, 2, 4, 4]);
    // What it landed on moved down below it.
    expect(named(visuals, 'Box')?.layoutRow).toBe(6);
  });
});

describe('dragging a child out of its container', () => {
  test('dropped outside, the child becomes a top-level visual at that cell and leaves the container', () => {
    const box = container('Box', [4, 0, 8, 6], [card('Child', [0, 0, 4, 2])]);
    const { visuals, noRoom } = moved(
      [box],
      { index: 0, child: 0 },
      { column: 1, row: 8 },
      { column: 0, row: 8 },
    );
    expect(noRoom).toBe(false);
    expect(placeOf(named(visuals, 'Child'))).toEqual([0, 8, 4, 2]);
    expect(named(visuals, 'Box')?.children).toEqual([]);
    // The container keeps its size when a child leaves.
    expect(placeOf(named(visuals, 'Box'))).toEqual([4, 0, 8, 6]);
  });

  test('dropped on its own container title bar, the child stays in its container (the title bar is part of it)', () => {
    const box = container('Box', [4, 2, 8, 6], [card('Child', [0, 1, 4, 2])]);
    const { visuals } = moved(
      [box],
      { index: 0, child: 0 },
      { column: 5, row: 2 },
      { column: 4, row: 2 },
    );
    expect(named(visuals, 'Box')?.children?.map((child) => child.title.en)).toEqual(['Child']);
    expect(named(visuals, 'Box')?.layoutRow).toBe(2);
  });

  test('dropped on another inner cell of the same container, it stays a child, moved there', () => {
    const box = container('Box', [4, 0, 8, 6], [card('Child', [0, 0, 4, 2])]);
    const { visuals } = moved(
      [box],
      { index: 0, child: 0 },
      { column: 9, row: 3 },
      { column: 8, row: 3 },
    );
    expect(named(visuals, 'Box')?.children?.map(placeOf)).toEqual([[4, 2, 4, 2]]);
  });
});

describe('moving and sizing a container', () => {
  const children = [card('A', [0, 0, 4, 2]), card('B', [4, 1, 3, 3])];

  test('moving a container moves it with its children: their inner places are unchanged', () => {
    const box = container('Box', [4, 0, 8, 6], children);
    const { visuals } = moved([box], top, { column: 1, row: 10 }, { column: 0, row: 10 });
    const after = named(visuals, 'Box');
    expect(placeOf(after)).toEqual([0, 10, 8, 6]);
    expect(after?.children).toEqual(children);
    // So each child is drawn under the container's new place.
    const b = children[1];
    if (after === undefined || b === undefined) {
      throw new Error('the container and its child B');
    }
    expect(onDashboard(after, b)).toEqual({ column: 4, row: 12 });
  });

  test('a container is never smaller than what its children need, and an empty one keeps one inner row', () => {
    const box = container('Box', [4, 0, 8, 6], children);
    // B reaches column 7 and inner row 4; the title bar adds a row.
    expect(containerMinSize(box)).toEqual({ width: 7, height: 5 });
    expect(containerMinSize(container('Empty', [0, 0, 6, 6]))).toEqual({ width: 1, height: 2 });
    expect(placeOf(containerResized(box, 2, 2))).toEqual([4, 0, 7, 5]);
    // Larger is free, up to the grid's end edge.
    expect(placeOf(containerResized(box, 12, 9))).toEqual([4, 0, 8, 9]);
  });

  test('sizing a container through the settings (changedAt) stops at what its children need', () => {
    const box = container('Box', [0, 0, 8, 6], children);
    const sized = changedAt([box], top, { ...box, layoutWidth: 3, layoutHeight: 2 });
    expect(placeOf(named(sized, 'Box'))).toEqual([0, 0, 7, 5]);
    expect(named(sized, 'Box')?.children).toEqual(children);
  });
});

describe('the keyboard on a child', () => {
  const box = container('Box', [0, 0, 8, 6], [card('A', [0, 0, 4, 2]), card('B', [4, 0, 4, 2])]);

  test('a child moves one cell within its container, and never past its end edge', () => {
    const a = box.children?.[0];
    const b = box.children?.[1];
    if (a === undefined || b === undefined) {
      throw new Error('two children');
    }
    const down = changedAt([box], { index: 0, child: 0 }, { ...a, layoutRow: 1 });
    expect(placeOf(named(named(down, 'Box')?.children, 'A'))).toEqual([0, 1, 4, 2]);
    const past = changedAt([box], { index: 0, child: 1 }, { ...b, layoutColumn: 5 });
    expect(placeOf(named(named(past, 'Box')?.children, 'B'))).toEqual([4, 0, 4, 2]);
  });

  test('a child moved below the inner rows grows its container, and what the container covers moves down', () => {
    const below = card('Below', [0, 6, 12, 2]);
    const a = box.children?.[0];
    if (a === undefined) {
      throw new Error('a child');
    }
    const result = changedAt([box, below], { index: 0, child: 0 }, { ...a, layoutRow: 4 });
    // Inner rows 4 and 5 under the title bar: seven rows.
    expect(named(result, 'Box')?.layoutHeight).toBe(7);
    expect(named(result, 'Below')?.layoutRow).toBe(7);
  });
});

describe('removing a container', () => {
  const box = container('Box', [2, 3, 8, 6], [card('A', [0, 0, 4, 2]), card('B', [4, 1, 4, 3])]);
  const aside = card('Aside', [0, 0, 2, 2]);

  test('with its visuals: nothing of it is left', () => {
    expect(withoutContainer([aside, box], 1, false)).toEqual([aside]);
  });

  test('keeping its visuals: each child moves to the dashboard at its place on screen, and no container is left', () => {
    const kept = withoutContainer([aside, box], 1, true);
    expect(kept.some((visual) => visual.visualType === 'CONTAINER')).toBe(false);
    // Container column + child column; container row + 1 (the title bar) + child row.
    expect(placeOf(named(kept, 'A'))).toEqual([2, 4, 4, 2]);
    expect(placeOf(named(kept, 'B'))).toEqual([6, 5, 4, 3]);
    expect(placeOf(named(kept, 'Aside'))).toEqual([0, 0, 2, 2]);
    expect(kept.map((visual) => visual.title.en)).toEqual(['Aside', 'A', 'B']);
  });
});

describe('the reading order and the count', () => {
  test('top to bottom then from the reading start, and each container children so within it', () => {
    const box = container(
      'Box',
      [0, 2, 12, 6],
      [card('Lower', [0, 2, 4, 2]), card('Right', [6, 0, 4, 2]), card('Left', [0, 0, 4, 2])],
    );
    const ordered = nestedReadingOrder([
      box,
      card('Second', [6, 0, 4, 2]),
      card('First', [0, 0, 4, 2]),
    ]);
    expect(ordered.map((visual) => visual.title.en)).toEqual(['First', 'Second', 'Box']);
    expect(named(ordered, 'Box')?.children?.map((visual) => visual.title.en)).toEqual([
      'Left',
      'Right',
      'Lower',
    ]);
  });

  test('children count toward the design limit', () => {
    expect(
      visualCount([
        card('A', [0, 0, 4, 2]),
        container('Box', [0, 2, 8, 6], [card('B', [0, 0, 4, 2])]),
      ]),
    ).toBe(3);
  });
});

describe('a new container and how one is drawn', () => {
  test('a new container is 6 by 6 at the first free place, titled in both languages, with no children', () => {
    const fresh = newContainer([card('A', [0, 0, 4, 2])]);
    expect(fresh).toMatchObject({
      visualType: 'CONTAINER',
      measures: [],
      groupBy: 'NONE',
      layoutColumn: 4,
      layoutRow: 0,
      layoutWidth: 6,
      layoutHeight: 6,
      children: [],
      title: { en: 'Container', ar: 'حاوية' },
    });
  });

  test('a child is drawn under the title bar of its container inner grid', () => {
    expect(
      innerGridPlace({ layoutColumn: 4, layoutRow: 1, layoutWidth: 4, layoutHeight: 3 }),
    ).toEqual({
      '--visual-column': '5',
      '--visual-row': '3',
      '--visual-width': '4',
      '--visual-height': '3',
    });
  });

  test('on the dashboard, the inner rows a left-out child leaves empty are taken out, and the container is as tall as what is left', () => {
    const box = container('Box', [0, 0, 8, 9]);
    const shown = withoutEmptyInnerRows(box, [card('A', [0, 0, 4, 2]), card('C', [0, 5, 4, 2])]);
    expect(shown.children?.map(placeOf)).toEqual([
      [0, 0, 4, 2],
      [0, 2, 4, 2],
    ]);
    expect(shown.layoutHeight).toBe(5);
  });
});
