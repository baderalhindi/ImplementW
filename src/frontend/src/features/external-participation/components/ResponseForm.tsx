import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { type Translate } from '@/features/identity-access/problems.ts';
import { useSaveAction } from '@/features/identity-access/forms.ts';
import { ConfirmCommandDialog } from '@/features/risks/dialogs/ConfirmCommandDialog.tsx';
import { type FieldCodes, useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TextAreaField, TextField } from '@/shared/ui/FormFields.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { contributionsApi } from '../api/externalParticipationApi.ts';
import {
  type ContributionFieldDefinition,
  type ExternalContributionDetail,
  type ExternalUpdateRequestDetail,
} from '../api/types.ts';
import { useFieldLabel } from '../labels.ts';
import { isStale, participationProblemMessage, responseFieldMessage } from '../problems.ts';
import {
  responseFieldCodes,
  responseFieldsToSend,
  responseIssuesOf,
  responseValuesOf,
} from '../rules.ts';
import { useContributionRecord } from '../useExternalParticipationData.ts';

/** NarrativeTextRequest.TextLength. */
const TEXT_LENGTH = 2000;

interface ResponseFormProps {
  request: ExternalUpdateRequestDetail;
  /** The draft revision being answered, or null before the first answer is started. */
  draftId: string | null;
  onSaved: () => void;
  onSubmitted: () => void;
  onStale: () => void;
}

/**
 * The entity's answer (SCR-162 external, WF-13 §8.2 "editable/respondable"): one input per field of the request's
 * typed schema and nothing else — the API refuses any other field. A draft may be saved incomplete; submitting checks
 * every required field and freezes the revision for AHDA's review.
 */
export function ResponseForm(props: ResponseFormProps): ReactElement {
  if (props.draftId === null) {
    return <ResponseFields {...props} draft={null} etag={null} />;
  }
  return <DraftResponse {...props} draftId={props.draftId} />;
}

function DraftResponse(props: ResponseFormProps & { draftId: string }): ReactElement {
  const { t } = useI18n();
  const record = useContributionRecord(props.draftId);
  if (record.data === undefined) {
    return record.loading ? (
      <LoadingState />
    ) : (
      <ErrorState message={participationProblemMessage(record.error, t)} onRetry={record.reload} />
    );
  }
  return (
    <ResponseFields
      // A new ETag means the draft was saved: start the inputs again from what was stored.
      key={record.data.etag ?? ''}
      {...props}
      draft={record.data.data}
      etag={record.data.etag}
    />
  );
}

