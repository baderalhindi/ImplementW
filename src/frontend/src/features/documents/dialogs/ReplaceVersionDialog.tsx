import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { useFocusFirstError } from '@/features/identity-access/forms.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { FileField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { documentsApi } from '../api/documentsApi.ts';
import { UploadOutcome } from '../upload/UploadOutcome.tsx';
import { useFileUpload } from '../upload/useFileUpload.ts';

interface ReplaceVersionDialogProps {
  /** The ACTIVE document to add a version to; null while closed. */
  documentId: string | null;
  onClose: () => void;
  /** The API accepted the file; the dialog goes on to show its scan. */
  onUploaded: () => void;
}

/**
 * MOD-052 Replace Version: a new file becomes the next version. Earlier versions, and evidence pinned to them, never
 * change (TASK-037 D-5). It is also how a quarantined file is replaced by a clean one.
 */
export function ReplaceVersionDialog({
  documentId,
  onClose,
  onUploaded,
}: ReplaceVersionDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={documentId !== null} title={t('documents.replace.title')} onClose={onClose}>
      {documentId !== null && (
        <ReplaceForm documentId={documentId} onClose={onClose} onUploaded={onUploaded} />
      )}
    </Dialog>
  );
}

function ReplaceForm({
  documentId,
  onClose,
  onUploaded,
}: ReplaceVersionDialogProps & { documentId: string }): ReactElement {
  const { t } = useI18n();
  const upload = useFileUpload();
  const { save } = upload;
  const formRef = useRef<HTMLFormElement>(null);
  const [file, setFile] = useState<File | null>(null);
  useFocusFirstError(formRef, save.fieldErrors);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!save.validate({ file: file === null ? 'REQUIRED' : null }) || file === null) {
      return;
    }
    const result = await upload.run((onProgress, signal) =>
      documentsApi.addVersion(documentId, file, onProgress, signal),
    );
    if (result.ok) {
      onUploaded();
    }
  };

  const busy = upload.progress !== null || upload.version !== null;
  return (
    <>
      {busy && <UploadOutcome upload={upload} onClose={onClose} link={null} />}
      <form
        ref={formRef}
        className="form"
        noValidate
        hidden={busy}
        onSubmit={(event) => void submit(event)}
      >
        <p>{t('documents.replace.body')}</p>
        <FormAlert message={save.formError} />
        <FileField
          label={t('documents.fields.file')}
          name="file"
          onChange={setFile}
          required
          hint={t('documents.upload.fileHint')}
          error={upload.fileError}
        />
        <div className="form__actions">
          <button type="submit" className="button button--primary" disabled={save.saving}>
            {t('documents.replace.confirm')}
          </button>
          <button type="button" className="button" onClick={onClose}>
            {t('common.actions.cancel')}
          </button>
        </div>
      </form>
    </>
  );
}
