import { useCallback, useState } from 'react';

import { useApiResource } from '@/shared/api/useApiResource.ts';

import { type BannerImage, type BannerPreference, bannerApi, DEFAULT_BANNER } from './bannerApi.ts';

export interface BannerState {
  images: BannerImage[];
  preference: BannerPreference;
  /** Shows the choice straight away and saves it; a refused save puts the last saved choice back. */
  choose: (next: BannerPreference) => void;
  saving: boolean;
  failed: boolean;
}

/** The images and this person's banner. Until both are read, whenever they cannot be, and before signing in, the green banner shows. */
export function useBanner(signedIn: boolean): BannerState {
  const load = useCallback(
    async (signal: AbortSignal) => {
      // Before signing in there is no banner to read: the sign-in page shows instead.
      if (!signedIn) {
        return { images: [], preference: DEFAULT_BANNER };
      }
      const [images, preference] = await Promise.all([
        bannerApi.images(signal),
        bannerApi.preference(signal),
      ]);
      return { images, preference };
    },
    [signedIn],
  );
  const read = useApiResource(load);
  // A choice made here, shown before the server answers; until then, the choice read.
  const [chosen, setChosen] = useState<BannerPreference | null>(null);
  const [saving, setSaving] = useState(false);
  const [failed, setFailed] = useState(false);
  const shown = chosen ?? read.data?.preference ?? null;

  const images = read.data?.images ?? [];
  const known = new Set(images.map((image) => image.id));
  const saved = shown ?? DEFAULT_BANNER;
  // An image removed since it was chosen is skipped; with none left, the green banner shows.
  const imageIds = saved.imageIds.filter((id) => known.has(id));
  const preference: BannerPreference =
    saved.style !== 'DEFAULT' && imageIds.length === 0 ? DEFAULT_BANNER : { ...saved, imageIds };

  const choose = (next: BannerPreference) => {
    const previous = chosen;
    setChosen(next);
    setSaving(true);
    setFailed(false);
    bannerApi
      .save(next)
      .then((stored) => {
        setChosen(stored);
      })
      .catch(() => {
        setChosen(previous);
        setFailed(true);
      })
      .finally(() => {
        setSaving(false);
      });
  };

  return { images, preference, choose, saving, failed };
}
