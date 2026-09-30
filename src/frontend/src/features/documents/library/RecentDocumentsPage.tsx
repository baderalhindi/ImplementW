import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { documentsApi } from '../api/documentsApi.ts';
import { DocumentTable } from '../components/DocumentTable.tsx';
import { documentProblemMessage } from '../problems.ts';
import { useDocumentLookups } from '../useDocumentLookups.ts';

/** How many documents SCR-122 shows; the library has the rest. */
export const RECENT_LIMIT = 10;

/** Module-level, so useApiResource reads once per mount. */
function loadRecent(signal: AbortSignal) {
  return documentsApi.list({ pageSize: RECENT_LIMIT }, signal);
}

/**
 * SCR-122 Recent Documents: the documents the caller may read that changed most recently — the API's own order
 * (TASK-037 `ListDocuments`) — first page only.
 */
export function RecentDocumentsPage(): ReactElement {
  const { t } = useI18n();
  const recent = useApiResource(loadRecent);
  const lookups = useDocumentLookups();
  return (
    <>
      <PageHeader
        title={t('documents.recent.title')}
        description={t('documents.recent.description', { count: RECENT_LIMIT })}
      />
      {recent.loading && <LoadingState label={t('documents.list.loading')} />}
      {recent.error !== null && (
        <ErrorState message={documentProblemMessage(recent.error, t)} onRetry={recent.reload} />
      )}
      {recent.data !== undefined &&
        (recent.data.items.length === 0 ? (
          <EmptyState title={t('documents.list.empty')} />
        ) : (
          <DocumentTable
            caption={t('documents.recent.title')}
            documents={recent.data.items}
            lookups={lookups}
            showProject
          />
        ))}
      <p>
        <Link to="/documents">{t('documents.recent.toLibrary')}</Link>
      </p>
    </>
  );
}
