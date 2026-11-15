import { type ReactElement, type SyntheticEvent, useCallback, useState } from 'react';
import { useNavigate } from 'react-router';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { projectsApi } from '@/features/projects/api/projectsApi.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { SelectField } from '@/shared/ui/FormFields.tsx';
import { LoadingState } from '@/shared/ui/States.tsx';

import { type QuickAction } from './quickActions.ts';

type ProjectTab = Extract<QuickAction['target'], { projectTab: string }>['projectTab'];

/** The tabs whose new-record form opens straight away from `?create=1`; the progress and financial updates start from their tab's own button. */
const OPENS_FORM: readonly ProjectTab[] = ['risks', 'issues-challenges'];

/**
 * A quick action done inside one project (review round 8, point 08): the person picks the project, then its tab opens. The list is the active projects
 * the person runs — for someone outside AHDA, the active projects they can see; the tab still checks that they may act there.
 */
export function ProjectActionDialog({
  action,
  user,
  onClose,
}: {
  action: QuickAction & { target: { projectTab: ProjectTab } };
  user: SessionUser;
  onClose: () => void;
}): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open title={t(action.label)} onClose={onClose}>
      <ProjectChoice tab={action.target.projectTab} user={user} onClose={onClose} />
    </Dialog>
  );
}

function ProjectChoice({
  tab,
  user,
  onClose,
}: {
  tab: ProjectTab;
  user: SessionUser;
  onClose: () => void;
}): ReactElement {
  const { t } = useI18n();
  const navigate = useNavigate();
  const projects = useApiResource(
    useCallback(
      (signal: AbortSignal) =>
        projectsApi.listAll(
          user.userType === 'EXTERNAL'
            ? { status: 'ACTIVE' }
            : { status: 'ACTIVE', projectManagerUserId: user.id },
          signal,
        ),
      [user],
    ),
  );
  const [projectId, setProjectId] = useState('');
  const [attempted, setAttempted] = useState(false);

  if (projects.data === undefined) {
    return projects.loading ? (
      <LoadingState />
    ) : (
      <p className="state">{t('home.quickActions.projectsUnavailable')}</p>
    );
  }
  if (projects.data.length === 0) {
    return (
      <>
        <p className="state">{t('home.quickActions.noProjects')}</p>
        <div className="form__actions">
          <button type="button" className="button" onClick={onClose}>
            {t('common.actions.close')}
          </button>
        </div>
      </>
    );
  }

  const open = (event: SyntheticEvent) => {
    event.preventDefault();
    setAttempted(true);
    if (projectId === '') {
      return;
    }
    onClose();
    void navigate(`/projects/${projectId}/${tab}${OPENS_FORM.includes(tab) ? '?create=1' : ''}`);
  };

  return (
    <form className="form" noValidate onSubmit={open}>
      <SelectField
        label={t('home.quickActions.project')}
        name="projectId"
        value={projectId}
        onChange={setProjectId}
        placeholder={t('home.quickActions.chooseProject')}
        options={projects.data.map((project) => ({
          value: project.id,
          label:
            project.formalProjectId === null
              ? project.title.text
              : `${project.formalProjectId} — ${project.title.text}`,
        }))}
        required
        error={attempted && projectId === '' ? t('home.quickActions.projectRequired') : undefined}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary">
          {t('home.quickActions.openProject')}
        </button>
        <button type="button" className="button" onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
