import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { MatrixUnavailable } from '@/features/risks/components/RiskHeatMap.tsx';
import { type RiskMatrix } from '@/features/risks/riskRules.ts';
import { type MatrixState } from '@/features/risks/useRiskData.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { type ApiResource } from '@/shared/api/useApiResource.ts';
import { useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { SelectField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { concernsApi } from '../api/concernsApi.ts';
import { type ConcernDetail } from '../api/types.ts';
import {
  checkImpacts,
  impactValuesOf,
  type ImpactValues,
  overallImpactOf,
  toAssessCommand,
} from '../concernRules.ts';
import { concernFieldMessage, concernProblemMessage, isStale } from '../problems.ts';
import { type ConcernLookups } from '../useConcernData.ts';

interface AssessConcernDialogProps {
  concern: ApiResponse<ConcernDetail>;
  matrixState: ApiResource<MatrixState>;
  lookups: ConcernLookups;
  onClose: () => void;
  onDone: (concern: ConcernDetail) => void;
  onStale: () => void;
}

/**
 * The impact assessment: a level for each dimension of the RISK_MATRIX version in force that applies — risks and
 * issues share the scale (ADR-011) — and "Not applicable" for any that does not (VAL-ISS-006). Only the levels are
 * sent. The overall impact (the highest) is previewed; the severity is the server's, computed from it under the
 * version's rule and pinned to it (TASK-057 D-4), and is shown once saved.
 */
export function AssessConcernDialog(props: AssessConcernDialogProps): ReactElement {
  const { t } = useI18n();
  const { matrixState } = props;
  return (
    <Dialog
      open
      title={t('issuesChallenges.assess.title', { concern: props.concern.data.title.text })}
      onClose={props.onClose}
    >
      {matrixState.data?.kind === 'ready' ? (
        <AssessForm {...props} matrix={matrixState.data.matrix} />
      ) : (
        <>
          <MatrixUnavailable state={matrixState} />
          <div className="form__actions">
            <button type="button" className="button" onClick={props.onClose}>
              {t('common.actions.back')}
            </button>
          </div>
        </>
      )}
    </Dialog>
  );
}

function AssessForm({
  concern: { data: concern, etag },
  matrix,
  lookups,
  onClose,
  onDone,
  onStale,
}: AssessConcernDialogProps & { matrix: RiskMatrix }): ReactElement {
  const { t, language } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(concernProblemMessage);
  const [values, setValues] = useState<ImpactValues>(() => impactValuesOf(matrix, concern));
  const fields = useFieldErrors(checkImpacts(values), formRef, concernFieldMessage);
  const overall = overallImpactOf(values);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const result = await save.run(
      async () => (await concernsApi.assess(concern.id, toAssessCommand(values), etag)).data,
    );
    if (result.ok) {
      onDone(result.value);
    } else if (isStale(result.error)) {
      onStale();
    } else {
      fields.showServer(result.error);
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p className="form__note">
        {t('issuesChallenges.assess.intro', { version: matrix.versionNo })}
      </p>
      <FormAlert message={fields.summary ?? save.formError} />
      <fieldset className="field field--group" aria-describedby="concern-impacts-hint">
        <legend className="field__label">{t('issuesChallenges.assess.impacts')}</legend>
        <p id="concern-impacts-hint" className="field__hint">
          {t('issuesChallenges.assess.impactsHint')}
        </p>
        {/* "At least one" is said on the first dimension, where focus lands. */}
        {matrix.dimensions.map((dimension, index) => (
          <SelectField
            key={dimension.id}
            label={lookups.itemLabel(dimension.id)}
            name={`impact-${dimension.id}`}
            value={values[dimension.id] ?? ''}
            options={[
              { value: '', label: t('issuesChallenges.assess.notApplicable') },
              ...dimension.levels.map((entry) => ({
                value: String(entry.level),
                label: t('issuesChallenges.assess.levelOption', {
                  level: entry.level,
                  label: entry.label[language],
                }),
              })),
            ]}
            onChange={(value) => {
              setValues((current) => ({ ...current, [dimension.id]: value }));
              fields.clearServer();
            }}
            error={index === 0 ? fields.errorOf('impacts') : undefined}
          />
        ))}
      </fieldset>
      <p className="form__note" role="status">
        {overall === null
          ? t('issuesChallenges.assess.previewPending')
          : t('issuesChallenges.assess.preview', { level: overall })}
      </p>
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('issuesChallenges.assess.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
