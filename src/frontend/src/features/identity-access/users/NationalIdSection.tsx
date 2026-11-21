import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { usersApi } from '../api/identityAccessApi.ts';
import { type UserDetail } from '../api/types.ts';
import { checkNationalId, useFocusFirstError, useSaveAction } from '../forms.ts';
import { useCan } from '../permissions/permissions.ts';
import { problemMessage } from '../problems.ts';

/**
 * Client review, item 7: the National ID or Iqama number, shown masked. A holder of USER_NATIONAL_ID_VIEW may show it in full, each time
 * on purpose and audited by the API; it is held only on this screen and only until hidden. USER_MANAGE records, changes or removes it.
 */
export function NationalIdSection({
  user,
  etag,
  onChanged,
}: {
  user: UserDetail;
  etag: string | null;
  onChanged: () => void;
}): ReactElement {
  const { t } = useI18n();
  const mayReveal = useCan('USER_NATIONAL_ID_VIEW');
  const mayManage = useCan('USER_MANAGE');
  const [revealed, setRevealed] = useState<string | null>(null);
  const [revealError, setRevealError] = useState<string | null>(null);
  const [editing, setEditing] = useState(false);
  const masked = user.nationalIdMasked ?? null;

  return (
    <section className="section" aria-labelledby="national-id-heading">
      <div className="section__header">
        <h2 id="national-id-heading">{t('identityAccess.users.nationalId.title')}</h2>
        {mayManage && (
          <button
            type="button"
            className="button"
            onClick={() => {
              setEditing(true);
            }}
          >
            {masked === null
              ? t('identityAccess.users.nationalId.add')
              : t('identityAccess.users.nationalId.change')}
          </button>
        )}
      </div>
      <p dir="ltr" className="national-id">
        {revealed ?? masked ?? t('identityAccess.users.nationalId.none')}
      </p>
      {revealError !== null && <FormAlert message={revealError} />}
      {mayReveal && masked !== null && (
        <button
          type="button"
          className="button"
          onClick={() => {
            if (revealed !== null) {
              setRevealed(null);
              return;
            }
            setRevealError(null);
            usersApi.revealNationalId(user.id).then(
              (response) => {
                setRevealed(response.data.nationalId);
              },
              (error: unknown) => {
                setRevealError(problemMessage(error, t));
              },
            );
          }}
        >
          {revealed === null
            ? t('identityAccess.users.nationalId.show')
            : t('identityAccess.users.nationalId.hide')}
        </button>
      )}
      <Dialog
        open={editing}
        title={t('identityAccess.users.nationalId.dialogTitle', { name: user.displayName })}
        onClose={() => {
          setEditing(false);
        }}
      >
        {editing && (
          <NationalIdForm
            user={user}
            etag={etag}
            onClose={() => {
              setEditing(false);
            }}
            onSaved={() => {
              setEditing(false);
              setRevealed(null);
              onChanged();
            }}
          />
        )}
      </Dialog>
    </section>
  );
}

function NationalIdForm({
  user,
  etag,
  onClose,
  onSaved,
}: {
  user: UserDetail;
  etag: string | null;
  onClose: () => void;
  onSaved: () => void;
}): ReactElement {
  const { t } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction();
  useFocusFirstError(formRef, save.fieldErrors);
  const [value, setValue] = useState('');

  const send = async (nationalId: string | null) => {
    const result = await save.run(() => usersApi.setNationalId(user.id, nationalId, etag));
    if (result.ok) {
      onSaved();
    }
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!save.validate({ nationalId: checkNationalId(value) })) {
      return;
    }
    await send(value.trim());
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p className="form__note">{t('identityAccess.stepUpNote')}</p>
      <FormAlert message={save.formError} />
      <TextField
        label={t('identityAccess.users.nationalId.field')}
        name="nationalId"
        required
        dir="ltr"
        inputMode="numeric"
        autoComplete="off"
        hint={t('identityAccess.users.nationalId.hint')}
        value={value}
        onChange={setValue}
        error={save.fieldErrors.nationalId}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('common.actions.save')}
        </button>
        {user.nationalIdMasked != null && (
          <button
            type="button"
            className="button button--danger"
            disabled={save.saving}
            onClick={() => void send(null)}
          >
            {t('identityAccess.users.nationalId.remove')}
          </button>
        )}
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
