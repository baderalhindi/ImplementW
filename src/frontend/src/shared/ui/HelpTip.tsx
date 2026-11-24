import { type ReactElement, type ReactNode, useLayoutEffect, useRef } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { usePopover } from './usePopover.ts';

/**
 * A note behind a small "?" (24 Nov 2026: "make the notes appear in a simple question mark icon"): pressed, it shows the note beside it; a press
 * elsewhere or Escape closes it. The button is named for what it explains (`label`), and the note is announced as its description once shown.
 */
export function HelpTip({
  label,
  align = 'start',
  children,
}: {
  label: string;
  /** The edge the note lines up with: the button's start (it opens toward the reading end), or its end for a "?" near the page's end edge. */
  align?: 'start' | 'end';
  children: ReactNode;
}): ReactElement {
  const { t } = useI18n();
  const { open, setOpen, containerRef, buttonRef, menuId } = usePopover();
  const noteRef = useRef<HTMLSpanElement>(null);
  // The note is placed against the window, under its button and kept inside the screen, so a scrolling pane (the designer's gallery) never cuts it.
  useLayoutEffect(() => {
    const button = buttonRef.current;
    const note = noteRef.current;
    if (!open || button === null || note === null) {
      return undefined;
    }
    const gap = 8;
    const anchor = button.getBoundingClientRect();
    const width = Math.min(note.offsetWidth, window.innerWidth - 2 * gap);
    const rtl = getComputedStyle(button).direction === 'rtl';
    const fromStart = rtl ? anchor.right - width : anchor.left;
    const fromEnd = rtl ? anchor.left : anchor.right - width;
    const left = Math.min(
      Math.max(gap, align === 'end' ? fromEnd : fromStart),
      window.innerWidth - gap - width,
    );
    const px = (value: number) => `${String(value)}px`;
    Object.assign(note.style, {
      position: 'fixed',
      top: px(anchor.bottom + gap),
      left: px(left),
      right: 'auto',
    });
    const close = () => {
      setOpen(false);
    };
    window.addEventListener('resize', close);
    window.addEventListener('scroll', close, true);
    return () => {
      window.removeEventListener('resize', close);
      window.removeEventListener('scroll', close, true);
      note.removeAttribute('style');
    };
  }, [open, align, buttonRef, setOpen]);
  return (
    <span className="help-tip" ref={containerRef}>
      <button
        ref={buttonRef}
        type="button"
        className="help-tip__button"
        aria-label={t('common.actions.helpAbout', { subject: label })}
        aria-expanded={open}
        aria-controls={menuId}
        onClick={() => {
          setOpen((value) => !value);
        }}
      >
        <span aria-hidden="true">?</span>
      </button>
      <span
        ref={noteRef}
        id={menuId}
        className={`help-tip__note${align === 'end' ? ' help-tip__note--end' : ''}`}
        role="note"
        hidden={!open}
      >
        {children}
      </span>
    </span>
  );
}
