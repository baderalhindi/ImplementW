import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { type UserSummary } from '@/features/identity-access/api/types.ts';
import { UserPicker } from '@/features/identity-access/components/UserPicker.tsx';
import {
  checkText,
  optionalText,
  useFocusFirstError,
  useSaveAction,
} from '@/features/identity-access/forms.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { approvalDelegationsApi } from '../api/approvalsApi.ts';
import { approvalProblemMessage } from '../problems.ts';

/** ApprovalDelegationCreateRequest.RoutingKeyLength on the API. */
const ROUTING_KEY_LENGTH = 100;

function toIsoOrNull(localDateTime: string): string | null {
  return localDateTime === '' ? null : new Date(localDateTime).toISOString();
}

interface DelegateDialogProps {
  open: boolean;
  onClose: () => void;
  onDone: () => void;
}

/**
 * MOD-043 Delegate. The delegate may then decide what the delegator could decide themselves, judged when the delegate
 * acts, and nothing more (TASK-035 D-5). Only an active internal user can be a delegate (ADR-013), so the search is
 * restricted to internal users; the API makes the same check.
 */
export function DelegateDialog({ open, onClose, onDone }: DelegateDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={open} title={t('approvals.delegate.title')} onClose={onClose}>
      <DelegateForm onClose={onClose} onDone={onDone} />
    </Dialog>
  );
}

function DelegateForm({ onClose, onDone }: Omit<DelegateDialogProps, 'open'>): ReactElement {
  const { t } = useI18n();
  const save = useSaveAction(approvalProblemMessage);
  const formRef = useRef<HTMLFormElement>(null);
  const [delegate, setDelegate] = useState<UserSummary | null>(null);
  const [routingKey, setRoutingKey] = useState('');
  const [validFrom, setValidFrom] = useState('');
  const [validTo, setValidTo] = useState('');
  useFocusFirstError(formRef, save.fieldErrors);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    const valid = save.validate({
      delegateUserId: delegate === null ? 'REQUIRED' : null,
      routingKey: checkText(routingKey, { maxLength: ROUTING_KEY_LENGTH }),
      validTo:
        validTo === ''
          ? 'REQUIRED'
          : validFrom !== '' && validTo <= validFrom
            ? 'DATE_BEFORE_START'
            : null,
    });
    const validToIso = toIsoOrNull(validTo);
    if (!valid || delegate === null || validToIso === null) {
      return;
    }
    const result = await save.run(() =>
      approvalDelegationsApi.create({
        delegateUserId: delegate.id,
        routingKey: optionalText(routingKey),
        validFrom: toIsoOrNull(validFrom),
        validTo: validToIso,
      }),
    );
    if (result.ok) {
      onDone();
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p className="form__note">{t('approvals.delegate.note')}</p>
      <FormAlert message={save.formError} />
      <UserPicker
        label={t('approvals.delegate.delegate')}
        value={delegate}
        onChange={setDelegate}
        userType="INTERNAL"
        hint={t('approvals.delegate.delegateHint')}
        error={save.fieldErrors.delegateUserId}
        required
      />
      <TextField
        label={t('approvals.delegate.routingKey')}
        name="routingKey"
        value={routingKey}
        onChange={setRoutingKey}
        dir="ltr"
        hint={t('approvals.delegate.routingKeyHint')}
        error={save.fieldErrors.routingKey}
      />
      <TextField
        label={t('approvals.delegate.validFrom')}
        name="validFrom"
        type="datetime-local"
        value={validFrom}
        onChange={setValidFrom}
        hint={t('approvals.delegate.validFromHint')}
        error={save.fieldErrors.validFrom}
      />
      <TextField
        label={t('approvals.delegate.validTo')}
        name="validTo"
        type="datetime-local"
        value={validTo}
        onChange={setValidTo}
        error={save.fieldErrors.validTo}
        required
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('approvals.delegate.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
