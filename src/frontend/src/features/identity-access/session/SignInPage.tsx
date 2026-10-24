import {
  type KeyboardEvent,
  type ReactElement,
  type ReactNode,
  type SyntheticEvent,
  useId,
  useRef,
  useState,
} from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { isAppPath } from '@/shared/navigation/appPath.ts';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { checkText, useFocusFirstError, useSaveAction } from '../forms.ts';
import {
  isMultiFactorPending,
  type MultiFactorChallenge,
  type MultiFactorPending,
  type Session,
  sessionApi,
} from './sessionApi.ts';
import { sessionStore } from './sessionStore.ts';
import { useSession } from './useSession.ts';

interface PendingSecondFactor {
  pending: MultiFactorPending;
  challenge: MultiFactorChallenge;
}

type AccountTab = 'microsoft' | 'directory';

const accountTabs: readonly AccountTab[] = ['microsoft', 'directory'];

/**
 * The directory owns passwords, so the platform neither resets nor changes one: it links to AHDA's reset page when the
 * build names it, and otherwise says where to go. Only an absolute https URL is used, so the link can never carry
 * javascript: or a relative path (TASK-077).
 */
function passwordResetUrl(): string | null {
  const value = import.meta.env.VITE_PASSWORD_RESET_URL ?? '';
  return URL.canParse(value) && new URL(value).protocol === 'https:' ? new URL(value).href : null;
}

function redirectTarget(state: unknown): string {
  const from = (state as { from?: unknown } | null)?.from;
  return isAppPath(from) ? from : '/';
}

function Icon({ children }: { children: ReactNode }): ReactElement {
  return (
    <svg
      className="sign-in__icon"
      viewBox="0 0 24 24"
      width="20"
      height="20"
      aria-hidden="true"
      focusable="false"
    >
      <g
        fill="none"
        stroke="currentColor"
        strokeWidth="1.75"
        strokeLinecap="round"
        strokeLinejoin="round"
      >
        {children}
      </g>
    </svg>
  );
}

const userIcon = (
  <Icon>
    <circle cx="12" cy="8" r="4" />
    <path d="M4 21c0-4.4 3.6-7 8-7s8 2.6 8 7" />
  </Icon>
);

function MicrosoftMark(): ReactElement {
  return (
    <svg
      className="sign-in__microsoft-mark"
      viewBox="0 0 21 21"
      aria-hidden="true"
      focusable="false"
    >
      <rect x="0" y="0" width="10" height="10" fill="#f25022" />
      <rect x="11" y="0" width="10" height="10" fill="#7fba00" />
      <rect x="0" y="11" width="10" height="10" fill="#00a4ef" />
      <rect x="11" y="11" width="10" height="10" fill="#ffb900" />
    </svg>
  );
}

interface CredentialFieldProps {
  label: string;
  name: string;
  value: string;
  onChange: (value: string) => void;
  error?: string | undefined;
  icon: ReactNode;
  type?: 'text' | 'password';
  autoComplete: string;
  disabled?: boolean;
}

/**
 * The mockup's input: the label is visually hidden and repeated as the placeholder, and an icon sits at the end. The
 * label stays the input's exact accessible name ("Username", "Password"), without a required suffix.
 */
function CredentialField({
  label,
  name,
  value,
  onChange,
  error,
  icon,
  type = 'text',
  autoComplete,
  disabled = false,
}: CredentialFieldProps): ReactElement {
  const id = useId();
  const errorId = `${id}-error`;
  return (
    <div className="field">
      <label className="visually-hidden" htmlFor={id}>
        {label}
      </label>
      <div className="sign-in__input">
        <input
          id={id}
          name={name}
          className="field__input"
          type={type}
          dir="ltr"
          placeholder={label}
          value={value}
          onChange={(event) => {
            onChange(event.target.value);
          }}
          required
          disabled={disabled}
          autoComplete={autoComplete}
          aria-invalid={error === undefined ? undefined : true}
          aria-describedby={error === undefined ? undefined : errorId}
        />
        {icon}
      </div>
      {error !== undefined && (
        <p id={errorId} className="field__error">
          {error}
        </p>
      )}
    </div>
  );
}

/**
 * National single sign-on as the mockup lays it out. Nafath and its biometric fallback have no API yet, so every
 * control here is disabled and the panel says so.
 */
