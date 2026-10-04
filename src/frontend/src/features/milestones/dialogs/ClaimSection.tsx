import {
  type ReactElement,
  type SyntheticEvent,
  useCallback,
  useId,
  useRef,
  useState,
} from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useSaveAction } from '@/features/identity-access/forms.ts';
import { languageTag } from '@/features/projects/presentation.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { TextAreaField, TextField } from '@/shared/ui/FormFields.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { milestonesApi } from '../api/milestonesApi.ts';
import {
  type MilestoneAchievementDetail,
  type MilestoneEvidenceDetail,
  type ProjectMilestoneDetail,
} from '../api/types.ts';
import { EvidenceList, EvidencePolicyLabel } from '../components/EvidencePanel.tsx';
import { RevisionStatusBadge } from '../components/MilestoneBadges.tsx';
import {
  checkClaim,
  type ClaimKind,
  type ClaimValues,
  evidenceAllowsSubmission,
  type EvidenceRule,
  evidenceRule,
  liveEvidence,
  toClaimRequest,
} from '../milestoneRules.ts';
import { isRefusal, isStale, milestoneFieldMessage, milestoneProblemMessage } from '../problems.ts';
import { type MilestoneLookups } from '../useMilestoneData.ts';

/** Something was saved (or overtaken): MOD-019 says so and reads the milestone and its revisions again. */
export type ClaimChanged = (message: TranslationKey, tone?: 'success' | 'warning') => void;

const CLAIM_ACTIONS: Record<ClaimKind, TranslationKey> = {
  first: 'milestones.claim.start.first',
  afterReturn: 'milestones.claim.start.afterReturn',
  correction: 'milestones.claim.start.correction',
};

/** The claim's two inputs, shared by a new claim and a draft. */
function ClaimFields({
  values,
  onChange,
  errorOf,
}: {
  values: ClaimValues;
  onChange: (field: keyof ClaimValues, value: string) => void;
  errorOf: (field: string) => string | undefined;
}): ReactElement {
  const { t } = useI18n();
  return (
    <>
      <TextField
        label={t('milestones.fields.claimedDate')}
        name="claimedAchievementDate"
        type="date"
        required
        hint={t('milestones.claim.dateHint')}
        value={values.claimedAchievementDate}
        onChange={(value) => {
          onChange('claimedAchievementDate', value);
        }}
        error={errorOf('claimedAchievementDate')}
      />
      <TextAreaField
        label={t('milestones.fields.narrative')}
        name="narrative"
        rows={3}
        maxLength={TEXT_LENGTH}
        hint={t('milestones.claim.narrativeHint')}
        value={values.narrative}
        onChange={(value) => {
          onChange('narrative', value);
        }}
        error={errorOf('narrative')}
      />
    </>
  );
}

/**
 * The next claim of a milestone — a first claim, a claim after a return, or a correction of an accepted achievement —
 * opened as a DRAFT revision (TASK-050 D-4). A correction leaves the accepted revision current until it is accepted.
 */
export function NewClaim({
  milestone,
  kind,
  onChanged,
}: {
  milestone: ProjectMilestoneDetail;
  kind: ClaimKind;
  onChanged: ClaimChanged;
}): ReactElement {
  const { t, language } = useI18n();
  const id = useId();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(milestoneProblemMessage);
  const [open, setOpen] = useState(false);
  const [values, setValues] = useState<ClaimValues>({ claimedAchievementDate: '', narrative: '' });
  const fields = useFieldErrors(checkClaim(values, todayUtc()), formRef, milestoneFieldMessage);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const result = await save.run(() =>
      milestonesApi.createAchievement({
        ...toClaimRequest(values, language, null),
        projectMilestoneId: milestone.id,
      }),
    );
    if (result.ok) {
      onChanged('milestones.done.claimOpened');
    } else if (isRefusal(result.error, 'MILESTONE_ACHIEVEMENT_OPEN')) {
      // Another revision was opened meanwhile (or this request was retried): read it instead.
      onChanged('milestones.done.stale', 'warning');
    } else {
      fields.showServer(result.error);
    }
  };

  return (
    <>
      {kind === 'correction' && (
        <p className="form__note">{t('milestones.claim.correctionNote')}</p>
      )}
      <button
        type="button"
        className="button button--primary"
        aria-expanded={open}
        aria-controls={`${id}-form`}
        onClick={() => {
          setOpen(!open);
        }}
      >
        {t(CLAIM_ACTIONS[kind])}
      </button>
      {open && (
        <form
          id={`${id}-form`}
          ref={formRef}
          className="form task-detail__confirm"
          noValidate
          onSubmit={(event) => void submit(event)}
        >
          <FormAlert message={fields.summary ?? save.formError} />
          <ClaimFields
            values={values}
            onChange={(field, value) => {
              setValues((current) => ({ ...current, [field]: value }));
              fields.clearServer();
            }}
            errorOf={fields.errorOf}
          />
          <p className="form__note">{t('milestones.claim.evidenceNext')}</p>
          <div className="form__actions">
            <button type="submit" className="button button--primary" disabled={save.saving}>
              {save.saving ? t('common.states.saving') : t('milestones.claim.create')}
            </button>
            <button
              type="button"
              className="button"
              disabled={save.saving}
              onClick={() => {
                setOpen(false);
              }}
            >
              {t('common.actions.cancel')}
            </button>
          </div>
        </form>
      )}
    </>
  );
}

