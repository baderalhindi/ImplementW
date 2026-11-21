import { type RefObject, useLayoutEffect, useState } from 'react';

import { type BoxSize } from '../design/useBoxSize.ts';

/** The ring's usual size, and the largest it grows to. */
export const DONUT_MIN_PX = 168;
export const DONUT_MAX_PX = 420;
/** The room the legend keeps beside the ring (its least width, 14rem, and the gap between them). */
export const LEGEND_ROOM_PX = 248;

/** The least a ring is drawn when its legend goes under it in a short visual. */
export const DONUT_STACKED_MIN_PX = 80;
/** The space between a ring and the legend under it. */
const STACK_GAP_PX = 16;

/**
 * The ring's size in a donut's room: beside its legend (the room wide enough for both), as tall as the room and as wide as the legend leaves,
 * from its usual size up to its largest; with the legend under it (a narrow room), its usual size or less, so ring and legend (`legendHeight`)
 * fit the room's height together (never under 80px). Null for its usual size, and when the room is not measured.
 */
export function donutSize(box: BoxSize | null, legendHeight = 0): number | null {
  if (box === null) {
    return null;
  }
  if (box.width - LEGEND_ROOM_PX < DONUT_MIN_PX) {
    const fits = Math.floor(box.height - legendHeight - STACK_GAP_PX);
    return fits >= DONUT_MIN_PX
      ? null
      : Math.max(DONUT_STACKED_MIN_PX, Math.min(DONUT_MIN_PX, fits));
  }
  const size = Math.floor(Math.min(box.height, box.width - LEGEND_ROOM_PX, DONUT_MAX_PX));
  return size <= DONUT_MIN_PX ? null : size;
}

/**
 * The room a donut box is given: its parent's content box (its width, and its height less what the parent's other children take and the gaps
 * between them). The parent's size comes from the layout, never from the ring, so a ring drawn large cannot hold its own room open (20 Nov
 * 2026: a donut made smaller kept its large ring and squeezed its legend).
 */
export function roomOf(box: HTMLElement): BoxSize | null {
  const parent = box.parentElement;
  if (parent === null) {
    return null;
  }
  const style = getComputedStyle(parent);
  const padding = (side: string) => Number.parseFloat(style.getPropertyValue(side)) || 0;
  const width = parent.clientWidth - padding('padding-left') - padding('padding-right');
  const others = [...parent.children].filter(
    (child) => child !== box && child instanceof HTMLElement,
  );
  const gap = Number.parseFloat(style.rowGap) || 0;
  const taken = others.reduce((sum, child) => sum + (child as HTMLElement).offsetHeight + gap, 0);
  const height = parent.clientHeight - padding('padding-top') - padding('padding-bottom') - taken;
  return { width, height };
}

/**
 * The ring's size for the donut box `element`, measured again whenever its room changes: only in a designed visual (which gives the donut its
 * room); elsewhere null, the usual size.
 */
export function useDonutSize(element: RefObject<HTMLElement | null>): number | null {
  const [size, setSize] = useState<number | null>(null);
  useLayoutEffect(() => {
    const box = element.current;
    const parent = box?.parentElement ?? null;
    if (
      box === null ||
      parent === null ||
      typeof ResizeObserver === 'undefined' ||
      box.closest('.visual') === null
    ) {
      return undefined;
    }
    const observer = new ResizeObserver(() => {
      // Inside a body drawn smaller to fit its visual (a widget whose legend or figures overflow), the ring keeps its usual size: that body is
      // laid out larger than the room, and a ring sized from it would grow it further.
      const room = box.closest('.is-scaled') === null ? roomOf(box) : null;
      const legend = box.querySelector('table');
      setSize(room === null ? null : donutSize(room, legend?.offsetHeight ?? 0));
    });
    observer.observe(parent);
    const legend = box.querySelector('table');
    if (legend !== null) {
      observer.observe(legend);
    }
    return () => {
      observer.disconnect();
    };
  }, [element]);
  return size;
}