function NafathPanel(): ReactElement {
  const { t } = useI18n();
  const idFieldId = useId();
  return (
    <section
      className="sign-in__panel sign-in__nafath"
      aria-label={t('identityAccess.signIn.nafath.name')}
    >
      <img
        className="sign-in__nafath-mark"
        src="/brand/nafath-logo.png"
        alt=""
        width="681"
        height="293"
      />
      <p className="badge sign-in__soon">{t('identityAccess.signIn.comingSoon')}</p>
      <p className="sign-in__note">
        <Icon>
          <circle cx="12" cy="12" r="9" />
          <path d="M12 11v5M12 8h.01" />
        </Icon>
        {t('identityAccess.signIn.nafath.instructions')}
      </p>
      <button type="button" className="button button--primary sign-in__wide" disabled>
        {t('identityAccess.signIn.nafath.submit')}
      </button>
      <p className="sign-in__divider">{t('identityAccess.signIn.or')}</p>
      <p className="sign-in__note">{t('identityAccess.signIn.biometric.instructions')}</p>
      <div className="field">
        <label className="field__label" htmlFor={idFieldId}>
          {t('identityAccess.signIn.biometric.nationalId')}
        </label>
        <input
          id={idFieldId}
          name="nationalId"
          className="field__input"
          inputMode="numeric"
          dir="ltr"
          placeholder={t('identityAccess.signIn.biometric.nationalIdPlaceholder')}
          disabled
        />
      </div>
      <button type="button" className="button sign-in__wide" disabled>
        {t('identityAccess.signIn.biometric.submit')}
      </button>
    </section>
  );
}

/**
 * Sign-in as the AHDA mockup lays it out: national single sign-on (Nafath) beside the account card. Only the directory
 * account works; Nafath, Microsoft and biometric sign-in are shown disabled until their integrations exist. The
 * directory sign-in keeps its second factor (TASK-028, TASK-029); R01 always requires MFA.
 */
