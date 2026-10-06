import { type RouteObject } from 'react-router';

import { type NavigationItem } from '@/features/identity-access/routes.tsx';

import { CreateProjectPage, EditProjectPage } from './form/ProjectFormPages.tsx';
import { MyProjectsPage, ProjectRegisterPage } from './register/ProjectListPages.tsx';
import { DocumentsTab } from './workspace/DocumentsTab.tsx';
import { FinancialHistoryTab, FinancialsTab } from './workspace/FinancialsTab.tsx';
import { KpiHistoryTab, KpisTab } from './workspace/KpisTab.tsx';
import { LocationTab } from './workspace/LocationTab.tsx';
import { MilestonesTab } from './workspace/MilestonesTab.tsx';
import { OverviewTab } from './workspace/OverviewTab.tsx';
import { ProgressHistoryTab, ProgressTab } from './workspace/ProgressTab.tsx';
import { ProjectWorkspace, WorkspaceTabGuard } from './workspace/ProjectWorkspace.tsx';
import { RegistrationTab } from './workspace/RegistrationTab.tsx';
import { ReviewsTab } from './workspace/ReviewsTab.tsx';
import { RiskDetailTab, RisksTab } from './workspace/RisksTab.tsx';
import { BaselinesTab, GanttTab, ScheduleTab } from './workspace/ScheduleTab.tsx';
import { TasksTab } from './workspace/TasksTab.tsx';

/**
 * WF-01, mounted under /projects for any signed-in person. No role is checked here: which projects a person sees and
 * what they may change is the API's answer (PROJECT_VIEW, PROJECT_REGISTER, PROJECT_REVIEW, PROJECT_ACTIVATE; TASK-041
 * D-10). The workspace tabs follow the person's scope (access.ts). SCR-033 is open to external entity users too: the
 * gate decision against it was changed by ADR-013's amendment (D-1).
 */
export const projectRoutes: RouteObject[] = [
  { index: true, element: <ProjectRegisterPage /> }, // SCR-025
  { path: 'mine', element: <MyProjectsPage /> }, // SCR-026
  { path: 'new', element: <CreateProjectPage /> }, // SCR-033
  { path: ':projectId/edit', element: <EditProjectPage /> }, // SCR-034
  {
    path: ':projectId',
    element: <ProjectWorkspace />, // SCR-040 shell, MOD-001, MOD-002, MOD-003
    children: [
      { index: true, element: <OverviewTab /> }, // SCR-040 Overview
      { path: 'registration', element: <RegistrationTab /> }, // SCR-041
      { path: 'location', element: <LocationTab /> }, // SCR-035
      {
        path: 'progress', // SCR-048, MOD-020, MOD-021, MOD-022 (TASK-045)
        element: (
          <WorkspaceTabGuard tab="progress">
            <ProgressTab />
          </WorkspaceTabGuard>
        ),
      },
      {
        path: 'progress/history', // SCR-070
        element: (
          <WorkspaceTabGuard tab="progress">
            <ProgressHistoryTab />
          </WorkspaceTabGuard>
        ),
      },
      {
        path: 'schedule', // SCR-060, MOD-015 (TASK-047)
        element: (
          <WorkspaceTabGuard tab="schedule">
            <ScheduleTab />
          </WorkspaceTabGuard>
        ),
      },
      {
        path: 'schedule/gantt', // SCR-045
        element: (
          <WorkspaceTabGuard tab="schedule">
            <GanttTab />
          </WorkspaceTabGuard>
        ),
      },
      {
        path: 'schedule/baselines', // SCR-061, MOD-017, MOD-018
        element: (
          <WorkspaceTabGuard tab="schedule">
            <BaselinesTab />
          </WorkspaceTabGuard>
        ),
      },
      {
        path: 'tasks', // SCR-047, MOD-010 to MOD-015 (TASK-049)
        element: (
          <WorkspaceTabGuard tab="tasks">
            <TasksTab />
          </WorkspaceTabGuard>
        ),
      },
      {
        path: 'milestones', // SCR-046, MOD-016, MOD-019 (TASK-051)
        element: (
          <WorkspaceTabGuard tab="milestones">
            <MilestonesTab />
          </WorkspaceTabGuard>
        ),
      },
      {
        path: 'financials', // SCR-049, MOD-023 (TASK-053)
        element: (
          <WorkspaceTabGuard tab="financials">
            <FinancialsTab />
          </WorkspaceTabGuard>
        ),
      },
      {
        path: 'financials/history', // SCR-071
        element: (
          <WorkspaceTabGuard tab="financials">
            <FinancialHistoryTab />
          </WorkspaceTabGuard>
        ),
      },
      {
        path: 'kpis', // SCR-050, MOD-024 (TASK-053)
        element: (
          <WorkspaceTabGuard tab="kpis">
            <KpisTab />
          </WorkspaceTabGuard>
        ),
      },
      {
        path: 'kpis/:assignmentId', // SCR-073
        element: (
          <WorkspaceTabGuard tab="kpis">
            <KpiHistoryTab />
          </WorkspaceTabGuard>
        ),
      },
      {
        path: 'risks', // SCR-080, MOD-030 (TASK-056)
        element: (
          <WorkspaceTabGuard tab="risks">
            <RisksTab />
          </WorkspaceTabGuard>
        ),
      },
      {
        path: 'risks/:riskId', // SCR-082, MOD-031–035 (TASK-056)
        element: (
          <WorkspaceTabGuard tab="risks">
            <RiskDetailTab />
          </WorkspaceTabGuard>
        ),
      },
      {
        path: 'reviews', // SCR-042
        element: (
          <WorkspaceTabGuard tab="reviews">
            <ReviewsTab />
          </WorkspaceTabGuard>
        ),
      },
      {
        path: 'documents', // SCR-043
        element: (
          <WorkspaceTabGuard tab="documents">
            <DocumentsTab />
          </WorkspaceTabGuard>
        ),
      },
    ],
  },
];

export const projectNavigation: NavigationItem[] = [
  { to: '/projects', label: 'projects.nav.register', end: true },
  { to: '/projects/mine', label: 'projects.nav.mine' },
  { to: '/projects/new', label: 'projects.nav.create' },
];
