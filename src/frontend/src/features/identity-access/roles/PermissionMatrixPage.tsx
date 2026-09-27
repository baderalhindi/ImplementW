import { Fragment, type ReactElement, useCallback } from 'react';
import { useSearchParams } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField } from '@/shared/ui/FormFields.tsx';
import { PageHeader, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { rolesApi } from '../api/identityAccessApi.ts';
import {
  type PermissionProfileDetail,
  type PermissionProfileVersion,
  type PermissionSummary,
} from '../api/types.ts';
import { labelOf } from '../lookups.ts';
import { problemMessage } from '../problems.ts';

interface MatrixColumn {
  profile: PermissionProfileDetail;
  version: PermissionProfileVersion | undefined;
}

/** The version a column shows: the one chosen, else the newest PUBLISHED, else the newest of any state. */
function versionFor(profile: PermissionProfileDetail, versionId: string | null) {
  return (
    profile.versions.find((version) => version.id === versionId) ??
    profile.versions.find((version) => version.lifecycleState === 'PUBLISHED') ??
    profile.versions[0]
  );
}

function groupByPermissionGroup(permissions: PermissionSummary[]): [string, PermissionSummary[]][] {
  const groups = new Map<string, PermissionSummary[]>();
  for (const permission of permissions) {
    groups.set(permission.permissionGroup, [
      ...(groups.get(permission.permissionGroup) ?? []),
      permission,
    ]);
  }
  return [...groups.entries()];
}

/**
 * ADM-009 Permission Matrix: catalogue permissions against permission profiles, each cell the data scope a profile
 * version grants. Read-only: profiles are authored, versioned and published elsewhere (record D-2, ADR-018).
 */
export function PermissionMatrixPage(): ReactElement {
  const { t, language } = useI18n();
  const [params, setParams] = useSearchParams();
  const profileId = params.get('profileId');
  const versionId = params.get('versionId');

  const load = useCallback(async (signal: AbortSignal) => {
    const [permissions, summaries] = await Promise.all([
      rolesApi.listPermissions(signal),
      rolesApi.listProfiles(signal),
    ]);
    const profiles = await Promise.all(summaries.map((s) => rolesApi.getProfile(s.id, signal)));
    return { permissions, profiles };
  }, []);
  const matrix = useApiResource(load);

  const choose = (name: 'profileId' | 'versionId', value: string) => {
    const next = new URLSearchParams(params);
    if (name === 'profileId') {
      next.delete('versionId');
    }
    if (value === '') {
      next.delete(name);
    } else {
      next.set(name, value);
    }
    setParams(next);
  };

  const header = (
    <PageHeader
      title={t('identityAccess.permissionMatrix.title')}
      description={t('identityAccess.permissionMatrix.description')}
    />
  );
  if (matrix.loading) {
    return (
      <>
        {header}
        <LoadingState />
      </>
    );
  }
  if (matrix.error !== null || matrix.data === undefined) {
    return (
      <>
        {header}
        <ErrorState message={problemMessage(matrix.error, t)} onRetry={matrix.reload} />
      </>
    );
  }

  const { permissions, profiles } = matrix.data;
  const selected = profiles.find((profile) => profile.id === profileId);
  const columns: MatrixColumn[] = (selected === undefined ? profiles : [selected]).map(
    (profile) => ({
      profile,
      version: versionFor(profile, profile === selected ? versionId : null),
    }),
  );

  return (
    <>
      {header}
      <div className="filters">
        <SelectField
          label={t('identityAccess.permissionMatrix.profile')}
          name="profileId"
          value={selected?.id ?? ''}
          placeholder={t('identityAccess.permissionMatrix.allProfiles')}
          options={profiles.map((profile) => ({
            value: profile.id,
            label: `${profile.baseRoleCode} — ${labelOf(profile.name, language)}`,
          }))}
          onChange={(value) => {
            choose('profileId', value);
          }}
        />
        {selected !== undefined && (
          <SelectField
            label={t('identityAccess.permissionMatrix.version')}
            name="versionId"
            value={columns[0]?.version?.id ?? ''}
            options={selected.versions.map((version) => ({
              value: version.id,
              label: t('identityAccess.permissionMatrix.versionLabel', {
                version: version.versionNo,
                state: t(`identityAccess.lifecycleState.${version.lifecycleState}`),
              }),
            }))}
            onChange={(value) => {
              choose('versionId', value);
            }}
          />
        )}
      </div>

      {permissions.length === 0 || columns.length === 0 ? (
        <EmptyState title={t('identityAccess.permissionMatrix.empty')} />
      ) : (
        <TableContainer caption={t('identityAccess.permissionMatrix.caption')}>
          <thead>
            <tr>
              <th scope="col">{t('identityAccess.permissionMatrix.permission')}</th>
              {columns.map(({ profile, version }) => (
                <th scope="col" key={profile.id}>
                  <span className="matrix__role">{profile.baseRoleCode}</span>
                  <span className="matrix__profile">{labelOf(profile.name, language)}</span>
                  <span className="matrix__version">
                    {version === undefined
                      ? t('identityAccess.permissionMatrix.noVersion')
                      : t('identityAccess.permissionMatrix.versionLabel', {
                          version: version.versionNo,
                          state: t(`identityAccess.lifecycleState.${version.lifecycleState}`),
                        })}
                  </span>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {groupByPermissionGroup(permissions).map(([group, members]) => (
              <Fragment key={group}>
                <tr className="matrix__group">
                  <th scope="colgroup" colSpan={columns.length + 1}>
                    {group}
                  </th>
                </tr>
                {members.map((permission) => (
                  <tr key={permission.id}>
                    <th scope="row" className="matrix__permission">
                      {labelOf(permission.name, language)}
                      <span className="matrix__code" dir="ltr">
                        {permission.code}
                      </span>
                      {permission.isPrivileged && (
                        <span className="badge badge--negative">
                          {t('identityAccess.permissionMatrix.privileged')}
                        </span>
                      )}
                    </th>
                    {columns.map(({ profile, version }) => {
                      const grant = version?.grants.find((g) => g.permissionId === permission.id);
                      return (
                        <td key={profile.id} className="matrix__cell">
                          {grant === undefined ? (
                            <>
                              <span aria-hidden="true">—</span>
                              <span className="visually-hidden">
                                {t('identityAccess.permissionMatrix.notGranted')}
                              </span>
                            </>
                          ) : (
                            t(`identityAccess.dataScope.${grant.dataScope}`)
                          )}
                        </td>
                      );
                    })}
                  </tr>
                ))}
              </Fragment>
            ))}
          </tbody>
        </TableContainer>
      )}
    </>
  );
}
