import { type SyntheticEvent, type ReactElement, useCallback, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { RadioGroupField, SelectField, TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { usersApi } from '../api/identityAccessApi.ts';
import { type LanguageCode, type UserDetail } from '../api/types.ts';
import {
  checkText,
  E164_PATTERN,
  EMAIL_PATTERN,
  optionalText,
  useFocusFirstError,
  useSaveAction,
} from '../forms.ts';
import { labelOf, useOrganizationLookups } from '../lookups.ts';
import { problemMessage } from '../problems.ts';

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
  };
}

interface UserFormProps {
  /** Absent on ADM-004 Create User; the user and its ETag on ADM-005 Edit User. */
  existing?: { user: UserDetail; etag: string | null };
}

/**
 * ADM-004 and ADM-005. The user type and an external user's entity are chosen once, at creation (D-4): the entity
 * is MOD-082's assignment for an external user. An internal user's job title, department and manager come from the
 * directory (ADR-007), so the form shows them and never sends a change.
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

  const creating = existing === undefined;
  const internal = values.userType === 'INTERNAL';
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
      directorySubjectId: checkText(values.directorySubjectId, { required: internal }),
      jobTitle: internal ? null : checkText(values.jobTitle, {}),
      externalEntityId: creating && !internal && values.externalEntityId === '' ? 'REQUIRED' : null,
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
      directorySubjectId: optionalText(values.directorySubjectId),
      // An internal user's job title is the directory's: an edit resends what the API returned, a creation none.
      jobTitle: internal ? (existing?.user.jobTitle ?? null) : optionalText(values.jobTitle),
    };
    const result = await save.run(() =>
      existing === undefined
        ? usersApi.create({
            ...common,
            userType: values.userType,
            externalEntityId: internal ? null : values.externalEntityId,
          })
        : usersApi.update(existing.user.id, common, existing.etag),
    );
    if (result.ok) {
      void navigate(`/admin/users/${result.value.data.id}`, {
        state: { notice: creating ? 'created' : 'updated' },
      });
    }
  };

  const activeEntities = (lookups?.entities ?? []).filter((entity) => entity.status === 'ACTIVE');
  const cancelTo = existing === undefined ? '/admin/users' : `/admin/users/${existing.user.id}`;

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
      {internal ? (
        <p className="form__note">{t('identityAccess.users.form.directoryAttributesNote')}</p>
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
        <Link className="button" to={cancelTo}>
          {t('common.actions.cancel')}
        </Link>
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
        <ErrorState message={problemMessage(user.error, t)} onRetry={user.reload} />
      )}
      {user.data !== undefined && (
        <UserForm existing={{ user: user.data.data, etag: user.data.etag }} />
      )}
    </>
  );
}
