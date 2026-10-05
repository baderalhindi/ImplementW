import { type ReactElement } from 'react';
import { useParams } from 'react-router';

import { KpiHistory } from '@/features/financial-kpi/KpiHistory.tsx';
import { ProjectKpis } from '@/features/financial-kpi/ProjectKpis.tsx';

import { useWorkspace } from './workspaceContext.ts';

/** SCR-050 Project KPIs, as WF-14 shows them (TASK-053), inside the workspace. */
export function KpisTab(): ReactElement {
  const { project, user } = useWorkspace();
  return <ProjectKpis key={project.id} project={project} user={user} />;
}

/** SCR-073 KPI History/Trend, under the KPIs tab. */
export function KpiHistoryTab(): ReactElement {
  const { project } = useWorkspace();
  const { assignmentId = '' } = useParams();
  return <KpiHistory key={assignmentId} project={project} assignmentId={assignmentId} />;
}
