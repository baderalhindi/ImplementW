/** How close to the window's top or bottom edge, in pixels, the pointer starts the page scrolling during a drag. */
const EDGE = 80;
/** The fastest scroll, in pixels a frame, with the pointer at (or past) the edge. */
const FASTEST = 28;

/**
 * Scrolls the page while a drag holds the pointer near the window's top or bottom edge (19 Nov 2026: a visual must reach a container far
 * down the dashboard), faster the closer it is; after each step `onScroll` is called with the pointer's last place, so the drag follows the
 * page. `update` gives the pointer's place on each move; `stop` ends it when the drag ends.
 */
export function edgeScroller(onScroll: (clientX: number, clientY: number) => void): {
  update: (clientX: number, clientY: number) => void;
  stop: () => void;
} {
  let x = 0;
  let y = 0;
  let frame: number | null = null;
  const speed = () => {
    const height = window.innerHeight;
    if (y < EDGE) {
      return -Math.ceil(FASTEST * Math.min(1, (EDGE - y) / EDGE));
    }
    if (y > height - EDGE) {
      return Math.ceil(FASTEST * Math.min(1, (y - (height - EDGE)) / EDGE));
    }
    return 0;
  };
  const step = () => {
    frame = null;
    const by = speed();
    if (by === 0) {
      return;
    }
    const before = window.scrollY;
    window.scrollBy(0, by);
    if (window.scrollY !== before) {
      onScroll(x, y);
    }
    frame = window.requestAnimationFrame(step);
  };
  return {
    update: (clientX, clientY) => {
      x = clientX;
      y = clientY;
      if (frame === null && speed() !== 0) {
        frame = window.requestAnimationFrame(step);
      }
    },
    stop: () => {
      if (frame !== null) {
        window.cancelAnimationFrame(frame);
        frame = null;
      }
    },
  };
}
