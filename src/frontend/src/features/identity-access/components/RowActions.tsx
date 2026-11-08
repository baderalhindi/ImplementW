import { type ReactElement, type ReactNode, useState } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type PermissionCode } from '../permissions/permissionCodes.ts';
import { useCan } from '../permissions/permissions.ts';
import { ConfirmDialog, type Confirmation } from './ConfirmDialog.tsx';

/** A table's actions column: each row's buttons, and the one confirmation they open, rendered beside the table. */
export interface RowActionsColumn<E> {
  render: (entry: E) => ReactNode;
  dialog: ReactElement;
}

interface RowActionsOptions<E> {
  /** Who may edit and delete the rows: a role without it (the Viewer, the Executive) gets no actions column at all. */
  permission: PermissionCode;
  name: (entry: E) => string;
  /** Opens the row's edit form on this screen. */
  onEdit?: (entry: E) => void;
  /** Where the row is edited, or null when it cannot be edited in its state. */
  editTo?: (entry: E) => string | null;
  remove: (entry: E) => Promise<unknown>;
  onDeleted: () => void;
  /** False for a row that takes no change, such as one of a CLOSED project: it gets no buttons. */
  changeable?: (entry: E) => boolean;
}

/**
 * Client review, item 8: Edit and Delete in a register's rows. Delete is confirmed by name; the record and what belongs to it leave the
 * lists and stay in the database with their history. The API refuses a record in an approval that has not finished, and says so.
 */
export function useRowActions<E>({
  permission,
  name,
  onEdit,
  editTo,
  remove,
  onDeleted,
  changeable,
}: RowActionsOptions<E>): RowActionsColumn<E> | undefined {
  const { t } = useI18n();
  const allowed = useCan(permission);
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const actions = (entry: E): ReactNode => {
    if (changeable !== undefined && !changeable(entry)) {
      return null;
    }
    const label = name(entry);
    const to = editTo?.(entry) ?? null;
    return (
      <span className="row-actions">
        {onEdit !== undefined ? (
          <button
            type="button"
            className="button"
            onClick={() => {
              onEdit(entry);
            }}
          >
            {t('common.actions.edit')} <span className="visually-hidden">{label}</span>
          </button>
        ) : (
          to !== null && (
            <Link className="button" to={to}>
              {t('common.actions.edit')} <span className="visually-hidden">{label}</span>
            </Link>
          )
        )}
        <button
          type="button"
          className="button button--danger"
          onClick={() => {
            setConfirmation({
              title: t('common.rowActions.deleteTitle'),
              body: t('common.rowActions.deleteBody', { name: label }),
              confirmLabel: t('common.rowActions.delete'),
              destructive: true,
              action: () => remove(entry),
            });
          }}
        >
          {t('common.rowActions.delete')} <span className="visually-hidden">{label}</span>
        </button>
      </span>
    );
  };
  return allowed
    ? {
        render: actions,
        dialog: (
          <ConfirmDialog
            confirmation={confirmation}
            onClose={() => {
              setConfirmation(null);
            }}
            onDone={() => {
              setConfirmation(null);
              onDeleted();
            }}
          />
        ),
      }
    : undefined;
}
