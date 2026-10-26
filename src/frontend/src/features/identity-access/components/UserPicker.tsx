import { type ReactElement, useId, useState } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { usersApi } from '../api/identityAccessApi.ts';
import { type UserSummary, type UserType } from '../api/types.ts';
import { problemMessage } from '../problems.ts';

const RESULT_LIMIT = 10;

interface UserPickerProps {
  label: string;
  value: UserSummary | null;
  onChange: (user: UserSummary | null) => void;
  /** Restricts the search, e.g. a sponsor must be an active internal user (ADR-013). */
  userType?: UserType;
  error?: string | undefined;
  hint?: string;
  required?: boolean;
}

/** Finds an active user by name, username or email (ADM-002's `q`) and selects one. */
export function UserPicker({
  label,
  value,
  onChange,
  userType,
  error,
  hint,
  required = false,
}: UserPickerProps): ReactElement {
  const { t } = useI18n();
  const id = useId();
  const [text, setText] = useState('');
  const [results, setResults] = useState<UserSummary[] | null>(null);
  const [searching, setSearching] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);

  const search = async () => {
    setSearching(true);
    setSearchError(null);
    try {
      const page = await usersApi.list({
        q: text.trim() === '' ? undefined : text.trim(),
        status: 'ACTIVE',
        userType,
        pageSize: RESULT_LIMIT,
      });
      setResults(page.items);
    } catch (caught) {
      setResults(null);
      setSearchError(problemMessage(caught, t));
    } finally {
      setSearching(false);
    }
  };

  const describedBy = [
    hint === undefined ? null : `${id}-hint`,
    error === undefined ? null : `${id}-error`,
  ]
    .filter((part): part is string => part !== null)
    .join(' ');

  return (
    <fieldset
      className="field field--group"
      aria-describedby={describedBy === '' ? undefined : describedBy}
      aria-invalid={error === undefined ? undefined : true}
    >
      <legend className="field__label">
        {label}
        {required && <span className="field__required">{` (${t('common.form.required')})`}</span>}
      </legend>
      {value === null ? (
        <>
          <div className="picker__search">
            <label className="visually-hidden" htmlFor={`${id}-q`}>
              {t('identityAccess.userPicker.searchLabel')}
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
                  void search();
                }
              }}
            />
            <button
              type="button"
              className="button"
              disabled={searching}
              onClick={() => void search()}
            >
              {searching ? t('common.states.searching') : t('common.actions.search')}
            </button>
          </div>
          {searchError !== null && (
            <p className="field__error" role="alert">
              {searchError}
            </p>
          )}
          {results !== null &&
            (results.length === 0 ? (
              <p className="field__hint" role="status">
                {t('identityAccess.userPicker.noResults')}
              </p>
            ) : (
              <ul className="picker__results" aria-label={t('identityAccess.userPicker.results')}>
                {results.map((user) => (
                  <li key={user.id}>
                    <button
                      type="button"
                      className="button button--link"
                      onClick={() => {
                        onChange(user);
                      }}
                    >
                      {user.displayName} <span dir="ltr">({user.username})</span>
                    </button>
                  </li>
                ))}
              </ul>
            ))}
        </>
      ) : (
        <div className="picker__selected">
          <span>
            {value.displayName} <span dir="ltr">({value.username})</span>
          </span>
          <button
            type="button"
            className="button"
            onClick={() => {
              setResults(null);
              onChange(null);
            }}
          >
            {t('identityAccess.userPicker.change')}
          </button>
        </div>
      )}
      {hint !== undefined && (
        <p id={`${id}-hint`} className="field__hint">
          {hint}
        </p>
      )}
      {error !== undefined && (
        <p id={`${id}-error`} className="field__error">
          {error}
        </p>
      )}
    </fieldset>
  );
}
