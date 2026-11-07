import { type ReactElement, type SyntheticEvent, useCallback, useState } from 'react';
import { useNavigate } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useSaveAction } from '@/features/identity-access/forms.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { isInternal } from '@/features/projects/access.ts';
import { projectsApi } from '@/features/projects/api/projectsApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { ProjectStatusBadge } from '@/features/projects/components/ProjectStatusBadge.tsx';
import { languageTag } from '@/features/projects/presentation.ts';
import { ProjectFilters } from '@/features/projects/register/ProjectFilters.tsx';
import { useProjectFilterValues } from '@/features/projects/register/projectFilterValues.ts';
import { type ProjectLookups, useProjectLookups } from '@/features/projects/useProjectLookups.ts';
import { type AssigneeValue, assigneeOf, assigneeValue, OTHER } from '@/features/tasks/assignee.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useListParams } from '@/shared/api/useListParams.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import {
  CheckboxField,
  ReadOnlyField,
  SelectField,
  TextAreaField,
  TextField,
} from '@/shared/ui/FormFields.tsx';
import { PageHeader, Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { updateRequestBatchesApi } from '../api/monthlyUpdatesApi.ts';
import { PersonField } from '../components/PersonField.tsx';
import { useCounted } from '../monthly/counts.ts';
import { NEW_UPDATE_BATCH_PATH, updateBatchPath } from '../paths.ts';
import { participationProblemMessage, requestFieldMessage } from '../problems.ts';

import {
  batchIssuesOf,
  batchItemsOf,
  entityCountOf,
  ineligibilityOf,
  isFlagged,
  type RowIssue,
  rowIssueMessage,
} from './batchRules.ts';

/** `UpdateRequestBatchRequest.MaxInstructions`. */
const INSTRUCTIONS_LENGTH = 4000;
const REPEAT_DAYS = Array.from({ length: 28 }, (_, index) => String(index + 1));

interface RequestValues {
  dueDate: string;
  repeatMonthly: boolean;
  repeatDay: string;
  instructions: string;
  reviewer: AssigneeValue;
}

type Step = 1 | 2 | 3;

const STEPS = [
  { step: 1, label: 'externalParticipation.batch.step1.title' },
  { step: 2, label: 'externalParticipation.batch.step2.title' },
  { step: 3, label: 'externalParticipation.batch.step3.title' },
] as const;

/** The form's own codes for step 2; the API checks all of it again. */
function requestCodes(values: RequestValues): Record<string, string | null> {
  return {
    dueDate:
      values.dueDate === '' ? 'REQUIRED' : values.dueDate < todayUtc() ? 'NOT_ALLOWED' : null,
    instructions: values.instructions.trim() === '' ? 'REQUIRED' : null,
    reviewerUserId:
      assigneeOf(values.reviewer).code ??
      (assigneeOf(values.reviewer).userId === null ? 'REQUIRED' : null),
  };
}

function initialValues(user: SessionUser): RequestValues {
  return {
    dueDate: '',
    repeatMonthly: false,
    repeatDay: '1',
    instructions: '',
    reviewer: assigneeValue(isInternal(user) ? user.id : null),
  };
}

/**
 * The three-step "طلب تحديث" (the 5 Nov 2026 review §2.2): choose the projects, set the request and who answers each, review and send. The
 * API takes the batch as a whole: a refusal names each project it cannot send to, and those rows come back flagged at step 2.
 */
export function NewBatchPage(): ReactElement | null {
  const { t, language } = useI18n();
  const { session } = useSession();
  const navigate = useNavigate();
  const lookups = useProjectLookups();
  const save = useSaveAction(participationProblemMessage);
  const [step, setStep] = useState<Step>(1);
  const [selected, setSelected] = useState<ReadonlyMap<string, ProjectSummary>>(new Map());
  const [values, setValues] = useState<RequestValues | null>(null);
  const [responders, setResponders] = useState<Record<string, AssigneeValue>>({});
  const [rowIssues, setRowIssues] = useState<Record<string, RowIssue>>({});
  const [formIssues, setFormIssues] = useState<Record<string, string>>({});
  const [attempted, setAttempted] = useState(false);

  if (session === null) {
    return null;
  }
  const request = values ?? initialValues(session.user);
  const projects = [...selected.values()];
  const flagged = projects.filter((project) =>
    isFlagged(project, responders[project.id], rowIssues[project.id]),
  );
  const codes = requestCodes(request);
  const ready = Object.values(codes).every((code) => code === null);

  const set = (changes: Partial<RequestValues>) => {
    setValues({ ...request, ...changes });
    setFormIssues({});
  };

  const send = async (event: SyntheticEvent) => {
    event.preventDefault();
    const items = batchItemsOf(projects, responders);
    const result = await save.run(() =>
      updateRequestBatchesApi.send({
        items,
        dueDate: request.dueDate,
        repeatMonthly: request.repeatMonthly,
        repeatDay: request.repeatMonthly ? Number(request.repeatDay) : null,
        instructions: request.instructions.trim(),
        instructionsLanguage: language,
        reviewerUserId: assigneeOf(request.reviewer).userId ?? '',
      }),
    );
    if (result.ok) {
      void navigate(updateBatchPath(result.value.batch.id), { state: { notice: 'sent' } });
      return;
    }
    const issues = batchIssuesOf(result.error, items);
    if (Object.keys(issues.rows).length > 0 || Object.keys(issues.form).length > 0) {
      setRowIssues(issues.rows);
      setFormIssues(issues.form);
      setAttempted(true);
      setStep(2);
    }
  };

  return (
    <>
      <PageHeader
        title={t('externalParticipation.batch.new')}
        description={t('externalParticipation.batch.description')}
      />
      <ol className="steps">
        {STEPS.map((candidate) => (
          <li
            key={candidate.step}
            className={candidate.step === step ? 'steps__step steps__step--current' : 'steps__step'}
            aria-current={candidate.step === step ? 'step' : undefined}
          >
            {t(candidate.label)}
          </li>
        ))}
      </ol>
      {step === 1 && (
        <ChooseProjects
          lookups={lookups}
          selected={selected}
          onChange={setSelected}
          onNext={() => {
            setStep(2);
          }}
        />
      )}
      {step === 2 && (
        <SetRequest
          user={session.user}
          lookups={lookups}
          projects={projects}
          values={request}
          attempted={attempted}
          codes={codes}
          formIssues={formIssues}
          responders={responders}
          rowIssues={rowIssues}
          flaggedCount={flagged.length}
          refusal={save.formError}
          onSet={set}
          onResponder={(projectIds, value) => {
            setResponders((current) => ({
              ...current,
              ...Object.fromEntries(projectIds.map((id) => [id, value])),
            }));
            setRowIssues((current) =>
              Object.fromEntries(
                Object.entries(current).filter(
                  ([id, issue]) => !projectIds.includes(id) || issue.field === 'projectId',
                ),
              ),
            );
          }}
          onRemove={(projectId) => {
            const next = new Map(selected);
            next.delete(projectId);
            setSelected(next);
            setRowIssues(({ [projectId]: _removed, ...rest }) => rest);
          }}
          onBack={() => {
            setStep(1);
          }}
          onNext={() => {
            setAttempted(true);
            if (ready && flagged.length === 0 && projects.length > 0) {
              setStep(3);
            }
          }}
        />
      )}
      {step === 3 && (
        <ReviewAndSend
          projects={projects}
          values={request}
          reviewerId={assigneeOf(request.reviewer).userId}
          blocked={flagged.length > 0}
          saving={save.saving}
          refusal={save.formError}
          onBack={() => {
            setStep(2);
          }}
          onSend={(event) => void send(event)}
        />
      )}
    </>
  );
}

function ProjectName({ project }: { project: ProjectSummary }): ReactElement {
  return (
    <>
      <span lang={languageTag(project.title.language)} dir="auto">
        {project.title.text}
      </span>
      <span className="cell__aside" dir="ltr">
        {project.formalProjectId ?? project.legacyReference}
      </span>
    </>
  );
}

function SelectionCounter({ projects }: { projects: readonly ProjectSummary[] }): ReactElement {
  const { t } = useI18n();
  const counted = useCounted();
  return (
    <p className="steps__counter" role="status">
      {t('externalParticipation.batch.step1.selected', {
        projects: counted('projects', projects.length),
        entities: counted('entities', entityCountOf(projects)),
      })}
    </p>
  );
}

function ChooseProjects({
  lookups,
  selected,
  onChange,
  onNext,
}: {
  lookups: ProjectLookups;
  selected: ReadonlyMap<string, ProjectSummary>;
  onChange: (selected: ReadonlyMap<string, ProjectSummary>) => void;
  onNext: () => void;
}): ReactElement {
  const { t } = useI18n();
  const { page, setFilter, setFilters, goToPage } = useListParams();
  const { q, status, departmentId, externalEntityId, itemFilters, itemKey } =
    useProjectFilterValues();
  const save = useSaveAction(participationProblemMessage);
  const [excluded, setExcluded] = useState<number | null>(null);
  const query = {
    q: q === '' ? undefined : q,
    status,
    departmentId,
    externalEntityId,
    ...itemFilters,
  };
  const load = useCallback(
    (signal: AbortSignal) => projectsApi.list({ ...query, page, pageSize: PAGE_SIZE }, signal),
    // itemKey stands for itemFilters, rebuilt on every render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [q, status, departmentId, externalEntityId, itemKey, page],
  );
  const projects = useApiResource(load);

  const toggle = (project: ProjectSummary, on: boolean) => {
    const next = new Map(selected);
    if (on) {
      next.set(project.id, project);
    } else {
      next.delete(project.id);
    }
    onChange(next);
  };

  /** Everything matching the filter across all pages, not just the page showing. */
  const selectAll = async () => {
    const result = await save.run(() => projectsApi.listAll(query));
    if (result.ok) {
      const next = new Map(selected);
      const eligible = result.value.filter((project) => ineligibilityOf(project) === null);
      eligible.forEach((project) => next.set(project.id, project));
      onChange(next);
      setExcluded(result.value.length - eligible.length);
    }
  };

  return (
    <section>
      <h2>{t('externalParticipation.batch.step1.title')}</h2>
      <ProjectFilters
        key={q}
        q={q}
        values={{ status, departmentId, externalEntityId, ...itemFilters }}
        organizationFilters
        entityFilter
        lookups={lookups}
        basePath={NEW_UPDATE_BATCH_PATH}
        onSearch={(text) => {
          setFilter('q', text);
        }}
        onFilter={setFilters}
      />
      <FormAlert message={save.formError} />
      <p className="form__note">{t('externalParticipation.batch.step1.rule')}</p>
      <div className="steps__toolbar">
        <SelectionCounter projects={[...selected.values()]} />
        <button
          type="button"
          className="button"
          disabled={save.saving}
          onClick={() => void selectAll()}
        >
          {save.saving
            ? t('common.states.saving')
            : t('externalParticipation.batch.step1.selectAll')}
        </button>
        {selected.size > 0 && (
          <button
            type="button"
            className="button button--quiet"
            onClick={() => {
              setExcluded(null);
              onChange(new Map());
            }}
          >
            {t('externalParticipation.batch.step1.clear')}
          </button>
        )}
      </div>
      {excluded !== null && excluded > 0 && (
        <p className="form__note">
          {t('externalParticipation.batch.step1.excluded', { count: excluded })}
        </p>
      )}
      {projects.loading && <LoadingState />}
      {projects.error !== null && (
        <ErrorState
          error={projects.error}
          message={participationProblemMessage(projects.error, t)}
          onRetry={projects.reload}
        />
      )}
      {projects.data !== undefined &&
        (projects.data.items.length === 0 ? (
          <EmptyState title={t('projects.list.emptyFiltered')} />
        ) : (
          <>
            <TableContainer caption={t('externalParticipation.batch.step1.title')}>
              <thead>
                <tr>
                  <th scope="col">{t('externalParticipation.batch.step1.choose')}</th>
                  <th scope="col">{t('projects.fields.title')}</th>
                  <th scope="col">{t('projects.fields.status')}</th>
                  <th scope="col">{t('externalParticipation.fields.entity')}</th>
                </tr>
              </thead>
              <tbody>
                {projects.data.items.map((project) => {
                  const refused = ineligibilityOf(project);
                  return (
                    <tr key={project.id}>
                      <td>
                        <input
                          type="checkbox"
                          aria-label={project.title.text}
                          checked={selected.has(project.id)}
                          disabled={refused !== null}
                          onChange={(event) => {
                            toggle(project, event.target.checked);
                          }}
                        />
                      </td>
                      <td>
                        <ProjectName project={project} />
                        {refused !== null && (
                          <span className="cell__aside">
                            {t(`externalParticipation.batch.ineligible.${refused}`)}
                          </span>
                        )}
                      </td>
                      <td>
                        <ProjectStatusBadge
                          status={project.status}
                          legacyIntakeDate={project.legacyIntakeDate}
                        />
                      </td>
                      <td>{lookups.entityName(project.externalEntityId) ?? '—'}</td>
                    </tr>
                  );
                })}
              </tbody>
            </TableContainer>
            <Pagination
              page={projects.data.page}
              pageSize={projects.data.pageSize}
              totalCount={projects.data.totalCount}
              onPageChange={goToPage}
            />
          </>
        ))}
      <div className="form__actions">
        <button
          type="button"
          className="button button--primary"
          disabled={selected.size === 0}
          onClick={onNext}
        >
          {t('common.actions.next')}
        </button>
      </div>
    </section>
  );
}

function SetRequest({
  user,
  lookups,
  projects,
  values,
  attempted,
  codes,
  formIssues,
  responders,
  rowIssues,
  flaggedCount,
  refusal,
  onSet,
  onResponder,
  onRemove,
  onBack,
  onNext,
}: {
  user: SessionUser;
  lookups: ProjectLookups;
  projects: ProjectSummary[];
  values: RequestValues;
  attempted: boolean;
  codes: Record<string, string | null>;
  formIssues: Record<string, string>;
  responders: Record<string, AssigneeValue>;
  rowIssues: Record<string, RowIssue>;
  flaggedCount: number;
  refusal: string | null;
  onSet: (changes: Partial<RequestValues>) => void;
  onResponder: (projectIds: string[], value: AssigneeValue) => void;
  onRemove: (projectId: string) => void;
  onBack: () => void;
  onNext: () => void;
}): ReactElement {
  const { t } = useI18n();
  const personName = usePersonNames(
    projects.flatMap((project) => [
      project.entityContactUserId ?? null,
      project.projectManagerUserId,
    ]),
  );
  const entityIds = [
    ...new Set(
      projects
        .filter((project) => (project.entityContactUserId ?? null) === null)
        .map((project) => project.externalEntityId)
        .filter((id): id is string => id !== null),
    ),
  ]
    .sort()
    .join(',');
  const loadPeople = useCallback(
    async (signal: AbortSignal) => {
      const ids = entityIds === '' ? [] : entityIds.split(',');
      const lists = await Promise.all(
        ids.map((id) => updateRequestBatchesApi.responders(id, signal)),
      );
      return Object.fromEntries(ids.map((id, index) => [id, lists[index] ?? []]));
    },
    [entityIds],
  );
  const entityPeople = useApiResource(loadPeople).data ?? {};
  const errorOf = (field: string): string | undefined => {
    const client = codes[field] ?? null;
    const code = (attempted ? client : null) ?? formIssues[field] ?? null;
    return code === null ? undefined : requestFieldMessage(field, code, t);
  };

  return (
    <section>
      <h2>{t('externalParticipation.batch.step2.title')}</h2>
      <FormAlert message={refusal} />
      <div className="form form--wide">
        <ReadOnlyField
          label={t('externalParticipation.batch.step2.type')}
          name="type"
          value={t('externalParticipation.schema.MONTHLY_UPDATE')}
          hint={t('externalParticipation.batch.step2.typeHint')}
        />
        <TextField
          label={t('externalParticipation.fields.dueDate')}
          name="dueDate"
          type="date"
          dir="ltr"
          required
          value={values.dueDate}
          error={errorOf('dueDate')}
          onChange={(dueDate) => {
            onSet({ dueDate });
          }}
        />
        <CheckboxField
          label={t('externalParticipation.batch.step2.repeat')}
          name="repeatMonthly"
          checked={values.repeatMonthly}
          onChange={(repeatMonthly) => {
            onSet({ repeatMonthly });
          }}
        />
        {values.repeatMonthly && (
          <SelectField
            label={t('externalParticipation.batch.step2.repeatDay')}
            name="repeatDay"
            required
            value={values.repeatDay}
            options={REPEAT_DAYS.map((day) => ({ value: day, label: day }))}
            error={errorOf('repeatDay')}
            onChange={(repeatDay) => {
              onSet({ repeatDay });
            }}
          />
        )}
        <TextAreaField
          label={t('externalParticipation.fields.instructions')}
          name="instructions"
          required
          maxLength={INSTRUCTIONS_LENGTH}
          value={values.instructions}
          error={errorOf('instructions')}
          onChange={(instructions) => {
            onSet({ instructions });
          }}
        />
        <PersonField
          label={t('externalParticipation.fields.reviewer')}
          name="reviewerUserId"
          userType="INTERNAL"
          user={user}
          value={values.reviewer}
          allowNone={false}
          hint={t('externalParticipation.batch.step2.reviewerHint')}
          error={errorOf('reviewerUserId')}
          onChange={(reviewer) => {
            onSet({ reviewer });
          }}
        />
      </div>
      <h3>{t('externalParticipation.batch.step2.responders')}</h3>
      {flaggedCount > 0 && (
        <p className="notice notice--warning" role="status">
          {t('externalParticipation.batch.step2.flagged', { count: flaggedCount })}
        </p>
      )}
      <TableContainer caption={t('externalParticipation.batch.step2.responders')}>
        <thead>
          <tr>
            <th scope="col">{t('projects.fields.title')}</th>
            <th scope="col">{t('externalParticipation.fields.entity')}</th>
            <th scope="col">{t('externalParticipation.batch.step2.responder')}</th>
            <th scope="col">
              <span className="visually-hidden">
                {t('externalParticipation.batch.step2.remove')}
              </span>
            </th>
          </tr>
        </thead>
        <tbody>
          {projects.map((project) => {
            const issue = rowIssues[project.id];
            const chosen = responders[project.id];
            const named = chosen === undefined ? null : assigneeOf(chosen).userId;
            const needsPicker =
              (project.entityContactUserId ?? null) === null ||
              issue?.field === 'responsibleUserId' ||
              chosen !== undefined;
            const sameEntityFlagged = projects.filter(
              (other) =>
                other.id !== project.id &&
                other.externalEntityId === project.externalEntityId &&
                isFlagged(other, responders[other.id], rowIssues[other.id]),
            );
            const flagged = isFlagged(project, chosen, issue);
            return (
              <tr key={project.id} data-flagged={flagged ? 'true' : undefined}>
                <td>
                  <ProjectName project={project} />
                  {issue?.field === 'projectId' && (
                    <p className="field__error">{t(rowIssueMessage(issue))}</p>
                  )}
                </td>
                <td>{lookups.entityName(project.externalEntityId) ?? '—'}</td>
                <td>
                  {needsPicker ? (
                    <>
                      {flagged && (
                        <StatusBadge
                          label={t('externalParticipation.batch.step2.noResponder')}
                          tone="negative"
                        />
                      )}
                      <PersonField
                        label={t('externalParticipation.batch.step2.responderFor', {
                          project: project.title.text,
                        })}
                        name="responsibleUserId"
                        userType="ANY"
                        user={user}
                        value={chosen ?? { choice: OTHER, found: null }}
                        suggestions={[
                          ...(entityPeople[project.externalEntityId ?? ''] ?? []).map((person) => ({
                            id: person.userId,
                            label: person.displayName,
                          })),
                          ...(project.projectManagerUserId === null
                            ? []
                            : [
                                {
                                  id: project.projectManagerUserId,
                                  label: t(
                                    'externalParticipation.batch.step2.projectManagerOption',
                                    {
                                      name: personName(project.projectManagerUserId),
                                    },
                                  ),
                                },
                              ]),
                        ]}
                        allowNone={false}
                        hint={t('externalParticipation.batch.step2.responderHint')}
                        error={
                          issue?.field === 'responsibleUserId'
                            ? t(rowIssueMessage(issue))
                            : undefined
                        }
                        onChange={(value) => {
                          onResponder([project.id], value);
                        }}
                      />
                      {named !== null && sameEntityFlagged.length > 0 && chosen !== undefined && (
                        <button
                          type="button"
                          className="button button--quiet"
                          onClick={() => {
                            onResponder(
                              sameEntityFlagged.map((other) => other.id),
                              chosen,
                            );
                          }}
                        >
                          {t('externalParticipation.batch.step2.applyToEntity', {
                            count: sameEntityFlagged.length,
                          })}
                        </button>
                      )}
                    </>
                  ) : (
                    personName(project.entityContactUserId ?? null)
                  )}
                </td>
                <td>
                  <button
                    type="button"
                    className="button button--quiet"
                    aria-label={`${t('externalParticipation.batch.step2.remove')}: ${project.title.text}`}
                    onClick={() => {
                      onRemove(project.id);
                    }}
                  >
                    {t('externalParticipation.batch.step2.remove')}
                  </button>
                </td>
              </tr>
            );
          })}
        </tbody>
      </TableContainer>
      <div className="form__actions">
        <button type="button" className="button" onClick={onBack}>
          {t('common.actions.back')}
        </button>
        <button
          type="button"
          className="button button--primary"
          disabled={flaggedCount > 0 || projects.length === 0}
          onClick={onNext}
        >
          {t('common.actions.next')}
        </button>
      </div>
    </section>
  );
}

function ReviewAndSend({
  projects,
  values,
  reviewerId,
  blocked,
  saving,
  refusal,
  onBack,
  onSend,
}: {
  projects: ProjectSummary[];
  values: RequestValues;
  reviewerId: string | null;
  blocked: boolean;
  saving: boolean;
  refusal: string | null;
  onBack: () => void;
  onSend: (event: SyntheticEvent) => void;
}): ReactElement {
  const { t } = useI18n();
  const counted = useCounted();
  const personName = usePersonNames([reviewerId]);
  const requests = counted('requests', projects.length);
  const entities = counted('entities', entityCountOf(projects));
  return (
    <section>
      <h2>{t('externalParticipation.batch.step3.title')}</h2>
      <FormAlert message={refusal} />
      <p className="steps__counter" data-field="batchSummary">
        {values.repeatMonthly
          ? t('externalParticipation.batch.step3.summaryRepeating', {
              requests,
              entities,
              day: values.repeatDay,
            })
          : t('externalParticipation.batch.step3.summaryOnce', { requests, entities })}
      </p>
      <dl className="details">
        <Detail term={t('externalParticipation.fields.dueDate')}>
          <span dir="ltr">{values.dueDate}</span>
        </Detail>
        <Detail term={t('externalParticipation.fields.reviewer')}>{personName(reviewerId)}</Detail>
        <Detail term={t('externalParticipation.fields.instructions')}>
          <span className="pre-line" dir="auto">
            {values.instructions}
          </span>
        </Detail>
      </dl>
      <form onSubmit={onSend}>
        <div className="form__actions">
          <button type="button" className="button" onClick={onBack}>
            {t('common.actions.back')}
          </button>
          <button
            type="submit"
            className="button button--primary"
            disabled={saving || blocked || projects.length === 0}
          >
            {saving ? t('common.states.saving') : t('externalParticipation.batch.step3.send')}
          </button>
        </div>
      </form>
    </section>
  );
}
