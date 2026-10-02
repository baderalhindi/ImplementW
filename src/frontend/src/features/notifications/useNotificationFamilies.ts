import { useApiResource, type ApiResource } from '@/shared/api/useApiResource.ts';

import { notificationsApi } from './api/notificationsApi.ts';
import { type NotificationFamilyPreference } from './api/types.ts';

/** Module-level, so useApiResource reads once per mount. */
function loadFamilies(signal: AbortSignal): Promise<NotificationFamilyPreference[]> {
  return notificationsApi.getPreferences(signal).then((set) => set.families);
}

/**
 * The event families of the routing configuration in force, with their labels, read from the caller's preference set:
 * the one WF-15 read open to everyone signed in that names them (TASK-039 §3). It answers 422 CONFIGURATION_MISSING
 * while no routing is published; screens then show codes and offer no family filter.
 */
export function useNotificationFamilies(): ApiResource<NotificationFamilyPreference[]> {
  return useApiResource(loadFamilies);
}