/** An open revision with the ETag its commands send, and its evidence; `undetermined` for an ambiguous policy. */
interface OpenClaim {
  revision: ApiResponse<MilestoneAchievementDetail>;
  evidence: MilestoneEvidenceDetail | null;
}

async function readOpenClaim(id: string, signal: AbortSignal): Promise<OpenClaim> {
  const [revision, evidence] = await Promise.all([
    milestonesApi.achievement(id, signal),
    milestonesApi.evidence(id, signal).then(
      (response) => response.data,
      (error: unknown) => {
        // An EVIDENCE_POLICY the API cannot read unambiguously fails closed (TASK-050 F-5): no evidence detail then.
        if (isRefusal(error, 'CONFIGURATION_MISSING')) {
          return null;
        }
        throw error;
      },
    ),
  ]);
  return { revision, evidence };
}

function ruleOf(evidence: MilestoneEvidenceDetail | null): EvidenceRule {
  return evidence === null ? { kind: 'undetermined' } : evidenceRule(evidence);
}

interface OpenRevisionProps {
  revisionId: string;
  /** The person may change the DRAFT (MILESTONE_SUBMIT on an ACTIVE project). */
  editable: boolean;
  lookups: MilestoneLookups;
  /** MOD-054 as evidence of this revision, with its ETag and the types the policy makes mandatory. */
  onAttach: (revision: ApiResponse<MilestoneAchievementDetail>, mandatory: string[]) => void;
  /** MOD-050, to upload a document to attach once it is clean. */
  onUpload: () => void;
  onChanged: ClaimChanged;
}

/** The open revision, DRAFT or SUBMITTED, read on its own for its ETag and its evidence. */
export function OpenRevision(props: OpenRevisionProps): ReactElement {
  const { t } = useI18n();
  const { revisionId } = props;
  const load = useCallback(
    (signal: AbortSignal) => readOpenClaim(revisionId, signal),
    [revisionId],
  );
  const claim = useApiResource(load, { keepWhileReloading: true });
  if (claim.data === undefined) {
    return claim.loading ? (
      <LoadingState />
    ) : (
      <ErrorState message={milestoneProblemMessage(claim.error, t)} onRetry={claim.reload} />
    );
  }
  return claim.data.revision.data.status === 'DRAFT' && props.editable ? (
    <DraftClaim {...props} claim={claim.data} reload={claim.reload} />
  ) : (
    <ReadOnlyClaim claim={claim.data} lookups={props.lookups} />
  );
}

/** A claim as it was sent (or as a draft another person prepares): read only, with its evidence. */
function ReadOnlyClaim({
  claim: { revision, evidence },
  lookups,
}: {
  claim: OpenClaim;
  lookups: MilestoneLookups;
}): ReactElement {
  const { t } = useI18n();
  const claim = revision.data;
  const personName = usePersonNames([claim.submittedByUserId]);
  return (
    <>
      <p className="figure-group">
        <RevisionStatusBadge status={claim.status} />
        <span>{t('milestones.revision.number', { number: claim.revisionNo })}</span>
      </p>
      {claim.status === 'SUBMITTED' && (
        <p className="notice" role="status">
          {t('milestones.claim.submittedBy', {
            person: personName(claim.submittedByUserId),
            date: (claim.submittedAt ?? '').slice(0, 10),
          })}
        </p>
      )}
      {claim.status === 'DRAFT' && (
        <p className="form__note">{t('milestones.claim.draftReadOnly')}</p>
      )}
      <ClaimSummary claim={claim} />
      <h4 className="task-detail__subheading">{t('milestones.evidence.title')}</h4>
      <EvidencePolicyLabel rule={ruleOf(evidence)} lookups={lookups} />
      {evidence !== null && (
        <EvidenceList pieces={liveEvidence(evidence)} lookups={lookups} saving={false} />
      )}
    </>
  );
}

