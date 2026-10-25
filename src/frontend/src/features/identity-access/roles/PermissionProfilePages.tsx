import { type ReactElement, type SyntheticEvent, useCallback, useRef, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { CheckboxField, SelectField, TextAreaField, TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader, TableContainer } from '@/shared/ui/Layout.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import {
  permissionProfilesApi,
  type PermissionProfileTransition,
  rolesApi,
} from '../api/identityAccessApi.ts';
import {
  type DataScope,
  type PermissionProfileDetail,
  type PermissionProfileVersion,
  type PermissionSummary,
} from '../api/types.ts';
import { usePersonNames } from '../assignments/useUserNames.ts';
import { CODE_PATTERN, checkText, useFocusFirstError, useSaveAction } from '../forms.ts';
import { labelOf } from '../lookups.ts';
import { useCan } from '../permissions/permissions.ts';
import { problemMessage } from '../problems.ts';
import { useSession } from '../session/useSession.ts';
import {
  DATA_SCOPES,
  type Grid,
  differences,
  gridOf,
  grantsOf,
  groupByPermissionGroup,
  versionPath,
} from './profileGrid.ts';

// ADM-008 permission profile authoring (TASK-110; the 24 Oct 2026 review, C4): a profile is cloned from another version, its grid
// edited while DRAFT by its author, validated by a second person and published by a third, neither holding the profile. The server
// holds every rule; these screens offer only what the version's state and the caller allow, and say why a refusal came.

/** The newest PUBLISHED version of the base role's shipped default: what a version is compared with. */
function shippedDefaultOf(profiles: readonly PermissionProfileDetail[], baseRoleId: string) {
  return profiles
    .find((profile) => profile.isShippedDefault && profile.baseRoleId === baseRoleId)
    ?.versions.find((version) => version.lifecycleState === 'PUBLISHED');
}

async function loadProfiles(signal: AbortSignal): Promise<PermissionProfileDetail[]> {
  const summaries = await rolesApi.listProfiles(signal);
  return Promise.all(summaries.map((summary) => rolesApi.getProfile(summary.id, signal)));
}

/** ADM-008: a new permission profile on a canonical role, its first version a DRAFT copying the version chosen. */
export function PermissionProfileCreatePage(): ReactElement {
  const { t, language } = useI18n();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction();
  useFocusFirstError(formRef, save.fieldErrors);
  const load = useCallback(async (signal: AbortSignal) => {
    const [roles, profiles] = await Promise.all([rolesApi.list(signal), loadProfiles(signal)]);
    return { roles, profiles };
  }, []);
  const lookups = useApiResource(load);
  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [baseRoleId, setBaseRoleId] = useState('');
  const [sourceVersionId, setSourceVersionId] = useState(params.get('sourceVersionId') ?? '');

  const header = (
    <PageHeader
      title={t('identityAccess.profiles.create.title')}
      description={t('identityAccess.profiles.create.description')}
    />
  );
  if (lookups.loading) {
    return (
      <>
        {header}
        <LoadingState />
      </>
    );
  }
  if (lookups.error !== null || lookups.data === undefined) {
    return (
      <>
        {header}
        <ErrorState
          error={lookups.error}
          message={problemMessage(lookups.error, t)}
          onRetry={lookups.reload}
        />
      </>
    );
  }
  const { roles, profiles } = lookups.data;
  const sources = profiles.flatMap((profile) =>
    profile.versions
      .filter((version) => version.lifecycleState !== 'RETIRED')
      .map((version) => ({ profile, version })),
  );
  const chosenRoleId =
    baseRoleId !== ''
      ? baseRoleId
      : (sources.find(({ version }) => version.id === sourceVersionId)?.profile.baseRoleId ?? '');

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    const valid = save.validate({
      code: checkText(code, { required: true, pattern: CODE_PATTERN }),
      'name.ar': checkText(nameAr, { required: true }),
      'name.en': checkText(nameEn, { required: true }),
      baseRoleId: chosenRoleId === '' ? 'REQUIRED' : null,
    });
    if (!valid) {
      return;
    }
    const result = await save.run(() =>
      permissionProfilesApi.create({
        code: code.trim(),
        name: { ar: nameAr.trim(), en: nameEn.trim() },
        baseRoleId: chosenRoleId,
        sourceVersionId: sourceVersionId === '' ? null : sourceVersionId,
      }),
    );
    if (result.ok) {
      void navigate(versionPath(result.value.id));
    }
  };

  return (
    <>
      {header}
      <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
        <FormAlert message={save.formError} />
        <TextField
          label={t('identityAccess.profiles.fields.code')}
          name="code"
          required
          dir="ltr"
          hint={t('identityAccess.codeHint')}
          value={code}
          onChange={setCode}
          error={save.fieldErrors.code}
        />
        <TextField
          label={t('identityAccess.profiles.fields.nameAr')}
          name="name.ar"
          required
          dir="rtl"
          value={nameAr}
          onChange={setNameAr}
          error={save.fieldErrors['name.ar']}
        />
        <TextField
          label={t('identityAccess.profiles.fields.nameEn')}
          name="name.en"
          required
          dir="ltr"
          value={nameEn}
          onChange={setNameEn}
          error={save.fieldErrors['name.en']}
        />
        <SelectField
          label={t('identityAccess.profiles.fields.source')}
          name="sourceVersionId"
          value={sourceVersionId}
          placeholder={t('identityAccess.profiles.fields.noSource')}
          hint={t('identityAccess.profiles.fields.sourceHint')}
          options={sources.map(({ profile, version }) => ({
            value: version.id,
            label: t('identityAccess.profiles.sourceLabel', {
              profile: `${profile.baseRoleCode} — ${labelOf(profile.name, language)}`,
              version: version.versionNo,
              state: t(`identityAccess.lifecycleState.${version.lifecycleState}`),
            }),
          }))}
          onChange={setSourceVersionId}
          error={save.fieldErrors.sourceVersionId}
        />
        <SelectField
          label={t('identityAccess.profiles.fields.baseRole')}
          name="baseRoleId"
          required
          value={chosenRoleId}
          placeholder={t('identityAccess.profiles.fields.chooseRole')}
          hint={t('identityAccess.profiles.fields.baseRoleHint')}
          options={roles.map((role) => ({
            value: role.id,
            label: `${role.code} — ${labelOf(role.name, language)}`,
          }))}
          onChange={setBaseRoleId}
          error={save.fieldErrors.baseRoleId}
        />
        <div className="form__actions">
          <button type="submit" className="button button--primary" disabled={save.saving}>
            {save.saving ? t('common.states.saving') : t('identityAccess.profiles.create.submit')}
          </button>
          <Link className="button" to="/admin/permission-matrix">
            {t('common.actions.cancel')}
          </Link>
        </div>
      </form>
    </>
  );
}

