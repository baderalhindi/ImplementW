import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import {
  checkText,
  optionalText,
  useFocusFirstError,
  useSaveAction,
} from '@/features/identity-access/forms.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { type SelectOption, TextAreaField, TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { documentsApi } from '../api/documentsApi.ts';
import { type DocumentDetail, type NarrativeTextRequest } from '../api/types.ts';
import { MetadataSelects } from '../components/MetadataSelects.tsx';
import { languageTag, TEXT_LENGTH } from '../presentation.ts';
import { documentProblemMessage } from '../problems.ts';
import { useDocumentLookups } from '../useDocumentLookups.ts';

export interface EditTarget {
  document: DocumentDetail;
  /** The ETag the document was read with, sent as If-Match (R-21). */
  etag: string | null;
}

interface EditMetadataDialogProps {
  target: EditTarget | null;
  onClose: () => void;
  onDone: () => void;
}

/**
 * MOD-051 Edit Metadata: title, description, type and classification, sent as a whole (R-5). The project, owner and
 * versions never change here. Someone else's change since the page was read is 412, and the page reads again.
 */
export function EditMetadataDialog({
  target,
  onClose,
  onDone,
}: EditMetadataDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={target !== null} title={t('documents.edit.title')} onClose={onClose}>
      {target !== null && <EditForm target={target} onClose={onClose} onDone={onDone} />}
    </Dialog>
  );
}

function EditForm({
  target,
  onClose,
  onDone,
}: EditMetadataDialogProps & { target: EditTarget }): ReactElement {
  const { t, language } = useI18n();
  const save = useSaveAction(documentProblemMessage);
  const lookups = useDocumentLookups();
  const formRef = useRef<HTMLFormElement>(null);
  const { document } = target;
  const [title, setTitle] = useState(document.title.text);
  const [description, setDescription] = useState(document.description?.text ?? '');
  const [documentTypeItemId, setDocumentTypeItemId] = useState(document.documentTypeItemId);
  const [dataClassificationItemId, setDataClassificationItemId] = useState(
    document.dataClassificationItemId,
  );
  useFocusFirstError(formRef, save.fieldErrors);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (
      !save.validate({
        'title.text': checkText(title, { required: true, maxLength: TEXT_LENGTH }),
        'description.text': checkText(description, { maxLength: TEXT_LENGTH }),
        documentTypeItemId: documentTypeItemId === '' ? 'REQUIRED' : null,
        dataClassificationItemId: dataClassificationItemId === '' ? 'REQUIRED' : null,
      })
    ) {
      return;
    }
    // A text left as it was keeps its recorded language; a changed one takes the interface language.
    const narrative = (
      text: string,
      original: DocumentDetail['title'] | null,
    ): NarrativeTextRequest =>
      original !== null && original.text === text
        ? { text, language: languageTag(original.language) === 'ar' ? 'ar' : 'en' }
        : { text, language };
    const descriptionText = optionalText(description);
    const result = await save.run(() =>
      documentsApi.update(
        document.id,
        {
          title: narrative(title.trim(), document.title),
          description:
            descriptionText === null ? null : narrative(descriptionText, document.description),
          documentTypeItemId,
          dataClassificationItemId,
        },
        target.etag,
      ),
    );
    if (result.ok) {
      onDone();
    }
  };

  // The current values stay choosable even if their item was retired since, so an unchanged save is not refused here.
  const withCurrent = (options: SelectOption[], current: string) =>
    options.some((option) => option.value === current)
      ? options
      : [{ value: current, label: lookups.label(current) }, ...options];

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={save.formError} />
      <FormAlert
        message={
          lookups.error === null
            ? null
            : `${t('documents.lookups.unavailable')} ${documentProblemMessage(lookups.error, t)}`
        }
      />
      <TextField
        label={t('documents.fields.title')}
        name="title"
        value={title}
        onChange={setTitle}
        required
        dir="auto"
        error={save.fieldErrors['title.text'] ?? save.fieldErrors.title}
      />
      <TextAreaField
        label={t('documents.fields.description')}
        name="description"
        value={description}
        onChange={setDescription}
        maxLength={TEXT_LENGTH}
        rows={3}
        error={save.fieldErrors['description.text'] ?? save.fieldErrors.description}
      />
      <MetadataSelects
        documentTypeItemId={documentTypeItemId}
        onDocumentTypeChange={setDocumentTypeItemId}
        dataClassificationItemId={dataClassificationItemId}
        onClassificationChange={setDataClassificationItemId}
        typeOptions={withCurrent(lookups.options('DOCUMENT_TYPE'), document.documentTypeItemId)}
        classificationOptions={withCurrent(
          lookups.options('DATA_CLASSIFICATION'),
          document.dataClassificationItemId,
        )}
        loading={lookups.loading}
        fieldErrors={save.fieldErrors}
      />
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
