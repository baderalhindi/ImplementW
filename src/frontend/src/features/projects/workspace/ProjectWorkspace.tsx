import { type ReactElement, type ReactNode, useCallback, useState } from 'react';
import { Link, NavLink, Outlet, useLocation, useNavigate, useParams } from 'react-router';

import { useSession } from '@/features/identity-access/session/useSession.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import {
  canSeeTab,
  isEditable,
  projectAccess,
  statusCommands,
  visibleTabs,
  WORKSPACE_TABS,
  type WorkspaceTabKey,
} from '../access.ts';
import { projectsApi } from '../api/projectsApi.ts';
import { ProjectStatusBadge } from '../components/ProjectStatusBadge.tsx';
import { AssignManagerDialog } from '../dialogs/AssignManagerDialog.tsx';
import { ChangeStatusDialog } from '../dialogs/ChangeStatusDialog.tsx';
import { DeleteDraftDialog } from '../dialogs/DeleteDraftDialog.tsx';
import { projectProblemMessage } from '../problems.ts';
import { useProjectLookups } from '../useProjectLookups.ts';

import { useWorkspace, type WorkspaceContext } from './workspaceContext.ts';

const TAB_LABELS: Record<WorkspaceTabKey, TranslationKey> = {
  overview: 'projects.workspace.tabs.overview',
  registration: 'projects.workspace.tabs.registration',
  location: 'projects.workspace.tabs.location',
  progress: 'projects.workspace.tabs.progress',
  reviews: 'projects.workspace.tabs.reviews',
  documents: 'projects.workspace.tabs.documents',
};

/** The notices SCR-033/034 hand over when they navigate here. */
const ARRIVAL_NOTICES: Record<string, TranslationKey> = {
  created: 'projects.done.created',
  updated: 'projects.done.updated',
};

type OpenDialog = 'delete' | 'submit' | 'status' | null;

/**
 * SCR-040 Project Workspace: the project's heading, the actions its state and the person allow, and the tabs the
 * person's scope reaches (acceptance criterion 3; access.ts). The project is read once here and lent to each tab.
 */
export function ProjectWorkspace(): ReactElement | null {
  const { t } = useI18n();
  const { session } = useSession();
  const { projectId = '' } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const load = useCallback(
    (signal: AbortSignal) => projectsApi.get(projectId, signal),
    [projectId],
  );
  const project = useApiResource(load);
  const lookups = useProjectLookups();
  const arrival = (location.state as { notice?: string } | null)?.notice;
  const arrivalKey = arrival === undefined ? undefined : ARRIVAL_NOTICES[arrival];
  const [notice, setNotice] = useState<Notice | null>(
    arrivalKey === undefined ? null : { tone: 'success', message: t(arrivalKey) },
  );
  const [dialog, setDialog] = useState<OpenDialog>(null);

  if (session === null) {
    return null;
  }
  if (project.data === undefined) {
    return project.loading ? (
      <LoadingState />
    ) : (
      <ErrorState message={projectProblemMessage(project.error, t)} onRetry={project.reload} />
    );
  }

  const detail = project.data.data;
  const etag = project.data.etag;
  const access = projectAccess(session.user, detail);
  const editable = access.reached && isEditable(detail.status);
  const commands = statusCommands(detail.status, access);
  const context: WorkspaceContext = {
    user: session.user,
    project: detail,
    etag,
    access,
    lookups,
    reload: project.reload,
    notify: setNotice,
  };
  const changed = (message: TranslationKey) => {
    setDialog(null);
    setNotice({ tone: 'success', message: t(message) });
    project.reload();
  };

  return (
    <>
      <PageHeader
        title={detail.title.text}
        description={
          detail.formalProjectId === null
            ? t('projects.formalProjectId.notIssuedLong')
            : t('projects.formalProjectId.issued', { id: detail.formalProjectId })
        }
        actions={
          <>
            {editable && (
              <Link className="button" to={`/projects/${detail.id}/edit`}>
                {t('projects.actions.edit')}
              </Link>
            )}
            {editable && (
              <ActionButton
                primary
                onClick={() => {
                  setDialog('submit');
                }}
              >
                {t('projects.actions.submit')}
              </ActionButton>
            )}
            {commands.length > 0 && (
              <ActionButton
                onClick={() => {
                  setDialog('status');
                }}
              >
                {t('projects.actions.changeStatus')}
              </ActionButton>
            )}
            {access.creator && detail.status === 'DRAFT' && (
              <ActionButton
                danger
                onClick={() => {
                  setDialog('delete');
                }}
              >
                {t('projects.actions.delete')}
              </ActionButton>
            )}
          </>
        }
      />
      <p className="workspace__status">
        <ProjectStatusBadge status={detail.status} legacyIntakeDate={detail.legacyIntakeDate} />
        <span className="details__aside">
          {t('projects.workspace.revision', { revision: detail.revisionNo })}
        </span>
      </p>
      <PageNotice notice={notice} />

      <nav className="tabs" aria-label={t('projects.workspace.tabs.label')}>
        <ul>
          {visibleTabs(access).map((tab) => (
            <li key={tab.key}>
              {/* Only the overview matches exactly: a tab with pages of its own (progress history) stays current. */}
              <NavLink
                to={`/projects/${detail.id}${tab.path === '' ? '' : `/${tab.path}`}`}
                end={tab.path === ''}
              >
                {t(TAB_LABELS[tab.key])}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>

      <div className="tabs__panel">
        <Outlet context={context} />
      </div>

      <DeleteDraftDialog
        open={dialog === 'delete'}
        project={detail}
        etag={etag}
        onClose={() => {
          setDialog(null);
        }}
        onDeleted={() => {
          void navigate('/projects', { state: { notice: 'deleted' } });
        }}
      />
      <AssignManagerDialog
        open={dialog === 'submit'}
        user={session.user}
        project={detail}
        etag={etag}
        lookups={lookups}
        onClose={() => {
          setDialog(null);
        }}
        onSubmitted={() => {
          changed('projects.done.submitted');
        }}
      />
      <ChangeStatusDialog
        open={dialog === 'status'}
        project={detail}
        etag={etag}
        commands={commands}
        onClose={() => {
          setDialog(null);
        }}
        onChanged={changed}
      />
    </>
  );
}

function ActionButton({
  children,
  onClick,
  primary = false,
  danger = false,
}: {
  children: ReactNode;
  onClick: () => void;
  primary?: boolean;
  danger?: boolean;
}): ReactElement {
  const variant = primary ? ' button--primary' : danger ? ' button--danger' : '';
  return (
    <button type="button" className={`button${variant}`} onClick={onClick}>
      {children}
    </button>
  );
}

/**
 * A tab reached by its address shows only to someone it is offered to; anyone else is told it is not available, and
 * nothing is read for them.
 */
export function WorkspaceTabGuard({
  tab,
  children,
}: {
  tab: WorkspaceTabKey;
  children: ReactNode;
}): ReactElement {
  const { t } = useI18n();
  const { access } = useWorkspace();
  const definition = WORKSPACE_TABS.find((candidate) => candidate.key === tab);
  if (definition === undefined || !canSeeTab(definition, access)) {
    return <p className="state">{t('projects.workspace.tabUnavailable')}</p>;
  }
  return <>{children}</>;
}
