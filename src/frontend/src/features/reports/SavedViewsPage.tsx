import { type ReactElement, useCallback, useState } from 'react';
import { Link } from 'react-router';

import { ConfirmDialog } from '@/features/identity-access/components/ConfirmDialog.tsx';
import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useListParams } from '@/shared/api/useListParams.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader, Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { reportsApi } from './api/reportsApi.ts';
import { type SavedViewSummary } from './api/types.ts';
import { SaveViewDialog } from './dialogs/SaveViewDialog.tsx';
import { entryLanguage, roleLabel, viewPath } from './presentation.ts';
import { isProblem, reportProblemMessage } from './problems.ts';
import { savedViewRequest } from './useSavedView.ts';

// SCR-139 Saved Reports / Views (FG-02 §13.4): the person's own views and those shared with one of their roles
// (TBC-RPT-20, enabled by TASK-072), each with what it runs, whose it is, who it is shared with (its owner alone reads
// that), and whether it still fits what is in force. A view neither theirs nor shared with them is not listed: the API
// does not return it (acceptance criterion 3). Opening a view runs it with the opener's own access.

export function SavedViewsPage(): ReactElement {
  const { t, language, formatDateTime } = useI18n();
  const { session } = useSession();
  const { page, goToPage } = useListParams();
  const [notice, setNotice] = useState<Notice | null>(null);
  const [sharing, setSharing] = useState<SavedViewSummary | null>(null);
  const [deleting, setDeleting] = useState<SavedViewSummary | null>(null);

  const load = useCallback(
    async (signal: AbortSignal) => {
      const [views, catalogue] = await Promise.all([
        reportsApi.savedViews(page, PAGE_SIZE, signal),
        reportsApi.catalogue(signal),
      ]);
      return { views, catalogue };
    },
    [page],
  );
  const { data, error, loading, reload } = useApiResource(load, { keepWhileReloading: true });
  const nameOf = usePersonNames(data?.views.items.map((view) => view.ownerUserId) ?? []);
  const title = t('reports.savedViews.title');

  if (isProblem(error, 'PERMISSION_DENIED')) {
    return (
      <>
        <PageHeader title={title} />
        <EmptyState title={t('reports.savedViews.notForRole')} />
      </>
    );
  }

  const reportName = (view: SavedViewSummary) =>
    view.viewType === 'EXPLORER_COMPOSITION'
      ? t('reports.savedViews.explorer')
      : (data?.catalogue.find((entry) => entry.code === view.reportCode)?.name[language] ??
        view.reportCode ??
        '—');

  const changeSharing = async (
    view: SavedViewSummary,
    values: { name: string; language: 'ar' | 'en'; sharedWithRoleCodes: string[] },
  ) => {
    const { data: detail, etag } = await reportsApi.savedView(view.id);
    await reportsApi.updateSavedView(
      view.id,
      savedViewRequest(values, {
        viewType: detail.viewType,
        reportCode: detail.reportCode,
        parameters: detail.parameters,
        columns: detail.columns,
        filters: detail.filters,
      }),
      etag,
    );
    setSharing(null);
    setNotice({
      tone: 'success',
      message: t('reports.savedViews.sharingSaved', { name: values.name }),
    });
    reload();
  };

  return (
    <>
      <PageHeader
        title={title}
        description={t('reports.savedViews.description')}
        actions={
          <Link className="button" to="/reports/explorer">
            {t('reports.nav.explorer')}
          </Link>
        }
      />
      <PageNotice notice={notice} />
      {loading && data === undefined ? (
        <LoadingState />
      ) : error !== null || data === undefined ? (
        <ErrorState message={reportProblemMessage(error, t)} onRetry={reload} />
      ) : data.views.items.length === 0 ? (
        <EmptyState title={t('reports.savedViews.empty')} />
      ) : (
        <>
          <TableContainer caption={title}>
            <thead>
              <tr>
                <th scope="col">{t('reports.savedViews.name')}</th>
                <th scope="col">{t('reports.savedViews.runs')}</th>
                <th scope="col">{t('reports.savedViews.owner')}</th>
                <th scope="col">{t('reports.savedViews.sharing')}</th>
                <th scope="col">{t('reports.savedViews.compatibility')}</th>
                <th scope="col">{t('reports.savedViews.updatedAt')}</th>
                <th scope="col">{t('common.table.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {data.views.items.map((view) => {
                const isOwn = view.ownerUserId === session?.user.id;
                const path = viewPath(view, data.catalogue);
                const name = (
                  <span lang={entryLanguage(view.name.language)} dir="auto">
                    {view.name.text}
                  </span>
                );
                return (
                  <tr key={view.id} data-saved-view={view.id}>
                    <td>
                      {name}
                      {entryLanguage(view.name.language) !== language && (
                        <span className="cell__aside">
                          {t('reports.savedViews.enteredIn', {
                            language: t(`common.languages.${entryLanguage(view.name.language)}`),
                          })}
                        </span>
                      )}
                    </td>
                    <td>{reportName(view)}</td>
                    <td>{isOwn ? t('common.people.you') : nameOf(view.ownerUserId)}</td>
                    <td>
                      {!isOwn
                        ? t('reports.savedViews.sharedWithYou')
                        : view.sharedWithRoleCodes.length === 0
                          ? t('reports.savedViews.private')
                          : t('reports.savedViews.sharedWith', {
                              roles: view.sharedWithRoleCodes
                                .map((role) => roleLabel(role, t))
                                .join(', '),
                            })}
                    </td>
                    <td>
                      <StatusBadge
                        label={t(`reports.compatibility.${view.compatibility}`)}
                        tone={view.compatibility === 'VALID' ? 'neutral' : 'warning'}
                      />
                    </td>
                    <td>{formatDateTime(view.updatedAt)}</td>
                    <td className="cell--actions cell--actions-wrap">
                      {path !== null && (
                        <Link className="button" to={path}>
                          {t('reports.savedViews.open')}{' '}
                          <span className="visually-hidden">{view.name.text}</span>
                        </Link>
                      )}
                      {isOwn && (
                        <button
                          type="button"
                          className="button"
                          onClick={() => {
                            setSharing(view);
                          }}
                        >
                          {t('reports.savedViews.changeSharing')}{' '}
                          <span className="visually-hidden">{view.name.text}</span>
                        </button>
                      )}
                      {isOwn && (
                        <button
                          type="button"
                          className="button button--danger"
                          onClick={() => {
                            setDeleting(view);
                          }}
                        >
                          {t('reports.savedViews.delete')}{' '}
                          <span className="visually-hidden">{view.name.text}</span>
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </TableContainer>
          <Pagination
            page={data.views.page}
            pageSize={data.views.pageSize}
            totalCount={data.views.totalCount}
            onPageChange={goToPage}
          />
        </>
      )}
      {sharing !== null && (
        <SaveViewDialog
          open
          title={t('reports.savedViews.sharingTitle')}
          initial={{
            name: sharing.name.text,
            language: entryLanguage(sharing.name.language),
            sharedWithRoleCodes: sharing.sharedWithRoleCodes,
          }}
          save={(values) => changeSharing(sharing, values)}
          onClose={() => {
            setSharing(null);
          }}
        />
      )}
      <ConfirmDialog
        confirmation={
          deleting === null
            ? null
            : {
                title: t('reports.savedViews.deleteTitle'),
                body: t('reports.savedViews.deleteMessage', { name: deleting.name.text }),
                confirmLabel: t('reports.savedViews.delete'),
                destructive: true,
                action: () => reportsApi.deleteSavedView(deleting.id),
                describeProblem: reportProblemMessage,
              }
        }
        onClose={() => {
          setDeleting(null);
        }}
        onDone={() => {
          setNotice({
            tone: 'success',
            message: t('reports.savedViews.deleted', { name: deleting?.name.text ?? '' }),
          });
          setDeleting(null);
          reload();
        }}
      />
    </>
  );
}
