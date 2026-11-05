import { type ReactElement, useState } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

type CopyState = 'idle' | 'copied' | 'failed';

/**
 * A temporary password in the one response that set it (the 5 Nov 2026 review §6.1). It lives in this screen's memory only: never in
 * the URL, the router's history state or browser storage, so leaving the screen loses it for good.
 */
export function TemporaryPasswordNotice({
  name,
  password,
  onDismiss,
}: {
  name: string;
  password: string;
  onDismiss?: () => void;
}): ReactElement {
  const { t } = useI18n();
  const [copy, setCopy] = useState<CopyState>('idle');

  const copyPassword = async () => {
    try {
      await navigator.clipboard.writeText(password);
      setCopy('copied');
    } catch {
      setCopy('failed');
    }
  };

  return (
    <div className="notice notice--warning" role="status">
      <p>{t('identityAccess.users.temporaryPassword.title', { name })}</p>
      <p>
        <code dir="ltr" className="break-all">
          {password}
        </code>
      </p>
      <p>{t('identityAccess.users.temporaryPassword.note')}</p>
      {copy === 'failed' && <p>{t('identityAccess.users.temporaryPassword.copyFailed')}</p>}
      <div className="form__actions">
        <button type="button" className="button" onClick={() => void copyPassword()}>
          {copy === 'copied'
            ? t('identityAccess.users.temporaryPassword.copied')
            : t('identityAccess.users.temporaryPassword.copy')}
        </button>
        {onDismiss !== undefined && (
          <button type="button" className="button button--quiet" onClick={onDismiss}>
            {t('identityAccess.users.temporaryPassword.dismiss')}
          </button>
        )}
      </div>
    </div>
  );
}
