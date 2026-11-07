import { type ReactElement, type SyntheticEvent, useCallback, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { fieldMessage } from '@/features/identity-access/problems.ts';
import { isPublished, readCatalogueItems } from '@/shared/api/masterData.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { AmountField, SelectField } from '@/shared/ui/FormFields.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { financialKpiApi } from '../api/financialKpiApi.ts';
import { financialKpiProblemMessage } from '../problems.ts';

interface AddKpiDialogProps {
  open: boolean;
  projectId: string;
  /** Who records its values: the project's manager. */
  ownerUserId: string | null;
  /** KPIs the project has already: a project holds one assignment of each. */
  assignedDefinitionIds: readonly string[];
  /** Turns a catalogue item's id into its label. */
  itemLabel: (id: string) => string;
  onClose: () => void;
  onDone: (notice: TranslationKey) => void;
}

function readChoices(signal: AbortSignal) {
  return Promise.all([
    financialKpiApi.definitions(signal),
    readCatalogueItems(['MEASUREMENT_FREQUENCY'], signal),
  ]);
}

/**
 * Add KPI (review round 5, item 4): one of the published KPI definitions, its frequency and its target. The name, unit and direction belong to
 * the definition. The target goes to AHDA for approval, and values can be recorded once it is approved.
 */
export function AddKpiDialog(props: AddKpiDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={props.open} title={t('financialKpi.addKpi.title')} onClose={props.onClose}>
      {props.open && <AddKpiBody {...props} />}
    </Dialog>
  );
}

function AddKpiBody({
  projectId,
  ownerUserId,
  assignedDefinitionIds,
  itemLabel,
  onClose,
  onDone,
}: AddKpiDialogProps): ReactElement {
  const { t, language } = useI18n();
  const load = useCallback(readChoices, []);
  const choices = useApiResource(load);
  const save = useSaveAction(financialKpiProblemMessage);
  const [definitionId, setDefinitionId] = useState('');
  const [frequencyId, setFrequencyId] = useState('');
  const [target, setTarget] = useState('');
  const [attempted, setAttempted] = useState(false);

  if (choices.data === undefined) {
    return choices.error === null ? (
      <LoadingState />
    ) : (
      <ErrorState
        error={choices.error}
        message={financialKpiProblemMessage(choices.error, t)}
        onRetry={choices.reload}
      />
    );
  }
  const [definitions, catalogues] = choices.data;
  const available = definitions.filter(
    (definition) =>
      definition.lifecycleState === 'PUBLISHED' && !assignedDefinitionIds.includes(definition.id),
  );
  const chosen = available.find((definition) => definition.id === definitionId);
  const required = fieldMessage('REQUIRED', t);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    setAttempted(true);
    if (definitionId === '' || frequencyId === '' || target === '' || target === '-') {
      return;
    }
    const result = await save.run(async () => {
      const assignment = await financialKpiApi.createAssignment({
        projectId,
        kpiDefinitionId: definitionId,
        ownerUserId,
        measurementFrequencyItemId: frequencyId,
      });
      const version = await financialKpiApi.createTargetVersion({
        kpiAssignmentId: assignment.id,
        targetValue: Number(target),
        greenThreshold: null,
        amberThreshold: null,
        effectiveFrom: null,
      });
      await financialKpiApi.submitTargetVersion(version.id);
    });
    if (result.ok) {
      onDone('financialKpi.addKpi.done');
    }
  };

  return (
    <form className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={save.formError} />
      <p className="form__note">{t('financialKpi.addKpi.intro')}</p>
      <SelectField
        label={t('financialKpi.addKpi.kpi')}
        name="kpiDefinitionId"
        required
        value={definitionId}
        placeholder={t('common.form.choose')}
        options={available.map((definition) => ({
          value: definition.id,
          label: definition.name[language],
        }))}
        onChange={setDefinitionId}
        error={attempted && definitionId === '' ? required : undefined}
      />
      {chosen !== undefined && (
        <p className="form__note">
          {t('financialKpi.addKpi.chosen', {
            unit: itemLabel(chosen.unitItemId),
            direction: t(`financialKpi.direction.${chosen.direction}`),
          })}
        </p>
      )}
      <SelectField
        label={t('financialKpi.addKpi.frequency')}
        name="measurementFrequencyItemId"
        required
        value={frequencyId}
        placeholder={t('common.form.choose')}
        options={catalogues.MEASUREMENT_FREQUENCY.filter(isPublished).map((item) => ({
          value: item.id,
          label: item.label[language],
        }))}
        onChange={setFrequencyId}
        error={attempted && frequencyId === '' ? required : undefined}
      />
      <AmountField
        label={t('financialKpi.addKpi.target')}
        name="targetValue"
        required
        value={target}
        onChange={setTarget}
        maxDecimals={4}
        allowNegative
        hint={t('financialKpi.addKpi.targetHint')}
        error={attempted && (target === '' || target === '-') ? required : undefined}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('financialKpi.addKpi.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
