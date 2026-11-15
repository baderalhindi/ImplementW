import { type ReactElement, type ReactNode, useCallback, useEffect, useState } from 'react';
import { Link, Outlet, useLocation, useNavigate, useParams } from 'react-router';

import { useAuditAccess } from '@/features/audit-activity/useAuditAccess.ts';
import { usePermissions } from '@/features/identity-access/permissions/permissions.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { LifecycleBanner } from '@/features/suspension-closure/components/LifecycleBanner.tsx';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import {
  canSeeTab,
  isClosed,
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
import { languageTag } from '../presentation.ts';
import { projectProblemMessage } from '../problems.ts';
import { useProjectLookups } from '../useProjectLookups.ts';

import { rememberedTab, rememberTab, tabOfPath } from './tabGroups.ts';
import { useWorkspace, type WorkspaceContext } from './workspaceContext.ts';
import { WorkspaceTabs } from './WorkspaceTabs.tsx';

const TAB_LABELS: Record<WorkspaceTabKey, TranslationKey> = {
  overview: 'projects.workspace.tabs.overview',
  registration: 'projects.workspace.tabs.registration',
  location: 'projects.workspace.tabs.location',
  progress: 'projects.workspace.tabs.progress',
  schedule: 'projects.workspace.tabs.schedule',
  tasks: 'projects.workspace.tabs.tasks',
  milestones: 'projects.workspace.tabs.milestones',
  financials: 'projects.workspace.tabs.financials',
  kpis: 'projects.workspace.tabs.kpis',
  risks: 'projects.workspace.tabs.risks',
  issuesChallenges: 'projects.workspace.tabs.issuesChallenges',
  changeRequests: 'projects.workspace.tabs.changeRequests',
  suspension: 'projects.workspace.tabs.suspension',
  closeout: 'projects.workspace.tabs.closeout',
  externalRequests: 'projects.workspace.tabs.externalRequests',
  reviews: 'projects.workspace.tabs.reviews',
  documents: 'projects.workspace.tabs.documents',
  activity: 'projects.workspace.tabs.activity',
  audit: 'projects.workspace.tabs.audit',
};

/** The notices SCR-033/034, and SCR-107 when it deletes a draft change request, hand over when they navigate here. */
const ARRIVAL_NOTICES: Record<string, TranslationKey> = {
  created: 'projects.done.created',
  updated: 'projects.done.updated',
  stagePlanNotSaved: 'projects.done.stagePlanNotSaved',
  progressMethodNotSaved: 'projects.done.progressMethodNotSaved',
  changeRequestDeleted: 'changeRequests.done.deleted',
};

type OpenDialog = 'delete' | 'submit' | 'status' | null;

