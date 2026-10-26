import { type ReactElement, useId } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { UserPicker } from '@/features/identity-access/components/UserPicker.tsx';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type AssigneeValue, NONE, OTHER } from '@/features/tasks/assignee.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TextField } from '@/shared/ui/FormFields.tsx';

interface PersonFieldProps {
  label: string;
  /** The API's field, so its refusal (…_INELIGIBLE) lands here. */
  name: 'responsibleUserId' | 'reviewerUserId';
  /** A responder is an entity user of the addressed entity; a reviewer an AHDA user (TASK-066 D-7, D-11). */
  userType: 'EXTERNAL' | 'INTERNAL';
  user: SessionUser;
  value: AssigneeValue;
  onChange: (value: AssigneeValue) => void;
  /** Whether the person may search people (USER_VIEW, R01 only today); otherwise someone is named by user id. */
  canSearch: boolean;
  /** A draft may leave the person unnamed until it is issued. */
  allowNone: boolean;
  hint: string;
  error?: string | undefined;
}

/**
 * The responder or the reviewer of a request (SCR-161, MOD-112). Offered by name: the person themself for a reviewer,
 * and whoever is named now; anyone else is found or given by user id. Whether they are eligible is the API's
 * decision, shown on this field.
 */
export function PersonField({
  label,
  name,
  userType,
  user,
  value,
  onChange,
  canSearch,
  allowNone,
  hint,
  error,
}: PersonFieldProps): ReactElement {
  const { t } = useI18n();
  const id = useId();
  const named = value.choice !== NONE && value.choice !== OTHER ? value.choice : null;
  const personName = usePersonNames([named]);
  const self = userType === 'INTERNAL' && user.userType !== 'EXTERNAL' ? user.id : null;
  const people = [...new Set([self, named].filter((p): p is string => p !== null))];
  const options = [
    ...people.map((personId) => ({
      value: personId,
      label:
        personId === user.id
          ? t('externalParticipation.people.self', { name: user.displayName })
          : personName(personId),
    })),
    { value: OTHER, label: t('externalParticipation.people.other') },
    ...(allowNone ? [{ value: NONE, label: t('externalParticipation.people.later') }] : []),
  ];
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;

  return (
    <fieldset
      className="field field--group"
      aria-describedby={error === undefined ? hintId : `${hintId} ${errorId}`}
    >
      <legend className="field__label">{label}</legend>
      <div className="field__options">
        {options.map((option) => (
          <label key={option.value} className="field__option">
            <input
              type="radio"
              name={`${id}-${name}`}
              value={option.value}
              checked={value.choice === option.value}
              aria-invalid={error !== undefined && value.choice === option.value ? true : undefined}
              onChange={() => {
                onChange({ ...value, choice: option.value });
              }}
            />
            <span dir="auto">{option.label}</span>
          </label>
        ))}
      </div>
      {value.choice === OTHER &&
        (canSearch ? (
          <UserPicker
            label={t('externalParticipation.people.find')}
            value={value.found}
            userType={userType}
            onChange={(found) => {
              onChange({ ...value, found });
            }}
            required
          />
        ) : (
          <TextField
            label={t('externalParticipation.people.userId')}
            name={name}
            value={value.typedId}
            dir="ltr"
            required
            hint={t('externalParticipation.people.userIdHint')}
            onChange={(typedId) => {
              onChange({ ...value, typedId });
            }}
          />
        ))}
      <p id={hintId} className="field__hint">
        {hint}
      </p>
      {error !== undefined && (
        <p id={errorId} className="field__error">
          {error}
        </p>
      )}
    </fieldset>
  );
}
