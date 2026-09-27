import { type SyntheticEvent, type ReactElement, useCallback, useRef, useState } from 'react';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { SelectField, TextField } from '@/shared/ui/FormFields.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { accessRelationshipsApi, rolesApi } from '../api/identityAccessApi.ts';
import {
  type AccessRelationshipDetail,
  type PermissionProfileDetail,
  type UserSummary,
} from '../api/types.ts';
import { UserPicker } from '../components/UserPicker.tsx';
import { UUID_PATTERN, useFocusFirstError, useSaveAction } from '../forms.ts';
import { labelOf, useOrganizationLookups } from '../lookups.ts';
import { problemMessage } from '../problems.ts';

type Step = 'role' | 'scope';

/** The request fields MOD-081 collects; a refusal naming any of them returns the administrator to that step. */
const ROLE_STEP_FIELDS = ['userId', 'permissionProfileVersionId', 'startsAt', 'endsAt'];

interface AssignRoleDialogProps {
  open: boolean;
  /** Fixed when opened from ADM-003 User Detail; chosen in the dialog when opened from ADM-010. */
  user?: UserSummary | undefined;
  onClose: () => void;
  onAssigned: (assignment: AccessRelationshipDetail) => void;
}

function toIsoOrNull(localDateTime: string): string | null {
  return localDateTime === '' ? null : new Date(localDateTime).toISOString();
}

/**
 * MOD-081 Assign Role, then MOD-082 Assign Department/Entity: one assignment (ADM-010) in two steps. MOD-081 binds
 * the user to a PUBLISHED permission-profile version (ADR-018); MOD-082 sets the assignment's scope anchors (record
 * D-4, F-14). For an external user MOD-082 is ADR-013's per-project grant: their own entity, a project and a named
 * internal sponsor.
 */
export function AssignRoleDialog({
  open,
  user,
  onClose,
  onAssigned,
}: AssignRoleDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={open} title={t('identityAccess.assignRole.title')} onClose={onClose}>
      <AssignRoleForm fixedUser={user} onClose={onClose} onAssigned={onAssigned} />
    </Dialog>
  );
}

interface AssignRoleFormProps {
  fixedUser: UserSummary | undefined;
  onClose: () => void;
  onAssigned: (assignment: AccessRelationshipDetail) => void;
}

