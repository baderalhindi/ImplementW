import {
  type DataScope,
  type PermissionProfileGrant,
  type PermissionSummary,
} from '../api/types.ts';

/** A version's grid: the scope each held permission is held at, by permission code. A permission absent is not held. */
export type Grid = ReadonlyMap<string, DataScope>;

/** ADM-008's editor of one version. */
export const versionPath = (versionId: string) => `/admin/permission-profile-versions/${versionId}`;

export const DATA_SCOPES: readonly DataScope[] = [
  'ALL',
  'DEPT',
  'OWN',
  'ASSIGNED',
  'ENTITY',
  'READ_ONLY',
];

export function gridOf(grants: readonly PermissionProfileGrant[]): Grid {
  return new Map(grants.map((grant) => [grant.permissionCode, grant.dataScope]));
}

/** The grid as `PUT …/grants` takes it, in catalogue order. */
export function grantsOf(grid: Grid, permissions: readonly PermissionSummary[]) {
  return permissions.flatMap((permission) => {
    const dataScope = grid.get(permission.code);
    return dataScope === undefined ? [] : [{ permissionCode: permission.code, dataScope }];
  });
}

/** The permissions whose scope differs between two grids, held in one and not the other included. */
export function differences(grid: Grid, other: Grid): Set<string> {
  const codes = new Set([...grid.keys(), ...other.keys()]);
  return new Set([...codes].filter((code) => grid.get(code) !== other.get(code)));
}

export function groupByPermissionGroup(
  permissions: readonly PermissionSummary[],
): [string, PermissionSummary[]][] {
  const groups = new Map<string, PermissionSummary[]>();
  for (const permission of permissions) {
    groups.set(permission.permissionGroup, [
      ...(groups.get(permission.permissionGroup) ?? []),
      permission,
    ]);
  }
  return [...groups.entries()];
}
