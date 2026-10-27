import { type ReactElement, useCallback } from 'react';
import { Link } from 'react-router';

import { approvalInstancesApi, approvalTasksApi } from '@/features/approvals/api/approvalsApi.ts';
import { SubjectSummary } from '@/features/approvals/components/SubjectSummary.tsx';
import { isOverdue } from '@/features/approvals/presentation.ts';
import { DASHBOARDS_PATH, PORTFOLIO_DASHBOARD_PATH } from '@/features/dashboards/paths.ts';
import { externalRequestsApi } from '@/features/external-participation/api/externalParticipationApi.ts';
import { MY_EXTERNAL_REQUESTS_PATH } from '@/features/external-participation/paths.ts';
import { usePermissions } from '@/features/identity-access/permissions/permissions.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { notificationsApi } from '@/features/notifications/api/notificationsApi.ts';
import { headline, isAppRoute } from '@/features/notifications/presentation.ts';
import { isOpen, listEntries, overdueDays, todayUtc } from '@/features/tasks/taskRules.ts';
import { useTaskPortfolio } from '@/features/tasks/useTaskData.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { HomeCard } from './HomeCard.tsx';

/** How many items a card lists before "view all". */
const SHOWN = 5;

/** The days ahead a task counts as due this week. */
const WEEK_DAYS = 7;

