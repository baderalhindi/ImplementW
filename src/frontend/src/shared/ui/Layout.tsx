import { type ReactElement, type ReactNode, useId, useLayoutEffect } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

interface PageHeaderProps {
  title: string;
  description?: string | undefined;
  actions?: ReactNode;
}

/**
 * The page's single h1, mirrored into the document title so each route is announced by name. A layout effect sets the
 * title in the same commit as the h1: a passive effect can run a task later, leaving the page briefly untitled.
 */
export function PageHeader({ title, description, actions }: PageHeaderProps): ReactElement {
  const { t } = useI18n();
  useLayoutEffect(() => {
    document.title = `${title} · ${t('common.appName')}`;
  }, [title, t]);
  return (
    <div className="page-header">
      <div>
        <h1 className="page-header__title">{title}</h1>
        {description !== undefined && <p className="page-header__description">{description}</p>}
      </div>
      {actions !== undefined && <div className="page-header__actions">{actions}</div>}
    </div>
  );
}

interface TableContainerProps {
  caption: string;
  children: ReactNode;
}

/**
 * A table that scrolls sideways on a narrow screen instead of the page. The scroll region is focusable and named so
 * a keyboard user can reach the columns off-screen.
 */
export function TableContainer({ caption, children }: TableContainerProps): ReactElement {
  const captionId = useId();
  return (
    <div className="table-container" role="region" aria-labelledby={captionId} tabIndex={0}>
      <table className="table">
        <caption id={captionId} className="visually-hidden">
          {caption}
        </caption>
        {children}
      </table>
    </div>
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
