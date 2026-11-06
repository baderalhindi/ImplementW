import {
  type KeyboardEvent,
  type ReactElement,
  type ReactNode,
  useId,
  useRef,
  useState,
} from 'react';

import { useSession } from '@/features/identity-access/session/useSession.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

/** A value a filter offers, with how many records have it when the source counts them. */
export interface FilterBarOption {
  value: string;
  label: string;
  count?: number | undefined;
}

export interface FilterBarField {
  /** The filter's name, as the URL and the API name it. */
  name: string;
  label: string;
  /** What the control reads with no value chosen, e.g. "All sectors". */
  all: string;
  options: FilterBarOption[];
  /** Under "More filters" rather than in the bar's first row. */
  more?: boolean;
}

/** An active filter set by a control the bar does not draw itself (a form passed as children). */
export interface FilterBarChip {
  name: string;
  text: string;
  onRemove: () => void;
}

interface FilterBarProps {
  /** The drop-downs the bar draws; a page whose filters are all typed passes children and chips instead. */
  fields?: FilterBarField[];
  values?: Partial<Record<string, string | undefined>>;
  onChange?: (name: string, value: string | undefined) => void;
  onClearAll: () => void;
  /** The page's other controls on the button's row: "Updated…", Refresh, Customize layout. */
  actions?: ReactNode;
  /** Controls the bar cannot draw (typed values, an Apply form); they sit in the panel under the bar's own drop-downs. */
  children?: ReactNode;
  /** The active filters of those controls, shown and counted with the bar's own. */
  chips?: FilterBarChip[];
  /** Which page's open-or-closed state this is. Defaults to the path, so two project pages share nothing. */
  pageKey?: string;
  /** Holds the panel open while a control in it needs the person's attention, such as a required value left empty. */
  forceOpen?: boolean;
}

function storageKey(userId: string, pageKey: string): string {
  return `pmplatform.filterPanel.${userId}.${pageKey}`;
}

function readOpen(key: string): boolean {
  try {
    return localStorage.getItem(key) === 'open';
  } catch {
    return false;
  }
}

function writeOpen(key: string, open: boolean): void {
  try {
    localStorage.setItem(key, open ? 'open' : 'closed');
  } catch {
    // Storage may be unavailable (a private window); the panel then closes on the next visit.
  }
}

