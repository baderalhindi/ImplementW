import { type ReactElement, useEffect, useRef, useState } from 'react';
import { Link, Navigate, useSearchParams } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { useSaveAction } from '../forms.ts';
import { SecondFactorForm } from './SecondFactorForm.tsx';
import { sessionApi } from './sessionApi.ts';
import { sessionStore } from './sessionStore.ts';
import { describeRefusal, nextStep, type PendingSecondFactor } from './signInSteps.ts';
import { ssoTransaction } from './ssoTransaction.ts';
import { useSession } from './useSession.ts';

/**
 * Where the identity provider sends the browser back (SSO_OIDC_CALLBACK_URL): it redeems the code once, then continues to the
 * second factor if the person requires one. What the provider said on a refusal is not shown: it is not this application's to
 * vouch for, and the query string is anyone's to write.
 */
export function SsoCallbackPage(): ReactElement {
  const { t } = useI18n();
  const { session } = useSession();
  const [params] = useSearchParams();
  const [started] = useState(ssoTransaction.read);
  const [secondFactor, setSecondFactor] = useState<PendingSecondFactor | null>(null);
  const save = useSaveAction(describeRefusal('identityAccess.signIn.sso.refused'));
  const { run } = save;
  const redeemed = useRef(false);

  const code = params.get('code');
  const state = params.get('state');
  const redeemable = started !== null && code !== null && state !== null && !params.has('error');

  useEffect(() => {
    // The effect runs twice in development (StrictMode), and a code can be redeemed only once.
    if (redeemed.current) {
      return;
    }
    redeemed.current = true;
    // The code is spent from here on; it stays out of the address bar and the history.
    window.history.replaceState(window.history.state, '', window.location.pathname);
    // Signing in swaps the shell, which mounts this page anew: only that second mount, holding the session, may forget the transaction.
    if (!redeemable || session !== null) {
      ssoTransaction.clear();
      return;
    }
    void run(async () =>
      nextStep(await sessionApi.completeSso(code, state, started.transaction)),
    ).then((result) => {
      if (!result.ok) {
        ssoTransaction.clear();
      } else if ('session' in result.value) {
        sessionStore.setSession(result.value.session);
      } else {
        setSecondFactor(result.value.secondFactor);
      }
    });
  }, [code, redeemable, run, session, started, state]);

  if (session !== null) {
    return <Navigate to={started?.returnTo ?? '/'} replace />;
  }

  const failure = redeemable ? save.formError : t('identityAccess.signIn.sso.notCompleted');

  return (
    <div className="sign-in">
      <div className="sign-in__card sign-in__card--single">
        <section className="sign-in__panel sign-in__account">
          <PageHeader title={t('identityAccess.signIn.title')} />
          {secondFactor !== null ? (
            <SecondFactorForm
              secondFactor={secondFactor}
              onSession={(signedIn) => {
                sessionStore.setSession(signedIn);
              }}
            />
          ) : failure !== null ? (
            <div className="sign-in__form">
              <FormAlert message={failure} />
              <Link className="button sign-in__wide" to="/sign-in" replace>
                {t('identityAccess.signIn.backToSignIn')}
              </Link>
            </div>
          ) : (
            <p role="status">{t('identityAccess.signIn.signingIn')}</p>
          )}
        </section>
      </div>
    </div>
  );
}
