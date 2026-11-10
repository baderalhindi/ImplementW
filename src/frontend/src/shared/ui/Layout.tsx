import { type ReactElement, type ReactNode, useId, useLayoutEffect, useRef } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { exportCheckedRows } from './exportTableRows.ts';
import { SelectionBar } from './rowSelection.tsx';
import { type RowSelection } from './useRowSelection.ts';

interface PageHeaderProps {
  title: string;
  /** The title's own language when it is stored narrative (a project title), which may differ from the interface's. */
  titleLanguage?: string | undefined;
  description?: string | undefined;
  actions?: ReactNode;
}

/**
 * The page's single h1, mirrored into the document title so each route is announced by name. A layout effect sets the
 * title in the same commit as the h1: a passive effect can run a task later, leaving the page briefly untitled.
 */
export function PageHeader({
  title,
  titleLanguage,
  description,
  actions,
}: PageHeaderProps): ReactElement {
  const { t } = useI18n();
  useLayoutEffect(() => {
    document.title = `${title} · ${t('common.appName')}`;
  }, [title, t]);
  return (
    <div className="page-header">
      <div>
        <h1
          className="page-header__title"
          lang={titleLanguage}
          dir={titleLanguage === undefined ? undefined : 'auto'}
        >
          {title}
        </h1>
        {description !== undefined && <p className="page-header__description">{description}</p>}
      </div>
      {actions !== undefined && <div className="page-header__actions">{actions}</div>}
    </div>
  );
}

/** A table whose rows can be chosen: the bar above it counts them, exports them, and holds the table's own bulk actions. */
export interface TableSelection {
  selection: RowSelection;
  /** The file name's stem for Export selected, such as "users". */
  exportName: string;
  /** The table's commands for the chosen rows (Delete, Retire …), beside Export selected. */
  actions?: ReactNode;
}

interface TableContainerProps {
  caption: string;
  children: ReactNode;
  /** Review round 5, item 2 and the 10 Nov 2026 review: select, select all and Export selected on every list. */
  selection?: TableSelection | undefined;
}

/**
 * A table that scrolls sideways on a narrow screen instead of the page. The scroll region is focusable and named so
 * a keyboard user can reach the columns off-screen.
 */
export function TableContainer({
  caption,
  children,
  selection,
}: TableContainerProps): ReactElement {
  const { t } = useI18n();
  const captionId = useId();
  const tableRef = useRef<HTMLTableElement>(null);
  return (
    <>
      {selection !== undefined && (
        <SelectionBar selection={selection.selection}>
          {selection.actions}
          <button
            type="button"
            className="button"
            onClick={() => {
              if (tableRef.current !== null) {
                exportCheckedRows(tableRef.current, selection.exportName);
              }
            }}
          >
            {t('common.table.exportSelected')}
          </button>
        </SelectionBar>
      )}
      <div className="table-container" role="region" aria-labelledby={captionId} tabIndex={0}>
        <table className="table" ref={tableRef}>
          <caption id={captionId} className="visually-hidden">
            {caption}
          </caption>
          {children}
        </table>
      </div>
    </>
  );
}

interface PaginationProps {
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}

export function Pagination({
  page,
  pageSize,
  totalCount,
  onPageChange,
}: PaginationProps): ReactElement | null {
  const { t } = useI18n();
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize));
  if (totalCount <= pageSize && page === 1) {
    return null;
  }
  return (
    <nav className="pagination" aria-label={t('common.pagination.label')}>
      <button
        type="button"
        className="button"
        disabled={page <= 1}
        onClick={() => {
          onPageChange(page - 1);
        }}
      >
        {t('common.pagination.previous')}
      </button>
      <span aria-live="polite">
        {t('common.pagination.status', { page, pageCount, totalCount })}
      </span>
      <button
        type="button"
        className="button"
        disabled={page >= pageCount}
        onClick={() => {
          onPageChange(page + 1);
        }}
      >
        {t('common.pagination.next')}
      </button>
    </nav>
  );
}
