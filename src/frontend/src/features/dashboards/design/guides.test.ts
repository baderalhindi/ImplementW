import { describe, expect, test } from 'vitest';

import { guidesFor, snappedPlace, snappedSize } from './guides.ts';
import { type Box } from './visuals.ts';

// Alignment guides (20 Nov 2026, "thin red lines like in Power BI").

const box = (layoutColumn: number, layoutRow: number, layoutWidth = 4, layoutHeight = 2): Box => ({
  layoutColumn,
  layoutRow,
  layoutWidth,
  layoutHeight,
});

describe('alignment guides', () => {
  test('a visual lined up under another of its width draws a guide on its start, centre and end', () => {
    expect(guidesFor(box(0, 5), [box(0, 0)])).toEqual([
      { axis: 'x', at: 0, line: 'start' },
      { axis: 'x', at: 4, line: 'end' },
      { axis: 'x', at: 2, line: 'center' },
    ]);
  });

  test('its top lining up with another visual draws a guide down, its end edge on the other visual’s start too', () => {
    const guides = guidesFor(box(8, 0, 4, 3), [box(0, 0, 4, 3)]);
    expect(guides).toContainEqual({ axis: 'y', at: 0, line: 'start' });
    expect(guides).toContainEqual({ axis: 'y', at: 3, line: 'end' });
  });

  test('centres line up', () => {
    expect(guidesFor(box(2, 6, 4, 2), [box(0, 0, 8, 2)])).toContainEqual({
      axis: 'x',
      at: 4,
      line: 'center',
    });
  });

  test('nothing lined up draws nothing', () => {
    expect(guidesFor(box(1.3, 5.2, 3.3, 1.9), [box(7, 0, 2, 2)])).toEqual([]);
  });
});

describe('snapping', () => {
  test('an edge within 0.3 of a cell of another visual’s is pulled onto it', () => {
    expect(snappedPlace(box(0.2, 5), [box(0, 0)])).toEqual({ column: 0, row: 5 });
    expect(snappedPlace(box(3.8, 5.2), [box(0, 0), box(4, 5)])).toEqual({ column: 4, row: 5 });
  });

  test('farther than that it stays where it was put', () => {
    expect(snappedPlace(box(0.6, 5), [box(0, 0)])).toEqual({ column: 0.6, row: 5 });
  });

  test('a sized visual’s end edge is pulled onto a near line, its start staying put', () => {
    // Its end (3.8) is pulled onto the other visual's end (4); its bottom (6.9) is near no line and stays.
    expect(snappedSize(box(0, 5, 3.8, 1.9), [box(0, 0, 4, 2)])).toEqual({ width: 4, height: 1.9 });
    expect(snappedSize(box(5, 0, 3, 1.8), [box(0, 0, 4, 2)])).toEqual({ width: 3, height: 2 });
  });
});
