import { type ReactElement } from 'react';
import { useParams } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { DocumentBrowser } from '../components/DocumentBrowser.tsx';
import { shortId } from '../presentation.ts';

/** SCR-120 Document Library: every document the caller may read, whatever its project. */
export function DocumentLibraryPage(): ReactElement {
  const { t } = useI18n();
  return (
    <DocumentBrowser
      title={t('documents.library.title')}
      description={t('documents.library.description')}
      projectId={undefined}
    />
  );
}

/**
 * SCR-121 Project Documents: one project's documents; an upload here belongs to the project. The project is named by
 * its id until the Project module publishes a name (TASK-038 F-2).
 */
export function ProjectDocumentsPage(): ReactElement {
  const { t } = useI18n();
  const { projectId = '' } = useParams();
  return (
    <DocumentBrowser
      // A new project starts from its own filters, upload dialog and notice.
      key={projectId}
      title={t('documents.project.title', { id: shortId(projectId) })}
      description={t('documents.project.description')}
      projectId={projectId}
    />
  );
}
