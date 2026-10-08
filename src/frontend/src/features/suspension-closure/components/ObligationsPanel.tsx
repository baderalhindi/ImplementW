import { type ReactElement, useState } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { ConfirmCommandDialog } from '@/features/risks/dialogs/ConfirmCommandDialog.tsx';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, FormAlert } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type ObligationCommand, obligationsApi } from '../api/closeoutApi.ts';
import {
  type PostProjectObligationCreateRequest,
  type PostProjectObligationDetail,
} from '../api/types.ts';
import { isSettled, obligationCommands } from '../closeoutRules.ts';
import { ObligationDialog } from '../dialogs/ObligationDialog.tsx';
import { managesProject, type ProjectFacts } from '../governedRequest.ts';
import { obligationTone } from '../presentation.ts';
import { isStale, suspensionClosureProblemMessage } from '../problems.ts';

type OpenDialog =
  | { kind: 'add' }
  | { kind: 'edit'; obligation: PostProjectObligationDetail; etag: string | null }
  | { kind: 'command'; obligation: PostProjectObligationDetail; command: ObligationCommand };

const COMMAND_DONE: Record<ObligationCommand, TranslationKey> = {
  start: 'suspensionClosure.done.obligationStarted',
  satisfy: 'suspensionClosure.done.obligationSatisfied',
  cancel: 'suspensionClosure.done.obligationCancelled',
  waive: 'suspensionClosure.done.obligationWaived',
};

/**
 * The project's post-project obligations (WF-10 §14.2 Obligations): they may stay open past Completion, and Closure waits
 * for every one to be settled (TASK-063 D-9). The Project Manager records and moves them; AHDA waives one.
 */
