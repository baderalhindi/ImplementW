import { type ReactElement, useState } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { usePopover } from '@/shared/ui/usePopover.ts';

import { type BannerImage, type BannerPreference, type BannerStyle } from './bannerApi.ts';
import { useBannerImage } from './useBannerImage.ts';

const STYLES: BannerStyle[] = ['DEFAULT', 'IMAGE', 'ROTATE'];

function PinIcon(): ReactElement {
  return (
    <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" focusable="false">
      <path
        d="M15 4l5 5-3 1-3 3 1 4-2 2-4-4-5 5M9 9l6 6"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function Thumbnail({
  image,
  chosen,
  onToggle,
}: {
  image: BannerImage;
  chosen: boolean;
  onToggle: () => void;
}): ReactElement {
  const { language } = useI18n();
  const address = useBannerImage(image.id);
  return (
    <li>
      <button
        type="button"
        className="banner-picker__image"
        aria-pressed={chosen}
        aria-label={image.caption[language]}
        title={image.caption[language]}
        onClick={onToggle}
      >
        {address === null ? (
          <span className="banner-picker__placeholder" />
        ) : (
          <span
            className="banner-picker__thumb"
            style={{
              backgroundImage: `url("${address}")`,
              backgroundPosition: `${String(image.focalX)}% ${String(image.focalY)}%`,
            }}
          />
        )}
        <span className="banner-picker__check" aria-hidden="true">
          ✓
        </span>
      </button>
    </li>
  );
}

/**
 * "مظهر الترحيب" (review round 8, point 07): a pin in the banner's corner opens the choice of the green banner, one image, or images that take turns
 * every ten minutes, and the images to use. A choice shows straight away and is saved for this person on the server; no settings page, no reload.
 */
export function BannerCustomizer({
  images,
  preference,
  onChoose,
  failed,
}: {
  images: BannerImage[];
  preference: BannerPreference;
  onChoose: (next: BannerPreference) => void;
  failed: boolean;
}): ReactElement {
  const { t } = useI18n();
  const { open, setOpen, containerRef, buttonRef, menuId } = usePopover();
  const [emptied, setEmptied] = useState(false);
  const chosen = new Set(preference.imageIds);

  const setStyle = (style: BannerStyle) => {
    setEmptied(false);
    if (style === 'DEFAULT') {
      onChoose({ style, imageIds: preference.imageIds });
    } else if (style === 'IMAGE') {
      const first = preference.imageIds[0] ?? images[0]?.id;
      onChoose({ style, imageIds: first === undefined ? [] : [first] });
    } else {
      // Turns need more than one image: with one or none chosen, the slideshow starts with them all.
      onChoose({
        style,
        imageIds:
          preference.imageIds.length > 1 ? preference.imageIds : images.map((image) => image.id),
      });
    }
  };
  const setImages = (ids: string[]) => {
    // The image styles need at least one image: emptying the choice keeps the last one and says so.
    if (ids.length === 0) {
      setEmptied(true);
      return;
    }
    setEmptied(false);
    onChoose({ style: preference.style, imageIds: ids });
  };
  const toggle = (id: string) => {
    if (preference.style === 'IMAGE') {
      setImages([id]);
    } else {
      setImages(
        chosen.has(id)
          ? preference.imageIds.filter((current) => current !== id)
          : images
              .map((image) => image.id)
              .filter((current) => current === id || chosen.has(current)),
      );
    }
  };

  return (
    <div className="popover banner-picker" ref={containerRef}>
      <button
        ref={buttonRef}
        type="button"
        className="banner-picker__toggle"
        aria-expanded={open}
        aria-controls={menuId}
        aria-label={t('home.banner.customize')}
        title={t('home.banner.customize')}
        onClick={() => {
          setOpen((current) => !current);
        }}
      >
        <PinIcon />
      </button>
      <div
        className="banner-picker__panel"
        id={menuId}
        hidden={!open}
        role="group"
        aria-label={t('home.banner.customize')}
      >
        {/* Its thumbnails are read only once it is open. */}
        {open && (
          <>
            <p className="banner-picker__title">{t('home.banner.customize')}</p>
            <div
              className="banner-picker__styles"
              role="radiogroup"
              aria-label={t('home.banner.style')}
            >
              {STYLES.map((style) => (
                <button
                  key={style}
                  type="button"
                  role="radio"
                  aria-checked={preference.style === style}
                  disabled={style !== 'DEFAULT' && images.length === 0}
                  onClick={() => {
                    setStyle(style);
                  }}
                >
                  {t(`home.banner.styles.${style}`)}
                </button>
              ))}
            </div>
            {preference.style !== 'DEFAULT' && (
              <>
                <p className="form__note">
                  {t(preference.style === 'IMAGE' ? 'home.banner.pickOne' : 'home.banner.pickMany')}
                </p>
                {preference.style === 'ROTATE' && (
                  <p className="banner-picker__all">
                    <button
                      type="button"
                      className="button button--quiet"
                      onClick={() => {
                        setImages(images.map((image) => image.id));
                      }}
                    >
                      {t('home.banner.selectAll')}
                    </button>
                    <button
                      type="button"
                      className="button button--quiet"
                      onClick={() => {
                        setImages([]);
                      }}
                    >
                      {t('home.banner.clearAll')}
                    </button>
                  </p>
                )}
                <ul className="banner-picker__images">
                  {images.map((image) => (
                    <Thumbnail
                      key={image.id}
                      image={image}
                      chosen={chosen.has(image.id)}
                      onToggle={() => {
                        toggle(image.id);
                      }}
                    />
                  ))}
                </ul>
                {emptied && (
                  <p className="form__note banner-picker__warning" role="status">
                    {t('home.banner.needOne')}
                  </p>
                )}
              </>
            )}
          </>
        )}
        {failed && (
          <p className="form__note banner-picker__warning" role="status">
            {t('home.banner.notSaved')}
          </p>
        )}
      </div>
    </div>
  );
}
