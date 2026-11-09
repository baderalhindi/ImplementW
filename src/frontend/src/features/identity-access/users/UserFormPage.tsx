import { type SyntheticEvent, type ReactElement, useCallback, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { CheckboxField, RadioGroupField, SelectField, TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';
import { CancelLink } from '@/shared/ui/CancelLink.tsx';

import { usersApi } from '../api/identityAccessApi.ts';
import { type AccountSource, type LanguageCode, type UserDetail } from '../api/types.ts';
import {
  checkNationalId,
  checkPassword,
  checkText,
  E164_PATTERN,
  EMAIL_PATTERN,
  optionalText,
  useFocusFirstError,
  useSaveAction,
} from '../forms.ts';
import { labelOf, useOrganizationLookups } from '../lookups.ts';
import { problemMessage } from '../problems.ts';
import { TemporaryPasswordNotice } from './TemporaryPasswordNotice.tsx';
import { type PasswordMode, PasswordModeField } from './UserAccountDialogs.tsx';

type CreatableUserType = 'INTERNAL' | 'EXTERNAL';

interface UserFormValues {
  userType: CreatableUserType;
  username: string;
  displayName: string;
  email: string;
  mobileNumber: string;
  preferredLanguage: LanguageCode;
  directorySubjectId: string;
  jobTitle: string;
  externalEntityId: string;
  accountSource: AccountSource;
  passwordMode: PasswordMode;
  temporaryPassword: string;
  mustChangePassword: boolean;
  /** Edit only: a new National ID or Iqama number; blank keeps the recorded one. */
  nationalId: string;
}

const EMPTY_VALUES: UserFormValues = {
  userType: 'INTERNAL',
  username: '',
  displayName: '',
  email: '',
  mobileNumber: '',
  preferredLanguage: 'ar',
  directorySubjectId: '',
  jobTitle: '',
  externalEntityId: '',
  accountSource: 'DIRECTORY',
  passwordMode: 'GENERATED',
  temporaryPassword: '',
  mustChangePassword: true,
  nationalId: '',
};

function valuesOf(user: UserDetail): UserFormValues {
  return {
    userType: user.userType === 'EXTERNAL' ? 'EXTERNAL' : 'INTERNAL',
    username: user.username,
    displayName: user.displayName,
    email: user.email,
    mobileNumber: user.mobileNumber ?? '',
    preferredLanguage: user.preferredLanguage,
    directorySubjectId: user.directorySubjectId ?? '',
    jobTitle: user.jobTitle ?? '',
    externalEntityId: user.externalEntityId ?? '',
    accountSource: user.accountSource,
    passwordMode: 'GENERATED',
    temporaryPassword: '',
    mustChangePassword: user.mustChangePassword,
    nationalId: '',
  };
}

interface UserFormProps {
  /** Absent on ADM-004 Create User; the user and its ETag on ADM-005 Edit User. */
  existing?: { user: UserDetail; etag: string | null };
}

/**
 * ADM-004 and ADM-005. The user type and an external user's entity are chosen once, at creation (D-4): the entity
 * is MOD-082's assignment for an external user. An internal user's job title, department and manager come from the
 * directory (ADR-007), so the form shows them and never sends a change. A local account (the 5 Nov 2026 review §6.1) has no directory
 * identifier: it signs in with a temporary password, typed or generated, shown once when the API returns it.
 */
function UserForm({ existing }: UserFormProps): ReactElement {
  const { t, language } = useI18n();
  const navigate = useNavigate();
  const formRef = useRef<HTMLFormElement>(null);
  const [values, setValues] = useState<UserFormValues>(
    existing === undefined ? EMPTY_VALUES : valuesOf(existing.user),
  );
  const save = useSaveAction();
  const { lookups } = useOrganizationLookups(language);
  useFocusFirstError(formRef, save.fieldErrors);
  const [created, setCreated] = useState<UserDetail | null>(null);
  // The National ID and the profile are two writes; each moves the ETag, so a retry after a partial save sends the newest.
  const etagRef = useRef(existing?.etag ?? null);

  const creating = existing === undefined;
  const internal = values.userType === 'INTERNAL';
  const local = values.accountSource === 'LOCAL';
  const typedPassword = creating && local && values.passwordMode === 'TYPED';
  const set = (field: keyof UserFormValues) => (value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    const valid = save.validate({
      username: checkText(values.username, { required: true, maxLength: 100 }),
      displayName: checkText(values.displayName, { required: true }),
      email: checkText(values.email, { required: true, pattern: EMAIL_PATTERN }),
      mobileNumber: checkText(values.mobileNumber, { maxLength: 16, pattern: E164_PATTERN }),
      directorySubjectId: local
        ? null
        : checkText(values.directorySubjectId, { required: internal }),
      temporaryPassword: typedPassword
        ? checkPassword(values.temporaryPassword, values.username)
        : null,
      jobTitle: internal ? null : checkText(values.jobTitle, {}),
      externalEntityId: creating && !internal && values.externalEntityId === '' ? 'REQUIRED' : null,
      nationalId: values.nationalId.trim() === '' ? null : checkNationalId(values.nationalId),
    });
    if (!valid) {
      return;
    }
    const common = {
      username: values.username.trim(),
      displayName: values.displayName.trim(),
      email: values.email.trim(),
      mobileNumber: optionalText(values.mobileNumber),
      preferredLanguage: values.preferredLanguage,
      directorySubjectId: local ? null : optionalText(values.directorySubjectId),
      // An internal user's job title is the directory's: an edit resends what the API returned, a creation none.
      jobTitle: internal ? (existing?.user.jobTitle ?? null) : optionalText(values.jobTitle),
    };
    const result = await save.run(async () => {
      if (existing === undefined) {
        return usersApi.create({
          ...common,
          userType: values.userType,
          externalEntityId: internal ? null : values.externalEntityId,
          accountSource: values.accountSource,
          ...(local
            ? {
                temporaryPassword: typedPassword ? values.temporaryPassword : null,
                mustChangePassword: values.mustChangePassword,
              }
            : {}),
        });
      }
      const nationalId = values.nationalId.trim();
      if (nationalId !== '') {
        const recorded = await usersApi.setNationalId(
          existing.user.id,
          nationalId,
          etagRef.current,
        );
        etagRef.current = recorded.etag;
        setValues((current) => ({ ...current, nationalId: '' }));
      }
      const updated = await usersApi.update(existing.user.id, common, etagRef.current);
      etagRef.current = updated.etag;
      return updated;
    });
    if (result.ok && result.value.data.temporaryPassword != null) {
      // Never through the router's state, which the browser keeps in its session history.
      setCreated(result.value.data);
    } else if (result.ok) {
      void navigate(`/admin/users/${result.value.data.id}`, {
        state: { notice: creating ? 'created' : 'updated' },
      });
    }
  };

  const activeEntities = (lookups?.entities ?? []).filter((entity) => entity.status === 'ACTIVE');
  const cancelTo = existing === undefined ? '/admin/users' : `/admin/users/${existing.user.id}`;

  if (created?.temporaryPassword != null) {
    return (
      <>
        <p className="notice" role="status">
          {t('identityAccess.users.detail.notice.created')}
        </p>
        <TemporaryPasswordNotice name={created.displayName} password={created.temporaryPassword} />
        <div className="form__actions">
          <Link className="button button--primary" to={`/admin/users/${created.id}`}>
            {t('identityAccess.users.temporaryPassword.openUser')}
          </Link>
        </div>
      </>
    );
  }

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={save.formError} />

      {creating ? (
        <RadioGroupField
          label={t('identityAccess.users.fields.userType')}
          name="userType"
          required
          value={values.userType}
          hint={t('identityAccess.users.form.userTypeHint')}
          options={[
            { value: 'INTERNAL', label: t('identityAccess.userType.INTERNAL') },
            { value: 'EXTERNAL', label: t('identityAccess.userType.EXTERNAL') },
          ]}
          onChange={set('userType')}
          error={save.fieldErrors.userType}
        />
      ) : (
        <p className="form__readonly">
          <span className="field__label">{t('identityAccess.users.fields.userType')}</span>
          {t(`identityAccess.userType.${existing.user.userType}`)}
        </p>
      )}

      {creating ? (
        <RadioGroupField
          label={t('identityAccess.users.fields.accountSource')}
          name="accountSource"
          required
          value={values.accountSource}
          hint={t('identityAccess.users.form.accountSourceHint')}
          options={[
            { value: 'DIRECTORY', label: t('identityAccess.users.form.accountSourceDirectory') },
            { value: 'LOCAL', label: t('identityAccess.users.form.accountSourceLocal') },
          ]}
          onChange={(value) => {
            setValues((current) => ({
              ...current,
              accountSource: value === 'LOCAL' ? 'LOCAL' : 'DIRECTORY',
            }));
          }}
          error={save.fieldErrors.accountSource}
        />
      ) : (
        <p className="form__readonly">
          <span className="field__label">{t('identityAccess.users.fields.accountSource')}</span>
          {t(`identityAccess.accountSource.${existing.user.accountSource}`)}
        </p>
      )}

      {!internal &&
        (creating ? (
          <SelectField
            label={t('identityAccess.users.fields.externalEntity')}
            name="externalEntityId"
            required
            value={values.externalEntityId}
            placeholder={t('common.form.choose')}
            hint={t('identityAccess.users.form.externalEntityHint')}
            options={activeEntities.map((entity) => ({
              value: entity.id,
              label: labelOf(entity.name, language),
            }))}
            onChange={set('externalEntityId')}
            error={save.fieldErrors.externalEntityId}
          />
        ) : (
          <p className="form__readonly">
            <span className="field__label">{t('identityAccess.users.fields.externalEntity')}</span>
            {lookups?.entityName(existing.user.externalEntityId) ?? '—'}
          </p>
        ))}

      <TextField
        label={t('identityAccess.users.fields.displayName')}
        name="displayName"
        required
        value={values.displayName}
        onChange={set('displayName')}
        autoComplete="off"
        error={save.fieldErrors.displayName}
      />
      <TextField
        label={t('identityAccess.users.fields.username')}
        name="username"
        required
        dir="ltr"
        value={values.username}
        onChange={set('username')}
        autoComplete="off"
        error={save.fieldErrors.username}
      />
      <TextField
        label={t('identityAccess.users.fields.email')}
        name="email"
        type="email"
        required
        dir="ltr"
        value={values.email}
        onChange={set('email')}
        autoComplete="off"
        error={save.fieldErrors.email}
      />
      <TextField
        label={t('identityAccess.users.fields.mobileNumber')}
        name="mobileNumber"
        type="tel"
        dir="ltr"
        inputMode="tel"
        value={values.mobileNumber}
        onChange={set('mobileNumber')}
        autoComplete="off"
        hint={t('identityAccess.users.form.mobileNumberHint')}
        error={save.fieldErrors.mobileNumber}
      />
      {!creating && (
        <TextField
          label={t('identityAccess.users.nationalId.field')}
          name="nationalId"
          dir="ltr"
          inputMode="numeric"
          value={values.nationalId}
          onChange={set('nationalId')}
          autoComplete="off"
          hint={t(
            existing.user.nationalIdMasked == null
              ? 'identityAccess.users.form.nationalIdHintNone'
              : 'identityAccess.users.form.nationalIdHintRecorded',
            { current: existing.user.nationalIdMasked ?? '' },
          )}
          error={save.fieldErrors.nationalId}
        />
      )}
      {local ? (
        creating && (
          <>
            <PasswordModeField
              mode={values.passwordMode}
              onChange={(passwordMode) => {
                setValues((current) => ({ ...current, passwordMode }));
              }}
            />
            {typedPassword && (
              <TextField
                label={t('identityAccess.users.form.temporaryPassword')}
                name="temporaryPassword"
                required
                dir="ltr"
                value={values.temporaryPassword}
                onChange={set('temporaryPassword')}
                autoComplete="off"
                hint={t('identityAccess.temporaryPasswordRules')}
                error={save.fieldErrors.temporaryPassword}
              />
            )}
            <CheckboxField
              label={t('identityAccess.users.fields.mustChangePassword')}
              name="mustChangePassword"
              checked={values.mustChangePassword}
              onChange={(mustChangePassword) => {
                setValues((current) => ({ ...current, mustChangePassword }));
              }}
              error={save.fieldErrors.mustChangePassword}
            />
          </>
        )
      ) : (
        <TextField
          label={t('identityAccess.users.fields.directorySubjectId')}
          name="directorySubjectId"
          required={internal}
          dir="ltr"
          value={values.directorySubjectId}
          onChange={set('directorySubjectId')}
          autoComplete="off"
          hint={t('identityAccess.users.form.directorySubjectIdHint')}
          error={save.fieldErrors.directorySubjectId}
        />
      )}
      {internal ? (
        !local && (
          <p className="form__note">{t('identityAccess.users.form.directoryAttributesNote')}</p>
        )
      ) : (
        <TextField
          label={t('identityAccess.users.fields.jobTitle')}
          name="jobTitle"
          value={values.jobTitle}
          onChange={set('jobTitle')}
          error={save.fieldErrors.jobTitle}
        />
      )}
      <SelectField
        label={t('identityAccess.users.fields.preferredLanguage')}
        name="preferredLanguage"
        required
        value={values.preferredLanguage}
        options={[
          { value: 'ar', label: t('common.languages.ar') },
          { value: 'en', label: t('common.languages.en') },
        ]}
        onChange={(value) => {
          setValues((current) => ({ ...current, preferredLanguage: value === 'en' ? 'en' : 'ar' }));
        }}
        error={save.fieldErrors.preferredLanguage}
      />

      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('common.actions.save')}
        </button>
        <CancelLink fallback={cancelTo} />
      </div>
    </form>
  );
}

/** ADM-004 Create User. */
export function UserCreatePage(): ReactElement {
  const { t } = useI18n();
  return (
    <>
      <PageHeader title={t('identityAccess.users.create.title')} />
      <UserForm />
    </>
  );
}

/** ADM-005 Edit User. */
export function UserEditPage(): ReactElement {
  const { t } = useI18n();
  const { userId = '' } = useParams();
  const load = useCallback((signal: AbortSignal) => usersApi.get(userId, signal), [userId]);
  const user = useApiResource(load);

  return (
    <>
      <PageHeader title={t('identityAccess.users.edit.title')} />
      {user.loading && <LoadingState />}
      {user.error !== null && (
        <ErrorState
          error={user.error}
          message={problemMessage(user.error, t)}
          onRetry={user.reload}
        />
      )}
      {user.data !== undefined && (
        <UserForm existing={{ user: user.data.data, etag: user.data.etag }} />
      )}
    </>
  );
}