/**
 * ADM-008: one version of a profile — its grid against the base role's shipped default, edited while DRAFT by its author, and the
 * governed steps the caller may take now.
 */
export function PermissionProfileVersionPage(): ReactElement {
  const { t } = useI18n();
  const { versionId = '' } = useParams();
  const load = useCallback(
    async (signal: AbortSignal) => {
      const [version, permissions, profiles] = await Promise.all([
        permissionProfilesApi.getVersion(versionId, signal),
        rolesApi.listPermissions(signal),
        loadProfiles(signal),
      ]);
      return { version, permissions, profiles };
    },
    [versionId],
  );
  const resource = useApiResource(load);

  if (resource.loading) {
    return <LoadingState />;
  }
  if (resource.error !== null || resource.data === undefined) {
    return (
      <ErrorState
        error={resource.error}
        message={problemMessage(resource.error, t)}
        onRetry={resource.reload}
      />
    );
  }
  const { version, permissions, profiles } = resource.data;
  const profile = profiles.find((candidate) => candidate.id === version.data.permissionProfileId);
  if (profile === undefined) {
    return <ErrorState message={t('common.problems.notFound')} onRetry={resource.reload} />;
  }
  return (
    <VersionEditor
      key={`${version.data.id}:${version.etag ?? ''}`}
      version={version.data}
      etag={version.etag}
      profile={profile}
      permissions={permissions}
      shippedDefault={shippedDefaultOf(profiles, profile.baseRoleId)}
      onChanged={resource.reload}
    />
  );
}

interface VersionEditorProps {
  version: PermissionProfileVersion;
  etag: string | null;
  profile: PermissionProfileDetail;
  permissions: PermissionSummary[];
  shippedDefault: PermissionProfileVersion | undefined;
  onChanged: () => void;
}