/**
 * SCR-040 Project Workspace: the project's heading, the actions its state and the person allow, and the tabs the
 * person's scope reaches (acceptance criterion 3; access.ts). The project is read once here and lent to each tab. A
 * CLOSED project's workspace is read-only (TASK-064): its banner says so on every tab, and no tab offers a write.
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
  const auditAccess = useAuditAccess();
  const permissions = usePermissions();
  const lookups = useProjectLookups();
  const arrival = (location.state as { notice?: string } | null)?.notice;
  const arrivalKey = arrival === undefined ? undefined : ARRIVAL_NOTICES[arrival];
  const [notice, setNotice] = useState<Notice | null>(
    arrivalKey === undefined ? null : { tone: 'success', message: t(arrivalKey) },
  );
  const [dialog, setDialog] = useState<OpenDialog>(null);

  // Review round 8, point 04: a project opened at its address returns to the tab this person last used on it, when it is still offered; a tab
  // opened is remembered. A tab chosen on the page (CHOSEN_TAB), the overview among them, is kept as chosen.
  const loaded = project.data?.data;
  const auditReady = !auditAccess.loading;
  useEffect(() => {
    if (session === null || loaded === undefined || !auditReady) {
      return;
    }
    const tab = tabOfPath(location.pathname, projectId, WORKSPACE_TABS);
    const chosen = (location.state as { chosenTab?: boolean } | null)?.chosenTab === true;
    if (tab?.key === 'overview' && !chosen) {
      const offered = visibleTabs(
        projectAccess(session.user, loaded, auditAccess.data ?? null, permissions),
      );
      const last = offered.find(
        (candidate) => candidate.key === rememberedTab(session.user.id, projectId),
      );
      if (last !== undefined && last.key !== 'overview') {
        void navigate(`/projects/${projectId}/${last.path}`, {
          replace: true,
          state: location.state as unknown,
        });
        return;
      }
    }
    if (tab !== undefined) {
      rememberTab(session.user.id, projectId, tab.key);
    }
    // permissions and auditAccess.data are read when the page opens a tab; the address decides when this runs.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [location.pathname, location.state, projectId, loaded, auditReady]);

  if (session === null) {
    return null;
  }
  if (project.data === undefined) {
    return project.loading ? (
      <LoadingState />
    ) : (
      <ErrorState
        error={project.error}
        message={projectProblemMessage(project.error, t)}
        onRetry={project.reload}
      />
    );
  }

  const detail = project.data.data;
  const etag = project.data.etag;
  // A failed GET /audit-access offers no history tab: audit permissions are never assumed.
  const audit = auditAccess.data ?? null;
  const access = projectAccess(session.user, detail, audit, permissions);
  const editable = access.reached && isEditable(detail.status);
  const commands = statusCommands(detail.status, access);
  const context: WorkspaceContext = {
    user: session.user,
    project: detail,
    etag,
    access,
    auditAccess: audit,
    auditAccessPending: auditAccess.loading,
    lookups,
    reload: project.reload,
    notify: setNotice,
  };
  const changed = (message: TranslationKey) => {
    setDialog(null);
    setNotice({ tone: 'success', message: t(message) });
    project.reload();
  };

  const closed = isClosed(detail.status);

  return (
    <div className={closed ? 'workspace workspace--read-only' : 'workspace'}>
      <PageHeader
        title={detail.title.text}
        titleLanguage={languageTag(detail.title.language)}
        description={
          detail.formalProjectId === null
            ? t('projects.formalProjectId.notIssuedLong')
            : t('projects.formalProjectId.issued', { id: detail.formalProjectId })
        }
        actions={
          <>
            {(editable || (access.permissions.can('PROJECT_REGISTRATION_EDIT') && !closed)) && (
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
            {(access.permissions.can('PROJECT_DELETE') ||
              (access.creator && detail.status === 'DRAFT')) && (
              <ActionButton
                danger
                onClick={() => {
                  setDialog('delete');
                }}
              >
                {t(
                  access.permissions.can('PROJECT_DELETE')
                    ? 'projects.delete.anyConfirm'
                    : 'projects.actions.delete',
                )}
              </ActionButton>
            )}
          </>
        }
      />
      <p className="workspace__status">
        <ProjectStatusBadge
          isCanceled={detail.isCanceled}
          status={detail.status}
          legacyIntakeDate={detail.legacyIntakeDate}
        />
        <span className="details__aside">
          {t('projects.workspace.revision', { revision: detail.revisionNo })}
        </span>
      </p>
      <LifecycleBanner project={detail} />
      <PageNotice notice={notice} />

      <WorkspaceTabs
        projectId={detail.id}
        userId={session.user.id}
        tabs={visibleTabs(access)}
        current={tabOfPath(location.pathname, detail.id, WORKSPACE_TABS)?.key ?? 'overview'}
        labels={TAB_LABELS}
      />

      <div className="tabs__panel">
        <Outlet context={context} />
      </div>

      <DeleteDraftDialog
        open={dialog === 'delete'}
        project={detail}
        anyState={access.permissions.can('PROJECT_DELETE')}
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
    </div>
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
 * A tab reached by its address shows only to someone it is offered to; anyone else is told it is not available (or
 * `denied`, when the tab says why), and nothing is read for them. A history tab waits for GET /audit-access first.
 */
export function WorkspaceTabGuard({
  tab,
  children,
  denied,
}: {
  tab: WorkspaceTabKey;
  children: ReactNode;
  denied?: ReactNode;
}): ReactElement {
  const { t } = useI18n();
  const { access, auditAccessPending } = useWorkspace();
  const definition = WORKSPACE_TABS.find((candidate) => candidate.key === tab);
  const waitsForAudit =
    definition?.audience === 'activity' || definition?.audience === 'projectAudit';
  if (waitsForAudit && auditAccessPending) {
    return <LoadingState />;
  }
  if (definition === undefined || !canSeeTab(definition, access)) {
    return denied === undefined ? (
      <p className="state">{t('projects.workspace.tabUnavailable')}</p>
    ) : (
      <>{denied}</>
    );
  }
  return <>{children}</>;
}