function AssignRoleForm({ fixedUser, onClose, onAssigned }: AssignRoleFormProps): ReactElement {
  const { t, language } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction();
  useFocusFirstError(formRef, save.fieldErrors);
  const { lookups } = useOrganizationLookups(language);

  const [step, setStep] = useState<Step>('role');
  const [user, setUser] = useState<UserSummary | null>(fixedUser ?? null);
  const [profileId, setProfileId] = useState('');
  const [versionId, setVersionId] = useState('');
  const [startsAt, setStartsAt] = useState('');
  const [endsAt, setEndsAt] = useState('');
  const [departmentId, setDepartmentId] = useState('');
  const [externalEntityId, setExternalEntityId] = useState('');
  const [projectId, setProjectId] = useState('');
  const [sponsor, setSponsor] = useState<UserSummary | null>(null);

  const loadCatalogue = useCallback(async (signal: AbortSignal) => {
    const [roles, profiles] = await Promise.all([
      rolesApi.list(signal),
      rolesApi.listProfiles(signal),
    ]);
    return { roles, profiles };
  }, []);
  const catalogue = useApiResource(loadCatalogue);

  const loadProfile = useCallback(
    (signal: AbortSignal): Promise<PermissionProfileDetail | null> =>
      profileId === '' ? Promise.resolve(null) : rolesApi.getProfile(profileId, signal),
    [profileId],
  );
  const profile = useApiResource(loadProfile);

  const external = user?.userType === 'EXTERNAL';
  const eligibleRoleIds = new Set(
    (catalogue.data?.roles ?? [])
      .filter((role) => !external || role.isExternalEligible)
      .map((role) => role.id),
  );
  const profileOptions = (catalogue.data?.profiles ?? [])
    .filter((p) => eligibleRoleIds.has(p.baseRoleId))
    .map((p) => ({ value: p.id, label: `${p.baseRoleCode} — ${labelOf(p.name, language)}` }));
  const publishedVersions = (profile.data?.versions ?? []).filter(
    (version) => version.lifecycleState === 'PUBLISHED',
  );

  const chooseProfile = (id: string) => {
    setProfileId(id);
    setVersionId('');
  };

  const chooseUser = (next: UserSummary | null) => {
    setUser(next);
    setProfileId('');
    setVersionId('');
    setExternalEntityId(next?.userType === 'EXTERNAL' ? (next.externalEntityId ?? '') : '');
  };

  const selectedVersionId = versionId === '' ? (publishedVersions[0]?.id ?? '') : versionId;

  const goToScope = (event: SyntheticEvent) => {
    event.preventDefault();
    const valid = save.validate({
      userId: user === null ? 'REQUIRED' : null,
      permissionProfileId: profileId === '' ? 'REQUIRED' : null,
      permissionProfileVersionId: profileId !== '' && selectedVersionId === '' ? 'REQUIRED' : null,
      endsAt: endsAt !== '' && startsAt !== '' && endsAt <= startsAt ? 'DATE_BEFORE_START' : null,
    });
    if (valid) {
      setStep('scope');
    }
  };

  const assign = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (user === null) {
      setStep('role');
      return;
    }
    const valid = save.validate({
      projectId:
        projectId.trim() !== '' && !UUID_PATTERN.test(projectId.trim()) ? 'MALFORMED' : null,
      sponsorUserId: external && sponsor === null ? 'REQUIRED' : null,
    });
    if (!valid) {
      return;
    }
    const result = await save.run(() =>
      accessRelationshipsApi.create({
        userId: user.id,
        permissionProfileVersionId: selectedVersionId,
        departmentId: external || departmentId === '' ? null : departmentId,
        externalEntityId: external ? (user.externalEntityId ?? null) : externalEntityId || null,
        projectId: projectId.trim() === '' ? null : projectId.trim(),
        sponsorUserId: sponsor?.id ?? null,
        startsAt: toIsoOrNull(startsAt),
        endsAt: toIsoOrNull(endsAt),
      }),
    );
    if (result.ok) {
      onAssigned(result.value);
    }
  };

  // A refusal that names a MOD-081 field is shown on MOD-081.
  const refusedOnRoleStep = ROLE_STEP_FIELDS.some((field) => save.fieldErrors[field] !== undefined);
  const visibleStep: Step = refusedOnRoleStep ? 'role' : step;

  if (catalogue.loading) {
    return <LoadingState />;
  }
  if (catalogue.error !== null) {
    return <ErrorState message={problemMessage(catalogue.error, t)} onRetry={catalogue.reload} />;
  }

  if (visibleStep === 'role') {
    return (
      <form
        ref={formRef}
        className="form"
        noValidate
        aria-label={t('identityAccess.assignRole.roleStep')}
        onSubmit={goToScope}
      >
        <p className="dialog__step">
          {t('identityAccess.assignRole.stepOf', { step: 1, count: 2 })}
        </p>
        <FormAlert message={save.formError} />
        {fixedUser === undefined ? (
          <UserPicker
            label={t('identityAccess.assignRole.user')}
            required
            value={user}
            onChange={chooseUser}
            error={save.fieldErrors.userId}
          />
        ) : (
          <p className="form__readonly">
            <span className="field__label">{t('identityAccess.assignRole.user')}</span>
            {fixedUser.displayName}
          </p>
        )}
        <SelectField
          label={t('identityAccess.assignRole.profile')}
          name="permissionProfileId"
          required
          value={profileId}
          placeholder={t('common.form.choose')}
          hint={
            external
              ? t('identityAccess.assignRole.externalProfileHint')
              : t('identityAccess.assignRole.profileHint')
          }
          options={profileOptions}
          onChange={chooseProfile}
          error={save.fieldErrors.permissionProfileId}
        />
        {profileId !== '' && profile.loading && <LoadingState />}
        {profileId !== '' && profile.data !== undefined && (
          <SelectField
            label={t('identityAccess.assignRole.version')}
            name="permissionProfileVersionId"
            required
            value={selectedVersionId}
            placeholder={
              publishedVersions.length === 0
                ? t('identityAccess.assignRole.noPublishedVersion')
                : undefined
            }
            options={publishedVersions.map((version) => ({
              value: version.id,
              label: t('identityAccess.assignRole.versionLabel', { version: version.versionNo }),
            }))}
            onChange={setVersionId}
            error={save.fieldErrors.permissionProfileVersionId}
          />
        )}
        <TextField
          label={t('identityAccess.assignRole.startsAt')}
          name="startsAt"
          type="datetime-local"
          value={startsAt}
          onChange={setStartsAt}
          hint={t('identityAccess.assignRole.startsAtHint')}
          error={save.fieldErrors.startsAt}
        />
        <TextField
          label={t('identityAccess.assignRole.endsAt')}
          name="endsAt"
          type="datetime-local"
          value={endsAt}
          onChange={setEndsAt}
          hint={t('identityAccess.assignRole.endsAtHint')}
          error={save.fieldErrors.endsAt}
        />
        <div className="form__actions">
          <button type="submit" className="button button--primary">
            {t('common.actions.next')}
          </button>
          <button type="button" className="button" onClick={onClose}>
            {t('common.actions.cancel')}
          </button>
        </div>
      </form>
    );
  }

  return (
    <form
      ref={formRef}
      className="form"
      noValidate
      aria-label={t('identityAccess.assignScope.title')}
      onSubmit={(event) => void assign(event)}
    >
      <p className="dialog__step">{t('identityAccess.assignRole.stepOf', { step: 2, count: 2 })}</p>
      <h3 className="dialog__subtitle">{t('identityAccess.assignScope.title')}</h3>
      <p className="form__note">
        {external
          ? t('identityAccess.assignScope.externalNote')
          : t('identityAccess.assignScope.internalNote')}
      </p>
      <FormAlert message={save.formError} />
      {external ? (
        <p className="form__readonly">
          <span className="field__label">{t('identityAccess.users.fields.externalEntity')}</span>
          {lookups?.entityName(user.externalEntityId) ?? '—'}
        </p>
      ) : (
        <>
          <SelectField
            label={t('identityAccess.users.fields.department')}
            name="departmentId"
            value={departmentId}
            placeholder={t('identityAccess.assignScope.noAnchor')}
            options={(lookups?.departments ?? [])
              .filter((d) => d.isActive)
              .map((d) => ({ value: d.id, label: labelOf(d.name, language) }))}
            onChange={setDepartmentId}
            error={save.fieldErrors.departmentId}
          />
          <SelectField
            label={t('identityAccess.users.fields.externalEntity')}
            name="externalEntityId"
            value={externalEntityId}
            placeholder={t('identityAccess.assignScope.noAnchor')}
            options={(lookups?.entities ?? [])
              .filter((e) => e.status === 'ACTIVE')
              .map((e) => ({ value: e.id, label: labelOf(e.name, language) }))}
            onChange={setExternalEntityId}
            error={save.fieldErrors.externalEntityId}
          />
        </>
      )}
      {external && save.fieldErrors.externalEntityId !== undefined && (
        <p className="field__error">{save.fieldErrors.externalEntityId}</p>
      )}
      <TextField
        label={t('identityAccess.assignScope.projectId')}
        name="projectId"
        dir="ltr"
        value={projectId}
        onChange={setProjectId}
        autoComplete="off"
        hint={t('identityAccess.assignScope.projectIdHint')}
        error={save.fieldErrors.projectId}
      />
      {external && (
        <UserPicker
          label={t('identityAccess.assignScope.sponsor')}
          userType="INTERNAL"
          required
          value={sponsor}
          onChange={setSponsor}
          hint={t('identityAccess.assignScope.sponsorHint')}
          error={save.fieldErrors.sponsorUserId}
        />
      )}
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('identityAccess.assignScope.submit')}
        </button>
        <button
          type="button"
          className="button"
          disabled={save.saving}
          onClick={() => {
            setStep('role');
          }}
        >
          {t('common.actions.back')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
