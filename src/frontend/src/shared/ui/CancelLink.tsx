import { type MouseEvent, type ReactElement } from 'react';
import { Link, useLocation, useNavigate } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';

/**
 * A form's Cancel: back to the page the user came from. A page opened directly (a bookmark, a new tab) has no
 * earlier entry in the app's history, so it goes to `fallback` instead. Still a real link, so it opens in a new tab.
 */
export function CancelLink({ fallback }: { fallback: string }): ReactElement {
  const { t } = useI18n();
  const navigate = useNavigate();
  // React Router gives the first entry of a session the key 'default'; any later entry was reached inside the app.
  const cameFromApp = useLocation().key !== 'default';

  const goBack = (event: MouseEvent<HTMLAnchorElement>) => {
    const plainClick =
      event.button === 0 && !event.metaKey && !event.ctrlKey && !event.shiftKey && !event.altKey;
    if (cameFromApp && plainClick) {
      event.preventDefault();
      void navigate(-1);
    }
  };

  return (
    <Link className="button" to={fallback} onClick={goBack}>
      {t('common.actions.cancel')}
    </Link>
  );
}
