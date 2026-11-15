import { type RefObject, useEffect, useRef, useState } from 'react';

/** How long each image shows when images take turns: ten minutes (the user's request of 16 Nov 2026, in place of the PDF's 8 seconds). */
export const ROTATION_MS = 10 * 60 * 1000;

/** The image the clock points to: the turns follow the time of day, so a person returning finds the image the time says, not the first. */
function slotIndex(count: number, now: number): number {
  return count === 0 ? 0 : Math.floor(now / ROTATION_MS) % count;
}

/**
 * Which of `count` images shows. It moves on at each ten-minute mark; while the pointer is on `host` (the banner) it waits, and moves on as soon
 * as the pointer leaves; when `still` (the system asks for reduced motion) one image shows for the visit.
 */
export function useRotation(
  count: number,
  still: boolean,
  host: RefObject<HTMLElement | null>,
): { index: number; next: number } {
  const [index, setIndex] = useState(() => slotIndex(count, Date.now()));
  const paused = useRef(false);
  const due = useRef(false);

  useEffect(() => {
    if (still || count < 2) {
      return undefined;
    }
    const element = host.current;
    const turn = () => {
      due.current = false;
      setIndex(slotIndex(count, Date.now()));
    };
    const enter = () => {
      paused.current = true;
    };
    const leave = () => {
      paused.current = false;
      if (due.current) {
        turn();
      }
    };
    element?.addEventListener('pointerenter', enter);
    element?.addEventListener('pointerleave', leave);
    const timer = window.setTimeout(
      () => {
        if (paused.current) {
          due.current = true;
        } else {
          turn();
        }
      },
      ROTATION_MS - (Date.now() % ROTATION_MS),
    );
    return () => {
      window.clearTimeout(timer);
      element?.removeEventListener('pointerenter', enter);
      element?.removeEventListener('pointerleave', leave);
    };
  }, [still, count, index, host]);

  const shown = count === 0 ? 0 : Math.min(index, count - 1);
  return { index: shown, next: count === 0 ? 0 : (shown + 1) % count };
}