export function ObligationsPanel({
  obligations,
  user,
  project,
  target,
  onChanged,
  headingLevel = 2,
}: {
  obligations: PostProjectObligationDetail[];
  user: SessionUser;
  /** Null when the person cannot read the project: nothing is offered, the API decides. */
  project: ProjectFacts | null;
  /** The case a new obligation is recorded against now; null when none may be recorded. */
  target: Pick<PostProjectObligationCreateRequest, 'completionCaseId' | 'closureCaseId'> | null;
  onChanged: (message: TranslationKey, tone?: 'success' | 'warning') => void;
  headingLevel?: 2 | 3;
}): ReactElement {
  const { t } = useI18n();
  const [dialog, setDialog] = useState<OpenDialog | null>(null);
  const [readError, setReadError] = useState<string | null>(null);
  const personName = usePersonNames(obligations.map((obligation) => obligation.ownerUserId));
  const Heading = headingLevel === 2 ? 'h2' : 'h3';
  const adds = project !== null && target !== null && managesProject(user, project);
  const open = obligations.filter((obligation) => !isSettled(obligation.status)).length;

  const done = (message: TranslationKey) => {
    setDialog(null);
    onChanged(message);
  };
  const stale = () => {
    setDialog(null);
    onChanged('suspensionClosure.problems.stale', 'warning');
  };
  const edit = async (obligation: PostProjectObligationDetail) => {
    setReadError(null);
    try {
      const record = await obligationsApi.get(obligation.id);
      setDialog({ kind: 'edit', obligation: record.data, etag: record.etag });
    } catch (error) {
      setReadError(suspensionClosureProblemMessage(error, t));
    }
  };

  return (
    <section className="section" aria-labelledby="closeout-obligations" id="obligations">
      <div className="section__header">
        <Heading id="closeout-obligations">{t('suspensionClosure.obligations.title')}</Heading>
        {adds && (
          <button
            type="button"
            className="button"
            onClick={() => {
              setDialog({ kind: 'add' });
            }}
          >
            {t('suspensionClosure.obligations.add')}
          </button>
        )}
      </div>
      <p className="form__note">
        {t('suspensionClosure.obligations.intro', { open, total: obligations.length })}
      </p>
      <FormAlert message={readError} />
      {obligations.length === 0 ? (
        <EmptyState title={t('suspensionClosure.obligations.none')} />
      ) : (
        <TableContainer caption={t('suspensionClosure.obligations.caption')}>
          <thead>
            <tr>
              <th scope="col">{t('suspensionClosure.obligations.fields.title')}</th>
              <th scope="col">{t('suspensionClosure.obligations.fields.owner')}</th>
              <th scope="col">{t('suspensionClosure.obligations.fields.dueDate')}</th>
              <th scope="col">{t('suspensionClosure.obligations.fields.status')}</th>
              <th scope="col">{t('suspensionClosure.obligations.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {obligations.map((obligation) => {
              const commands =
                project === null ? [] : obligationCommands(user, project, obligation);
              const editable =
                project !== null && managesProject(user, project) && !isSettled(obligation.status);
              const name = obligation.title.text;
              return (
                <tr key={obligation.id}>
                  <td>
                    <span dir="auto">{name}</span>
                    {obligation.description !== null && (
                      <span className="cell__aside" dir="auto">
                        {obligation.description.text}
                      </span>
                    )}
                  </td>
                  <td>
                    {obligation.ownerUserId === null
                      ? t('suspensionClosure.obligations.noOwner')
                      : personName(obligation.ownerUserId)}
                  </td>
                  <td>
                    {obligation.dueDate === null ? (
                      t('suspensionClosure.obligations.noDueDate')
                    ) : (
                      <span dir="ltr">{obligation.dueDate}</span>
                    )}
                  </td>
                  <td>
                    <StatusBadge
                      label={t(`suspensionClosure.obligations.status.${obligation.status}`)}
                      tone={obligationTone(obligation.status)}
                    />
                  </td>
                  <td>
                    <span className="figure-group">
                      {editable && (
                        <button
                          type="button"
                          className="button button--link"
                          aria-label={t('suspensionClosure.obligations.editNamed', { name })}
                          onClick={() => void edit(obligation)}
                        >
                          {t('suspensionClosure.obligations.edit')}
                        </button>
                      )}
                      {commands.map((command) => (
                        <button
                          key={command}
                          type="button"
                          className="button button--link"
                          aria-label={t(`suspensionClosure.obligations.command.${command}.named`, {
                            name,
                          })}
                          onClick={() => {
                            setDialog({ kind: 'command', obligation, command });
                          }}
                        >
                          {t(`suspensionClosure.obligations.command.${command}.action`)}
                        </button>
                      ))}
                      {!editable && commands.length === 0 && t('common.values.none')}
                    </span>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </TableContainer>
      )}

      {(dialog?.kind === 'add' || dialog?.kind === 'edit') && project !== null && (
        <ObligationDialog
          user={user}
          project={project}
          knownUserIds={obligations.map((entry) => entry.ownerUserId)}
          target={target}
          existing={
            dialog.kind === 'edit' ? { obligation: dialog.obligation, etag: dialog.etag } : null
          }
          onClose={() => {
            setDialog(null);
          }}
          onDone={() => {
            done(
              dialog.kind === 'add'
                ? 'suspensionClosure.done.obligationAdded'
                : 'suspensionClosure.done.obligationSaved',
            );
          }}
          onStale={stale}
        />
      )}
      {dialog?.kind === 'command' && (
        <ConfirmCommandDialog
          title={t(`suspensionClosure.obligations.command.${dialog.command}.title`, {
            name: dialog.obligation.title.text,
          })}
          consequence={t(`suspensionClosure.obligations.command.${dialog.command}.consequence`)}
          confirmLabel={t(`suspensionClosure.obligations.command.${dialog.command}.action`)}
          run={() => obligationsApi.command(dialog.obligation.id, dialog.command, null)}
          describe={suspensionClosureProblemMessage}
          isStale={isStale}
          onClose={() => {
            setDialog(null);
          }}
          onDone={() => {
            done(COMMAND_DONE[dialog.command]);
          }}
          onStale={stale}
        />
      )}
    </section>
  );
}
