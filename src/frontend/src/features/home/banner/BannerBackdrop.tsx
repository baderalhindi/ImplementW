import { type ReactElement, type RefObject, useEffect } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type BannerImage } from './bannerApi.ts';
import { preloadBannerImage, useBannerImage } from './useBannerImage.ts';
import { ROTATION_MS, useRotation } from './useRotation.ts';

/** How long before its turn the next image starts loading: it is read only when it is about to show. */
const PRELOAD_AHEAD_MS = 30 * 1000;

function prefersReducedMotion(): boolean {
  return (
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches
  );
}

/**
 * The banner's picture (review round 8, point 07): the chosen images, one at a time, cropped around each one's focal point under an overlay that
 * keeps the greeting readable; the place it shows, small, in a corner; and with more than one image, dots that say which shows and open another.
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
  const { t, language } = useI18n();
  const still = prefersReducedMotion();
  const { index, choose, next } = useRotation(images.length, still, host);
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
        <span aria-hidden="true">{current.caption[language]}</span>
        {current.credit !== null && <span className="home__hero-credit">{current.credit}</span>}
      </p>
      {images.length > 1 && (
        <div className="home__hero-dots" role="group" aria-label={t('home.banner.position')}>
          {images.map((image, position) => (
            <button
              key={image.id}
              type="button"
              className="home__hero-dot"
              aria-label={image.caption[language]}
              aria-pressed={position === index}
              onClick={() => {
                choose(position);
              }}
            />
          ))}
        </div>
      )}
    </>
  );
}
