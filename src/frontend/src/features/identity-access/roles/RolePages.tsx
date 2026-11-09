import { type SyntheticEvent, type ReactElement, useCallback, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { CheckboxField, SelectField, TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';
import { CancelLink } from '@/shared/ui/CancelLink.tsx';

import { rolesApi } from '../api/identityAccessApi.ts';
import {
  type RoleDetail,
  type RoleLandingDashboard,
  type RoleScope,
  type RoleSummary,
} from '../api/types.ts';
import { ConfirmDialog, type Confirmation } from '../components/ConfirmDialog.tsx';
import { useRowActions } from '../components/RowActions.tsx';
import {
  checkText,
  ROLE_CODE_LENGTH,
  ROLE_CODE_PATTERN,
  useFocusFirstError,
  useSaveAction,
} from '../forms.ts';
import { labelOf } from '../lookups.ts';
import { useCan } from '../permissions/permissions.ts';
import { problemMessage } from '../problems.ts';
import { versionPath } from './profileGrid.ts';

// R01–R08 are shipped and never deleted (record D-2, ERD F-080); the 5 Nov 2026 review §6.2 adds created roles beside them. What a role
// may do is a permission profile (ADR-018); the role itself holds its names, allowed scopes, landing dashboard and, when created, its
// active state.

const ROLE_SCOPES: readonly RoleScope[] = ['ALL', 'DEPARTMENT', 'PROJECT', 'ENTITY'];
const LANDINGS: readonly RoleLandingDashboard[] = ['PORTFOLIO', 'GOVERNANCE', 'PROJECT'];

function landingOf(value: string): RoleLandingDashboard | null {
  return LANDINGS.find((landing) => landing === value) ?? null;
}

function ScopesText({ scopes }: { scopes: readonly RoleScope[] | null }): ReactElement {
  const { t } = useI18n();
  return (
    <>
      {(scopes ?? []).length === 0
        ? t('common.values.none')
        : (scopes ?? [])
            .map((scope) => t(`identityAccess.roles.scope.${scope}`))
            .join(t('identityAccess.roles.scopeSeparator'))}
    </>
  );
}

function RoleStateBadge({ active }: { active: boolean }): ReactElement {
  const { t } = useI18n();
  return (
    <StatusBadge
      label={t(
        active ? 'identityAccess.roles.state.active' : 'identityAccess.roles.state.inactive',
      )}
      tone={active ? 'positive' : 'neutral'}
    />
  );
}

function landingLabelKey(landing: RoleLandingDashboard | null) {
  return landing === null
    ? ('identityAccess.roles.landing.none' as const)
    : (`identityAccess.roles.landing.${landing}` as const);
}

interface RoleSettingsFieldsProps {
  scopes: readonly RoleScope[];
  landing: RoleLandingDashboard | null;
  onScopesChange: (scopes: RoleScope[]) => void;
  onLandingChange: (landing: RoleLandingDashboard | null) => void;
  scopesError: string | undefined;
  landingError: string | undefined;
}

/** A role's allowed scopes and landing dashboard, as a new role and the settings dialog both ask for them. */
function RoleSettingsFields({
  scopes,
  landing,
  onScopesChange,
  onLandingChange,
  scopesError,
  landingError,
}: RoleSettingsFieldsProps): ReactElement {
  const { t } = useI18n();
  return (
    <>
      <fieldset
        className="field field--group"
        aria-invalid={scopesError === undefined ? undefined : true}
      >
        <legend className="field__label">
          {t('identityAccess.roles.fields.allowedScopes')}
          <span className="field__required">{` (${t('common.form.required')})`}</span>
        </legend>
        <p className="field__hint">{t('identityAccess.roles.settings.scopesHint')}</p>
        {ROLE_SCOPES.map((scope) => (
          <CheckboxField
            key={scope}
            label={t(`identityAccess.roles.scope.${scope}`)}
            name={`allowedScopes.${scope}`}
            checked={scopes.includes(scope)}
            onChange={(checked) => {
              onScopesChange(
                ROLE_SCOPES.filter((candidate) =>
                  candidate === scope ? checked : scopes.includes(candidate),
                ),
              );
            }}
          />
        ))}
        {scopesError !== undefined && <p className="field__error">{scopesError}</p>}
      </fieldset>
      <SelectField
        label={t('identityAccess.roles.fields.landing')}
        name="landingDashboardCode"
        value={landing ?? ''}
        placeholder={t('identityAccess.roles.landing.none')}
        options={LANDINGS.map((value) => ({
          value,
          label: t(`identityAccess.roles.landing.${value}`),
        }))}
        onChange={(value) => {
          onLandingChange(landingOf(value));
        }}
        error={landingError}
      />
    </>
  );
}

/** ADM-006 Roles. */
export function RoleListPage(): ReactElement {
  const { t, language } = useI18n();
  const mayManage = useCan('ROLE_MANAGE');
  const load = useCallback((signal: AbortSignal) => rolesApi.list(signal), []);
  const roles = useApiResource(load);
  // The eight built-in roles are edited but never deleted: approvals, the matrix and every assignment name them.
  const rowActions = useRowActions<RoleSummary>({
    permission: 'ROLE_MANAGE',
    name: (role) => labelOf(role.name, language),
    editTo: (role) => `/admin/roles/${role.id}/edit`,
    deletable: (role) => !role.isSystem,
    remove: (role) => rolesApi.delete(role.id, null),
    onDeleted: () => {
      roles.reload();
    },
  });

  return (
    <>
      <PageHeader
        title={t('identityAccess.roles.list.title')}
        description={t('identityAccess.roles.list.description')}
        actions={
          <>
            <Link className="button" to="/admin/permission-matrix">
              {t('identityAccess.nav.permissionMatrix')}
            </Link>
            {mayManage && (
              <Link className="button button--primary" to="/admin/roles/new">
                {t('identityAccess.roles.list.create')}
              </Link>
            )}
          </>
        }
      />
      {roles.loading && <LoadingState />}
      {roles.error !== null && (
        <ErrorState
          error={roles.error}
          message={problemMessage(roles.error, t)}
          onRetry={roles.reload}
        />
      )}
      {roles.data !== undefined &&
        (roles.data.length === 0 ? (
          <EmptyState title={t('identityAccess.roles.list.empty')} />
        ) : (
          <TableContainer caption={t('identityAccess.roles.list.title')}>
            <thead>
              <tr>
                <th scope="col">{t('identityAccess.roles.fields.code')}</th>
                <th scope="col">{t('identityAccess.roles.fields.name')}</th>
                <th scope="col">{t('identityAccess.roles.fields.externalEligible')}</th>
                <th scope="col">{t('identityAccess.roles.fields.state')}</th>
                <th scope="col">{t('identityAccess.roles.fields.allowedScopes')}</th>
                <th scope="col">{t('identityAccess.roles.fields.landing')}</th>
                {rowActions !== undefined && <th scope="col">{t('common.table.actions')}</th>}
              </tr>
            </thead>
            <tbody>
              {roles.data.map((role) => (
                <tr key={role.id}>
                  <td>{role.code}</td>
                  <td>
                    <Link to={`/admin/roles/${role.id}`}>{labelOf(role.name, language)}</Link>
                  </td>
                  <td>
                    {role.isExternalEligible ? t('common.values.yes') : t('common.values.no')}
                  </td>
                  <td>
                    <RoleStateBadge active={role.isActive} />
                  </td>
                  <td>
                    <ScopesText scopes={role.allowedScopes} />
                  </td>
                  <td>{t(landingLabelKey(role.landingDashboardCode))}</td>
                  {rowActions !== undefined && (
                    <td className="cell--actions">{rowActions.render(role)}</td>
                  )}
                </tr>
              ))}
            </tbody>
          </TableContainer>
        ))}
      {rowActions?.dialog}
    </>
  );
}

/** A new role (the 5 Nov 2026 review §6.2), empty or copying a role; its draft profile opens next, to be validated and published. */
export function RoleCreatePage(): ReactElement {
  const { t } = useI18n();
  const load = useCallback((signal: AbortSignal) => rolesApi.list(signal), []);
  const roles = useApiResource(load);
  return (
    <>
      <PageHeader
        title={t('identityAccess.roles.create.title')}
        description={t('identityAccess.roles.create.description')}
      />
      {roles.loading && <LoadingState />}
      {roles.error !== null && (
        <ErrorState
          error={roles.error}
          message={problemMessage(roles.error, t)}
          onRetry={roles.reload}
        />
      )}
      {roles.data !== undefined && <RoleCreateForm roles={roles.data} />}
    </>
  );
}

function RoleCreateForm({ roles }: { roles: RoleSummary[] }): ReactElement {
  const { t, language } = useI18n();
  const navigate = useNavigate();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction();
  useFocusFirstError(formRef, save.fieldErrors);
  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [sourceRoleId, setSourceRoleId] = useState('');
  const [externalEligible, setExternalEligible] = useState(false);
  const [scopes, setScopes] = useState<RoleScope[]>([]);
  const [landing, setLanding] = useState<RoleLandingDashboard | null>(null);

  const chooseSource = (id: string) => {
    setSourceRoleId(id);
    const source = roles.find((role) => role.id === id);
    if (source !== undefined) {
      setExternalEligible(source.isExternalEligible);
      setScopes(source.allowedScopes ?? []);
      setLanding(source.landingDashboardCode);
    }
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    const valid = save.validate({
      code: checkText(code, {
        required: true,
        maxLength: ROLE_CODE_LENGTH,
        pattern: ROLE_CODE_PATTERN,
      }),
      nameAr: checkText(nameAr, { required: true }),
      nameEn: checkText(nameEn, { required: true }),
      allowedScopes: scopes.length === 0 ? 'REQUIRED' : null,
    });
    if (!valid) {
      return;
    }
    const result = await save.run(() =>
      rolesApi.create({
        code,
        nameAr: nameAr.trim(),
        nameEn: nameEn.trim(),
        isExternalEligible: externalEligible,
        allowedScopes: scopes,
        sourceRoleId: sourceRoleId === '' ? null : sourceRoleId,
        landingDashboardCode: landing,
      }),
    );
    if (!result.ok) {
      return;
    }
    // The role exists from here on, so a failed read of its draft leads to the role, not to a second attempt at creating it.
    const created = result.value;
    const draftProfile = created.profiles[0];
    try {
      const profile =
        draftProfile === undefined ? undefined : await rolesApi.getProfile(draftProfile.id);
      const draft = profile?.versions[0];
      void navigate(draft === undefined ? `/admin/roles/${created.id}` : versionPath(draft.id));
    } catch {
      void navigate(`/admin/roles/${created.id}`);
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={save.formError} />
      <TextField
        label={t('identityAccess.roles.fields.code')}
        name="code"
        required
        dir="ltr"
        autoComplete="off"
        hint={t('identityAccess.roles.create.codeHint')}
        value={code}
        onChange={setCode}
        error={save.fieldErrors.code}
      />
      <TextField
        label={t('identityAccess.roles.fields.nameAr')}
        name="nameAr"
        required
        dir="rtl"
        value={nameAr}
        onChange={setNameAr}
        error={save.fieldErrors.nameAr}
      />
      <TextField
        label={t('identityAccess.roles.fields.nameEn')}
        name="nameEn"
        required
        dir="ltr"
        value={nameEn}
        onChange={setNameEn}
        error={save.fieldErrors.nameEn}
      />
      <SelectField
        label={t('identityAccess.roles.create.source')}
        name="sourceRoleId"
        value={sourceRoleId}
        placeholder={t('identityAccess.roles.create.sourceNone')}
        hint={t('identityAccess.roles.create.sourceHint')}
        options={roles.map((role) => ({
          value: role.id,
          label: `${role.code} — ${labelOf(role.name, language)}`,
        }))}
        onChange={chooseSource}
        error={save.fieldErrors.sourceRoleId}
      />
      <CheckboxField
        label={t('identityAccess.roles.fields.externalEligible')}
        name="isExternalEligible"
        checked={externalEligible}
        onChange={setExternalEligible}
      />
      <RoleSettingsFields
        scopes={scopes}
        landing={landing}
        onScopesChange={setScopes}
        onLandingChange={setLanding}
        scopesError={save.fieldErrors.allowedScopes}
        landingError={save.fieldErrors.landingDashboardCode}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('identityAccess.roles.create.submit')}
        </button>
        <CancelLink fallback="/admin/roles" />
      </div>
    </form>
  );
}

function RoleSettingsDialog({
  open,
  role,
  etag,
  onClose,
  onSaved,
}: {
  open: boolean;
  role: RoleDetail;
  etag: string | null;
  onClose: () => void;
  onSaved: () => void;
}): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={open} title={t('identityAccess.roles.settings.title')} onClose={onClose}>
      <RoleSettingsForm role={role} etag={etag} onClose={onClose} onSaved={onSaved} />
    </Dialog>
  );
}

