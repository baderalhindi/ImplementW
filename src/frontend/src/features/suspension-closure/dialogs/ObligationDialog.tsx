import { type ReactElement, type SyntheticEvent, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { assigneeOf, assigneeValue, useUserSearch } from '@/features/tasks/assignee.ts';
import { AssigneeField } from '@/features/tasks/components/AssigneeField.tsx';
import { type FieldCodes, serverCodesOf } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { TextAreaField, TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { obligationsApi } from '../api/closeoutApi.ts';
import {
  type PostProjectObligationCreateRequest,
  type PostProjectObligationDetail,
} from '../api/types.ts';
import {
  checkObligation,
  emptyObligationForm,
  obligationFormValuesOf,
  type ObligationFormValues,
  toObligationRequest,
} from '../closeoutRules.ts';
import {
  isStale,
  suspensionClosureFieldMessage,
  suspensionClosureProblemMessage,
} from '../problems.ts';

const FIELDS = ['title', 'description', 'ownerUserId', 'dueDate'] as const;

/**
 * A post-project obligation (warranty, defect liability, handover, …) recorded against the project's completion case,
 * or its terminal closure case, or one changed while it is not settled. Owner and due date may be added later;
 * completion needs both on every open obligation (TASK-063 D-9).
 */
export function ObligationDialog({
  user,
  project,
  knownUserIds,
  target,
  existing,
  onClose,
  onDone,
  onStale,
}: {
  user: SessionUser;
  project: Pick<ProjectDetail, 'projectManagerUserId'>;
  /** The project's obligation owners: people offered by name. */
  knownUserIds: (string | null)[];
  /** The case a new obligation is recorded against; ignored when editing. */
  target: Pick<PostProjectObligationCreateRequest, 'completionCaseId' | 'closureCaseId'> | null;
  existing: { obligation: PostProjectObligationDetail; etag: string | null } | null;
  onClose: () => void;
  onDone: () => void;
  onStale: () => void;
}): ReactElement {
  const { t, language } = useI18n();
  const save = useSaveAction(suspensionClosureProblemMessage);
  const [values, setValues] = useState<ObligationFormValues>(
    existing === null ? emptyObligationForm() : obligationFormValuesOf(existing.obligation),
  );
  const canSearch = useUserSearch() ?? false;
  const [owner, setOwner] = useState(() => assigneeValue(existing?.obligation.ownerUserId ?? null));
  const chosen = assigneeOf(owner, canSearch);
  const [tried, setTried] = useState(false);
  const [serverCodes, setServerCodes] = useState<Partial<Record<string, string | null>>>({});
  const codes: FieldCodes = { ...checkObligation(values), ownerUserId: chosen.code };

  const errorOf = (field: (typeof FIELDS)[number]): string | undefined => {
    const client = codes[field] ?? null;
    const code =
      client !== null && (client !== 'REQUIRED' || tried) ? client : (serverCodes[field] ?? null);
    return code === null ? undefined : suspensionClosureFieldMessage(field, code, t);
  };

  const set = <K extends keyof ObligationFormValues>(field: K, value: ObligationFormValues[K]) => {
    setValues((current) => ({ ...current, [field]: value }));
    setServerCodes({});
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    setTried(true);
    if (FIELDS.some((field) => codes[field])) {
      return;
    }
    const body = toObligationRequest(
      { ...values, ownerUserId: chosen.userId },
      language,
      existing?.obligation ?? null,
    );
    const result = await save.run(() =>
      existing === null
        ? obligationsApi.create({
            ...body,
            completionCaseId: target?.completionCaseId ?? null,
            closureCaseId: target?.closureCaseId ?? null,
          })
        : obligationsApi.update(existing.obligation.id, body, existing.etag),
    );
    if (result.ok) {
      onDone();
    } else if (isStale(result.error)) {
      onStale();
    } else {
      setServerCodes(serverCodesOf(result.error));
    }
  };

  return (
    <Dialog
      open
      title={t(
        existing === null
          ? 'suspensionClosure.obligations.addTitle'
          : 'suspensionClosure.obligations.editTitle',
      )}
      onClose={onClose}
    >
      <form className="form" noValidate onSubmit={(event) => void submit(event)}>
        <FormAlert message={save.formError} />
        <TextField
          label={t('suspensionClosure.obligations.fields.title')}
          name="title"
          required
          dir="auto"
          value={values.title}
          onChange={(value) => {
            set('title', value);
          }}
          error={errorOf('title')}
        />
        <TextAreaField
          label={t('suspensionClosure.obligations.fields.description')}
          name="description"
          rows={3}
          maxLength={TEXT_LENGTH}
          value={values.description}
          onChange={(value) => {
            set('description', value);
          }}
          error={errorOf('description')}
        />
        <p className="form__note">{t('suspensionClosure.obligations.ownerHint')}</p>
        <AssigneeField
          user={user}
          project={project}
          knownUserIds={knownUserIds}
          value={owner}
          canSearch={canSearch}
          name="ownerUserId"
          onChange={(value) => {
            setOwner(value);
            setServerCodes({});
          }}
          error={errorOf('ownerUserId')}
        />
        <TextField
          label={t('suspensionClosure.obligations.fields.dueDate')}
          name="dueDate"
          type="date"
          dir="ltr"
          value={values.dueDate}
          onChange={(value) => {
            set('dueDate', value);
          }}
          error={errorOf('dueDate')}
        />
        <div className="form__actions">
          <button type="submit" className="button button--primary" disabled={save.saving}>
            {save.saving ? t('common.states.saving') : t('suspensionClosure.obligations.save')}
          </button>
          <button type="button" className="button" disabled={save.saving} onClick={onClose}>
            {t('common.actions.cancel')}
          </button>
        </div>
      </form>
    </Dialog>
  );
}
