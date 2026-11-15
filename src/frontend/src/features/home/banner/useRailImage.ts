import { useCallback } from 'react';

import { useApiResource } from '@/shared/api/useApiResource.ts';

import { bannerApi } from './bannerApi.ts';
import { useBannerImage } from './useBannerImage.ts';

/**
 * The side menu's picture (review round 8, point 07): one image, the same for everyone and not among anyone's settings — the banner image with this
 * code, the garden at the foot of Jabal Al-Qarah. Without it in the database the menu keeps its plain green.
 */
export const SIDE_MENU_IMAGE_CODE = 'qarah-garden';

/** The side menu picture's local address once read; null otherwise. */
export function useRailImage(): string | null {
  const load = useCallback((signal: AbortSignal) => bannerApi.images(signal), []);
  const images = useApiResource(load);
  const image = images.data?.find((candidate) => candidate.code === SIDE_MENU_IMAGE_CODE);
  return useBannerImage(image?.id ?? null);
}
