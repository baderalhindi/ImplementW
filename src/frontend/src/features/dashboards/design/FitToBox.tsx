import {
  type CSSProperties,
  type HTMLAttributes,
  type ReactElement,
  type ReactNode,
  type RefObject,
  useLayoutEffect,
  useRef,
  useState,
} from 'react';

import { fitScale, fitsToBox, largestFit, MIN_FIT_SCALE } from './fitToBox.ts';

/**
 * A visual's body (`className`, the element scaled) drawn whole in the room its visual gives it: laid out at that room's size, and, where what
 * it holds is larger (it overflows), scaled down evenly from its top-start corner until it fits (`fitScale`); never scaled up. Measured again
 * whenever the room or what the body holds changes size. Purely presentational: the designer and the dashboard draw a body in it alike.
 */
export function FitToBox({
  className,
  label,
  children,
}: {
  className: string;
  label: string;
  children: ReactNode;
}): ReactElement {
  const box = useRef<HTMLDivElement>(null);
  const content = useRef<HTMLDivElement>(null);
  const [scale, setScale] = useState(1);
  useLayoutEffect(() => {
    const outer = box.current;
    const inner = content.current;
    if (outer === null || inner === null || typeof ResizeObserver === 'undefined') {
      return undefined;
    }
    // A transform leaves the layout as it was, so what is measured is the natural size, whatever the scale. Drawn at a scale, the body is laid
    // out at its room's size over the scale (so it fills the room once scaled): each scale tried is laid out and measured here, then the
    // body's own size is put back.
    const measure = () => {
      const room = { width: outer.clientWidth, height: outer.clientHeight };
      // Measured at its own height (auto): a part that fills the body (100% of its height) and clips what it holds would hide the overflow.
      const layOut = (at: number) => {
        inner.style.inlineSize = `${String(room.width / at)}px`;
        inner.style.blockSize = 'auto';
      };
      const fitsAt = (at: number) => {
        layOut(at);
        return (
          inner.scrollWidth <= room.width / at + 1 && inner.scrollHeight <= room.height / at + 1
        );
      };
      // React set the body's size for the scale it has: put back as it was, as React will not set an unchanged style again.
      const { inlineSize, blockSize, zoom } = inner.style;
      inner.style.zoom = '';
      layOut(1);
      const least = fitScale({ width: inner.scrollWidth, height: inner.scrollHeight }, room);
      const best = least === 1 ? 1 : largestFit(least, fitsAt);
      inner.style.inlineSize = inlineSize;
      inner.style.blockSize = blockSize;
      inner.style.zoom = zoom;
      setScale(best);
    };
    const sizes = new ResizeObserver(measure);
    const observeAll = () => {
      sizes.disconnect();
      sizes.observe(outer);
      sizes.observe(inner);
      for (const child of inner.children) {
        sizes.observe(child);
      }
    };
    // What the body holds changes as its figures load: its new parts are measured too.
    const parts = new MutationObserver(() => {
      observeAll();
      measure();
    });
    observeAll();
    parts.observe(inner, { childList: true });
    return () => {
      sizes.disconnect();
      parts.disconnect();
    };
  }, []);
  // Too large to fit at a readable size: drawn at the least readable scale (zoom, which scales the layout too) and scrolled in its visual.
  const scrolls = scale < MIN_FIT_SCALE;
  const boxScrolls = useScrolls(box);
  return (
    <div
      ref={box}
      className={scrolls ? 'visual__fit is-scrolling' : 'visual__fit'}
      {...scrollRegion(boxScrolls, label)}
    >
      <div
        ref={content}
        className={
          scrolls ? `${className} is-zoomed` : scale < 1 ? `${className} is-scaled` : className
        }
        style={
          (scrolls
            ? {
                zoom: MIN_FIT_SCALE,
                inlineSize: `${String(100 / MIN_FIT_SCALE)}%`,
                blockSize: 'auto',
              }
            : scale < 1
              ? {
                  '--fit-scale': String(scale),
                  inlineSize: `${String(100 / scale)}%`,
                  blockSize: `${String(100 / scale)}%`,
                }
              : {}) as CSSProperties
        }
      >
        {children}
      </div>
    </div>
  );
}

/** A visual's body: fitted to its room (`FitToBox`) where `fitsToBox` says so, else drawn at its room's size as it is. */
export function VisualBodyBox({
  visual,
  className,
  label,
  children,
}: {
  visual: { visualType: string; section?: string | null | undefined };
  className: string;
  /** The visual's title: the name of its body when the body scrolls and so takes keyboard focus. */
  label: string;
  children: ReactNode;
}): ReactElement {
  return fitsToBox(visual) ? (
    <FitToBox className={className} label={label}>
      {children}
    </FitToBox>
  ) : (
    <ScrolledBody className={className} label={label}>
      {children}
    </ScrolledBody>
  );
}

/** A body drawn at its room's size, which scrolls when what it holds is larger. */
function ScrolledBody({
  className,
  label,
  children,
}: {
  className: string;
  label: string;
  children: ReactNode;
}): ReactElement {
  const body = useRef<HTMLDivElement>(null);
  const scrolls = useScrolls(body);
  return (
    <div ref={body} className={className} {...scrollRegion(scrolls, label)}>
      {children}
    </div>
  );
}

/**
 * A body that scrolls is a named region in the tab order, so a keyboard can scroll it (WCAG 2.1.1; stage QA F-01: the designer's smaller
 * rooms made a table visual scroll with nothing in it to focus). One that does not scroll stays out of the tab order.
 */
function scrollRegion(scrolls: boolean, label: string): HTMLAttributes<HTMLDivElement> {
  return scrolls ? { role: 'region', 'aria-label': label, tabIndex: 0 } : {};
}

/** Whether the element's content is larger than its box (it scrolls), measured again as either changes size. */
function useScrolls(element: RefObject<HTMLElement | null>): boolean {
  const [scrolls, setScrolls] = useState(false);
  useLayoutEffect(() => {
    const target = element.current;
    if (target === null) {
      return undefined;
    }
    const measure = () => {
      setScrolls(
        target.scrollHeight > target.clientHeight + 1 ||
          target.scrollWidth > target.clientWidth + 1,
      );
    };
    measure();
    if (typeof ResizeObserver === 'undefined') {
      return undefined;
    }
    const sizes = new ResizeObserver(measure);
    const observeAll = () => {
      sizes.disconnect();
      sizes.observe(target);
      for (const child of target.children) {
        sizes.observe(child);
      }
    };
    const parts = new MutationObserver(() => {
      observeAll();
      measure();
    });
    observeAll();
    parts.observe(target, { childList: true });
    return () => {
      sizes.disconnect();
      parts.disconnect();
    };
  }, [element]);
  return scrolls;
}
