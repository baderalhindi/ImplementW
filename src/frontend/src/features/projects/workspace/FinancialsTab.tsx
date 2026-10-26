import { type ReactElement } from 'react';

import { FinancialHistory } from '@/features/financial-kpi/FinancialHistory.tsx';
import { ProjectFinancials } from '@/features/financial-kpi/ProjectFinancials.tsx';

import { useWorkspace } from './workspaceContext.ts';

/** SCR-049 Project Financials, as WF-14 shows it (TASK-053), inside the workspace. */
export function FinancialsTab(): ReactElement {
  const { project, user } = useWorkspace();
  return <ProjectFinancials key={project.id} project={project} user={user} />;
}

/** SCR-071 Financial Progress, under the Financials tab. */
export function FinancialHistoryTab(): ReactElement {
  const { project } = useWorkspace();
  return <FinancialHistory key={project.id} project={project} />;
}
