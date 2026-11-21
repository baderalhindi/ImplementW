import { type BilingualLabel } from '@/features/identity-access/api/types.ts';
import { apiDownload, apiRequest } from '@/shared/api/httpClient.ts';

// The Home welcome banner (review round 8, point 07): the images the platform keeps in its database, and this person's choice, kept on the
// server so it follows them across browsers.

/** The green banner, one image, or images that take turns. */
export type BannerStyle = 'DEFAULT' | 'IMAGE' | 'ROTATE';

export interface BannerImage {
  id: string;
  code: string;
  caption: BilingualLabel;
  alt: BilingualLabel;
  /** Where the subject is, in percent; the crop keeps it in view. */
  focalX: number;
  focalY: number;
  credit: string | null;
  width: number;
  height: number;
}

export interface BannerPreference {
  style: BannerStyle;
  /** The images chosen, in the order they show. */
  imageIds: string[];
}

export const DEFAULT_BANNER: BannerPreference = { style: 'DEFAULT', imageIds: [] };

export const bannerApi = {
  images: async (signal?: AbortSignal) =>
    (await apiRequest<{ items: BannerImage[] }>('/banner-images', { signal })).data.items,
  preference: async (signal?: AbortSignal) =>
    (await apiRequest<BannerPreference>('/users/current/banner', { signal })).data,
  save: async (preference: BannerPreference) =>
    (
      await apiRequest<BannerPreference>('/users/current/banner', {
        method: 'PUT',
        body: preference,
      })
    ).data,
  /** An image's bytes, read with the person's session: an image element cannot send it. */
  content: async (id: string, signal?: AbortSignal) =>
    (await apiDownload(`/banner-images/${id}/content`, { signal })).content,
};
