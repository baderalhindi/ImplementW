import { type ReactElement } from 'react';
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
export function MoreMenu({ items }: { items: MoreMenuItem[] }): ReactElement | null {
  const { t } = useI18n();
  const { open, setOpen, containerRef, buttonRef, menuId } = usePopover();
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
        className="button more-menu__button"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={menuId}
        onClick={() => {
          setOpen((current) => !current);
        }}
      >
        <span aria-hidden="true">⋯</span> {t('common.actions.more')}
      </button>
      <ul className="popover__menu" id={menuId} role="menu" hidden={!open}>
        {everyday.map(option)}
        {everyday.length > 0 && destructive.length > 0 && (
          <li role="separator" className="popover__divider" />
        )}
        {destructive.map(option)}
      </ul>
    </div>
  );
}
