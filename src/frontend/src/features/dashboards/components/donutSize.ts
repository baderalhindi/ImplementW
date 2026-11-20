import { type RefObject, useLayoutEffect, useState } from 'react';

import { type BoxSize } from '../design/useBoxSize.ts';

/** The ring's usual size, and the largest it grows to. */
export const DONUT_MIN_PX = 168;
export const DONUT_MAX_PX = 420;
/** The room the legend keeps beside the ring (its least width, 14rem, and the gap between them). */
export const LEGEND_ROOM_PX = 248;

/**
 * The ring's size in a donut's box: as tall as the box, as wide as the box leaves beside the legend, from its usual size up to its largest;
 * null (its usual size) when the box is not measured or is too narrow for the ring and the legend side by side, where the legend goes under
 * the ring.
 */
export function donutSize(box: BoxSize | null): number | null {
  if (box === null || box.width - LEGEND_ROOM_PX < DONUT_MIN_PX) {
    return null;
  }
  const size = Math.floor(Math.min(box.height, box.width - LEGEND_ROOM_PX, DONUT_MAX_PX));
  return size <= DONUT_MIN_PX ? null : size;
}

/**
 * The ring's size for the donut box `element`, measured again whenever the box changes size: only in a designed visual (which gives the
 * donut its room); elsewhere null, the usual size.
 */
export function useDonutSize(element: RefObject<HTMLElement | null>): number | null {
  const [size, setSize] = useState<number | null>(null);
  useLayoutEffect(() => {
    const box = element.current;
    if (box === null || typeof ResizeObserver === 'undefined' || box.closest('.visual') === null) {
      return undefined;
    }
    const observer = new ResizeObserver(([entry]) => {
      if (entry !== undefined) {
        setSize(donutSize({ width: entry.contentRect.width, height: entry.contentRect.height }));
      }
    });
    observer.observe(box);
    return () => {
      observer.disconnect();
    };
  }, [element]);
  return size;
}
