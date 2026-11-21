import { type ReactElement, type RefObject, useEffect } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type BannerImage } from './bannerApi.ts';
import { preloadBannerImage, useBannerImage } from './useBannerImage.ts';
import { ROTATION_MS, useRotation } from './useRotation.ts';

/** How long before its turn the next image starts loading: it is read only when it is about to show. */
const PRELOAD_AHEAD_MS = 30 * 1000;

/** The map pin before the caption: the caption names the place the picture shows. */
function LocationIcon(): ReactElement {
  return (
    <svg
      className="home__hero-caption-icon"
      viewBox="0 0 24 24"
      width="16"
      height="16"
      aria-hidden="true"
      focusable="false"
    >
      <path
        d="M12 21s-7-6.2-7-11.5a7 7 0 0 1 14 0C19 14.8 12 21 12 21z"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinejoin="round"
      />
      <circle cx="12" cy="9.5" r="2.5" fill="none" stroke="currentColor" strokeWidth="1.8" />
    </svg>
  );
}

function prefersReducedMotion(): boolean {
  return (
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches
  );
}

/**
 * The banner's picture (review round 8, point 07): the chosen images, one at a time, cropped around each one's focal point under an overlay that
 * keeps the greeting readable; and the place it shows, small, in a corner.
 * The green banner underneath shows until an image is read, and whenever none can be.
 */
export function BannerBackdrop({
  images,
  host,
}: {
  images: BannerImage[];
  /** The banner: while the pointer is on it, the turns wait. */
  host: RefObject<HTMLElement | null>;
}): ReactElement | null {
  const { language } = useI18n();
  const still = prefersReducedMotion();
  const { index, next } = useRotation(images.length, still, host);
  const current = images[index] ?? null;
  const address = useBannerImage(current?.id ?? null);

  // The next image is read shortly before its turn, not with the first.
  const upcoming = images.length > 1 && !still ? images[next] : undefined;
  useEffect(() => {
    if (upcoming === undefined) {
      return undefined;
    }
    const timer = window.setTimeout(
      () => {
        void preloadBannerImage(upcoming.id).catch(() => undefined);
      },
      Math.max(0, ROTATION_MS - (Date.now() % ROTATION_MS) - PRELOAD_AHEAD_MS),
    );
    return () => {
      window.clearTimeout(timer);
    };
  }, [upcoming]);

  if (current === null || address === null) {
    return null;
  }
  return (
    <>
      <div className="home__hero-media" aria-hidden="true">
        {/* A background, not an image element: it is cropped around the focal point, and its text alternative is the caption's. */}
        <span
          key={current.id}
          className={still ? 'home__hero-image' : 'home__hero-image home__hero-image--fade'}
          style={{
            backgroundImage: `url("${address}")`,
            backgroundPosition: `${String(current.focalX)}% ${String(current.focalY)}%`,
          }}
        />
        <span className="home__hero-overlay" />
      </div>
      <p className="home__hero-caption">
        <span className="visually-hidden">{current.alt[language]}</span>
        <span className="home__hero-place" aria-hidden="true">
          <LocationIcon />
          {current.caption[language]}
        </span>
        {current.credit !== null && <span className="home__hero-credit">{current.credit}</span>}
      </p>
    </>
  );
}
