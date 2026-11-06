import { type ReactElement, type ReactNode } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';

/**
 * The directory owns its accounts' passwords, so the platform neither resets nor changes one: it links to AHDA's reset page when
 * the build names it, and otherwise says where to go. Only an absolute https URL is used, so the link can never carry
 * javascript: or a relative path (TASK-077).
 */
function passwordResetUrl(): string | null {
  const value = import.meta.env.VITE_PASSWORD_RESET_URL ?? '';
  return URL.canParse(value) && new URL(value).protocol === 'https:' ? new URL(value).href : null;
}

/** Where a directory or Microsoft account's password is changed: AHDA's page, or the service desk where the build names none. */
export function DirectoryPasswordHelp({
  linkLabel,
  contact,
}: {
  linkLabel: string;
  contact: string;
}): ReactElement {
  const resetUrl = passwordResetUrl();
  return resetUrl === null ? (
    <>{contact}</>
  ) : (
    // The one bound link: a build-time https URL checked by passwordResetUrl, never data.
    // eslint-disable-next-line no-restricted-syntax
    <a href={resetUrl}>{linkLabel}</a>
  );
}

interface NewPasswordFieldsProps {
  newPassword: string;
  confirmPassword: string;
  onNewPassword: (value: string) => void;
  onConfirmPassword: (value: string) => void;
  errors: Partial<Record<string, string>>;
}

export function NewPasswordFields({
  newPassword,
  confirmPassword,
  onNewPassword,
  onConfirmPassword,
  errors,
}: NewPasswordFieldsProps): ReactElement {
  const { t } = useI18n();
  return (
    <>
      <TextField
        label={t('identityAccess.signIn.newPassword')}
        name="newPassword"
        type="password"
        required
        dir="ltr"
        autoComplete="new-password"
        hint={t('identityAccess.passwordRules')}
        value={newPassword}
        onChange={onNewPassword}
        error={errors.newPassword}
      />
      <TextField
        label={t('identityAccess.signIn.confirmPassword')}
        name="confirmPassword"
        type="password"
        required
        dir="ltr"
        autoComplete="new-password"
        value={confirmPassword}
        onChange={onConfirmPassword}
        error={errors.confirmPassword}
      />
    </>
  );
}

/** A signed-out step of the account's password: the sign-in page's account panel alone. */
export function PasswordCard({
  title,
  children,
}: {
  title: string;
  children: ReactNode;
}): ReactElement {
  return (
    <div className="sign-in">
      <div className="sign-in__card sign-in__card--single">
        <section className="sign-in__panel sign-in__account">
          <PageHeader title={title} />
          {children}
        </section>
      </div>
    </div>
  );
}