function VersionEditor({
  version,
  etag,
  profile,
  permissions,
  shippedDefault,
  onChanged,
}: VersionEditorProps): ReactElement {
  const { t, language } = useI18n();
  const navigate = useNavigate();
  const { session } = useSession();
  const canManage = useCan('ROLE_MANAGE');
  const canAssign = useCan('ROLE_ASSIGN');
  const nameOf = usePersonNames([
    version.createdBy,
    version.validatedByUserId,
    version.publishedByUserId,
  ]);
  const save = useSaveAction();
  const [grid, setGrid] = useState<Grid>(() => gridOf(version.grants));
  const [summary, setSummary] = useState(version.changeSummary?.text ?? '');
  const [differencesOnly, setDifferencesOnly] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  const editable =
    canManage && version.lifecycleState === 'DRAFT' && version.createdBy === session?.user.id;
  const baseline =
    shippedDefault === undefined || shippedDefault.id === version.id
      ? undefined
      : gridOf(shippedDefault.grants);
  const changed = baseline === undefined ? new Set<string>() : differences(grid, baseline);
  // A refusal names a grant by its index in the grid sent, which is in catalogue order.
  const sentIndex = new Map(
    grantsOf(grid, permissions).map((grant, index) => [grant.permissionCode, index]),
  );
  const shown = differencesOnly
    ? permissions.filter((permission) => changed.has(permission.code))
    : permissions;
  const isDirty =
    differences(grid, gridOf(version.grants)).size > 0 ||
    summary.trim() !== (version.changeSummary?.text ?? '');

  const setScope = (code: string, value: string) => {
    const next = new Map(grid);
    if (value === '') {
      next.delete(code);
    } else {
      next.set(code, value as DataScope);
    }
    setGrid(next);
  };

  const saveGrid = async () => {
    const text = summary.trim();
    const result = await save.run(() =>
      permissionProfilesApi.updateGrants(
        version.id,
        {
          grants: grantsOf(grid, permissions),
          changeSummary: text === '' ? null : { text, language },
        },
        etag,
      ),
    );
    if (result.ok) {
      onChanged();
    }
  };

  const take = async (transition: PermissionProfileTransition) => {
    const result = await save.run(() =>
      permissionProfilesApi.transition(version.id, transition, etag),
    );
    if (result.ok) {
      onChanged();
    }
  };

  const newVersion = async () => {
    const result = await save.run(() =>
      permissionProfilesApi.createVersion(profile.id, version.id),
    );
    if (result.ok) {
      void navigate(versionPath(result.value.id));
    }
  };

  const migrate = async () => {
    const result = await save.run(() => permissionProfilesApi.migrateAssignments(version.id));
    if (result.ok) {
      setNotice(
        t('identityAccess.profiles.version.migrated', { count: result.value.migratedAssignments }),
      );
    }
  };

  const state = version.lifecycleState;
  return (
    <>
      <PageHeader
        title={`${profile.baseRoleCode} — ${labelOf(profile.name, language)}`}
        description={t('identityAccess.profiles.version.description')}
        actions={
          <Link
            className="button"
            to={`/admin/permission-matrix?profileId=${profile.id}&versionId=${version.id}`}
          >
            {t('identityAccess.nav.permissionMatrix')}
          </Link>
        }
      />
      <dl className="details">
        <div className="details__row">
          <dt>{t('identityAccess.permissionMatrix.version')}</dt>
          <dd>
            {t('identityAccess.permissionMatrix.versionLabel', {
              version: version.versionNo,
              state: t(`identityAccess.lifecycleState.${state}`),
            })}
          </dd>
        </div>
        <div className="details__row">
          <dt>{t('identityAccess.profiles.fields.author')}</dt>
          <dd>{nameOf(version.createdBy)}</dd>
        </div>
        {version.validatedByUserId !== null && (
          <div className="details__row">
            <dt>{t('identityAccess.profiles.fields.reviewer')}</dt>
            <dd>{nameOf(version.validatedByUserId)}</dd>
          </div>
        )}
        {version.publishedByUserId !== null && (
          <div className="details__row">
            <dt>{t('identityAccess.profiles.fields.publisher')}</dt>
            <dd>{nameOf(version.publishedByUserId)}</dd>
          </div>
        )}
      </dl>

      <p className="form__note">{t(`identityAccess.profiles.version.next.${state}`)}</p>
      <FormAlert message={save.formError} />
      {notice !== null && (
        <p className="form__note" role="status">
          {notice}
        </p>
      )}

      {canManage && (
        <div className="form__actions">
          {editable && (
            <button
              type="button"
              className="button button--primary"
              disabled={save.saving || !isDirty}
              onClick={() => void saveGrid()}
            >
              {save.saving ? t('common.states.saving') : t('identityAccess.profiles.version.save')}
            </button>
          )}
          {state === 'DRAFT' && !isDirty && (
            <button
              type="button"
              className="button"
              disabled={save.saving}
              onClick={() => void take('validate')}
            >
              {t('identityAccess.profiles.version.validate')}
            </button>
          )}
          {state === 'VALIDATED' && (
            <button
              type="button"
              className="button"
              disabled={save.saving}
              onClick={() => void take('publish')}
            >
              {t('identityAccess.profiles.version.publish')}
            </button>
          )}
          {state !== 'RETIRED' && (
            <button
              type="button"
              className="button"
              disabled={save.saving}
              onClick={() => void take('retire')}
            >
              {state === 'PUBLISHED'
                ? t('identityAccess.profiles.version.withdraw')
                : t('identityAccess.profiles.version.abandon')}
            </button>
          )}
          {state === 'PUBLISHED' && (
            <button
              type="button"
              className="button"
              disabled={save.saving}
              onClick={() => void newVersion()}
            >
              {t('identityAccess.profiles.version.newVersion')}
            </button>
          )}
          {state === 'PUBLISHED' && canAssign && (
            <button
              type="button"
              className="button"
              disabled={save.saving}
              onClick={() => void migrate()}
            >
              {t('identityAccess.profiles.version.migrate')}
            </button>
          )}
          <Link
            className="button"
            to={`/admin/permission-profiles/new?sourceVersionId=${version.id}`}
          >
            {t('identityAccess.profiles.version.clone')}
          </Link>
        </div>
      )}
      {canManage && state !== 'DRAFT' && state !== 'RETIRED' && (
        <p className="form__note">{t('identityAccess.stepUpNote')}</p>
      )}

      {editable ? (
        <TextAreaField
          label={t('identityAccess.profiles.fields.changeSummary')}
          name="changeSummary.text"
          value={summary}
          onChange={setSummary}
          rows={2}
          maxLength={2000}
          error={save.fieldErrors['changeSummary.text']}
        />
      ) : (
        version.changeSummary !== null && (
          <p className="form__readonly">
            <span className="field__label">
              {t('identityAccess.profiles.fields.changeSummary')}
            </span>
            <span lang={version.changeSummary.language}>{version.changeSummary.text}</span>
          </p>
        )
      )}

      {baseline !== undefined && (
        <CheckboxField
          label={t('identityAccess.profiles.version.differencesOnly', { count: changed.size })}
          name="differencesOnly"
          checked={differencesOnly}
          onChange={setDifferencesOnly}
        />
      )}

      <TableContainer caption={t('identityAccess.profiles.version.caption')}>
        <thead>
          <tr>
            <th scope="col">{t('identityAccess.permissionMatrix.permission')}</th>
            <th scope="col">{t('identityAccess.profiles.version.thisVersion')}</th>
            {baseline !== undefined && (
              <th scope="col">{t('identityAccess.profiles.version.shippedDefault')}</th>
            )}
          </tr>
        </thead>
        {groupByPermissionGroup(shown).map(([group, members]) => (
          <tbody key={group}>
            <tr className="matrix__group">
              <th scope="rowgroup" colSpan={baseline === undefined ? 2 : 3}>
                {group}
              </th>
            </tr>
            {members.map((permission) => {
              const scope = grid.get(permission.code);
              const index = sentIndex.get(permission.code);
              const fieldError =
                index === undefined
                  ? undefined
                  : save.fieldErrors[`grants[${String(index)}].permissionCode`];
              return (
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
                    {changed.has(permission.code) && (
                      <StatusBadge
                        label={t('identityAccess.profiles.version.changed')}
                        tone="warning"
                      />
                    )}
                  </th>
                  <td className="matrix__cell">
                    {editable ? (
                      <SelectField
                        label={t('identityAccess.profiles.version.scopeOf', {
                          permission: permission.code,
                        })}
                        name={`scope.${permission.code}`}
                        value={scope ?? ''}
                        placeholder={t('identityAccess.permissionMatrix.notGranted')}
                        options={DATA_SCOPES.map((value) => ({
                          value,
                          label: t(`identityAccess.dataScope.${value}`),
                        }))}
                        onChange={(value) => {
                          setScope(permission.code, value);
                        }}
                        error={fieldError}
                      />
                    ) : (
                      <ScopeText scope={scope} />
                    )}
                  </td>
                  {baseline !== undefined && (
                    <td className="matrix__cell">
                      <ScopeText scope={baseline.get(permission.code)} />
                    </td>
                  )}
                </tr>
              );
            })}
          </tbody>
        ))}
      </TableContainer>
    </>
  );
}

function ScopeText({ scope }: { scope: DataScope | undefined }): ReactElement {
  const { t } = useI18n();
  return scope === undefined ? (
    <>
      <span aria-hidden="true">—</span>
      <span className="visually-hidden">{t('identityAccess.permissionMatrix.notGranted')}</span>
    </>
  ) : (
    <>{t(`identityAccess.dataScope.${scope}`)}</>
  );
}
