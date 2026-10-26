import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { assigneeOf, assigneeValue, useUserSearch } from '@/features/tasks/assignee.ts';
import { AssigneeField } from '@/features/tasks/components/AssigneeField.tsx';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { SelectField, TextAreaField, TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { risksApi } from '../api/risksApi.ts';
import { type RiskDetail } from '../api/types.ts';
import { isStale, riskFieldMessage, riskProblemMessage } from '../problems.ts';
import {
  checkRiskForm,
  emptyRiskForm,
  riskFormValuesOf,
  type RiskFormValues,
  toRiskRequest,
} from '../riskRules.ts';
import { type RiskLookups } from '../useRiskData.ts';

interface RiskFormDialogProps {
  /** null: MOD-030 Create Risk; a risk with its ETag: MOD-031 Edit Risk. */
  editing: ApiResponse<RiskDetail> | null;
  project: ProjectSummary;
  user: SessionUser;
  lookups: RiskLookups;
  /** The owners of the project's other risks: offered by name. */
  knownUserIds: (string | null)[];
  onClose: () => void;
  onDone: (risk: RiskDetail) => void;
  onStale: () => void;
}

/**
 * MOD-030 Create Risk and MOD-031 Edit Risk: title, description, category, the date it was identified and the next
 * review. A new risk names its owner here; an existing one changes owner through MOD-033, and its rating only through
 * an assessment (MOD-032). The edit re-sends every field (R-5) with the ETag it was read with.
 */
export function RiskFormDialog(props: RiskFormDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open
      title={t(props.editing === null ? 'risks.form.createTitle' : 'risks.form.editTitle')}
      onClose={props.onClose}
    >
      <RiskForm {...props} />
    </Dialog>
  );
}

function RiskForm({
  editing,
  project,
  user,
  lookups,
  knownUserIds,
  onClose,
  onDone,
  onStale,
}: RiskFormDialogProps): ReactElement {
  const { t, language } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(riskProblemMessage);
  const canSearch = useUserSearch() ?? false;
  const today = todayUtc();
  const before = editing?.data ?? null;
  const [values, setValues] = useState<RiskFormValues>(() =>
    before === null ? emptyRiskForm(today) : riskFormValuesOf(before),
  );
  const [owner, setOwner] = useState(() => assigneeValue(before?.ownerUserId ?? null));
  const ownerChoice = assigneeOf(owner, canSearch);
  const fields = useFieldErrors(
    {
      ...checkRiskForm(values, today),
      ...(before === null ? { ownerUserId: ownerChoice.code } : {}),
    },
    formRef,
    riskFieldMessage,
  );

  const set = (field: keyof RiskFormValues) => (value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
    fields.clearServer();
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const request = toRiskRequest(
      values,
      before === null ? ownerChoice.userId : before.ownerUserId,
      language,
      before,
    );
    const result = await save.run(async () =>
      before === null
        ? (await risksApi.create({ ...request, projectId: project.id })).data
        : (await risksApi.update(before.id, request, editing?.etag ?? null)).data,
    );
    if (result.ok) {
      onDone(result.value);
    } else if (isStale(result.error)) {
      onStale();
    } else {
      fields.showServer(result.error);
    }
  };

  // A category it already names stays listed, even if no longer PUBLISHED, so an edit does not drop it unseen.
  const categoryOptions = [
    ...lookups.categoryOptions,
    ...(values.riskCategoryItemId !== '' &&
    !lookups.categoryOptions.some((option) => option.value === values.riskCategoryItemId)
      ? [{ value: values.riskCategoryItemId, label: lookups.itemLabel(values.riskCategoryItemId) }]
      : []),
  ];

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={fields.summary ?? save.formError} />
      <TextField
        label={t('risks.fields.title')}
        name="title"
        required
        dir="auto"
        value={values.title}
        onChange={set('title')}
        error={fields.errorOf('title')}
      />
      <TextAreaField
        label={t('risks.fields.description')}
        name="description"
        required
        maxLength={TEXT_LENGTH}
        hint={t('risks.form.descriptionHint')}
        value={values.description}
        onChange={set('description')}
        error={fields.errorOf('description')}
      />
      {lookups.readable || before !== null ? (
        <SelectField
          label={t('risks.fields.category')}
          name="riskCategoryItemId"
          required
          value={values.riskCategoryItemId}
          options={categoryOptions}
          placeholder={t('risks.form.chooseCategory')}
          hint={lookups.readable ? undefined : t('risks.form.categoriesUnreadable')}
          onChange={set('riskCategoryItemId')}
          error={fields.errorOf('riskCategoryItemId')}
        />
      ) : (
        <p className="form__note">{t('risks.form.categoriesUnreadableCreate')}</p>
      )}
      <TextField
        label={t('risks.fields.identifiedDate')}
        name="identifiedDate"
        type="date"
        required
        value={values.identifiedDate}
        onChange={set('identifiedDate')}
        error={fields.errorOf('identifiedDate')}
      />
      <TextField
        label={t('risks.fields.nextReviewDate')}
        name="nextReviewDate"
        type="date"
        hint={t('risks.form.nextReviewHint')}
        value={values.nextReviewDate}
        onChange={set('nextReviewDate')}
        error={fields.errorOf('nextReviewDate')}
      />
      {before === null && (
        <AssigneeField
          user={user}
          project={project}
          knownUserIds={knownUserIds}
          value={owner}
          canSearch={canSearch}
          name="ownerUserId"
          onChange={(value) => {
            setOwner(value);
            fields.clearServer();
          }}
          error={fields.errorOf('ownerUserId')}
        />
      )}
      <div className="form__actions">
        <button
          type="submit"
          className="button button--primary"
          disabled={save.saving || (!lookups.readable && before === null)}
        >
          {save.saving ? t('common.states.saving') : t('common.actions.save')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
