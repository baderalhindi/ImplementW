import { type ReactElement, type ReactNode, useEffect, useId, useRef } from 'react';

interface DialogProps {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
  /** `drawer`: a panel along the inline-end edge, for a record read and worked on at length (MOD-019). */
  variant?: 'modal' | 'drawer';
}

/**
 * A modal on the native <dialog>: showModal() gives the focus trap, the inert background and Escape for free. The
 * content mounts only while open, so each opening starts from a fresh form. A drawer is the same modal, laid out as a
 * side panel.
 */
export function Dialog({
  open,
  title,
  onClose,
  children,
  variant = 'modal',
}: DialogProps): ReactElement {
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();

  useEffect(() => {
    const dialog = ref.current;
    if (dialog === null) {
      return;
    }
    if (open && !dialog.open) {
      dialog.showModal();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  }, [open]);

  return (
    <dialog
      ref={ref}
      className={variant === 'drawer' ? 'dialog dialog--drawer' : 'dialog'}
      aria-labelledby={titleId}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
    >
      {open && (
        <>
          <h2 id={titleId} className="dialog__title">
            {title}
          </h2>
          {children}
        </>
      )}
    </dialog>
  );
}
