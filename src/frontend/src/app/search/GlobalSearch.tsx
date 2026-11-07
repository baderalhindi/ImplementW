import {
  type KeyboardEvent,
  type ReactElement,
  useCallback,
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
} from 'react';
import { flushSync } from 'react-dom';
import { useNavigate } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';

import { type Menu } from '../menus.ts';
import {
  type GlobalSearchRecordType,
  MINIMUM_RECORD_SEARCH_LENGTH,
  recordPath,
  searchRecords,
} from './globalSearchApi.ts';

/** Long enough to skip the requests between keystrokes, short enough to read as immediate. */
const TYPING_PAUSE_MS = 150;
const MAXIMUM_TEXT_LENGTH = 200;
const MAXIMUM_SCREENS = 5;

const GROUP_LABELS: Record<GlobalSearchRecordType, TranslationKey> = {
  PROJECT: 'common.globalSearch.groups.projects',
  PROJECT_TASK: 'common.globalSearch.groups.tasks',
  RISK: 'common.globalSearch.groups.risks',
  MANAGEMENT_CONCERN: 'common.globalSearch.groups.concerns',
  CHANGE_REQUEST: 'common.globalSearch.groups.changeRequests',
  DOCUMENT: 'common.globalSearch.groups.documents',
};

const RECORD_TYPES = Object.keys(GROUP_LABELS) as GlobalSearchRecordType[];

interface SearchOption {
  id: string;
  to: string;
  primary: string;
  secondary: string | null;
}

interface SearchGroup {
  label: string;
  options: SearchOption[];
}

/** Arabic is matched without its vowel marks and whichever way a person writes alef, teh marbuta or yeh. */
function comparable(text: string): string {
  return text
    .toLocaleLowerCase()
    .replace(/[ً-ْ]/gu, '')
    .replace(/[أإآ]/gu, 'ا')
    .replace(/ة/gu, 'ه')
    .replace(/ى/gu, 'ي');
}

/**
 * The search in the top bar: the screens this person's menus offer, matched here on each keystroke, and the records the API says they may read
 * once there are two characters. The WAI-ARIA combobox pattern: focus stays in the input and the arrow keys move the active option.
 */
