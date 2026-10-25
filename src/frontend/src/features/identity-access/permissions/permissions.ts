import { createContext, useContext } from 'react';

import { type PermissionCode } from './permissionCodes.ts';
import { type EffectivePermissions } from './permissionsApi.ts';

/**
 * What the signed-in person holds, as the shell offers menus, tabs and buttons on it (the 24 Oct 2026 review, C3: "menu items
 * appear only if useCan"). Until the API has answered nothing is held, so nothing is offered that may then disappear.
 */
export interface Permissions {
  /** The API has answered. */
  loaded: boolean;
  /** Whether any of `codes` is held, at any scope; with no code, true. */
  can: (...codes: readonly PermissionCode[]) => boolean;
}

export const NO_PERMISSIONS: Permissions = { loaded: false, can: (...codes) => codes.length === 0 };

export function permissionsOf(effective: EffectivePermissions | null | undefined): Permissions {
  if (effective === null || effective === undefined) {
    return NO_PERMISSIONS;
  }
  const held = new Set(effective.permissions.map((permission) => permission.code));
  return {
    loaded: true,
    can: (...codes) => codes.length === 0 || codes.some((code) => held.has(code)),
  };
}

export const PermissionsContext = createContext<Permissions>(NO_PERMISSIONS);

export function usePermissions(): Permissions {
  return useContext(PermissionsContext);
}

/** Whether the person holds any of `codes` now. */
export function useCan(...codes: readonly PermissionCode[]): boolean {
  return usePermissions().can(...codes);
}
