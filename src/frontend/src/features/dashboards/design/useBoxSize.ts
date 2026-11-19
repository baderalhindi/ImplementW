import { type RefObject, useLayoutEffect, useState } from 'react';

export interface BoxSize {
  width: number;
  height: number;
}

/**
 * The element's content box in pixels, measured again whenever it changes size; null until measured, and without ResizeObserver (an old
 * browser, the test DOM) or while the element has no size yet (it is not laid out). The element's size must come from its layout, not
 * from what it holds, or the measurement would feed itself.
 */
export function useBoxSize(element: RefObject<HTMLElement | null>): BoxSize | null {
  const [size, setSize] = useState<BoxSize | null>(null);
  useLayoutEffect(() => {
    const box = element.current;
    if (box === null || typeof ResizeObserver === 'undefined') {
      return undefined;
    }
    const observer = new ResizeObserver(([entry]) => {
      if (entry === undefined) {
        return;
      }
      const width = Math.round(entry.contentRect.width);
      const height = Math.round(entry.contentRect.height);
      setSize((previous) =>
        width === 0 || height === 0
          ? null
          : previous?.width === width && previous.height === height
            ? previous
            : { width, height },
      );
    });
    observer.observe(box);
    return () => {
      observer.disconnect();
    };
  }, [element]);
  return size;
}
