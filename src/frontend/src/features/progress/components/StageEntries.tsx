import { type ReactElement } from 'react';

import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { oneDecimal } from '@/features/projects/stagePlan/stageProgress.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TextAreaField, TextField } from '@/shared/ui/FormFields.tsx';

import { type ProgressStageDetail } from '../api/types.ts';
import {
  isDecrease,
  liveFigures,
  stageField,
  type StageEntryValues,
  type ProgressUpdateValues,
} from '../progressUpdate.ts';

interface StageEntriesProps {
  values: ProgressUpdateValues;
  stages: readonly ProgressStageDetail[];
  onChange: (stages: StageEntryValues[]) => void;
  errorOf: (field: string) => string | undefined;
}

/**
 * The monthly "Stage progress" step (review round 7, item 1): each stage that applies with its planned % on the period's date, which the plan gives,
 * and its % complete, actual start and finish and an optional one-line note, which the Project Manager enters. Execution's % is the schedule's while
 * the project has an active baseline. A % below last period's asks why. The overall figure and the current stage follow as they are typed.
 */
export function StageEntries({
  values,
  stages,
  onChange,
  errorOf,
}: StageEntriesProps): ReactElement {
  const { t, language } = useI18n();
  const live = liveFigures(values, stages);
  const ordered = [...stages].sort((a, b) => a.sortOrder - b.sortOrder);
  const set = (stageCode: string, change: Partial<StageEntryValues>) => {
    onChange(
      values.stages.map((entry) =>
        entry.stageCode === stageCode ? { ...entry, ...change } : entry,
      ),
    );
  };
  const percent = (value: number | null) =>
    value === null ? t('progress.stages.notKnown') : `${oneDecimal(value)}%`;

  return (
    <fieldset className="form__section stage-entries">
      <legend>{t('progress.stages.title')}</legend>
      <p className="form__note" role="status" aria-live="polite">
        {t('progress.stages.overall', {
          actual: percent(live.actual),
          planned: percent(live.planned),
        })}
        {live.current !== null &&
          ` ${t('progress.stages.current', { stage: live.current.name?.[language] ?? live.current.stageCode })}`}
      </p>
      <ol className="stage-entries__list">
        {ordered.map((stage) => {
          const name = stage.name?.[language] ?? stage.stageCode;
          const entry = values.stages.find((e) => e.stageCode === stage.stageCode);
          return (
            <li
              key={stage.stageCode}
              className={`stage-entries__stage${live.current?.stageCode === stage.stageCode ? ' is-current' : ''}`}
            >
              <p className="stage-plan__name">
                <span>{name}</span>
                <span className="stage-plan__weight" dir="ltr">
                  {t('projects.stagePlan.weight', { weight: Number(stage.weightPercent) })}
                </span>
              </p>
              {!stage.applies || entry === undefined ? (
                <p className="form__note">{t('projects.stageProgress.notApplicable')}</p>
              ) : (
                <div className="stage-plan__fields">
                  <p className="form__readonly">
                    <span className="field__label">{t('progress.stages.planned')}</span>
                    <span dir="ltr">
                      {percent(stage.plannedPercent === null ? null : Number(stage.plannedPercent))}
                    </span>
                  </p>
                  {stage.isFromSchedule ? (
                    <p className="form__readonly">
                      <span className="field__label">{t('progress.stages.percentComplete')}</span>
                      <span dir="ltr">
                        {percent(
                          stage.percentComplete === null ? null : Number(stage.percentComplete),
                        )}
                      </span>
                      <span className="details__aside">{t('progress.stages.fromSchedule')}</span>
                    </p>
                  ) : (
                    <TextField
                      label={t('progress.stages.percentComplete')}
                      name={`stage-${stage.stageCode}-percent`}
                      inputMode="decimal"
                      dir="ltr"
                      required
                      value={entry.percent}
                      hint={
                        stage.previousPercent === null
                          ? undefined
                          : t('progress.stages.previous', {
                              percent: `${oneDecimal(Number(stage.previousPercent))}%`,
                            })
                      }
                      onChange={(value) => {
                        set(stage.stageCode, { percent: value });
                      }}
                      error={errorOf(stageField(stage.stageCode, 'percent'))}
                    />
                  )}
                  <TextField
                    label={t('progress.stages.actualStart')}
                    name={`stage-${stage.stageCode}-start`}
                    type="date"
                    dir="ltr"
                    value={entry.actualStartDate}
                    onChange={(value) => {
                      set(stage.stageCode, { actualStartDate: value });
                    }}
                    error={errorOf(stageField(stage.stageCode, 'actualStartDate'))}
                  />
                  <TextField
                    label={t('progress.stages.actualFinish')}
                    name={`stage-${stage.stageCode}-finish`}
                    type="date"
                    dir="ltr"
                    value={entry.actualFinishDate}
                    hint={t('progress.stages.actualFinishHint')}
                    onChange={(value) => {
                      set(stage.stageCode, { actualFinishDate: value });
                    }}
                    error={errorOf(stageField(stage.stageCode, 'actualFinishDate'))}
                  />
                  <TextField
                    label={t('progress.stages.note')}
                    name={`stage-${stage.stageCode}-note`}
                    dir="auto"
                    value={entry.note}
                    onChange={(value) => {
                      set(stage.stageCode, { note: value });
                    }}
                    error={errorOf(stageField(stage.stageCode, 'note'))}
                  />
                  {isDecrease(entry, stage) && (
                    <TextAreaField
                      label={t('progress.stages.decreaseReason')}
                      name={`stage-${stage.stageCode}-decrease`}
                      required
                      rows={2}
                      maxLength={TEXT_LENGTH}
                      value={entry.decreaseReason}
                      onChange={(value) => {
                        set(stage.stageCode, { decreaseReason: value });
                      }}
                      error={errorOf(stageField(stage.stageCode, 'decreaseReason'))}
                    />
                  )}
                </div>
              )}
            </li>
          );
        })}
      </ol>
    </fieldset>
  );
}
