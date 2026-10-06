import { type ReactElement } from 'react';
import { useParams } from 'react-router';

import { ProjectRisks } from '@/features/risks/ProjectRisks.tsx';
import { RiskDetail } from '@/features/risks/RiskDetail.tsx';

import { useWorkspace } from './workspaceContext.ts';

/** SCR-080 Risk Register for the project, as WF-06 shows it (TASK-056), inside the workspace. */
export function RisksTab(): ReactElement {
  const { project, user } = useWorkspace();
  return <ProjectRisks key={project.id} project={project} user={user} />;
}

/** SCR-082 Risk Detail, under the Risks tab. */
export function RiskDetailTab(): ReactElement {
  const { project, user } = useWorkspace();
  const { riskId = '' } = useParams();
  return <RiskDetail key={riskId} project={project} user={user} riskId={riskId} />;
}
