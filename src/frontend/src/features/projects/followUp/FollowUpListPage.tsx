import { type ReactElement, useCallback } from 'react';
import { Link } from 'react-router';

import { PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useListParams } from '@/shared/api/useListParams.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { FilterBar } from '@/shared/ui/FilterBar.tsx';
import { PageHeader, Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { SelectAllHeader, SelectRowCell } from '@/shared/ui/rowSelection.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';
import { useRowSelection } from '@/shared/ui/useRowSelection.ts';

import { followUpApi } from '../api/followUpApi.ts';
import { FOLLOW_UP_REASONS, type FollowUpReason } from '../api/types.ts';
import { GapValue, OnPlanBadge } from '../components/FollowUpBadges.tsx';
import { ProjectStatusBadge } from '../components/ProjectStatusBadge.tsx';
import { projectProblemMessage } from '../problems.ts';
import { useProjectLookups } from '../useProjectLookups.ts';

function isReason(value: string | null): value is FollowUpReason {
  return FOLLOW_UP_REASONS.includes(value as FollowUpReason);
}

/**
 * قائمة المتابعة, RPMO's Follow-up list (the 2 Nov 2026 review, R3-5): every project in the person's scope that needs following up, with the
 * reason, the obstacle, the next action, its owner and due date, most urgent first — stalled, suspended, late, an overdue action, then an open
 * obstacle. The order and the reasons are the API's.
 */
export function FollowUpListPage(): ReactElement {
  const { t } = useI18n();
  const { params, page, setFilter, setFilters, goToPage } = useListParams();
  const lookups = useProjectLookups();
  const reasonParam = params.get('reason');
  const reason = isReason(reasonParam) ? reasonParam : undefined;
  const departmentId = params.get('departmentId') ?? undefined;
  const load = useCallback(
    (signal: AbortSignal) =>
      followUpApi.list({ reason, departmentId, page, pageSize: PAGE_SIZE }, signal),
    [reason, departmentId, page],
  );
  const items = useApiResource(load);
  const selection = useRowSelection();

  return (
    <>
      <PageHeader
        title={t('projects.followUpList.title')}
        description={t('projects.followUpList.description')}
      />
      <FilterBar
        fields={[
          {
            name: 'reason',
            label: t('projects.followUpList.reason'),
            all: t('projects.followUpList.allReasons'),
            options: FOLLOW_UP_REASONS.map((value) => ({
              value,
              label: t(`projects.followUpReason.${value}`),
            })),
          },
          ...(lookups.organizationError === null
            ? [
                {
                  name: 'departmentId',
                  label: t('projects.fields.department'),
                  all: t('common.filters.any'),
                  options: lookups.departmentOptions,
                },
              ]
            : []),
        ]}
        values={{ reason, departmentId }}
        onChange={(name, value) => {
          setFilter(name, value ?? '');
        }}
        onClearAll={() => {
          setFilters({ reason: '', departmentId: '' });
        }}
      />
      {items.loading && <LoadingState />}
      {items.error !== null && (
        <ErrorState
          error={items.error}
          message={projectProblemMessage(items.error, t)}
          onRetry={items.reload}
        />
      )}
      {items.data !== undefined &&
        (items.data.items.length === 0 ? (
          <EmptyState title={t('projects.followUpList.empty')} />
        ) : (
          <>
            <TableContainer
              caption={t('projects.followUpList.caption')}
              selection={{ selection, exportName: 'follow-up-list' }}
            >
              <thead>
                <tr>
                  <SelectAllHeader
                    ids={items.data.items.map((item) => item.projectId)}
                    selection={selection}
                  />
                  <th scope="col">{t('projects.followUpList.project')}</th>
                  <th scope="col">{t('projects.followUpList.reason')}</th>
                  <th scope="col">{t('projects.followUpList.onPlan')}</th>
                  <th scope="col">{t('projects.followUpList.gap')}</th>
                  <th scope="col">{t('projects.followUpList.obstacle')}</th>
                  <th scope="col">{t('projects.followUpList.nextAction')}</th>
                  <th scope="col">{t('projects.followUpList.owner')}</th>
                  <th scope="col">{t('projects.followUpList.due')}</th>
                </tr>
              </thead>
              <tbody>
                {items.data.items.map((item) => (
                  <tr key={item.projectId}>
                    <SelectRowCell
                      id={item.projectId}
                      name={item.title.text}
                      selection={selection}
                    />
                    <td>
                      <Link to={`/projects/${item.projectId}`} dir="auto">
                        {item.title.text}
                      </Link>
                      <span className="cell__aside" dir="ltr">
                        {item.formalProjectId ?? item.legacyReference ?? ''}
                      </span>
                      <span className="cell__aside">
                        {lookups.departmentName(item.departmentId)}
                      </span>
                      <ProjectStatusBadge status={item.status} legacyIntakeDate={null} />
                    </td>
                    <td>
                      {item.indicators.followUpReasons.map((value) => (
                        <StatusBadge
                          key={value}
                          label={t(`projects.followUpReason.${value}`)}
                          tone="warning"
                        />
                      ))}
                    </td>
                    <td>
                      <OnPlanBadge status={item.indicators.onPlan} />
                    </td>
                    <td>
                      <GapValue points={item.indicators.gapPoints} />
                    </td>
                    <td dir="auto">{item.currentObstacle?.text ?? t('common.values.none')}</td>
                    <td dir="auto">{item.nextAction?.text ?? t('common.values.none')}</td>
                    <td dir="auto">{item.nextActionOwner ?? t('common.values.none')}</td>
                    <td>
                      {item.nextActionDueDate === null ? (
                        t('common.values.none')
                      ) : (
                        <>
                          <span dir="ltr">{item.nextActionDueDate}</span>{' '}
                          {item.indicators.actionDue === 'OVERDUE' && (
                            <StatusBadge
                              label={t('projects.followUp.actionOverdue')}
                              tone="negative"
                            />
                          )}
                        </>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </TableContainer>
            <Pagination
              page={items.data.page}
              pageSize={items.data.pageSize}
              totalCount={items.data.totalCount}
              onPageChange={goToPage}
            />
          </>
        ))}
    </>
  );
}
