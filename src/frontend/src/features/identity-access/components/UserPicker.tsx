import { type ReactElement, useEffect, useId, useState } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { SEARCH_TYPING_PAUSE_MS } from '@/shared/ui/useSearchText.ts';

import { personLookupsApi } from '../api/identityAccessApi.ts';
import { type PersonMatch, type UserSummary, type UserType } from '../api/types.ts';
import { problemMessage } from '../problems.ts';

const RESULT_LIMIT = 10;

/** What a form keeps of the person chosen. */
export type PickedUser = Pick<UserSummary, 'id' | 'displayName' | 'username'> &
  Partial<Pick<UserSummary, 'userType' | 'externalEntityId' | 'departmentId'>>;

interface UserPickerProps {
  label: string;
  value: PickedUser | null;
  onChange: (user: PickedUser | null) => void;
  /** Restricts the search, e.g. a sponsor must be an active internal user (ADR-013). */
  userType?: UserType;
  /** Only people of this entity: a monthly batch's responder. */
  externalEntityId?: string | null;
  /** Only people holding a role over this project: a task's or a risk's owner. */
  projectId?: string | null;
  error?: string | undefined;
  hint?: string;
  required?: boolean;
}

/**
 * Finds an active person by name, username or email as it is typed (GET /person-lookups) and selects one. Only those the field may name are
 * offered, with their role and unit so that two people of one name can be told apart.
 */
export function UserPicker({
  label,
  value,
  onChange,
  userType,
  externalEntityId,
  projectId,
  error,
  hint,
  required = false,
}: UserPickerProps): ReactElement {
  const { t, language } = useI18n();
  const id = useId();
  const [text, setText] = useState('');
  const [results, setResults] = useState<PersonMatch[] | null>(null);
  const [searchError, setSearchError] = useState<string | null>(null);

  const wanted = text.trim();
  useEffect(() => {
    if (wanted === '') {
      return undefined;
    }
    const controller = new AbortController();
    const search = async () => {
      setSearchError(null);
      try {
        const page = await personLookupsApi.list(
          {
            q: wanted,
            userType,
            externalEntityId: externalEntityId ?? undefined,
            projectId: projectId ?? undefined,
            pageSize: RESULT_LIMIT,
          },
          controller.signal,
        );
        if (!controller.signal.aborted) {
          setResults(page.items);
        }
      } catch (caught) {
        if (!controller.signal.aborted) {
          setResults(null);
          setSearchError(problemMessage(caught, t));
        }
      }
    };
    const timer = setTimeout(() => void search(), SEARCH_TYPING_PAUSE_MS);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [externalEntityId, projectId, t, userType, wanted]);

  const clearSearch = () => {
    setResults(null);
    setSearchError(null);
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
              autoComplete="off"
              placeholder={t('identityAccess.userPicker.searchLabel')}
              value={text}
              onChange={(event) => {
                setText(event.target.value);
                if (event.target.value.trim() === '') {
                  clearSearch();
                }
              }}
              onKeyDown={(event) => {
                if (event.key === 'Enter') {
                  event.preventDefault();
                }
              }}
            />
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
                    {describe(user, language) !== '' && (
                      <span className="picker__detail">{describe(user, language)}</span>
                    )}
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
              setText('');
              clearSearch();
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

/** The person's roles and unit, in the reader's language. */
function describe(person: PersonMatch, language: 'ar' | 'en'): string {
  const name = (label: { ar: string; en: string }) => (language === 'ar' ? label.ar : label.en);
  const parts = [...(person.roles ?? []).map(name), ...(person.unit ? [name(person.unit)] : [])];
  return parts.join(' · ');
}
