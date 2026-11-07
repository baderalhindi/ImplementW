import { type ReactElement, type ReactNode } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type RowSelection } from './useRowSelection.ts';

// Review round 5, item 2: a checkbox per row and one in the header, the count of what is chosen, and the actions that work on it. What is chosen
// stays chosen across pages and filters until it is cleared.

/** The header's checkbox: every row shown, with the mixed state when only some are chosen. */
export function SelectAllHeader({
  ids,
  selection,
}: {
  ids: readonly string[];
  selection: RowSelection;
}): ReactElement {
  const { t } = useI18n();
  const chosen = ids.filter((id) => selection.selected.has(id)).length;
  return (
    <th scope="col" className="cell--select">
      <input
        type="checkbox"
        aria-label={t('common.table.selectAll')}
        checked={ids.length > 0 && chosen === ids.length}
        ref={(input) => {
          if (input !== null) {
            input.indeterminate = chosen > 0 && chosen < ids.length;
          }
        }}
        onChange={(event) => {
          selection.setAll(ids, event.target.checked);
        }}
      />
    </th>
  );
}

export function SelectRowCell({
  id,
  name,
  selection,
}: {
  id: string;
  /** What the row is called, so a screen reader names the checkbox. */
  name: string;
  selection: RowSelection;
}): ReactElement {
  const { t } = useI18n();
  return (
    <td className="cell--select">
      <input
        type="checkbox"
        aria-label={t('common.table.selectRow', { name })}
        checked={selection.selected.has(id)}
        onChange={(event) => {
          selection.toggle(id, event.target.checked);
        }}
      />
    </td>
  );
}

/** The count of what is chosen and the actions on it; nothing while nothing is chosen. */
export function SelectionBar({
  selection,
  children,
}: {
  selection: RowSelection;
  children?: ReactNode;
}): ReactElement | null {
  const { t } = useI18n();
  if (selection.selected.size === 0) {
    return null;
  }
  return (
    <div className="selection-bar" role="status">
      <span>{t('common.table.selected', { count: selection.selected.size })}</span>
      {children}
      <button type="button" className="button button--quiet" onClick={selection.clear}>
        {t('common.table.clearSelection')}
      </button>
    </div>
  );
}
