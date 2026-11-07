import { type ReactElement, useId } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { UserPicker } from '@/features/identity-access/components/UserPicker.tsx';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { type AssigneeValue, NONE, OTHER } from '../assignee.ts';

interface AssigneeFieldProps {
  user: SessionUser;
  project: Pick<ProjectSummary, 'id' | 'projectManagerUserId'>;
  /** The project's current owners and the task's own: people already known to work on it. */
  knownUserIds: (string | null)[];
  value: AssigneeValue;
  onChange: (value: AssigneeValue) => void;
  error?: string | undefined;
}

/**
 * The task's owner (MOD-010, MOD-013, MOD-014), and a risk's or treatment action's (MOD-030, MOD-033, MOD-034, TASK-056:
 * the same rule, RISK_OWNER_NOT_ELIGIBLE). An owner must hold a role over the project (ERD "must hold a project
 * relationship", TASK-048 D-12): that is the API's decision, and its refusal is shown on this field (acceptance
 * criterion 2). Offered by name: the person, the Project Manager and whoever owns a task here already.
 */
export function AssigneeField({
  user,
  project,
  knownUserIds,
  value,
  onChange,
  error,
}: AssigneeFieldProps): ReactElement {
  const { t } = useI18n();
  const id = useId();
  const personName = usePersonNames(knownUserIds);
  const people = [
    ...new Set(
      [user.id, project.projectManagerUserId, ...knownUserIds].filter(
        (candidate): candidate is string => candidate !== null,
      ),
    ),
  ];
  const options = [
    ...people.map((personId) => ({
      value: personId,
      label:
        personId === user.id
          ? t('tasks.owner.self', { name: user.displayName })
          : personId === project.projectManagerUserId
            ? t('tasks.owner.manager', { name: personName(personId) })
            : personName(personId),
    })),
    { value: OTHER, label: t('tasks.owner.other') },
    { value: NONE, label: t('tasks.owner.none') },
  ];
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;

  return (
    <fieldset
      className="field field--group"
      aria-describedby={error === undefined ? hintId : `${hintId} ${errorId}`}
    >
      <legend className="field__label">{t('tasks.fields.owner')}</legend>
      <div className="field__options">
        {options.map((option) => (
          <label key={option.value} className="field__option">
            <input
              type="radio"
              name={`${id}-owner`}
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
          label={t('tasks.owner.find')}
          value={value.found}
          projectId={project.id}
          onChange={(found) => {
            onChange({ ...value, found });
          }}
          required
        />
      )}
      <p id={hintId} className="field__hint">
        {t('tasks.owner.hint')}
      </p>
      {error !== undefined && (
        <p id={errorId} className="field__error">
          {error}
        </p>
      )}
    </fieldset>
  );
}