function ResponseFields({
  request,
  draft,
  etag,
  onSaved,
  onSubmitted,
  onStale,
}: ResponseFormProps & {
  draft: ExternalContributionDetail | null;
  etag: string | null;
}): ReactElement {
  const { t, language } = useI18n();
  const label = useFieldLabel();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(participationProblemMessage);
  const definitions = request.responseFields;
  const [values, setValues] = useState<Record<string, string>>(() =>
    responseValuesOf(draft?.fields ?? []),
  );
  const [serverCodes, setServerCodes] = useState<FieldCodes>({});
  const [confirming, setConfirming] = useState(false);
  // A missing required answer is a code like any other; useFieldErrors shows it only once saving was tried.
  const clientCodes = responseFieldCodes(definitions, values, true);
  const codes = Object.fromEntries(
    definitions.map((definition) => [
      definition.fieldCode,
      clientCodes[definition.fieldCode] ?? serverCodes[definition.fieldCode] ?? null,
    ]),
  );
  const describe = (field: string, code: string, translate: Translate) => {
    const definition = definitions.find((candidate) => candidate.fieldCode === field);
    return code === 'OUT_OF_RANGE' && definition !== undefined
      ? translate('externalParticipation.fieldErrors.answerOutOfRange', {
          min: definition.minimum ?? '—',
          max: definition.maximum ?? '—',
        })
      : responseFieldMessage(code, translate);
  };
  const fields = useFieldErrors(codes, formRef, describe);

  /** Stores the values as the draft (creating revision 1 if there is none) and answers with the stored draft's ETag. */
  const store = async () => {
    const sent = responseFieldsToSend(definitions, values, language);
    try {
      const response =
        draft === null
          ? await contributionsApi.create(request.id, sent)
          : await contributionsApi.update(draft.id, sent, etag);
      return response;
    } catch (error) {
      setServerCodes(responseIssuesOf(error, sent));
      throw error;
    }
  };

  const saveDraft = async (event: SyntheticEvent) => {
    event.preventDefault();
    // A draft may be incomplete: only a value of the wrong shape stops it.
    const shapeOnly = responseFieldCodes(definitions, values, false);
    if (Object.values(shapeOnly).some((code) => code !== null)) {
      fields.attempt();
      return;
    }
    const result = await save.run(store);
    if (result.ok) {
      onSaved();
    } else if (isStale(result.error)) {
      onStale();
    }
  };

  const askToSubmit = () => {
    if (fields.attempt()) {
      setConfirming(true);
    }
  };

  return (
    <>
      <form
        ref={formRef}
        className="form"
        noValidate
        aria-labelledby="external-response-title"
        onSubmit={(event) => void saveDraft(event)}
      >
        <FormAlert message={fields.summary ?? save.formError} />
        {definitions.map((definition) => (
          <ResponseInput
            key={definition.fieldCode}
            definition={definition}
            label={label(definition.fieldCode)}
            value={values[definition.fieldCode] ?? ''}
            error={fields.errorOf(definition.fieldCode)}
            onChange={(value) => {
              setValues((current) => ({ ...current, [definition.fieldCode]: value }));
              setServerCodes({});
              fields.clearServer();
            }}
          />
        ))}
        <div className="form__actions">
          <button type="submit" className="button" disabled={save.saving}>
            {save.saving ? t('common.states.saving') : t('externalParticipation.actions.saveDraft')}
          </button>
          <button
            type="button"
            className="button button--primary"
            disabled={save.saving}
            onClick={askToSubmit}
          >
            {t('externalParticipation.actions.submitResponse')}
          </button>
        </div>
      </form>
      {confirming && (
        <ConfirmCommandDialog
          title={t('externalParticipation.command.submit.title')}
          consequence={t('externalParticipation.command.submit.consequence')}
          confirmLabel={t('externalParticipation.actions.submitResponse')}
          run={async () => {
            const stored = await store();
            try {
              await contributionsApi.submit(stored.data.id, stored.etag);
            } catch (error) {
              // CONTRIBUTION_REQUIRED_ITEM_MISSING names each missing field by its code.
              setServerCodes(responseIssuesOf(error, []));
              throw error;
            }
          }}
          describe={participationProblemMessage}
          isStale={isStale}
          onClose={() => {
            setConfirming(false);
          }}
          onDone={onSubmitted}
          onStale={onStale}
        />
      )}
    </>
  );
}

function ResponseInput({
  definition,
  label,
  value,
  error,
  onChange,
}: {
  definition: ContributionFieldDefinition;
  label: string;
  value: string;
  error: string | undefined;
  onChange: (value: string) => void;
}): ReactElement {
  const { t } = useI18n();
  switch (definition.fieldType) {
    case 'NARRATIVE':
      return (
        <TextAreaField
          label={label}
          name={definition.fieldCode}
          value={value}
          required={definition.required}
          maxLength={TEXT_LENGTH}
          error={error}
          onChange={onChange}
        />
      );
    case 'DATE':
      return (
        <TextField
          label={label}
          name={definition.fieldCode}
          type="date"
          value={value}
          required={definition.required}
          dir="ltr"
          error={error}
          onChange={onChange}
        />
      );
    case 'NUMBER':
      return (
        <TextField
          label={label}
          name={definition.fieldCode}
          value={value}
          required={definition.required}
          dir="ltr"
          inputMode="decimal"
          hint={
            definition.minimum === null || definition.maximum === null
              ? t('externalParticipation.response.numberHint')
              : t('externalParticipation.response.rangeHint', {
                  min: definition.minimum,
                  max: definition.maximum,
                })
          }
          error={error}
          onChange={onChange}
        />
      );
  }
}
