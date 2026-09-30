import { type ReactElement, type SyntheticEvent, useCallback, useId, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { EmptyState, ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { documentsApi } from '../api/documentsApi.ts';
import { type DocumentSummary } from '../api/types.ts';
import { ScanStateBadge } from '../components/ScanState.tsx';
import { isUsable, languageTag } from '../presentation.ts';
import { documentProblemMessage } from '../problems.ts';

/** Enough for a record's candidates; a longer list is narrowed by title. */
const CANDIDATE_LIMIT = 50;

interface AttachDocumentDialogProps {
  open: boolean;
  /** The project whose documents may be attached; null: any document the caller may read. */
  projectId: string | null;
  /** The record being attached to, as its module names it (e.g. a milestone's title). */
  targetLabel: string;
  /**
   * The owning module's attach call. WF-12 exposes no link API: a module links a document in process through
   * IDocumentLinks after authorizing its own record (TASK-037 D-8, F-8), so the module supplies the request.
   */
  onAttach: (document: DocumentSummary) => Promise<unknown>;
  onClose: () => void;
  onDone: () => void;
}

/**
 * MOD-054 Attach: choose one of the project's active documents for a record of another module. A document whose
 * latest version is not CLEAN — scanning, scan failed or quarantined — cannot be chosen: its content cannot be served
 * or be evidence (TASK-037 D-4, D-12), and the list says why next to it.
 */
export function AttachDocumentDialog(props: AttachDocumentDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={props.open} title={t('documents.attach.title')} onClose={props.onClose}>
      {props.open && <AttachForm {...props} />}
    </Dialog>
  );
}

function AttachForm({
  projectId,
  targetLabel,
  onAttach,
  onClose,
  onDone,
}: AttachDocumentDialogProps): ReactElement {
  const { t } = useI18n();
  const id = useId();
  const save = useSaveAction(documentProblemMessage);
  const [text, setText] = useState('');
  const [query, setQuery] = useState('');
  const [chosen, setChosen] = useState<DocumentSummary | null>(null);
  const load = useCallback(
    (signal: AbortSignal) =>
      documentsApi.list(
        {
          projectId: projectId ?? undefined,
          status: 'ACTIVE',
          q: query === '' ? undefined : query,
          pageSize: CANDIDATE_LIMIT,
        },
        signal,
      ),
    [projectId, query],
  );
  const candidates = useApiResource(load);

  const search = () => {
    setChosen(null);
    setQuery(text.trim());
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!save.validate({ document: chosen === null ? 'REQUIRED' : null }) || chosen === null) {
      return;
    }
    const result = await save.run(() => onAttach(chosen));
    if (result.ok) {
      onDone();
    }
  };

  const documentError = save.fieldErrors.document;
  return (
    <form className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p>{t('documents.attach.body', { target: targetLabel })}</p>
      <FormAlert message={save.formError} />
      <div className="picker__search">
        <label className="visually-hidden" htmlFor={`${id}-q`}>
          {t('documents.attach.searchLabel')}
        </label>
        <input
          id={`${id}-q`}
          className="field__input"
          type="search"
          value={text}
          onChange={(event) => {
            setText(event.target.value);
          }}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault();
              search();
            }
          }}
        />
        <button type="button" className="button" onClick={search}>
          {t('common.actions.search')}
        </button>
      </div>
      {candidates.loading && <LoadingState label={t('documents.attach.loading')} />}
      {candidates.error !== null && (
        <ErrorState
          message={documentProblemMessage(candidates.error, t)}
          onRetry={candidates.reload}
        />
      )}
      {candidates.data !== undefined &&
        (candidates.data.items.length === 0 ? (
          <EmptyState title={t('documents.attach.empty')} />
        ) : (
          <fieldset
            className="field field--group"
            aria-invalid={documentError === undefined ? undefined : true}
            aria-describedby={documentError === undefined ? undefined : `${id}-error`}
          >
            <legend className="field__label">
              {t('documents.attach.choose')}
              <span className="field__required">{` (${t('common.form.required')})`}</span>
            </legend>
            <ul className="choice-list">
              {candidates.data.items.map((document) => {
                const usable = isUsable(document.latestScanState);
                const reasonId = `${id}-${document.id}-reason`;
                return (
                  <li key={document.id} className="choice-list__item">
                    <label className="field__option">
                      <input
                        type="radio"
                        name="document"
                        value={document.id}
                        checked={chosen?.id === document.id}
                        disabled={!usable}
                        aria-describedby={usable ? undefined : reasonId}
                        onChange={() => {
                          setChosen(document);
                        }}
                      />
                      <span lang={languageTag(document.title.language)} dir="auto">
                        {document.title.text}
                      </span>
                    </label>
                    <span className="choice-list__meta">
                      {document.latestVersionNo !== null &&
                        t('documents.fields.versionNo', { number: document.latestVersionNo })}{' '}
                      {document.latestScanState !== null && (
                        <ScanStateBadge state={document.latestScanState} />
                      )}
                    </span>
                    {!usable && (
                      <span id={reasonId} className="choice-list__reason">
                        {t(
                          document.latestScanState === 'QUARANTINED'
                            ? 'documents.attach.quarantined'
                            : 'documents.attach.notClean',
                        )}
                      </span>
                    )}
                  </li>
                );
              })}
            </ul>
            {documentError !== undefined && (
              <p id={`${id}-error`} className="field__error">
                {t('documents.attach.chooseError')}
              </p>
            )}
          </fieldset>
        ))}
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('documents.attach.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