export function SignInPage(): ReactElement {
  const { t } = useI18n();
  const { session } = useSession();
  const location = useLocation();
  const navigate = useNavigate();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction();
  useFocusFirstError(formRef, save.fieldErrors);
  const tabsId = useId();
  const tabRefs = useRef<Record<AccountTab, HTMLButtonElement | null>>({
    microsoft: null,
    directory: null,
  });

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [passwordVisible, setPasswordVisible] = useState(false);
  const [code, setCode] = useState('');
  const [secondFactor, setSecondFactor] = useState<PendingSecondFactor | null>(null);
  const [tab, setTab] = useState<AccountTab>('microsoft');

  const resetUrl = passwordResetUrl();
  const target = redirectTarget(location.state);
  if (session !== null) {
    return <Navigate to={target} replace />;
  }

  const finish = (session: Session) => {
    sessionStore.setSession(session);
    void navigate(target, { replace: true });
  };

  const submitCredentials = async (event: SyntheticEvent) => {
    event.preventDefault();
    const valid = save.validate({
      username: checkText(username, { required: true }),
      password: password === '' ? 'REQUIRED' : null,
    });
    if (!valid) {
      return;
    }
    const result = await save.run(async () => {
      const signedIn = await sessionApi.signIn(username.trim(), password);
      if (!isMultiFactorPending(signedIn)) {
        return { session: signedIn };
      }
      const challenge = await sessionApi.beginMultiFactor(signedIn.mfaToken);
      return { secondFactor: { pending: signedIn, challenge } };
    });
    if (!result.ok) {
      return;
    }
    setPassword('');
    if ('session' in result.value) {
      finish(result.value.session);
    } else {
      setSecondFactor(result.value.secondFactor);
    }
  };

  const submitCode = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (secondFactor === null || !save.validate({ code: code.trim() === '' ? 'REQUIRED' : null })) {
      return;
    }
    const result = await save.run(() =>
      sessionApi.completeMultiFactor(
        secondFactor.pending.mfaToken,
        secondFactor.challenge.challengeId,
        code.trim(),
      ),
    );
    if (result.ok && !isMultiFactorPending(result.value)) {
      finish(result.value);
    }
  };

  // The WAI-ARIA tabs pattern: arrow keys move between the tabs, which are one Tab stop.
  const moveTab = (event: KeyboardEvent) => {
    if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') {
      return;
    }
    event.preventDefault();
    const next = accountTabs[(accountTabs.indexOf(tab) + 1) % accountTabs.length] ?? tab;
    setTab(next);
    tabRefs.current[next]?.focus();
  };

  const credentialsForm = (
    <form
      ref={formRef}
      className="form sign-in__form"
      noValidate
      onSubmit={(event) => void submitCredentials(event)}
    >
      <FormAlert message={save.formError} />
      <CredentialField
        label={t('identityAccess.signIn.username')}
        name="username"
        autoComplete="username"
        value={username}
        onChange={setUsername}
        error={save.fieldErrors.username}
        icon={userIcon}
      />
      <CredentialField
        label={t('identityAccess.signIn.password')}
        name="password"
        type={passwordVisible ? 'text' : 'password'}
        autoComplete="current-password"
        value={password}
        onChange={setPassword}
        error={save.fieldErrors.password}
        icon={
          <button
            type="button"
            className="sign-in__reveal"
            aria-pressed={passwordVisible}
            title={t('identityAccess.signIn.showPassword')}
            onClick={() => {
              setPasswordVisible((value) => !value);
            }}
          >
            <Icon>
              <path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12z" />
              <circle cx="12" cy="12" r="3" />
              {passwordVisible && <path d="M4 4l16 16" />}
            </Icon>
            <span className="visually-hidden">{t('identityAccess.signIn.showPassword')}</span>
          </button>
        }
      />
      <p className="sign-in__forgot">
        {resetUrl === null ? (
          t('identityAccess.signIn.forgotPasswordContact')
        ) : (
          // The one bound link: a build-time https URL checked by passwordResetUrl, never data.
          // eslint-disable-next-line no-restricted-syntax
          <a href={resetUrl}>{t('identityAccess.signIn.forgotPassword')}</a>
        )}
      </p>
      <button type="submit" className="button button--primary sign-in__wide" disabled={save.saving}>
        {save.saving ? t('identityAccess.signIn.signingIn') : t('identityAccess.signIn.submit')}
      </button>
    </form>
  );

  return (
    <div className="sign-in">
      <div className="sign-in__card">
        <section className="sign-in__panel sign-in__account">
          <span className="sign-in__avatar">{userIcon}</span>
          <PageHeader
            title={t('identityAccess.signIn.title')}
            description={
              secondFactor === null ? t('identityAccess.signIn.chooseMethod') : undefined
            }
          />
          {secondFactor === null ? (
            <div className="sign-in__tabs">
              <div
                className="sign-in__tablist"
                role="tablist"
                aria-label={t('identityAccess.signIn.chooseMethod')}
              >
                {accountTabs.map((value) => (
                  <button
                    key={value}
                    ref={(element) => {
                      tabRefs.current[value] = element;
                    }}
                    type="button"
                    role="tab"
                    id={`${tabsId}-${value}`}
                    className="sign-in__tab"
                    aria-selected={tab === value}
                    aria-controls={`${tabsId}-panel`}
                    tabIndex={tab === value ? 0 : -1}
                    onKeyDown={moveTab}
                    onClick={() => {
                      setTab(value);
                    }}
                  >
                    {value === 'microsoft' ? <MicrosoftMark /> : userIcon}
                    {t(`identityAccess.signIn.tabs.${value}`)}
                  </button>
                ))}
              </div>
              <div
                className="sign-in__tabpanel"
                role="tabpanel"
                id={`${tabsId}-panel`}
                aria-labelledby={`${tabsId}-${tab}`}
              >
                {tab === 'microsoft' && (
                  <>
                    <button type="button" className="sign-in__provider" disabled>
                      <MicrosoftMark />
                      <span className="sign-in__provider-text">
                        <span>{t('identityAccess.signIn.microsoft.submit')}</span>
                        <span className="sign-in__provider-hint">
                          {t('identityAccess.signIn.microsoft.hint')}
                        </span>
                      </span>
                      <span className="badge sign-in__soon">
                        {t('identityAccess.signIn.comingSoon')}
                      </span>
                    </button>
                    <p className="sign-in__divider">{t('identityAccess.signIn.or')}</p>
                  </>
                )}
                <h2 className="sign-in__subtitle">{t('identityAccess.signIn.directoryTitle')}</h2>
                {credentialsForm}
              </div>
            </div>
          ) : (
            <form
              ref={formRef}
              className="form sign-in__form"
              noValidate
              onSubmit={(event) => void submitCode(event)}
            >
              <FormAlert message={save.formError} />
              <p>
                {secondFactor.pending.enrolmentRequired
                  ? t('identityAccess.signIn.enrolInstructions')
                  : t('identityAccess.signIn.codeInstructions')}
              </p>
              {secondFactor.challenge.provisioningUri !== null && (
                <p className="form__readonly">
                  <span className="field__label">{t('identityAccess.signIn.provisioningUri')}</span>
                  <code dir="ltr" className="break-all">
                    {secondFactor.challenge.provisioningUri}
                  </code>
                </p>
              )}
              <TextField
                label={t('identityAccess.signIn.code')}
                name="code"
                required
                dir="ltr"
                inputMode="numeric"
                autoComplete="one-time-code"
                value={code}
                onChange={setCode}
                error={save.fieldErrors.code}
              />
              <button
                type="submit"
                className="button button--primary sign-in__wide"
                disabled={save.saving}
              >
                {save.saving
                  ? t('identityAccess.signIn.verifying')
                  : t('identityAccess.signIn.verify')}
              </button>
            </form>
          )}
        </section>
        <NafathPanel />
      </div>
      <footer className="sign-in__footer">
        {t('identityAccess.signIn.copyright', { year: new Date().getFullYear() })}
      </footer>
    </div>
  );
}
