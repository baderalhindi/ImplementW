import { type ReactElement, type ReactNode, useState } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { type RowSelection } from '@/shared/ui/useRowSelection.ts';

import { type PermissionCode } from '../permissions/permissionCodes.ts';
import { useCan } from '../permissions/permissions.ts';
import { ConfirmDialog, type Confirmation } from './ConfirmDialog.tsx';

/** A table's actions column: each row's buttons, and the one confirmation they open, rendered beside the table. */
export interface RowActionsColumn<E> {
  render: (entry: E) => ReactNode;
  /** Delete for the chosen rows that allow it, or nothing when none does; the rows the API refuses stay chosen. */
  bulk: (rows: readonly E[], selection: RowSelection, id: (entry: E) => string) => ReactNode;
  dialog: ReactElement;
}

/** Leaves exactly these rows chosen: none after a command that went through, the refused ones after one that did not. */
export function keepChosen(selection: RowSelection, ids: readonly string[]): void {
  selection.clear();
  selection.setAll(ids, true);
}

/**
 * Sends the command for every entry, past a refusal, then hands over the refused ones; the first refusal is thrown once all
 * were tried, so the confirmation shows why.
 */
export async function runForEach<E>(
  entries: readonly E[],
  command: (entry: E) => Promise<unknown>,
  onFinished: (refused: E[]) => void,
): Promise<void> {
  const refused: E[] = [];
  const problems: unknown[] = [];
  for (const entry of entries) {
    try {
      await command(entry);
    } catch (problem) {
      refused.push(entry);
      problems.push(problem);
    }
  }
  onFinished(refused);
  if (problems.length > 0) {
    throw problems[0];
  }
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
  /** False for a row that is edited but never deleted, such as a built-in role. */
  deletable?: (entry: E) => boolean;
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
  deletable,
}: RowActionsOptions<E>): RowActionsColumn<E> | undefined {
  const { t } = useI18n();
  const allowed = useCan(permission);
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const mayDelete = (entry: E) =>
    (changeable === undefined || changeable(entry)) &&
    (deletable === undefined || deletable(entry));
  const bulk = (
    rows: readonly E[],
    selection: RowSelection,
    id: (entry: E) => string,
  ): ReactNode => {
    const targets = rows.filter((entry) => selection.selected.has(id(entry)) && mayDelete(entry));
    if (targets.length === 0) {
      return null;
    }
    return (
      <button
        type="button"
        className="button button--danger"
        onClick={() => {
          setConfirmation({
            title: t('common.rowActions.deleteTitle'),
            body: t('common.rowActions.deleteBody', { name: targets.map(name).join('، ') }),
            confirmLabel: t('common.rowActions.delete'),
            destructive: true,
            action: () =>
              runForEach(targets, remove, (refused) => {
                keepChosen(selection, refused.map(id));
                // A refusal keeps the dialog open: the rows that were deleted leave the list now.
                if (refused.length > 0) {
                  onDeleted();
                }
              }),
          });
        }}
      >
        {t('common.rowActions.delete')}
      </button>
    );
  };
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
        {(deletable === undefined || deletable(entry)) && (
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
        )}
      </span>
    );
  };
  return allowed
    ? {
        render: actions,
        bulk,
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