/** The ISO date `days` after the ISO date `day`. */
function daysAfter(day: string, days: number): string {
  const date = new Date(`${day}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

/** The Portfolio Dashboard's audience (dashboards.md D-2), offered it as a quick way to the analysis. */
const PORTFOLIO_ROLES: readonly string[] = ['R02', 'R03', 'R06', 'R07'];

/**
 * The person's own work on the Home (the 24 Oct 2026 review, H2: "Home = my work, for every role"): approvals waiting for
 * them with their due dates, their tasks due this week and overdue, update requests waiting for their answer, the requests
 * they submitted, and unread notifications. Each card is offered to a holder of the permission its list reads under.
 */
export function MyWork({ user }: { user: SessionUser }): ReactElement {
  const { t } = useI18n();
  const { can } = usePermissions();
  return (
    <section className="home__work" aria-labelledby="home-work">
      <h2 id="home-work">{t('home.work.title')}</h2>
      <div className="home__grid">
        {can('APPROVAL_DECIDE') && <ApprovalsWaiting />}
        {can('TASK_UPDATE') && <MyTasks userId={user.id} />}
        {can('EXTERNAL_CONTRIBUTION_RESPOND') && <RequestsToAnswer userId={user.id} />}
        {can('APPROVAL_VIEW') && <MyRequests />}
        <UnreadNotifications />
      </div>
    </section>
  );
}

/** The records the person may start and the screens they open most, in the Home's banner so the way in is seen first. */
export function StartOrOpen({ user }: { user: SessionUser }): ReactElement {
  const { t } = useI18n();
  const { can } = usePermissions();
  return (
    <nav className="home__actions-bar" aria-label={t('home.actions.title')}>
      <ul className="home__actions">
        {can('PROJECT_REGISTER') && (
          <li>
            <Link to="/projects/new">{t('projects.nav.create')}</Link>
          </li>
        )}
        {can('CHANGE_REQUEST_RAISE') && (
          <li>
            <Link to="/change-requests/new">{t('common.shell.createChangeRequest')}</Link>
          </li>
        )}
        {can('PROJECT_VIEW') && (
          <li>
            <Link to="/projects">{t('projects.nav.register')}</Link>
          </li>
        )}
        {user.userType !== 'EXTERNAL' &&
          user.roleAssignments.some((assignment) =>
            PORTFOLIO_ROLES.includes(assignment.roleCode),
          ) && (
            <li>
              <Link to={PORTFOLIO_DASHBOARD_PATH}>{t('common.menu.portfolioDashboard')}</Link>
            </li>
          )}
        {user.userType !== 'EXTERNAL' && can('PROJECT_VIEW') && (
          <li>
            <Link to={DASHBOARDS_PATH}>{t('common.menu.dashboards')}</Link>
          </li>
        )}
        <li>
          <Link to="/notifications/preferences">{t('notifications.nav.preferences')}</Link>
        </li>
      </ul>
    </nav>
  );
}

function ApprovalsWaiting(): ReactElement {
  const { t, formatDateTime } = useI18n();
  const inbox = useApiResource(
    useCallback((signal: AbortSignal) => approvalTasksApi.listInbox(1, SHOWN, signal), []),
  );
  return (
    <HomeCard
      title={t('home.approvals.title')}
      resource={inbox}
      figure={(page) => page.totalCount}
      viewAll={{ to: '/approvals/inbox', label: t('home.approvals.all') }}
      empty={t('home.approvals.none')}
    >
      {(page) => (
        <ul className="home__list">
          {page.items.map((item) => (
            <li key={item.taskId}>
              <SubjectSummary
                subject={item.instance.subject}
                routingKey={item.instance.routingKey}
              />
              {item.dueAt !== null && (
                <span className="home__aside">
                  {isOverdue(item.dueAt) ? (
                    <StatusBadge label={t('approvals.overdue')} tone="negative" />
                  ) : (
                    t('home.dueAt', { date: formatDateTime(item.dueAt) })
                  )}
                </span>
              )}
            </li>
          ))}
        </ul>
      )}
    </HomeCard>
  );
}

function MyRequests(): ReactElement {
  const { t, formatDateTime } = useI18n();
  const mine = useApiResource(
    useCallback(
      (signal: AbortSignal) =>
        approvalInstancesApi.listMine({ status: 'PENDING', page: 1, pageSize: SHOWN }, signal),
      [],
    ),
  );
  return (
    <HomeCard
      title={t('home.requests.title')}
      resource={mine}
      figure={(page) => page.totalCount}
      viewAll={{ to: '/approvals/requests', label: t('home.requests.all') }}
      empty={t('home.requests.none')}
    >
      {(page) => (
        <ul className="home__list">
          {page.items.map((instance) => (
            <li key={instance.id}>
              <SubjectSummary subject={instance.subject} routingKey={instance.routingKey} />
              <span className="home__aside">
                {t('home.requests.submitted', { date: formatDateTime(instance.requestedAt) })}
              </span>
            </li>
          ))}
        </ul>
      )}
    </HomeCard>
  );
}

function MyTasks({ userId }: { userId: string }): ReactElement {
  const { t } = useI18n();
  const portfolio = useTaskPortfolio();
  const today = todayUtc();
  const weekEnd = daysAfter(today, WEEK_DAYS);
  return (
    <HomeCard
      title={t('home.tasks.title')}
      resource={portfolio}
      figure={({ entries }) =>
        listEntries('mine', entries, userId, today).filter(({ task }) => isOpen(task)).length
      }
      viewAll={{ to: '/tasks', label: t('home.tasks.all') }}
      empty={t('home.tasks.none')}
    >
      {({ entries }) => {
        const open = listEntries('mine', entries, userId, today).filter(({ task }) => isOpen(task));
        const overdue = open.filter(({ task }) => overdueDays(task, today) !== null).length;
        const dueThisWeek = open
          .filter(({ task }) => task.plannedFinishDate <= weekEnd)
          .sort((a, b) => a.task.plannedFinishDate.localeCompare(b.task.plannedFinishDate));
        return (
          <>
            <p className="home__aside">
              {t('home.tasks.summary', { overdue, week: dueThisWeek.length })}
            </p>
            <ul className="home__list">
              {dueThisWeek.slice(0, SHOWN).map(({ task, project }) => (
                <li key={task.id}>
                  <Link to={`/projects/${project.id}/tasks`}>{task.title.text}</Link>
                  <span className="home__aside">
                    {overdueDays(task, today) === null ? (
                      t('home.dueOn', { date: task.plannedFinishDate })
                    ) : (
                      <StatusBadge label={t('approvals.overdue')} tone="negative" />
                    )}
                  </span>
                </li>
              ))}
            </ul>
          </>
        );
      }}
    </HomeCard>
  );
}

function RequestsToAnswer({ userId }: { userId: string }): ReactElement {
  const { t, language } = useI18n();
  const awaiting = useApiResource(
    useCallback(
      async (signal: AbortSignal) =>
        (await externalRequestsApi.list({}, signal)).filter(
          (request) => request.status === 'ISSUED' && request.responsibleUserId === userId,
        ),
      [userId],
    ),
  );
  return (
    <HomeCard
      title={t('home.externalRequests.title')}
      resource={awaiting}
      figure={(requests) => requests.length}
      viewAll={{ to: MY_EXTERNAL_REQUESTS_PATH, label: t('home.externalRequests.all') }}
      empty={t('home.externalRequests.none')}
    >
      {(requests) => (
        <ul className="home__list">
          {requests.slice(0, SHOWN).map((request) => (
            <li key={request.id}>
              <Link to={MY_EXTERNAL_REQUESTS_PATH}>
                {(request.targetLabel ?? request.instructions).text}
              </Link>
              {request.dueDate !== null && (
                <span className="home__aside" lang={language}>
                  {t('home.dueOn', { date: request.dueDate })}
                </span>
              )}
            </li>
          ))}
        </ul>
      )}
    </HomeCard>
  );
}

function UnreadNotifications(): ReactElement {
  const { t, formatDateTime } = useI18n();
  const unread = useApiResource(
    useCallback(
      (signal: AbortSignal) =>
        notificationsApi.list({ unread: true, page: 1, pageSize: SHOWN }, signal),
      [],
    ),
  );
  return (
    <HomeCard
      title={t('home.notifications.title')}
      resource={unread}
      figure={(page) => page.totalCount}
      viewAll={{ to: '/notifications', label: t('home.notifications.all') }}
      empty={t('home.notifications.none')}
    >
      {(page) => (
        <ul className="home__list">
          {page.items.map((notification) => (
            <li key={notification.id}>
              {isAppRoute(notification.deepLink) ? (
                <Link to={notification.deepLink}>{headline(notification)}</Link>
              ) : (
                <span>{headline(notification)}</span>
              )}
              <span className="home__aside">{formatDateTime(notification.occurredAt)}</span>
            </li>
          ))}
        </ul>
      )}
    </HomeCard>
  );
}
