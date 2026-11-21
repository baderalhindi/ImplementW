import { type ReactElement, type SyntheticEvent, useEffect, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { languageTag } from '@/features/projects/presentation.ts';
import { oneDecimal } from '@/features/projects/stagePlan/stageProgress.ts';
import { type Translate } from '@/features/identity-access/problems.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { type FieldCodes, useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { SelectField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { contributionsApi } from '../api/externalParticipationApi.ts';
import { myUpdateRequestsApi } from '../api/monthlyUpdatesApi.ts';
import { type MyUpdateRequest } from '../api/monthlyUpdateTypes.ts';
import { type ExternalContributionDetail } from '../api/types.ts';
import { ResponseInput } from '../components/ResponseForm.tsx';
import { useFieldLabel } from '../labels.ts';
import { isStale, participationProblemMessage, responseFieldMessage } from '../problems.ts';
import { responseFieldCodes, responseIssuesOf } from '../rules.ts';

import { useCounted, useDayAndMonth, useMonthName } from './counts.ts';
import {
  CURRENT_STAGE_FIELD,
  monthlyFields,
  monthlyFieldsToSend,
  monthlyStageFigures,
} from './monthlyForm.ts';
import { myUpdatesStore, useMyUpdates } from './myUpdatesStore.ts';

/** The revision being answered: where the next save goes and the ETag it sends. */
interface DraftRevision {
  id: string;
  etag: string | null;
}

/** What is typed for one project; kept here, so closing the window or moving on never loses it. */
interface LocalDraft {
  values: Record<string, string>;
  /** Typed since the last save. */
  dirty: boolean;
  revision: DraftRevision | null;
}

/**
 * The session's identity: `sessionExpiresAt` is set at sign-in and neither a refresh nor a step-up moves it, unlike `authenticatedAt`,
 * which a step-up does. Session storage survives a reload of the tab, so the window opens once per sign-in.
 */
function shownKey(session: Session): string {
  return `pmplatform.monthlyWindowShown.${session.user.id}.${session.sessionExpiresAt}`;
}

function wasShown(key: string): boolean {
  try {
    return sessionStorage.getItem(key) !== null;
  } catch {
    return false;
  }
}

function markShown(key: string): void {
  try {
    sessionStorage.setItem(key, 'true');
  } catch {
    // A blocked store only opens the window again on the next reload.
  }
}

function revisionOf(request: MyUpdateRequest): DraftRevision | null {
  return request.contributionId === null
    ? null
    : {
        id: request.contributionId,
        etag:
          request.contributionVersion === null ? null : `"${String(request.contributionVersion)}"`,
      };
}

/**
 * The monthly update window (the 5 Nov 2026 review §2.1), for anyone a request names as its responder. It opens by itself once per sign-in
 * while something is due, and from the bell's pinned item or the Home at any time. One short form per project: what is typed is saved on
 * leaving the project, closing the window or the save button; closing never loses a draft.
 */
export function MonthlyUpdateWindow(): ReactElement | null {
  const { session } = useSession();
  const { page, windowOpen } = useMyUpdates();
  const decided = useRef(false);
  const key = session === null ? null : shownKey(session);
  const drafts = useLocalDrafts();
  const [activeId, setActiveId] = useState<string | null>(null);
  const opened = useRef(new Set<string>());

  useEffect(() => myUpdatesStore.watch(), []);

  // The first answer decides: a request that arrives later waits in the bell rather than interrupting the person.
  useEffect(() => {
    if (page === null || decided.current || key === null) {
      return;
    }
    decided.current = true;
    if (page.totalCount > 0 && !wasShown(key)) {
      markShown(key);
      myUpdatesStore.openWindow();
    }
  }, [page, key]);

  const leave = () => {
    const leaving = page?.items.find((item) => item.requestId === activeId);
    if (leaving !== undefined) {
      void drafts.saveQuietly(leaving);
    }
  };

  const open = (request: MyUpdateRequest | null) => {
    setActiveId(request?.requestId ?? null);
    if (request !== null && !opened.current.has(request.requestId)) {
      opened.current.add(request.requestId);
      // Only the batch page's "opened" depends on it; the form works without.
      void myUpdateRequestsApi.markOpened(request.requestId).catch(() => undefined);
    }
  };

  return (
    <WindowDialog
      open={windowOpen}
      activeId={activeId}
      drafts={drafts}
      onChoose={(request) => {
        leave();
        open(request);
      }}
      onNext={open}
      onClose={() => {
        leave();
        myUpdatesStore.closeWindow();
      }}
    />
  );
}

interface LocalDrafts {
  draftOf: (request: MyUpdateRequest) => LocalDraft;
  edit: (request: MyUpdateRequest, fieldCode: string, value: string) => void;
  /** Stores what is typed as the draft, when anything is, and answers with the revision that holds it. */
  persist: (request: MyUpdateRequest) => Promise<DraftRevision>;
  /** Stores what is typed when it can be; a value of the wrong shape, or a failed save, stays typed and marked unsaved. */
  saveQuietly: (request: MyUpdateRequest) => Promise<void>;
  /** The record moved on: the next save starts from the version the API now reports. */
  forgetRevision: (requestId: string) => void;
}

function useLocalDrafts(): LocalDrafts {
  const { language } = useI18n();
  const [drafts, setDrafts] = useState<Record<string, LocalDraft>>({});

  const draftOf = (request: MyUpdateRequest): LocalDraft =>
    drafts[request.requestId] ?? {
      values: request.prefill,
      dirty: false,
      revision: revisionOf(request),
    };

  const persist = async (request: MyUpdateRequest): Promise<DraftRevision> => {
    const draft = draftOf(request);
    const revision = draft.revision ?? revisionOf(request);
    if (!draft.dirty && revision !== null) {
      return revision;
    }
    const sent = monthlyFieldsToSend(request, draft.values, language);
    const saved: ApiResponse<ExternalContributionDetail> =
      revision === null
        ? await contributionsApi.create(request.requestId, sent)
        : await contributionsApi.update(revision.id, sent, revision.etag);
    const stored = { id: saved.data.id, etag: saved.etag };
    setDrafts((current) => {
      const now = current[request.requestId] ?? draft;
      return {
        ...current,
        // Whatever was typed while the save ran is still unsaved.
        [request.requestId]: { ...now, dirty: now.values !== draft.values, revision: stored },
      };
    });
    return stored;
  };

  return {
    draftOf,
    edit: (request, fieldCode, value) => {
      setDrafts((current) => {
        const draft = current[request.requestId] ?? draftOf(request);
        return {
          ...current,
          [request.requestId]: {
            ...draft,
            dirty: true,
            values: { ...draft.values, [fieldCode]: value },
          },
        };
      });
    },
    persist,
    saveQuietly: async (request) => {
      const draft = draftOf(request);
      const malformed = Object.values(
        responseFieldCodes(monthlyFields(request), draft.values, false),
      ).some((code) => code !== null);
      if (!draft.dirty || malformed) {
        return;
      }
      try {
        await persist(request);
      } catch {
        // Still typed here and marked unsaved on its row; saving again retries.
      }
    },
    forgetRevision: (requestId) => {
      setDrafts((current) => {
        const draft = current[requestId];
        return draft === undefined
          ? current
          : { ...current, [requestId]: { ...draft, revision: null } };
      });
    },
  };
}

function WindowDialog({
  open,
  activeId,
  drafts,
  onChoose,
  onNext,
  onClose,
}: {
  open: boolean;
  activeId: string | null;
  drafts: LocalDrafts;
  /** Another project chosen: what is typed in this one is saved first. */
  onChoose: (request: MyUpdateRequest) => void;
  /** After a submit, which stored and froze what was typed. */
  onNext: (request: MyUpdateRequest | null) => void;
  onClose: () => void;
}): ReactElement {
  const { t } = useI18n();
  const { page, submitted } = useMyUpdates();
  const counted = useCounted();
  const monthName = useMonthName();
  const dayAndMonth = useDayAndMonth();
  const items = page?.items ?? [];
  const earliest = page?.earliestDueDate ?? null;
  const active = items.find((item) => item.requestId === activeId) ?? null;
  const total = items.length + submitted.length;

  return (
    <Dialog
      open={open}
      wide
      title={
        earliest === null
          ? t('externalParticipation.monthly.window.titleGeneric')
          : t('externalParticipation.monthly.window.title', { month: monthName(earliest) })
      }
      onClose={onClose}
    >
      <div className="monthly-window">
        {items.length > 0 && (
          <p className="dialog__subtitle">
            {earliest === null
              ? t('externalParticipation.monthly.window.summaryNoDate', {
                  projects: counted('projects', items.length),
                })
              : t('externalParticipation.monthly.window.summary', {
                  projects: counted('projects', items.length),
                  date: dayAndMonth(earliest),
                })}
          </p>
        )}
        <div className="monthly-window__progress">
          <span id="monthly-progress">
            {t('externalParticipation.monthly.window.progress', {
              done: submitted.length,
              total,
            })}
          </span>
          <span
            className="widget__bar-track"
            role="progressbar"
            aria-labelledby="monthly-progress"
            aria-valuemin={0}
            aria-valuemax={total}
            aria-valuenow={submitted.length}
          >
            <span
              className="widget__bar"
              style={{
                inlineSize: `${String(total === 0 ? 0 : (submitted.length / total) * 100)}%`,
              }}
            />
          </span>
        </div>
        <div className="monthly-window__panes">
          <ul className="monthly-window__list">
            {items.map((request) => (
              <li key={request.requestId}>
                <ProjectRow
                  request={request}
                  draft={drafts.draftOf(request)}
                  current={request.requestId === activeId}
                  onClick={() => {
                    onChoose(request);
                  }}
                />
              </li>
            ))}
            {submitted.map((request) => (
              <li key={`submitted-${request.requestId}`}>
                <div className="monthly-window__row monthly-window__row--done">
                  <ProjectName request={request} />
                  <StatusBadge
                    label={t('externalParticipation.monthly.state.submitted')}
                    tone="positive"
                  />
                </div>
              </li>
            ))}
          </ul>
          <div className="monthly-window__pane">
            {active === null ? (
              <p className="form__note">
                {items.length === 0
                  ? t('externalParticipation.monthly.window.allDone')
                  : t('externalParticipation.monthly.window.choose')}
              </p>
            ) : (
              <ProjectForm
                key={active.requestId}
                request={active}
                drafts={drafts}
                onSubmitted={() => {
                  const next = items.find((item) => item.requestId !== active.requestId) ?? null;
                  myUpdatesStore.markSubmitted(active);
                  void myUpdatesStore.refresh();
                  onNext(next);
                }}
              />
            )}
          </div>
        </div>
        <p className="form__note">{t('externalParticipation.monthly.window.closeNote')}</p>
        <div className="form__actions">
          <button type="button" className="button" onClick={onClose}>
            {t('externalParticipation.monthly.window.close')}
          </button>
        </div>
      </div>
    </Dialog>
  );
}

function ProjectName({ request }: { request: MyUpdateRequest }): ReactElement {
  return (
    <span className="monthly-window__name">
      <span lang={languageTag(request.projectTitle.language)} dir="auto">
        {request.projectTitle.text}
      </span>
      {(request.formalProjectId ?? request.legacyReference) !== null && (
        <span className="cell__aside" dir="ltr">
          {request.formalProjectId ?? request.legacyReference}
        </span>
      )}
    </span>
  );
}

/** A due project: its name, due date and where its answer stands, with what is typed but not yet stored. */
function ProjectRow({
  request,
  draft,
  current,
  onClick,
}: {
  request: MyUpdateRequest;
  draft: LocalDraft;
  current: boolean;
  onClick: () => void;
}): ReactElement {
  const { t } = useI18n();
  const dayAndMonth = useDayAndMonth();
  return (
    <button
      type="button"
      className={
        current ? 'monthly-window__row monthly-window__row--current' : 'monthly-window__row'
      }
      aria-current={current ? 'true' : undefined}
      onClick={onClick}
    >
      <ProjectName request={request} />
      <span className="monthly-window__state">
        {request.dueDate !== null && (
          <span className="cell__aside">
            {t('externalParticipation.monthly.window.due', { date: dayAndMonth(request.dueDate) })}
          </span>
        )}
        {request.overdue && (
          <StatusBadge label={t('externalParticipation.monthly.overdue')} tone="negative" />
        )}
        {draft.dirty ? (
          <StatusBadge label={t('externalParticipation.monthly.state.unsaved')} tone="warning" />
        ) : request.previouslyReturned ? (
          <StatusBadge label={t('externalParticipation.monthly.state.returned')} tone="warning" />
        ) : draft.revision !== null ? (
          <StatusBadge label={t('externalParticipation.monthly.state.draft')} tone="info" />
        ) : (
          <StatusBadge label={t('externalParticipation.monthly.state.notStarted')} tone="neutral" />
        )}
      </span>
    </button>
  );
}

function ProjectForm({
  request,
  drafts,
  onSubmitted,
}: {
  request: MyUpdateRequest;
  drafts: LocalDrafts;
  onSubmitted: () => void;
}): ReactElement {
  const { t, language } = useI18n();
  const label = useFieldLabel();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(participationProblemMessage);
  const [serverCodes, setServerCodes] = useState<FieldCodes>({});
  const [saved, setSaved] = useState(false);
  const draft = drafts.draftOf(request);
  const definitions = monthlyFields(request);
  const stages = request.stages ?? [];
  const stageFigures = stages.length > 0 ? monthlyStageFigures(request, draft.values) : null;
  const clientCodes = responseFieldCodes(definitions, draft.values, true);
  const codes = Object.fromEntries(
    definitions.map(({ fieldCode }) => [
      fieldCode,
      clientCodes[fieldCode] ?? serverCodes[fieldCode] ?? null,
    ]),
  );
  const describe = (field: string, code: string, translate: Translate) => {
    const definition = definitions.find((candidate) => candidate.fieldCode === field);
    return code === 'OUT_OF_RANGE' && definition !== undefined
      ? translate('externalParticipation.fieldErrors.answerOutOfRange', {
          min: definition.minimum ?? '—',
          max: definition.maximum ?? '—',
        })
      : responseFieldMessage(code, translate);
  };
  const fields = useFieldErrors(codes, formRef, describe);

  const refused = (error: unknown) => {
    setServerCodes(responseIssuesOf(error, []));
    if (isStale(error)) {
      drafts.forgetRevision(request.requestId);
      void myUpdatesStore.refresh();
    }
  };

  const saveDraft = async (event: SyntheticEvent) => {
    event.preventDefault();
    setSaved(false);
    // A draft may be incomplete: only a value of the wrong shape stops it.
    const shapeOnly = responseFieldCodes(definitions, draft.values, false);
    if (Object.values(shapeOnly).some((code) => code !== null)) {
      fields.attempt();
      return;
    }
    const result = await save.run(() => drafts.persist(request));
    if (result.ok) {
      setSaved(true);
    } else {
      refused(result.error);
    }
  };

  const submit = async () => {
    setSaved(false);
    if (!fields.attempt()) {
      return;
    }
    const result = await save.run(async () => {
      const revision = await drafts.persist(request);
      await contributionsApi.submit(revision.id, revision.etag);
    });
    if (result.ok) {
      onSubmitted();
    } else {
      refused(result.error);
    }
  };

  return (
    <form
      ref={formRef}
      className="form"
      noValidate
      aria-label={request.projectTitle.text}
      onSubmit={(event) => void saveDraft(event)}
    >
      <h3 className="monthly-window__heading" dir="auto">
        {request.projectTitle.text}
      </h3>
      <FormAlert message={fields.summary ?? save.formError} />
      {request.previouslyReturned && request.returnReason !== null && (
        <div className="notice notice--warning" role="status">
          <strong>{t('externalParticipation.monthly.form.returned')}</strong>{' '}
          <span lang={languageTag(request.returnReason.language)} dir="auto">
            {request.returnReason.text}
          </span>
        </div>
      )}
      {request.instructions.text !== '' && (
        <p className="form__note" lang={languageTag(request.instructions.language)} dir="auto">
          {request.instructions.text}
        </p>
      )}
      <p className="form__note">
        {request.plannedProgressPercent === null
          ? t('externalParticipation.monthly.form.plannedUnknown')
          : t('externalParticipation.monthly.form.planned', {
              percent: request.plannedProgressPercent,
            })}
      </p>
      {stageFigures !== null && (
        <p className="form__note" role="status" aria-live="polite">
          {t('externalParticipation.monthly.form.stageOverall', {
            actual: stageFigures.actual === null ? '—' : `${oneDecimal(stageFigures.actual)}%`,
            planned: stageFigures.planned === null ? '—' : `${oneDecimal(stageFigures.planned)}%`,
          })}
        </p>
      )}
      {definitions.map((definition) => {
        const change = (value: string) => {
          drafts.edit(request, definition.fieldCode, value);
          setServerCodes({});
          setSaved(false);
          fields.clearServer();
        };
        // The current stage is chosen from the plan, each stage shown with its planned % today.
        return definition.fieldCode === CURRENT_STAGE_FIELD ? (
          <SelectField
            key={definition.fieldCode}
            label={label(definition.fieldCode)}
            name={definition.fieldCode}
            required
            value={draft.values[definition.fieldCode] ?? ''}
            placeholder={t('common.form.choose')}
            options={stages.map((stage) => ({
              value: stage.stageCode,
              label: t('externalParticipation.monthly.form.stageOption', {
                stage: stage.name[language],
                planned:
                  stage.plannedPercent === null
                    ? '—'
                    : `${oneDecimal(Number(stage.plannedPercent))}%`,
              }),
            }))}
            error={fields.errorOf(definition.fieldCode)}
            onChange={change}
          />
        ) : (
          <ResponseInput
            key={definition.fieldCode}
            definition={definition}
            label={label(definition.fieldCode)}
            value={draft.values[definition.fieldCode] ?? ''}
            error={fields.errorOf(definition.fieldCode)}
            onChange={change}
          />
        );
      })}
      {saved && (
        <p className="form__note" role="status">
          {t('externalParticipation.monthly.form.saved')}
        </p>
      )}
      <div className="form__actions">
        <button type="submit" className="button" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('externalParticipation.actions.saveDraft')}
        </button>
        <button
          type="button"
          className="button button--primary"
          disabled={save.saving}
          onClick={() => void submit()}
        >
          {t('externalParticipation.monthly.form.submit')}
        </button>
      </div>
    </form>
  );
}
