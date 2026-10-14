import { type ReactElement, type ReactNode } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

/** A titled part of a page. */
export function Section({
  title,
  description,
  actions,
  children,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
  children: ReactNode;
}): ReactElement {
  return (
    <section className="section">
      <div className="section__header">
        <h2>{title}</h2>
        {actions}
      </div>
      {description !== undefined && <p className="integration-note">{description}</p>}
      {children}
    </section>
  );
}

/** A technical identifier or code: left-to-right in either language, wrapped anywhere so it never widens the page. */
export function Code({ children }: { children: string }): ReactElement {
  return (
    <code className="break-all" dir="ltr">
      {children}
    </code>
  );
}

/** A time, or the word for its absence (`never` by default: no success yet is not a date). */
export function Moment({ value, absent }: { value: string | null; absent?: string }): ReactElement {
  const { t, formatDateTime } = useI18n();
  if (value === null) {
    return <span className="cell__aside">{absent ?? t('integrationAdmin.values.never')}</span>;
  }
  return <time dateTime={value}>{formatDateTime(value)}</time>;
}

/** The environment the screen describes, always visible on an operational screen (FG-05 §13.1). */
export function EnvironmentBanner({ environment }: { environment: string }): ReactElement {
  const { t } = useI18n();
  return (
    <p className="integration-environment" data-environment={environment}>
      {t('integrationAdmin.environment.label')} <strong dir="ltr">{environment}</strong>
    </p>
  );
}
