import { useEffect, type ReactElement } from 'react';
import { Link, useRouteError } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';

import { reportError } from './errorReporting.ts';

/**
 * The route error boundary (TASK-090): a screen that throws while rendering or loading is reported to the APM and replaced by
 * this page, which says what happened and how to go on. The error itself is never shown: it is for the APM, not the user.
 */
export function RouteErrorPage(): ReactElement {
  const { t } = useI18n();
  const error = useRouteError();

  useEffect(() => {
    reportError(error, 'RENDER_ERROR');
  }, [error]);

  return (
    <main>
      <PageHeader title={t('common.screenError.title')} />
      <p>{t('common.screenError.description')}</p>
      <p>
        <button
          type="button"
          onClick={() => {
            window.location.reload();
          }}
        >
          {t('common.screenError.reload')}
        </button>
      </p>
      <p>
        <Link to="/">{t('common.notFound.home')}</Link>
      </p>
    </main>
  );
}
