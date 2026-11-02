import { shortId } from '@/features/projects/presentation.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { type DashboardPlace } from './api/types.ts';

/** A place's name in the interface language, or its short id when the view does not name it. */
export function usePlaceName(places: DashboardPlace[]): (id: string) => string {
  const { t, language } = useI18n();
  return (id) =>
    places.find((place) => place.id === id)?.label[language] ??
    t('projects.lookups.unknownItem', { id: shortId(id) });
}
