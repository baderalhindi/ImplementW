import { type ReactElement, type SyntheticEvent, useCallback, useRef, useState } from 'react';

import { checkText } from '@/features/identity-access/forms.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { RadioGroupField, SelectField, TextAreaField } from '@/shared/ui/FormFields.tsx';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { usersApi } from '../api/identityAccessApi.ts';
import { type DataScope, type UserPermissionEntry } from '../api/types.ts';
import { useFocusFirstError, useSaveAction } from '../forms.ts';
import { labelOf } from '../lookups.ts';
import { useCan } from '../permissions/permissions.ts';
import { problemMessage } from '../problems.ts';
import { DATA_SCOPES } from '../roles/profileGrid.ts';

type Choice = 'GRANT' | 'REMOVE' | 'ROLE_DEFAULT';

/**
 * Client review, item 5: start from the user's roles, then grant or remove single permissions for them. It applies at once, with a required
 * reason, and the API holds it to the safety rules no one can override.
 */
export function UserPermissionsSection({
  userId,
  displayName,
}: {
  userId: string;
  displayName: string;
}): ReactElement | null {
  const { t, language } = useI18n();
  const { session } = useSession();
  const mayRead = useCan('ROLE_VIEW');
  const ownAccount = session?.user.id === userId;
  const mayAdjust = useCan('ROLE_ASSIGN') && !ownAccount;
  const [editing, setEditing] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const load = useCallback((signal: AbortSignal) => usersApi.permissions(userId, signal), [userId]);
  const permissions = useApiResource(load);

  if (!mayRead) {
    return null;
  }

  const items = permissions.data?.items ?? [];
  const adjusted = items.filter((item) => item.overrideEffect !== null);
  const held = items.filter((item) => item.isHeld);
  const name = (item: UserPermissionEntry) => labelOf(item.name, language);
  const scopeOf = (scope: DataScope) => t(`identityAccess.dataScope.${scope}`);
  const fromRoles = (item: UserPermissionEntry) =>
    item.fromRoles.length === 0
      ? t('identityAccess.users.permissions.notFromRole')
      : item.fromRoles.map((grant) => `${grant.roleCode} (${scopeOf(grant.dataScope)})`).join('، ');

  return (
    <section className="section" aria-labelledby="user-permissions-heading">
      <div className="section__header">
        <h2 id="user-permissions-heading">{t('identityAccess.users.permissions.title')}</h2>
        {mayAdjust && items.length > 0 && (
          <button
            type="button"
            className="button button--primary"
            onClick={() => {
              setSaved(false);
              setEditing(items[0]?.permissionCode ?? null);
            }}
          >
            {t('identityAccess.users.permissions.adjust')}
          </button>
        )}
      </div>
      <p>{t('identityAccess.users.permissions.description')}</p>
      {ownAccount && (
        <p className="form__note">{t('identityAccess.users.permissions.ownAccount')}</p>
      )}
      {saved && (
        <p className="notice" role="status">
          {t('identityAccess.users.permissions.saved')}
        </p>
      )}
      {permissions.loading && permissions.data === undefined && <LoadingState />}
      {permissions.error !== null && (
        <ErrorState
          error={permissions.error}
          message={problemMessage(permissions.error, t)}
          onRetry={permissions.reload}
        />
      )}
      {permissions.data !== undefined &&
        (adjusted.length === 0 ? (
          <EmptyState title={t('identityAccess.users.permissions.none')} />
        ) : (
          <TableContainer
            caption={t('identityAccess.users.permissions.caption', { name: displayName })}
          >
            <thead>
              <tr>
                <th scope="col">{t('identityAccess.users.permissions.permission')}</th>
                <th scope="col">{t('identityAccess.users.permissions.fromRole')}</th>
                <th scope="col">{t('identityAccess.users.permissions.adjustment')}</th>
                <th scope="col">{t('identityAccess.users.permissions.reason')}</th>
                {mayAdjust && <th scope="col">{t('common.actions.edit')}</th>}
              </tr>
            </thead>
            <tbody>
              {adjusted.map((item) => (
                <tr key={item.permissionCode}>
                  <th scope="row">{name(item)}</th>
                  <td>{fromRoles(item)}</td>
                  <td>
                    {item.overrideEffect === 'GRANT' && item.overrideScope !== null
                      ? t('identityAccess.users.permissions.granted', {
                          scope: scopeOf(item.overrideScope),
                        })
                      : t('identityAccess.users.permissions.removed')}
                  </td>
                  <td>{item.overrideReason?.text}</td>
                  {mayAdjust && (
                    <td>
                      <button
                        type="button"
                        className="button"
                        aria-label={t('identityAccess.users.permissions.changeOf', {
                          permission: name(item),
                        })}
                        onClick={() => {
                          setSaved(false);
                          setEditing(item.permissionCode);
                        }}
                      >
                        {t('common.actions.edit')}
                      </button>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </TableContainer>
        ))}
      {held.length > 0 && (
        <details>
          <summary>{t('identityAccess.users.permissions.held', { count: held.length })}</summary>
          <ul>
            {held.map((item) => (
              <li key={item.permissionCode}>
                {name(item)} — {fromRoles(item)}
              </li>
            ))}
          </ul>
        </details>
      )}
      <Dialog
        open={editing !== null}
        title={t('identityAccess.users.permissions.adjustFor', { name: displayName })}
        onClose={() => {
          setEditing(null);
        }}
      >
        {editing !== null && (
          <AdjustPermissionForm
            key={editing}
            userId={userId}
            items={items}
            initialCode={editing}
            onClose={() => {
              setEditing(null);
            }}
            onSaved={() => {
              setEditing(null);
              setSaved(true);
              permissions.reload();
            }}
          />
        )}
      </Dialog>
    </section>
  );
}

function AdjustPermissionForm({
  userId,
  items,
  initialCode,
  onClose,
  onSaved,
}: {
  userId: string;
  items: UserPermissionEntry[];
  initialCode: string;
  onClose: () => void;
  onSaved: () => void;
}): ReactElement {
  const { t, language } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction();
  useFocusFirstError(formRef, save.fieldErrors);
  const initial = items.find((item) => item.permissionCode === initialCode);
  const [code, setCode] = useState(initialCode);
  const [choice, setChoice] = useState<Choice>(initial?.overrideEffect ?? 'GRANT');
  const [scope, setScope] = useState<DataScope>(initial?.overrideScope ?? 'OWN');
  const [reason, setReason] = useState('');

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!save.validate({ reason: checkText(reason, { required: true }) })) {
      return;
    }
    const text = { text: reason.trim(), language };
    const result = await save.run(() =>
      choice === 'ROLE_DEFAULT'
        ? usersApi.clearPermission(userId, code, text)
        : usersApi.setPermission(userId, code, {
            effect: choice,
            dataScope: choice === 'GRANT' ? scope : null,
            reason: text,
          }),
    );
    if (result.ok) {
      onSaved();
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p className="form__note">{t('identityAccess.stepUpNote')}</p>
      <FormAlert message={save.formError} />
      <SelectField
        label={t('identityAccess.users.permissions.permission')}
        name="permissionCode"
        required
        value={code}
        options={items.map((item) => ({
          value: item.permissionCode,
          label: labelOf(item.name, language),
        }))}
        onChange={setCode}
      />
      <RadioGroupField
        label={t('identityAccess.users.permissions.adjustment')}
        name="effect"
        value={choice}
        options={[
          { value: 'GRANT', label: t('identityAccess.users.permissions.choiceGrant') },
          { value: 'REMOVE', label: t('identityAccess.users.permissions.choiceRemove') },
          { value: 'ROLE_DEFAULT', label: t('identityAccess.users.permissions.choiceRole') },
        ]}
        onChange={(value) => {
          setChoice(value as Choice);
        }}
      />
      {choice === 'GRANT' && (
        <SelectField
          label={t('identityAccess.users.permissions.scope')}
          name="dataScope"
          required
          value={scope}
          options={DATA_SCOPES.map((value) => ({
            value,
            label: t(`identityAccess.dataScope.${value}`),
          }))}
          onChange={(value) => {
            setScope(value as DataScope);
          }}
        />
      )}
      <TextAreaField
        label={t('identityAccess.users.permissions.reason')}
        name="reason"
        required
        value={reason}
        onChange={setReason}
        error={save.fieldErrors.reason}
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
