import {
  type CSSProperties,
  type ReactElement,
  type ReactNode,
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
  children,
}: {
  className: string;
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
  return (
    <div ref={box} className={scrolls ? 'visual__fit is-scrolling' : 'visual__fit'}>
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
  children,
}: {
  visual: { visualType: string; section?: string | null | undefined };
  className: string;
  children: ReactNode;
}): ReactElement {
  return fitsToBox(visual) ? (
    <FitToBox className={className}>{children}</FitToBox>
  ) : (
    <div className={className}>{children}</div>
  );
}