function RoleSettingsForm({
  role,
  etag,
  onClose,
  onSaved,
}: {
  role: RoleDetail;
  etag: string | null;
  onClose: () => void;
  onSaved: () => void;
}): ReactElement {
  const { t } = useI18n();
  const save = useSaveAction();
  const [scopes, setScopes] = useState<RoleScope[]>(role.allowedScopes ?? []);
  const [landing, setLanding] = useState(role.landingDashboardCode);
  const [showCreate, setShowCreate] = useState(role.showCreateInHeader !== false);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!save.validate({ allowedScopes: scopes.length === 0 ? 'REQUIRED' : null })) {
      return;
    }
    const result = await save.run(() =>
      rolesApi.updateSettings(
        role.id,
        { allowedScopes: scopes, landingDashboardCode: landing, showCreateInHeader: showCreate },
        etag,
      ),
    );
    if (result.ok) {
      onSaved();
    }
  };

  return (
    <form className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={save.formError} />
      <RoleSettingsFields
        scopes={scopes}
        landing={landing}
        onScopesChange={setScopes}
        onLandingChange={setLanding}
        scopesError={save.fieldErrors.allowedScopes}
        landingError={save.fieldErrors.landingDashboardCode}
      />
      <label className="field__option">
        <input
          type="checkbox"
          name="showCreateInHeader"
          checked={showCreate}
          onChange={(event) => {
            setShowCreate(event.target.checked);
          }}
        />
        <span>{t('identityAccess.roles.fields.showCreateInHeader')}</span>
      </label>
      <p className="field__hint">{t('identityAccess.roles.showCreateHint')}</p>
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('common.actions.save')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}

