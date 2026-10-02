import { type ReactElement } from 'react';

import { DocumentBrowser } from '@/features/documents/components/DocumentBrowser.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { useWorkspace } from './workspaceContext.ts';

/** SCR-043 Documents: the project's documents as WF-12 lists them (SCR-121), inside the workspace. */
export function DocumentsTab(): ReactElement {
  const { t } = useI18n();
  const { project } = useWorkspace();
  return (
    <DocumentBrowser
      key={project.id}
      embedded
      title={t('projects.documents.title')}
      description={t('projects.documents.description')}
      projectId={project.id}
    />
  );
}
