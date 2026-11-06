import { isAppPath } from '@/shared/navigation/appPath.ts';

// What a single sign-on keeps while the browser is away at the identity provider (sso-directory-integration.md D-4). It is
// in sessionStorage, which a redirect in the same tab keeps, so the sign-in completes only in the tab that started it.

const STORAGE_KEY = 'pmplatform.sso';

export interface SsoTransaction {
  /** Sealed by the API; returned with the authorization code. */
  transaction: string;
  /** The route to open once signed in. */
  returnTo: string;
}

export const ssoTransaction = {
  save: (value: SsoTransaction): void => {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(value));
  },

  read: (): SsoTransaction | null => {
    try {
      const stored = JSON.parse(sessionStorage.getItem(STORAGE_KEY) ?? 'null') as {
        transaction?: unknown;
        returnTo?: unknown;
      } | null;
      return typeof stored?.transaction === 'string'
        ? {
            transaction: stored.transaction,
            returnTo: isAppPath(stored.returnTo) ? stored.returnTo : '/',
          }
        : null;
    } catch {
      return null;
    }
  },

  clear: (): void => {
    try {
      sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // Storage unavailable: nothing was kept.
    }
  },
};

/** Leaves the application for the identity provider. A module of its own so a test can stand in for the navigation. */
export const identityProviderRedirect = {
  go: (url: string): void => {
    window.location.assign(url);
  },
};
