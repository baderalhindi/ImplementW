import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';
import { Link } from 'react-router';

import { checkText, optionalText, useFocusFirstError } from '@/features/identity-access/forms.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { FileField, TextAreaField, TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { documentsApi } from '../api/documentsApi.ts';
import { MetadataSelects } from '../components/MetadataSelects.tsx';
import { TEXT_LENGTH } from '../presentation.ts';
import { documentProblemMessage } from '../problems.ts';
import { UploadOutcome } from '../upload/UploadOutcome.tsx';
import { useFileUpload } from '../upload/useFileUpload.ts';
import { useDocumentLookups } from '../useDocumentLookups.ts';

interface UploadDocumentDialogProps {
  open: boolean;
  /** SCR-121 uploads to its project; the library uploads a document of no project. */
  projectId: string | null;
  onClose: () => void;
  /** The API accepted the file: the list behind the dialog reads again while the dialog shows the scan. */
  onUploaded: () => void;
}

/** MOD-050 Upload Document: a file and its metadata. Version 1 is scanned before it can be used. */
export function UploadDocumentDialog({
  open,
  projectId,
  onClose,
  onUploaded,
}: UploadDocumentDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={open} title={t('documents.upload.title')} onClose={onClose}>
      {open && <UploadForm projectId={projectId} onClose={onClose} onUploaded={onUploaded} />}
    </Dialog>
  );
}

function UploadForm({
  projectId,
  onClose,
  onUploaded,
}: Omit<UploadDocumentDialogProps, 'open'>): ReactElement {
  const { t, language } = useI18n();
  const upload = useFileUpload();
  const { save } = upload;
  const lookups = useDocumentLookups();
  const formRef = useRef<HTMLFormElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [documentTypeItemId, setDocumentTypeItemId] = useState('');
  const [dataClassificationItemId, setDataClassificationItemId] = useState('');
  useFocusFirstError(formRef, save.fieldErrors);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (
      !save.validate({
        file: file === null ? 'REQUIRED' : null,
        'title.text': checkText(title, { required: true, maxLength: TEXT_LENGTH }),
        'description.text': checkText(description, { maxLength: TEXT_LENGTH }),
        documentTypeItemId: documentTypeItemId === '' ? 'REQUIRED' : null,
        dataClassificationItemId: dataClassificationItemId === '' ? 'REQUIRED' : null,
      }) ||
      file === null
    ) {
      return;
    }
    const descriptionText = optionalText(description);
    // Tagged with the interface language: the language the person is writing in (ADR-012 extension, ERD D-7).
    const draft = {
      title: { text: title.trim(), language },
      description: descriptionText === null ? null : { text: descriptionText, language },
      documentTypeItemId,
      dataClassificationItemId,
      projectId,
    };
    const result = await upload.run(async (onProgress, signal) => {
      const created = await documentsApi.create(file, draft, onProgress, signal);
      if (created.latestVersion === null) {
        throw new Error('The API returned a document without its first version.');
      }
      return created.latestVersion;
    });
    if (result.ok) {
      onUploaded();
    }
  };

  const typeOptions = lookups.options('DOCUMENT_TYPE');
  const classificationOptions = lookups.options('DATA_CLASSIFICATION');
  const lookupsUnavailable = lookups.error !== null;
  // The form stays mounted (hidden) while the file is sent, so a refused upload comes back with the same choices.
  const busy = upload.progress !== null || upload.version !== null;
  return (
    <>
      {busy && (
        <UploadOutcome
          upload={upload}
          onClose={onClose}
          link={
            upload.version === null ? null : (
              <Link className="button" to={`/documents/${upload.version.documentId}`}>
                {t('documents.upload.openDocument')}
              </Link>
            )
          }
        />
      )}
      <form
        ref={formRef}
        className="form"
        noValidate
        hidden={busy}
        onSubmit={(event) => void submit(event)}
      >
        <p>{t('documents.upload.body')}</p>
        <FormAlert message={save.formError} />
        <FormAlert
          message={
            lookupsUnavailable
              ? `${t('documents.lookups.unavailable')} ${documentProblemMessage(lookups.error, t)}`
              : null
          }
        />
        <FileField
          label={t('documents.fields.file')}
          name="file"
          onChange={setFile}
          required
          hint={t('documents.upload.fileHint')}
          error={upload.fileError}
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
          typeOptions={typeOptions}
          classificationOptions={classificationOptions}
          loading={lookups.loading}
          fieldErrors={save.fieldErrors}
        />
        <div className="form__actions">
          <button
            type="submit"
            className="button button--primary"
            disabled={save.saving || lookupsUnavailable}
          >
            {t('documents.upload.confirm')}
          </button>
          <button type="button" className="button" onClick={onClose}>
            {t('common.actions.cancel')}
          </button>
        </div>
      </form>
    </>
  );
}
