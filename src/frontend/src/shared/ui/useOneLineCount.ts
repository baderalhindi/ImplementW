import { type RefObject, useLayoutEffect, useState } from 'react';

/**
 * How many of a row's items fit on one line. `measure` holds every item at its natural width, then the "More" control, then `trailing`
 * controls that are always shown (a pencil); `container` is the line they must fit in. Counts again whenever the container's width changes.
 * `signature` changes when the items or their labels do.
 */
export function useOneLineCount(
  container: RefObject<HTMLElement | null>,
  measure: RefObject<HTMLElement | null>,
  total: number,
  signature: string,
  trailing = 0,
): number {
  // Null until measured: everything shows until the row has been measured once.
  const [count, setCount] = useState<number | null>(null);
  useLayoutEffect(() => {
    const line = container.current;
    const row = measure.current;
    // Without ResizeObserver (an old browser, the test DOM) every item stays in the row.
    if (line === null || row === null || typeof ResizeObserver === 'undefined') {
      return undefined;
    }
    const fit = (): void => {
      const children = [...row.children];
      const widthOf = (element: Element | undefined): number =>
        element?.getBoundingClientRect().width ?? 0;
      const gap = parseFloat(getComputedStyle(row).columnGap) || 0;
      const items = children.slice(0, children.length - 1 - trailing).map(widthOf);
      const more = widthOf(children[children.length - 1 - trailing]);
      const fixed = children
        .slice(children.length - trailing)
        .reduce((sum, element) => sum + widthOf(element) + gap, 0);
      const available = line.clientWidth - fixed;
      const all =
        items.reduce((sum, width) => sum + width, 0) + gap * Math.max(0, items.length - 1);
      if (all <= available) {
        setCount(items.length);
        return;
      }
      const room = available - more - gap;
      let used = 0;
      let fitting = 0;
      for (const width of items) {
        const next = used + width + (fitting > 0 ? gap : 0);
        if (next > room) {
          break;
        }
        used = next;
        fitting += 1;
      }
      setCount(fitting);
    };
    const observer = new ResizeObserver(fit);
    observer.observe(line);
    return () => {
      observer.disconnect();
    };
  }, [container, measure, signature, trailing]);
  return count === null ? total : Math.min(count, total);
}
