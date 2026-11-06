import { checkPassword } from '../forms.ts';

/** The navigation state that admits a person to the confirmation page, which then ends this browser's session. */
export const PASSWORD_UPDATED = 'passwordUpdated';

/** The API code of each new-password field's first failed check. The API's rules stay the authority. */
export function checkNewPassword(
  newPassword: string,
  confirmPassword: string,
  username: string,
): { newPassword: string | null; confirmPassword: string | null } {
  return {
    newPassword: checkPassword(newPassword, username),
    confirmPassword:
      confirmPassword === ''
        ? 'REQUIRED'
        : confirmPassword === newPassword
          ? null
          : 'PASSWORD_MISMATCH',
  };
}
