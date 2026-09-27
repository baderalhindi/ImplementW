import { type SyntheticEvent, type ReactElement, useCallback, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { rolesApi } from '../api/identityAccessApi.ts';
import { type RoleDetail } from '../api/types.ts';
import { checkText, useFocusFirstError, useSaveAction } from '../forms.ts';
import { labelOf } from '../lookups.ts';
import { problemMessage } from '../problems.ts';

// Roles are the canonical R01–R08: shipped, never created or deleted here (record D-2, ERD F-080). What a role may
// do is a permission profile (ADR-018), so ADM-008 edits the role's bilingual name and points to its profiles.

/** ADM-006 Roles. */
export function RoleListPage(): ReactElement {
  const { t, language } = useI18n();
  const load = useCallback((signal: AbortSignal) => rolesApi.list(signal), []);
  const roles = useApiResource(load);

  return (
    <>
      <PageHeader
        title={t('identityAccess.roles.list.title')}
        description={t('identityAccess.roles.list.description')}
        actions={
          <Link className="button" to="/admin/permission-matrix">
            {t('identityAccess.nav.permissionMatrix')}
          </Link>
        }
      />
      {roles.loading && <LoadingState />}
      {roles.error !== null && (
        <ErrorState message={problemMessage(roles.error, t)} onRetry={roles.reload} />
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
                </tr>
              ))}
            </tbody>
          </TableContainer>
        ))}
    </>
  );
}

/** ADM-007 Role Detail: the role and the permission profiles rooted at it. */
export function RoleDetailPage(): ReactElement {
  const { t, language, formatDateTime } = useI18n();
  const { roleId = '' } = useParams();
  const load = useCallback((signal: AbortSignal) => rolesApi.get(roleId, signal), [roleId]);
  const role = useApiResource(load);

  if (role.loading) {
    return <LoadingState />;
  }
  if (role.error !== null || role.data === undefined) {
    return <ErrorState message={problemMessage(role.error, t)} onRetry={role.reload} />;
  }
  const detail = role.data.data;

  return (
    <>
      <PageHeader
        title={`${detail.code} — ${labelOf(detail.name, language)}`}
        actions={
          <Link className="button" to={`/admin/roles/${detail.id}/edit`}>
            {t('common.actions.edit')}
          </Link>
        }
      />
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
    </>
  );
}

/** ADM-008 Create/Edit Role: the canonical role's bilingual name. New roles are permission profiles (ADR-018). */
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
        <ErrorState message={problemMessage(role.error, t)} onRetry={role.reload} />
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
        <Link className="button" to={`/admin/roles/${role.id}`}>
          {t('common.actions.cancel')}
        </Link>
      </div>
    </form>
  );
}
