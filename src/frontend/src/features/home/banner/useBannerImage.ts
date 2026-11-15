import { useEffect, useState } from 'react';

import { bannerApi } from './bannerApi.ts';

/** Each image is read once per visit and kept as a local address, so taking turns does not download it again. */
const loaded = new Map<string, Promise<string>>();

/** Starts reading an image now, ahead of showing it. */
export function preloadBannerImage(id: string): Promise<string> {
  let address = loaded.get(id);
  if (address === undefined) {
    address = bannerApi.content(id).then((blob) => URL.createObjectURL(blob));
    // A failed read is tried again next time rather than remembered.
    address.catch(() => loaded.delete(id));
    loaded.set(id, address);
  }
  return address;
}

/** The local address of an image once read; null while it is read, or when there is none to show. */
export function useBannerImage(id: string | null): string | null {
  const [state, setState] = useState<{ id: string; address: string } | null>(null);
  useEffect(() => {
    if (id === null) {
      return undefined;
    }
    let current = true;
    preloadBannerImage(id)
      .then((address) => {
        if (current) {
          setState({ id, address });
        }
      })
      .catch(() => {
        // The green banner shows instead.
      });
    return () => {
      current = false;
    };
  }, [id]);
  return id !== null && state?.id === id ? state.address : null;
}
