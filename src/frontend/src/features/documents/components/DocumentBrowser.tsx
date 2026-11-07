import { type ReactElement, useCallback, useState } from 'react';
import { useSearchParams } from 'react-router';

import { pageFrom, PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { FilterBar } from '@/shared/ui/FilterBar.tsx';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader, Pagination } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { useSearchText } from '@/shared/ui/useSearchText.ts';

import { documentsApi } from '../api/documentsApi.ts';
import { useCan } from '@/features/identity-access/permissions/permissions.ts';

import { UploadDocumentDialog } from '../dialogs/UploadDocumentDialog.tsx';
import { DOCUMENT_STATUSES } from '../presentation.ts';
import { documentProblemMessage } from '../problems.ts';
import { useDocumentLookups } from '../useDocumentLookups.ts';

import { DocumentTable } from './DocumentTable.tsx';

interface DocumentBrowserProps {
  title: string;
  description: string;
  /** SCR-121's project; undefined on the library (SCR-120), which lists every document the caller may read. */
  projectId: string | undefined;
  /** Inside another page (the project workspace's Documents tab): a section heading, not the page's h1. */
  embedded?: boolean;
  /** False on a CLOSED project's tab: WF-12 refuses an upload to it (409 DOCUMENT_PROJECT_CLOSED, TASK-063 D-8). */
  canUpload?: boolean;
}

/**
 * SCR-120 and SCR-121: the documents the caller may read, most recently changed first, filtered by title and status
 * (kept in the URL), with MOD-050 Upload. The API lists only what the caller may open (TASK-037 D-6); the screen adds
 * nothing and hides nothing.
 */
export function DocumentBrowser({
  title,
  description,
  projectId,
  embedded = false,
  canUpload = true,
}: DocumentBrowserProps): ReactElement {
  const { t } = useI18n();
  const [params, setParams] = useSearchParams();
  const page = pageFrom(params);
  const q = params.get('q') ?? '';
  const status = DOCUMENT_STATUSES.find((candidate) => candidate === params.get('status'));
  const load = useCallback(
    (signal: AbortSignal) =>
      documentsApi.list(
        { projectId, status, q: q === '' ? undefined : q, page, pageSize: PAGE_SIZE },
        signal,
      ),
    [projectId, status, q, page],
  );
  const documents = useApiResource(load, { keepPreviousResult: true });
  const lookups = useDocumentLookups();
  const [uploading, setUploading] = useState(false);
  const [notice, setNotice] = useState<Notice | null>(null);

  const setFilter = (name: string, value: string, replace = false) => {
    const updated = new URLSearchParams(params);
    updated.delete('page');
    if (value === '') {
      updated.delete(name);
    } else {
      updated.set(name, value);
    }
    setParams(updated, { replace });
  };

  const [text, setText] = useSearchText(q, (value) => {
    setFilter('q', value, true);
  });

  const clearFilters = () => {
    const updated = new URLSearchParams(params);
    updated.delete('page');
    updated.delete('q');
    updated.delete('status');
    setParams(updated);
  };

  const goToPage = (next: number) => {
    const updated = new URLSearchParams(params);
    updated.set('page', String(next));
    setParams(updated);
  };

  const filtered = q !== '' || status !== undefined;
  const mayUpload = useCan('DOCUMENT_UPLOAD');
  const uploadButton = canUpload && mayUpload && (
    <button
      type="button"
      className="button button--primary"
      onClick={() => {
        setNotice(null);
        setUploading(true);
      }}
    >
      {t('documents.upload.action')}
    </button>
  );
  return (
    <>
      {embedded ? (
        <div className="section__header">
          <div>
            <h2>{title}</h2>
            <p className="page-header__description">{description}</p>
          </div>
          {uploadButton}
        </div>
      ) : (
        <PageHeader title={title} description={description} actions={uploadButton} />
      )}
      <PageNotice notice={notice} />

      <FilterBar
        fields={[
          {
            name: 'status',
            label: t('documents.fields.status'),
            all: t('common.filters.any'),
            options: DOCUMENT_STATUSES.map((value) => ({
              value,
              label: t(`documents.status.${value}`),
            })),
          },
        ]}
        values={{ status }}
        onChange={(name, value) => {
          setFilter(name, value ?? '');
        }}
        onClearAll={clearFilters}
        chips={
          q === ''
            ? []
            : [
                {
                  name: 'q',
                  text: t('common.filters.inline', {
                    label: t('documents.filters.title'),
                    value: q,
                  }),
                  onRemove: () => {
                    setFilter('q', '');
                  },
                },
              ]
        }
      >
        <form
          className="filters filters--inline"
          onSubmit={(event) => {
            event.preventDefault();
          }}
        >
          <TextField
            label={t('documents.filters.title')}
            name="q"
            type="search"
            value={text}
            onChange={setText}
            dir="auto"
          />
        </form>
      </FilterBar>

      {documents.loading && documents.data === undefined && (
        <LoadingState label={t('documents.list.loading')} />
      )}
      {documents.error !== null && (
        <ErrorState
          error={documents.error}
          message={documentProblemMessage(documents.error, t)}
          onRetry={documents.reload}
        />
      )}
      {documents.data !== undefined &&
        (documents.data.items.length === 0 ? (
          <EmptyState
            title={filtered ? t('documents.list.emptyFiltered') : t('documents.list.empty')}
          />
        ) : (
          <>
            <DocumentTable
              caption={title}
              documents={documents.data.items}
              lookups={lookups}
              showProject={projectId === undefined}
            />
            <Pagination
              page={documents.data.page}
              pageSize={documents.data.pageSize}
              totalCount={documents.data.totalCount}
              onPageChange={goToPage}
            />
          </>
        ))}

      <UploadDocumentDialog
        open={uploading}
        projectId={projectId ?? null}
        onClose={() => {
          setUploading(false);
        }}
        onUploaded={() => {
          setNotice({ tone: 'success', message: t('documents.done.uploaded') });
          documents.reload();
        }}
      />
    </>
  );
}
