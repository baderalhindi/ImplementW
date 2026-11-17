import { type ReactElement, type ReactNode, type RefObject, useLayoutEffect, useRef } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { usePopover } from './usePopover.ts';

/** One action of a "More" menu: a link or a command; `danger` draws it in red, after a divider. */
export interface MoreMenuItem {
  key: string;
  label: string;
  to?: string;
  onSelect?: () => void;
  danger?: boolean;
}

/**
 * A record's less frequent actions behind one "More" (⋯) button (review round 8, point 06): the everyday ones first, then a divider and the
 * destructive ones, in red, last. The caller passes only the actions the person may take; with none, there is no button.
 */
export function MoreMenu({
  items,
  label,
}: {
  items: MoreMenuItem[];
  /** In a table row: the record's name. The button is then the ⋯ alone, named "More <label>" for a screen reader. */
  label?: string;
}): ReactElement | null {
  const { t } = useI18n();
  const { open, setOpen, containerRef, buttonRef, menuId } = usePopover();
  const menuRef = useRef<HTMLUListElement>(null);
  useFloatingMenu(open && label !== undefined, buttonRef, menuRef, setOpen);
  if (items.length === 0) {
    return null;
  }
  const close = () => {
    setOpen(false);
  };
  const everyday = items.filter((item) => item.danger !== true);
  const destructive = items.filter((item) => item.danger === true);
  const option = (item: MoreMenuItem) => (
    <li key={item.key} role="none">
      {item.to === undefined ? (
        <button
          type="button"
          role="menuitem"
          className={`popover__option${item.danger === true ? ' popover__option--danger' : ''}`}
          onClick={() => {
            close();
            item.onSelect?.();
          }}
        >
          {item.label}
        </button>
      ) : (
        <Link role="menuitem" className="popover__option" to={item.to} onClick={close}>
          {item.label}
        </Link>
      )}
    </li>
  );
  return (
    <div className="popover more-menu" ref={containerRef}>
      <button
        ref={buttonRef}
        type="button"
        className={`button more-menu__button${label === undefined ? '' : ' more-menu__button--compact'}`}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={menuId}
        onClick={() => {
          setOpen((current) => !current);
        }}
      >
        {label === undefined ? (
          <>
            <span aria-hidden="true">⋯</span> {t('common.actions.more')}
          </>
        ) : (
          <>
            <span aria-hidden="true">⋯</span>
            <span className="visually-hidden">
              {t('common.actions.more')} {label}
            </span>
          </>
        )}
      </button>
      <ul ref={menuRef} className="popover__menu" id={menuId} role="menu" hidden={!open}>
        {everyday.map(option)}
        {everyday.length > 0 && destructive.length > 0 && (
          <li role="separator" className="popover__divider" />
        )}
        {destructive.map(option)}
      </ul>
    </div>
  );
}

/**
 * A row's menu is fixed to the viewport under its button (above it when there is no room below), so the table's scrolling box does not
 * clip it. Scrolling or resizing closes it rather than leaving it behind the row it belongs to.
 */
function useFloatingMenu(
  active: boolean,
  buttonRef: RefObject<HTMLButtonElement | null>,
  menuRef: RefObject<HTMLUListElement | null>,
  setOpen: (open: boolean) => void,
): void {
  useLayoutEffect(() => {
    const button = buttonRef.current;
    const menu = menuRef.current;
    if (!active || button === null || menu === null) {
      return undefined;
    }
    const anchor = (button.closest('.row-action-group') ?? button).getBoundingClientRect();
    const gap = 4;
    const height = menu.offsetHeight;
    const fitsBelow = anchor.bottom + gap + height <= window.innerHeight;
    const rtl = getComputedStyle(button).direction === 'rtl';
    menu.style.position = 'fixed';
    const px = (value: number): string => `${String(value)}px`;
    menu.style.top = px(fitsBelow ? anchor.bottom + gap : Math.max(gap, anchor.top - gap - height));
    menu.style.left = rtl ? px(anchor.left) : 'auto';
    menu.style.right = rtl ? 'auto' : px(window.innerWidth - anchor.right);
    const close = (): void => {
      setOpen(false);
    };
    window.addEventListener('resize', close);
    window.addEventListener('scroll', close, true);
    return () => {
      window.removeEventListener('resize', close);
      window.removeEventListener('scroll', close, true);
      menu.removeAttribute('style');
    };
  }, [active, buttonRef, menuRef, setOpen]);
}

/**
 * A table row's actions on one line: the everyday one as a button (usually Edit) joined to a ⋯ menu with the rest, destructive ones last.
 */
export function RowActionGroup({
  primary,
  items,
  label,
}: {
  primary?: ReactNode;
  items: MoreMenuItem[];
  /** The record's name, for the screen reader's name of the ⋯ button. */
  label: string;
}): ReactElement | null {
  if (primary === undefined && items.length === 0) {
    return null;
  }
  return (
    <span className="row-action-group">
      {primary}
      <MoreMenu items={items} label={label} />
    </span>
  );
}