export function GlobalSearch({ menus }: { menus: Menu[] }): ReactElement {
  const { t } = useI18n();
  const navigate = useNavigate();
  const id = useId();
  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const toggleRef = useRef<HTMLButtonElement>(null);
  const [text, setText] = useState('');
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);
  const [collapsed, setCollapsed] = useState(true);

  const wanted = text.trim();
  const isCollapsed = collapsed && text === '';
  const reveal = useCallback((): void => {
    // The box cannot take the focus until it has stopped being hidden.
    flushSync(() => {
      setCollapsed(false);
    });
    inputRef.current?.focus();
  }, []);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setQuery(wanted);
    }, TYPING_PAUSE_MS);
    return () => {
      window.clearTimeout(timer);
    };
  }, [wanted]);

  const load = useCallback(
    (signal: AbortSignal) =>
      query.length < MINIMUM_RECORD_SEARCH_LENGTH
        ? Promise.resolve([])
        : searchRecords(query, signal),
    [query],
  );
  const records = useApiResource(load);
  const searching =
    wanted.length >= MINIMUM_RECORD_SEARCH_LENGTH && (query !== wanted || records.loading);

  const screens = useMemo<SearchOption[]>(() => {
    if (wanted === '') {
      return [];
    }
    const needle = comparable(wanted);
    const offered = new Map<string, SearchOption>();
    for (const menu of menus) {
      const items = menu.items.length > 0 ? menu.items : [{ to: menu.to, label: menu.label }];
      for (const item of items) {
        const label = t(item.label);
        if (!offered.has(item.to) && comparable(label).includes(needle)) {
          offered.set(item.to, {
            id: `screen:${item.to}`,
            to: item.to,
            primary: label,
            secondary: t(menu.label),
          });
        }
      }
    }
    return [...offered.values()].slice(0, MAXIMUM_SCREENS);
  }, [menus, t, wanted]);

  const groups = useMemo<SearchGroup[]>(() => {
    const hits = query === wanted ? (records.data ?? []) : [];
    const recordGroups = RECORD_TYPES.map((type) => ({
      label: t(GROUP_LABELS[type]),
      options: hits
        .filter((hit) => hit.type === type)
        .map((hit) => ({
          id: `${type}:${hit.id}`,
          to: recordPath(hit),
          primary: hit.title.text,
          secondary: hit.projectTitle?.text ?? hit.reference,
        })),
    }));
    return [
      { label: t('common.globalSearch.groups.screens'), options: screens },
      ...recordGroups,
    ].filter((group) => group.options.length > 0);
  }, [query, records.data, screens, t, wanted]);

  const options = groups.flatMap((group) => group.options);
  const active = options[activeIndex];
  const expanded = open && wanted !== '';
  const listId = `${id}-results`;
  const optionId = (option: SearchOption) => `${id}-${option.id}`;

  useEffect(() => {
    const focusOnShortcut = (event: globalThis.KeyboardEvent): void => {
      const typing =
        event.target instanceof HTMLElement &&
        (event.target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(event.target.tagName));
      if (
        ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') ||
        (event.key === '/' && !typing && !event.ctrlKey && !event.metaKey && !event.altKey)
      ) {
        event.preventDefault();
        reveal();
      }
    };
    document.addEventListener('keydown', focusOnShortcut);
    return () => {
      document.removeEventListener('keydown', focusOnShortcut);
    };
  }, [reveal]);

  useEffect(() => {
    if (!open) {
      return undefined;
    }
    const closeOnOutsideClick = (event: PointerEvent): void => {
      if (!containerRef.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener('pointerdown', closeOnOutsideClick);
    return () => {
      document.removeEventListener('pointerdown', closeOnOutsideClick);
    };
  }, [open]);

  const choose = (option: SearchOption): void => {
    setText('');
    setQuery('');
    setOpen(false);
    setActiveIndex(-1);
    setCollapsed(true);
    inputRef.current?.blur();
    void navigate(option.to);
  };

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>): void => {
    if (event.key === 'Escape') {
      if (expanded) {
        event.preventDefault();
        setOpen(false);
      } else {
        flushSync(() => {
          setText('');
          setCollapsed(true);
        });
        toggleRef.current?.focus();
      }
      setActiveIndex(-1);
    } else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      setOpen(true);
      if (options.length > 0) {
        const step = event.key === 'ArrowDown' ? 1 : -1;
        const start = activeIndex === -1 && step === -1 ? 0 : activeIndex;
        setActiveIndex((start + step + options.length) % options.length);
      }
    } else if (event.key === 'Enter' && expanded) {
      event.preventDefault();
      const chosen = active ?? options[0];
      if (chosen !== undefined) {
        choose(chosen);
      }
    }
  };

  let message: string | null = null;
  if (searching) {
    message = t('common.globalSearch.searching');
  } else if (records.error !== null) {
    message = t('common.globalSearch.failed');
  } else if (options.length === 0) {
    message =
      wanted.length < MINIMUM_RECORD_SEARCH_LENGTH
        ? t('common.globalSearch.keepTyping')
        : t('common.globalSearch.noResults', { text: wanted });
  }

  return (
    <div
      className={isCollapsed ? 'global-search global-search--collapsed' : 'global-search'}
      ref={containerRef}
    >
      <button
        ref={toggleRef}
        type="button"
        className="global-search__toggle"
        hidden={!isCollapsed}
        aria-label={t('common.globalSearch.label')}
        title={t('common.globalSearch.label')}
        onClick={reveal}
      />
      <svg className="global-search__icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
        <circle cx="11" cy="11" r="7" />
        <path d="m20 20-3.5-3.5" />
      </svg>
      <input
        ref={inputRef}
        className="global-search__input"
        type="search"
        role="combobox"
        name="global-search"
        onBlur={() => {
          if (text === '') {
            setCollapsed(true);
          }
        }}
        autoComplete="off"
        maxLength={MAXIMUM_TEXT_LENGTH}
        placeholder={t('common.globalSearch.placeholder')}
        aria-label={t('common.globalSearch.label')}
        aria-autocomplete="list"
        aria-expanded={expanded}
        aria-controls={listId}
        aria-activedescendant={expanded && active !== undefined ? optionId(active) : undefined}
        aria-keyshortcuts="Control+K Meta+K"
        value={text}
        onChange={(event) => {
          setText(event.target.value);
          setOpen(true);
          setActiveIndex(-1);
        }}
        onFocus={() => {
          setOpen(true);
        }}
        onKeyDown={onKeyDown}
      />
      <div className="global-search__panel" hidden={!expanded}>
        <div id={listId} role="listbox" aria-label={t('common.globalSearch.label')}>
          {groups.map((group) => (
            <div key={group.label} role="group" aria-label={group.label}>
              <div className="global-search__group" role="presentation">
                {group.label}
              </div>
              {group.options.map((option) => (
                <div
                  key={option.id}
                  id={optionId(option)}
                  role="option"
                  className="global-search__option"
                  aria-selected={option === active}
                  onPointerDown={(event) => {
                    // Keeps the focus in the input until the choice is made.
                    event.preventDefault();
                  }}
                  onClick={() => {
                    choose(option);
                  }}
                >
                  <bdi className="global-search__primary">{option.primary}</bdi>
                  {option.secondary !== null && (
                    <bdi className="global-search__secondary">{option.secondary}</bdi>
                  )}
                </div>
              ))}
            </div>
          ))}
        </div>
        <p className="global-search__message" role="status" hidden={message === null}>
          {message}
        </p>
      </div>
    </div>
  );
}
