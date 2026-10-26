import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { type ApiResource } from '@/shared/api/useApiResource.ts';
import { useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { SelectField, TextAreaField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { risksApi } from '../api/risksApi.ts';
import { type RiskAssessmentDetail, type RiskDetail } from '../api/types.ts';
import { MatrixUnavailable, RiskHeatMap } from '../components/RiskHeatMap.tsx';
import { isStale, riskFieldMessage, riskProblemMessage } from '../problems.ts';
import {
  assessmentValuesOf,
  type AssessmentValues,
  checkAssessment,
  chosenLevels,
  overallImpact,
  ratingAt,
  type RiskMatrix,
  toAssessCommand,
} from '../riskRules.ts';
import { type MatrixState, type RiskLookups } from '../useRiskData.ts';

interface AssessRiskDialogProps {
  risk: ApiResponse<RiskDetail>;
  /** The latest assessment, whose levels a reassessment starts from. */
  previous: RiskAssessmentDetail | null;
  matrixState: ApiResource<MatrixState>;
  lookups: RiskLookups;
  onClose: () => void;
  onDone: (risk: RiskDetail) => void;
  onStale: () => void;
}

/**
 * MOD-032 Risk Assessment: a probability and one impact level for each dimension the RISK_MATRIX version in force
 * defines, with that version's labels, and the matrix as a preview. The server works out the overall impact (the
 * highest) and looks the rating up in the version, which the assessment pins (TASK-055 D-4, D-5); the preview says
 * which cell that should be, and the risk shows the rating the server returns. Without a readable, published matrix
 * there is nothing to assess against, and the form says so.
 */
export function AssessRiskDialog(props: AssessRiskDialogProps): ReactElement {
  const { t } = useI18n();
  const { matrixState } = props;
  return (
    <Dialog
      open
      title={t('risks.assess.title', { risk: props.risk.data.title.text })}
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
  risk: { data: risk, etag },
  previous,
  matrix,
  lookups,
  onClose,
  onDone,
  onStale,
}: AssessRiskDialogProps & { matrix: RiskMatrix }): ReactElement {
  const { t, language } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(riskProblemMessage);
  const [values, setValues] = useState<AssessmentValues>(() =>
    assessmentValuesOf(matrix, previous),
  );
  const fields = useFieldErrors(checkAssessment(values, matrix), formRef, riskFieldMessage);

  const chosen = chosenLevels(values, matrix);
  const impact = overallImpact(chosen.impacts);
  const preview =
    chosen.probability === null || impact === null
      ? null
      : { probabilityLevel: chosen.probability, impactLevel: impact };
  const previewRating =
    preview === null ? null : ratingAt(matrix, preview.probabilityLevel, preview.impactLevel);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const result = await save.run(
      async () =>
        (await risksApi.assess(risk.id, toAssessCommand(values, matrix, language), etag)).data,
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
      <p className="form__note">{t('risks.assess.intro', { version: matrix.versionNo })}</p>
      <FormAlert message={fields.summary ?? save.formError} />
      <SelectField
        label={t('risks.fields.probability')}
        name="probabilityLevel"
        required
        value={values.probabilityLevel}
        placeholder={t('risks.assess.chooseLevel')}
        options={matrix.probabilityLevels.map((entry) => ({
          value: String(entry.level),
          label: t('risks.assess.levelOption', {
            level: entry.level,
            label: entry.label[language],
          }),
        }))}
        onChange={(value) => {
          setValues((current) => ({ ...current, probabilityLevel: value }));
          fields.clearServer();
        }}
        error={fields.errorOf('probabilityLevel')}
      />
      <fieldset className="field field--group">
        <legend className="field__label">{t('risks.assess.impacts')}</legend>
        <p className="field__hint">{t('risks.assess.impactsHint')}</p>
        {matrix.dimensions.map((dimension) => (
          <SelectField
            key={dimension.id}
            label={lookups.itemLabel(dimension.id)}
            name={`impact-${dimension.id}`}
            required
            value={values.impacts[dimension.id] ?? ''}
            placeholder={t('risks.assess.chooseLevel')}
            options={dimension.levels.map((entry) => ({
              value: String(entry.level),
              label: t('risks.assess.levelOption', {
                level: entry.level,
                label: entry.label[language],
              }),
            }))}
            onChange={(value) => {
              setValues((current) => ({
                ...current,
                impacts: { ...current.impacts, [dimension.id]: value },
              }));
              fields.clearServer();
            }}
            error={fields.errorOf(`impact-${dimension.id}`)}
          />
        ))}
      </fieldset>
      <TextAreaField
        label={t('risks.fields.assessmentRationale')}
        name="rationale"
        maxLength={TEXT_LENGTH}
        value={values.rationale}
        onChange={(value) => {
          setValues((current) => ({ ...current, rationale: value }));
          fields.clearServer();
        }}
        error={fields.errorOf('rationale')}
      />
      <section className="section risk-assess__preview" aria-labelledby="risk-assess-preview">
        <h3 id="risk-assess-preview">{t('risks.assess.previewTitle')}</h3>
        <p className="form__note" role="status">
          {preview === null
            ? t('risks.assess.previewPending')
            : t('risks.assess.preview', {
                probability: preview.probabilityLevel,
                impact: preview.impactLevel,
                rating:
                  previewRating === null
                    ? t('risks.matrix.unmapped')
                    : previewRating.label[language],
              })}
        </p>
        <RiskHeatMap
          matrix={matrix}
          caption={t('risks.assess.matrixCaption')}
          marked={preview}
          markedLabel={t('risks.assess.marked')}
        />
      </section>
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('risks.assess.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