/** ADM-007 Role Detail: the role, its settings and the permission profiles rooted at it; a created role can be retired or deleted. */
export function RoleDetailPage(): ReactElement {
  const { t, language, formatDateTime } = useI18n();
  const { roleId = '' } = useParams();
  const navigate = useNavigate();
  const load = useCallback((signal: AbortSignal) => rolesApi.get(roleId, signal), [roleId]);
  const role = useApiResource(load);
  const mayManage = useCan('ROLE_MANAGE');
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [saved, setSaved] = useState(false);
  const [confirmation, setConfirmation] = useState<(Confirmation & { onDone: () => void }) | null>(
    null,
  );

  if (role.loading) {
    return <LoadingState />;
  }
  if (role.error !== null || role.data === undefined) {
    return (
      <ErrorState
        error={role.error}
        message={problemMessage(role.error, t)}
        onRetry={role.reload}
      />
    );
  }
  const detail = role.data.data;
  const { etag } = role.data;
  const name = labelOf(detail.name, language);
  const reload = () => {
    setConfirmation(null);
    role.reload();
  };

  return (
    <>
      <PageHeader
        title={`${detail.code} — ${name}`}
        actions={
          <>
            <Link className="button" to={`/admin/roles/${detail.id}/edit`}>
              {t('common.actions.edit')}
            </Link>
            {mayManage && (
              <button
                type="button"
                className="button"
                onClick={() => {
                  setSettingsOpen(true);
                }}
              >
                {t('identityAccess.roles.settings.open')}
              </button>
            )}
            {mayManage && !detail.isSystem && (
              <>
                <button
                  type="button"
                  className="button"
                  onClick={() => {
                    setConfirmation(
                      detail.isActive
                        ? {
                            title: t('identityAccess.roles.actions.deactivateTitle', { name }),
                            body: t('identityAccess.roles.actions.deactivateBody'),
                            confirmLabel: t('identityAccess.roles.actions.deactivate'),
                            destructive: true,
                            action: () => rolesApi.deactivate(detail.id, etag),
                            onDone: reload,
                          }
                        : {
                            title: t('identityAccess.roles.actions.reactivateTitle', { name }),
                            body: t('identityAccess.roles.actions.reactivateBody'),
                            confirmLabel: t('identityAccess.roles.actions.reactivate'),
                            destructive: false,
                            action: () => rolesApi.reactivate(detail.id, etag),
                            onDone: reload,
                          },
                    );
                  }}
                >
                  {detail.isActive
                    ? t('identityAccess.roles.actions.deactivate')
                    : t('identityAccess.roles.actions.reactivate')}
                </button>
                <button
                  type="button"
                  className="button button--danger"
                  onClick={() => {
                    setConfirmation({
                      title: t('identityAccess.roles.actions.deleteTitle', { name }),
                      body: t('identityAccess.roles.actions.deleteBody'),
                      confirmLabel: t('identityAccess.roles.actions.delete'),
                      destructive: true,
                      action: () => rolesApi.delete(detail.id, etag),
                      onDone: () => {
                        void navigate('/admin/roles');
                      },
                    });
                  }}
                >
                  {t('identityAccess.roles.actions.delete')}
                </button>
              </>
            )}
          </>
        }
      />
      {saved && (
        <p className="notice" role="status">
          {t('identityAccess.roles.settings.saved')}
        </p>
      )}
      <dl className="details">
        <div className="details__row">
          <dt>{t('identityAccess.roles.fields.nameAr')}</dt>
          <dd lang="ar" dir="rtl">
            {detail.name.ar}
          </dd>
        </div>
        <div className="details__row">
          <dt>{t('identityAccess.roles.fields.nameEn')}</dt>
          <dd lang="en" dir="ltr">
            {detail.name.en}
          </dd>
        </div>
        <div className="details__row">
          <dt>{t('identityAccess.roles.fields.externalEligible')}</dt>
          <dd>{detail.isExternalEligible ? t('common.values.yes') : t('common.values.no')}</dd>
        </div>
        <div className="details__row">
          <dt>{t('identityAccess.roles.fields.state')}</dt>
          <dd>
            <RoleStateBadge active={detail.isActive} />
          </dd>
        </div>
        <div className="details__row">
          <dt>{t('identityAccess.roles.fields.allowedScopes')}</dt>
          <dd>
            <ScopesText scopes={detail.allowedScopes} />
          </dd>
        </div>
        <div className="details__row">
          <dt>{t('identityAccess.roles.fields.landing')}</dt>
          <dd>{t(landingLabelKey(detail.landingDashboardCode))}</dd>
        </div>
        <div className="details__row">
          <dt>{t('identityAccess.roles.fields.showCreateInHeader')}</dt>
          <dd>
            {t(detail.showCreateInHeader === false ? 'common.values.no' : 'common.values.yes')}
          </dd>
        </div>
        <div className="details__row">
          <dt>{t('identityAccess.users.fields.updatedAt')}</dt>
          <dd>{formatDateTime(detail.updatedAt)}</dd>
        </div>
      </dl>

      <section className="section" aria-labelledby="profiles-heading">
        <h2 id="profiles-heading">{t('identityAccess.roles.detail.profiles')}</h2>
        <p className="form__note">{t('identityAccess.roles.detail.profilesNote')}</p>
        {detail.profiles.length === 0 ? (
          <EmptyState title={t('identityAccess.roles.detail.noProfiles')} />
        ) : (
          <ul className="link-list">
            {detail.profiles.map((profile) => (
              <li key={profile.id}>
                <Link to={`/admin/permission-matrix?profileId=${profile.id}`}>
                  {labelOf(profile.name, language)}
                </Link>{' '}
                <span dir="ltr">({profile.code})</span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <RoleSettingsDialog
        open={settingsOpen}
        role={detail}
        etag={etag}
        onClose={() => {
          setSettingsOpen(false);
        }}
        onSaved={() => {
          setSettingsOpen(false);
          setSaved(true);
          role.reload();
        }}
      />
      <ConfirmDialog
        confirmation={confirmation}
        onClose={() => {
          setConfirmation(null);
        }}
        onDone={() => {
          confirmation?.onDone();
        }}
      />
    </>
  );
}

/** ADM-008 Edit Role: the role's bilingual name. */
export function RoleEditPage(): ReactElement {
  const { t } = useI18n();
  const { roleId = '' } = useParams();
  const load = useCallback((signal: AbortSignal) => rolesApi.get(roleId, signal), [roleId]);
  const role = useApiResource(load);

  return (
    <>
      <PageHeader
        title={t('identityAccess.roles.edit.title')}
        description={t('identityAccess.roles.edit.description')}
      />
      {role.loading && <LoadingState />}
      {role.error !== null && (
        <ErrorState
          error={role.error}
          message={problemMessage(role.error, t)}
          onRetry={role.reload}
        />
      )}
      {role.data !== undefined && <RoleNameForm role={role.data.data} etag={role.data.etag} />}
    </>
  );
}

function RoleNameForm({ role, etag }: { role: RoleDetail; etag: string | null }): ReactElement {
  const { t } = useI18n();
  const navigate = useNavigate();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction();
  useFocusFirstError(formRef, save.fieldErrors);
  const [nameAr, setNameAr] = useState(role.name.ar);
  const [nameEn, setNameEn] = useState(role.name.en);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    const valid = save.validate({
      'name.ar': checkText(nameAr, { required: true }),
      'name.en': checkText(nameEn, { required: true }),
    });
    if (!valid) {
      return;
    }
    const result = await save.run(() =>
      rolesApi.update(role.id, { ar: nameAr.trim(), en: nameEn.trim() }, etag),
    );
    if (result.ok) {
      void navigate(`/admin/roles/${role.id}`);
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={save.formError} />
      <p className="form__readonly">
        <span className="field__label">{t('identityAccess.roles.fields.code')}</span>
        {role.code}
      </p>
      <TextField
        label={t('identityAccess.roles.fields.nameAr')}
        name="name.ar"
        required
        dir="rtl"
        value={nameAr}
        onChange={setNameAr}
        error={save.fieldErrors['name.ar']}
      />
      <TextField
        label={t('identityAccess.roles.fields.nameEn')}
        name="name.en"
        required
        dir="ltr"
        value={nameEn}
        onChange={setNameEn}
        error={save.fieldErrors['name.en']}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('common.actions.save')}
        </button>
        <CancelLink fallback={`/admin/roles/${role.id}`} />
      </div>
    </form>
  );
}
