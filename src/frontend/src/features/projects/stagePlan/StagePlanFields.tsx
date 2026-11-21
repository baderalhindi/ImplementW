import { type ReactElement } from 'react';

import { fieldMessage } from '@/features/identity-access/problems.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { CheckboxField, TextField } from '@/shared/ui/FormFields.tsx';

import { fieldOf, type StagePlanCodes, type StagePlanRow } from './stagePlan.ts';

const MESSAGES: Partial<Record<string, TranslationKey>> = {
  REQUIRED: 'projects.stagePlan.errors.required',
  DATE_BEFORE_START: 'projects.stagePlan.errors.beforeProject',
  OUTSIDE_RANGE: 'projects.stagePlan.errors.outsideProject',
  OUT_OF_RANGE: 'projects.stagePlan.errors.percent',
  NOT_ALLOWED: 'projects.stagePlan.errors.notAllowed',
};

interface StagePlanFieldsProps {
  rows: StagePlanRow[];
  onChange: (rows: StagePlanRow[]) => void;
  /** Codes by request path, from the form's own checks and the API's refusal. */
  codes: StagePlanCodes;
  acceptsStartingPercent: boolean;
  disabled?: boolean;
}

/**
 * The stage plan (review round 7, item 1): each stage of the version in force with its weight, which AHDA sets; whether it applies (yes unless
 * changed) and, when it does, its planned start and end within the project's dates; and for a project already under way, how far it had come.
 */
export function StagePlanFields({
  rows,
  onChange,
  codes,
  acceptsStartingPercent,
  disabled = false,
}: StagePlanFieldsProps): ReactElement {
  const { t, language } = useI18n();
  const errorOf = (field: string): string | undefined => {
    const code = codes[field];
    if (code === undefined) {
      return undefined;
    }
    const key = MESSAGES[code];
    return key === undefined ? fieldMessage(code, t) : t(key);
  };
  const update = (index: number, change: Partial<StagePlanRow>) => {
    onChange(rows.map((row, at) => (at === index ? { ...row, ...change } : row)));
  };

  return (
    <div className="stage-plan">
      {codes.stages !== undefined && (
        <p className="field__error" role="alert">
          {t('projects.stagePlan.errors.noneApplies')}
        </p>
      )}
      <ol className="stage-plan__list">
        {rows.map((row, index) => (
          <li key={row.stageCode} className="stage-plan__stage">
            <p className="stage-plan__name">
              <span>{row.name[language]}</span>
              <span className="stage-plan__weight" dir="ltr">
                {t('projects.stagePlan.weight', { weight: row.weight })}
              </span>
            </p>
            <div className="stage-plan__fields">
              <CheckboxField
                label={t('projects.stagePlan.applies')}
                name={`stage-${row.stageCode}-applies`}
                checked={row.applies}
                disabled={disabled}
                onChange={(applies) => {
                  update(index, { applies });
                }}
              />
              {row.applies && (
                <>
                  <TextField
                    label={t('projects.stagePlan.plannedStart')}
                    name={`stage-${row.stageCode}-start`}
                    type="date"
                    dir="ltr"
                    value={row.plannedStartDate}
                    disabled={disabled}
                    onChange={(plannedStartDate) => {
                      update(index, { plannedStartDate });
                    }}
                    error={errorOf(fieldOf(index, 'plannedStartDate'))}
                  />
                  <TextField
                    label={t('projects.stagePlan.plannedEnd')}
                    name={`stage-${row.stageCode}-end`}
                    type="date"
                    dir="ltr"
                    value={row.plannedEndDate}
                    disabled={disabled}
                    onChange={(plannedEndDate) => {
                      update(index, { plannedEndDate });
                    }}
                    error={errorOf(fieldOf(index, 'plannedEndDate'))}
                  />
                  {acceptsStartingPercent && (
                    <TextField
                      label={t('projects.stagePlan.startingPercent')}
                      name={`stage-${row.stageCode}-starting`}
                      inputMode="decimal"
                      dir="ltr"
                      value={row.startingPercent}
                      disabled={disabled}
                      hint={t('projects.stagePlan.startingPercentHint')}
                      onChange={(startingPercent) => {
                        update(index, { startingPercent });
                      }}
                      error={errorOf(fieldOf(index, 'startingPercent'))}
                    />
                  )}
                </>
              )}
            </div>
          </li>
        ))}
      </ol>
    </div>
  );
}