/** The claimed date and the narrative of a revision. */
export function ClaimSummary({ claim }: { claim: MilestoneAchievementDetail }): ReactElement {
  const { t } = useI18n();
  return (
    <dl className="details">
      <div className="details__row">
        <dt>{t('milestones.fields.claimedDate')}</dt>
        <dd dir="ltr">{claim.claimedAchievementDate}</dd>
      </div>
      {claim.narrative !== null && (
        <div className="details__row">
          <dt>{t('milestones.fields.narrative')}</dt>
          <dd className="pre-line" lang={languageTag(claim.narrative.language)} dir="auto">
            {claim.narrative.text}
          </dd>
        </div>
      )}
    </dl>
  );
}

type Pending = 'confirmSubmit' | 'delete' | null;

/**
 * A DRAFT the person may change: the claim, its evidence under the rule in force, and submission. Submission is refused
 * before any request while a mandatory type is missing or the policy is undetermined, with the reason beside the button;
 * while the policy is pending, a claim with no evidence is submitted only after the person confirms they send it without
 * any (acceptance criterion 1). The API decides again: its MILESTONE_EVIDENCE_REQUIRED reads the evidence again, so the
 * label follows the policy the server enforces now (the workbook's validation check).
 */
function DraftClaim({
  claim: { revision, evidence },
  reload,
  lookups,
  onAttach,
  onUpload,
  onChanged,
}: OpenRevisionProps & { claim: OpenClaim; reload: () => void }): ReactElement {
  const { t, language } = useI18n();
  const id = useId();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(milestoneProblemMessage);
  const command = useSaveAction(milestoneProblemMessage);
  const draft = revision.data;
  const etag = revision.etag;
  const saved: ClaimValues = {
    claimedAchievementDate: draft.claimedAchievementDate,
    narrative: draft.narrative?.text ?? '',
  };
  const [values, setValues] = useState<ClaimValues>(saved);
  const [pending, setPending] = useState<Pending>(null);
  const fields = useFieldErrors(checkClaim(values, todayUtc()), formRef, milestoneFieldMessage);
  const dirty =
    values.claimedAchievementDate !== saved.claimedAchievementDate ||
    values.narrative.trim() !== saved.narrative;
  const rule = ruleOf(evidence);
  const pieces = evidence === null ? [] : liveEvidence(evidence);
  const allowed = evidenceAllowsSubmission(rule);

  /** A command overtaken by another change, or refused for evidence: the claim is read again before anything else. */
  const settle = (error: unknown) => {
    if (isStale(error)) {
      onChanged('milestones.done.stale', 'warning');
      return true;
    }
    if (isRefusal(error, 'MILESTONE_EVIDENCE_REQUIRED')) {
      reload();
    }
    return false;
  };

  const saveClaim = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const result = await save.run(() =>
      milestonesApi.updateAchievement(draft.id, toClaimRequest(values, language, draft), etag),
    );
    if (result.ok) {
      onChanged('milestones.done.claimSaved');
    } else if (!settle(result.error)) {
      fields.showServer(result.error);
    }
  };

  const submit = async () => {
    const result = await command.run(() => milestonesApi.submitAchievement(draft.id, etag));
    setPending(null);
    if (result.ok) {
      onChanged('milestones.done.submitted');
    } else {
      settle(result.error);
    }
  };

  const remove = async () => {
    const result = await command.run(() => milestonesApi.deleteAchievement(draft.id, etag));
    if (result.ok) {
      onChanged('milestones.done.claimDeleted');
    } else {
      setPending(null);
      settle(result.error);
    }
  };

  const withdraw = async (pieceId: string) => {
    const result = await command.run(() => milestonesApi.withdrawEvidence(draft.id, pieceId, etag));
    if (result.ok) {
      onChanged('milestones.done.evidenceWithdrawn');
    } else {
      settle(result.error);
    }
  };

  const blockedReason =
    rule.kind === 'required' && rule.missing.length > 0
      ? t('milestones.submit.missing', {
          types: rule.missing.map((type) => lookups.itemLabel(type)).join(', '),
        })
      : rule.kind === 'undetermined'
        ? t('milestones.submit.undetermined')
        : dirty
          ? t('milestones.submit.saveFirst')
          : null;

  return (
    <>
      <p className="figure-group">
        <RevisionStatusBadge status={draft.status} />
        <span>{t('milestones.revision.number', { number: draft.revisionNo })}</span>
      </p>

      <form ref={formRef} className="form" noValidate onSubmit={(event) => void saveClaim(event)}>
        <FormAlert message={fields.summary ?? save.formError} />
        <ClaimFields
          values={values}
          onChange={(field, value) => {
            setValues((current) => ({ ...current, [field]: value }));
            fields.clearServer();
          }}
          errorOf={fields.errorOf}
        />
        <div className="form__actions">
          <button type="submit" className="button" disabled={save.saving || !dirty}>
            {save.saving ? t('common.states.saving') : t('milestones.claim.save')}
          </button>
        </div>
      </form>

      <section className="task-detail__section" aria-labelledby={`${id}-evidence`}>
        <h4 id={`${id}-evidence`} className="task-detail__subheading">
          {t('milestones.evidence.title')}
        </h4>
        <EvidencePolicyLabel rule={rule} lookups={lookups} />
        <EvidenceList
          pieces={pieces}
          lookups={lookups}
          saving={command.saving}
          onWithdraw={(piece) => void withdraw(piece.id)}
        />
        <div className="form__actions">
          <button
            type="button"
            className="button"
            onClick={() => {
              onAttach(revision, rule.kind === 'required' ? rule.mandatory : []);
            }}
          >
            {t('milestones.evidence.attach')}
          </button>
          <button type="button" className="button" onClick={onUpload}>
            {t('milestones.evidence.upload')}
          </button>
        </div>
        <p className="form__note">{t('milestones.evidence.uploadHint')}</p>
      </section>

      <section className="task-detail__section" aria-labelledby={`${id}-submit`}>
        <h4 id={`${id}-submit`} className="task-detail__subheading">
          {t('milestones.submit.title')}
        </h4>
        <FormAlert message={command.formError} />
        {blockedReason !== null && (
          <p id={`${id}-blocked`} className="notice notice--warning">
            {blockedReason}
          </p>
        )}
        {pending === 'confirmSubmit' ? (
          <div className="task-detail__confirm">
            <p>{t('milestones.submit.confirmWithoutEvidence')}</p>
            <div className="form__actions">
              <button
                type="button"
                className="button button--primary"
                disabled={command.saving}
                onClick={() => void submit()}
              >
                {t('milestones.submit.confirm')}
              </button>
              <button
                type="button"
                className="button"
                disabled={command.saving}
                onClick={() => {
                  setPending(null);
                }}
              >
                {t('milestones.submit.keepEditing')}
              </button>
            </div>
          </div>
        ) : pending === 'delete' ? (
          <div className="task-detail__confirm">
            <p>{t('milestones.claim.deleteBody')}</p>
            <div className="form__actions">
              <button
                type="button"
                className="button button--danger"
                disabled={command.saving}
                onClick={() => void remove()}
              >
                {t('milestones.claim.deleteConfirm')}
              </button>
              <button
                type="button"
                className="button"
                disabled={command.saving}
                onClick={() => {
                  setPending(null);
                }}
              >
                {t('milestones.claim.keep')}
              </button>
            </div>
          </div>
        ) : (
          <div className="form__actions">
            <button
              type="button"
              className="button button--primary"
              disabled={command.saving || !allowed || dirty}
              aria-describedby={blockedReason === null ? undefined : `${id}-blocked`}
              onClick={() => {
                if (rule.kind === 'pending' && pieces.length === 0) {
                  setPending('confirmSubmit');
                } else {
                  void submit();
                }
              }}
            >
              {command.saving ? t('common.states.saving') : t('milestones.submit.action')}
            </button>
            <button
              type="button"
              className="button button--danger"
              disabled={command.saving}
              onClick={() => {
                setPending('delete');
              }}
            >
              {t('milestones.claim.delete')}
            </button>
          </div>
        )}
      </section>
    </>
  );
}
