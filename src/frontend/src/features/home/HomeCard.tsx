import { type ReactElement, type ReactNode } from 'react';
import { Link } from 'react-router';

import { type ApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { LoadingState } from '@/shared/ui/States.tsx';

/**
 * One card of the Home: a headline figure, the first few items and a way to the full list. A card whose read fails says so in a
 * line and keeps the page whole: the Home is a summary, each list's own screen the place to retry.
 */
export function HomeCard<T>({
  title,
  resource,
  figure,
  viewAll,
  empty,
  children,
}: {
  title: string;
  resource: ApiResource<T>;
  /** The headline number, from the data. */
  figure: (data: T) => number;
  viewAll: { to: string; label: string };
  empty: string;
  children: (data: T) => ReactNode;
}): ReactElement {
  const { t } = useI18n();
  const data = resource.data;
  return (
    <section className="widget home__card" aria-label={title}>
      <h2 className="widget__title">{title}</h2>
      {data === undefined ? (
        resource.loading ? (
          <LoadingState />
        ) : (
          <p className="state">{t('home.unavailable')}</p>
        )
      ) : (
        <>
          <p className="home__figure">{figure(data)}</p>
          {figure(data) === 0 ? <p className="home__empty">{empty}</p> : children(data)}
        </>
      )}
      <Link className="home__more" to={viewAll.to}>
        {viewAll.label}
      </Link>
    </section>
  );
}