function FunnelIcon(): ReactElement {
  return (
    <svg
      className="filter-bar__icon"
      viewBox="0 0 24 24"
      width="16"
      height="16"
      aria-hidden="true"
      focusable="false"
    >
      <path
        d="M4 5h16l-6.5 7.5V19l-3 1.5v-8z"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.75"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

/**
 * The one filter component of every filtered page (the 5 Nov 2026 review, R4-1). The page opens with its filters folded behind a Filters
 * button at every screen size; the button counts the active filters, the active ones stay visible as chips while the panel is closed, and
 * open-or-closed is remembered per person, per page. Filter values live in the page's URL, as before. A filter applies as it is chosen; a
 * filter with nothing to offer is not shown, unless it is set. Esc closes the panel and returns focus to the button.
 */
export function FilterBar({
  fields = [],
  values = {},
  onChange,
  onClearAll,
  actions,
  children,
  chips = [],
  pageKey,
  forceOpen = false,
}: FilterBarProps): ReactElement | null {
  const { t } = useI18n();
  const { session } = useSession();
  const moreId = useId();
  const panelId = useId();
  const toggle = useRef<HTMLButtonElement>(null);
  const openKey = storageKey(session?.user.id ?? 'anonymous', pageKey ?? window.location.pathname);
  const shown = fields.filter(
    (field) => field.options.length > 0 || values[field.name] !== undefined,
  );
  const primary = shown.filter((field) => field.more !== true);
  const secondary = shown.filter((field) => field.more === true);
  const active = shown.filter((field) => values[field.name] !== undefined);
  const activeSecondary = secondary.filter((field) => values[field.name] !== undefined).length;
  const [moreOpen, setMoreOpen] = useState(activeSecondary > 0);
  const [remembered, setRemembered] = useState(() => readOpen(openKey));
  const open = remembered || forceOpen;
  const hasFilters = shown.length > 0 || children !== undefined;
  if (!hasFilters) {
    return actions === undefined ? null : <div className="filter-bar__toolbar">{actions}</div>;
  }

  const setPanel = (next: boolean) => {
    setRemembered(next);
    writeOpen(openKey, next);
  };

  const closeOnEscape = (event: KeyboardEvent) => {
    if (event.key === 'Escape' && open) {
      event.stopPropagation();
      setPanel(false);
      toggle.current?.focus();
    }
  };

  const labelOf = (field: FilterBarField, value: string) =>
    field.options.find((option) => option.value === value)?.label ?? value;

  const control = (field: FilterBarField) => {
    const value = values[field.name] ?? '';
    return (
      <select
        key={field.name}
        className="filter-bar__select"
        name={field.name}
        aria-label={field.label}
        value={value}
        onChange={(event) => {
          onChange?.(field.name, event.target.value === '' ? undefined : event.target.value);
        }}
      >
        <option value="">
          {t('common.filters.inline', { label: field.label, value: field.all })}
        </option>
        {value !== '' && !field.options.some((option) => option.value === value) && (
          <option value={value}>{t('common.filters.inline', { label: field.label, value })}</option>
        )}
        {field.options.map((option) => (
          <option key={option.value} value={option.value}>
            {t('common.filters.inline', {
              label: field.label,
              value:
                option.count === undefined
                  ? option.label
                  : t('common.filters.optionCount', { label: option.label, count: option.count }),
            })}
          </option>
        ))}
      </select>
    );
  };

  const allChips: FilterBarChip[] = [
    ...active.map((field) => ({
      name: field.name,
      text: t('common.filters.inline', {
        label: field.label,
        value: labelOf(field, values[field.name] ?? ''),
      }),
      onRemove: () => {
        onChange?.(field.name, undefined);
      },
    })),
    ...chips,
  ];

  return (
    <div className={`filter-bar${open ? ' filter-bar--open' : ''}`} onKeyDown={closeOnEscape}>
      <div className="filter-bar__toolbar">
        <div className="filter-bar__controls">
          <button
            ref={toggle}
            type="button"
            className="button filter-bar__toggle"
            aria-expanded={open}
            aria-controls={panelId}
            onClick={() => {
              setPanel(!open);
            }}
          >
            <FunnelIcon />
            {allChips.length > 0
              ? t('common.filters.toggle', { count: allChips.length })
              : t('common.filters.button')}
          </button>
          {allChips.length > 0 && (
            <button type="button" className="button button--quiet" onClick={onClearAll}>
              {t('common.filters.clearAll')}
            </button>
          )}
        </div>
        {actions}
      </div>
      <div
        id={panelId}
        className="filter-bar__body"
        role="search"
        aria-label={t('common.filters.label')}
        hidden={!open}
      >
        {shown.length > 0 && (
          <div className="filter-bar__row">
            {primary.map(control)}
            {secondary.length > 0 && (
              <button
                type="button"
                className="button button--quiet filter-bar__more"
                aria-expanded={moreOpen}
                aria-controls={moreId}
                onClick={() => {
                  setMoreOpen((current) => !current);
                }}
              >
                {activeSecondary > 0
                  ? t('common.filters.moreCount', { count: activeSecondary })
                  : t('common.filters.more')}
              </button>
            )}
          </div>
        )}
        {secondary.length > 0 && (
          <div id={moreId} className="filter-bar__row" hidden={!moreOpen}>
            {secondary.map(control)}
          </div>
        )}
        {children}
      </div>
      {allChips.length > 0 && (
        <ul className="filter-bar__chips" aria-label={t('common.filters.active')}>
          {allChips.map((chip) => (
            <li key={chip.name}>
              <button
                type="button"
                className="chip"
                aria-label={t('common.filters.remove', { filter: chip.text })}
                onClick={chip.onRemove}
              >
                {chip.text} <span aria-hidden="true">×</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
