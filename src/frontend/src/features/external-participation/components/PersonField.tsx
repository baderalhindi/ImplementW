import { type ReactElement, useId } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { UserPicker } from '@/features/identity-access/components/UserPicker.tsx';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type AssigneeValue, NONE, OTHER } from '@/features/tasks/assignee.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

interface PersonFieldProps {
  label: string;
  /** The API's field, so its refusal (…_INELIGIBLE) lands here. */
  name: 'responsibleUserId' | 'reviewerUserId';
  /** A responder is an entity user of the addressed entity; a reviewer an AHDA user (TASK-066 D-7, D-11); a monthly update's responder either. */
  userType: 'EXTERNAL' | 'INTERNAL' | 'ANY';
  user: SessionUser;
  value: AssigneeValue;
  onChange: (value: AssigneeValue) => void;
  /** Only people of this entity: a responder. */
  externalEntityId?: string | null;
  /** People to offer by name besides the person themself and whoever is named now. */
  suggestions?: { id: string; label: string }[];
  /** A draft may leave the person unnamed until it is issued. */
  allowNone: boolean;
  hint: string;
  error?: string | undefined;
}

/**
 * The responder or the reviewer of a request (SCR-161, MOD-112). Offered by name: the person themself for a reviewer,
 * and whoever is named now; anyone else is found by name. Whether they are eligible is the API's
 * decision, shown on this field.
 */
export function PersonField({
  label,
  name,
  userType,
  user,
  value,
  onChange,
  externalEntityId,
  suggestions = [],
  allowNone,
  hint,
  error,
}: PersonFieldProps): ReactElement {
  const { t } = useI18n();
  const id = useId();
  const named = value.choice !== NONE && value.choice !== OTHER ? value.choice : null;
  const personName = usePersonNames([named]);
  const self = userType === 'INTERNAL' && user.userType !== 'EXTERNAL' ? user.id : null;
  const people = [...new Set([self, named].filter((p): p is string => p !== null))].filter(
    (personId) => !suggestions.some((suggestion) => suggestion.id === personId),
  );
  const options = [
    ...suggestions.map((suggestion) => ({ value: suggestion.id, label: suggestion.label })),
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
      {value.choice === OTHER && (
        <UserPicker
          label={t('externalParticipation.people.find')}
          value={value.found}
          {...(externalEntityId == null ? {} : { externalEntityId })}
          {...(userType === 'ANY' ? {} : { userType })}
          onChange={(found) => {
            onChange({ ...value, found });
          }}
          required
        />
      )}
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
