import { type ReactElement, type ReactNode } from 'react';
import { Link } from 'react-router';

import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type CloseoutStage } from '../api/closeoutApi.ts';
import {
  CLOSEOUT_STAGES,
  type CloseoutStages as Stages,
  type StageState,
} from '../closeoutRules.ts';
import { closeoutCasePath } from '../paths.ts';
import { stageTone } from '../presentation.ts';

import { GovernedStatusBadge } from './GovernedState.tsx';

/** The stage whose turn it is: the first one not yet done (or skipped). */
function currentStage(stages: Stages): CloseoutStage | null {
  const done = (state: StageState) => state === 'DONE' || state === 'SKIPPED';
  return CLOSEOUT_STAGES.find((stage) => !done(stages[stage].state)) ?? null;
}

function stateKey(stage: CloseoutStage, state: StageState, terminal: boolean): TranslationKey {
  if (stage === 'closure' && terminal && state !== 'DONE') {
    return state === 'IN_PROGRESS'
      ? 'suspensionClosure.stages.explain.closure.TERMINAL_IN_PROGRESS'
      : 'suspensionClosure.stages.explain.closure.TERMINAL_READY';
  }
  return `suspensionClosure.stages.explain.${stage}.${state}`;
}

/**
 * The closeout as two sequential steps (acceptance criterion 1; WF-10 §14 "Stage = Completion / Closure"): Stage 1,
 * Completion, takes an ACTIVE project to COMPLETED; Stage 2, Closure, takes a COMPLETED project to CLOSED, and opens only
 * once Stage 1 is effected — or, on the terminal path, closes a SUSPENDED project without Stage 1. Each stage has its own
 * state, its own case and its own action; nothing here starts both.
 */
export function CloseoutStages({
  stages,
  actions = {},
  highlight,
  headingLevel = 2,
}: {
  stages: Stages;
  /** The action each stage offers the person now (raise its case); none is shared. */
  actions?: Partial<Record<CloseoutStage, ReactNode>>;
  /** On a case's detail, its own stage is marked as the one shown. */
  highlight?: CloseoutStage;
  headingLevel?: 2 | 3;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  const current = currentStage(stages);
  const Heading = headingLevel === 2 ? 'h2' : 'h3';
  const StepHeading = headingLevel === 2 ? 'h3' : 'h4';

  return (
    <section className="section" aria-labelledby="closeout-stages">
      <Heading id="closeout-stages">{t('suspensionClosure.stages.title')}</Heading>
      <p className="form__note">{t('suspensionClosure.stages.intro')}</p>
      <ol className="closeout-stages">
        {CLOSEOUT_STAGES.map((stage, index) => {
          const view = stages[stage];
          const closeoutCase = view.current;
          const classes = [
            'closeout-stage',
            `closeout-stage--${view.state.toLowerCase().replaceAll('_', '-')}`,
            highlight === stage ? 'closeout-stage--shown' : null,
          ].filter((name): name is string => name !== null);
          return (
            <li
              key={stage}
              className={classes.join(' ')}
              data-stage={stage}
              aria-current={current === stage ? 'step' : undefined}
              aria-labelledby={`closeout-stage-${stage}`}
            >
              <span className="closeout-stage__marker" aria-hidden="true">
                {view.state === 'DONE' ? '✓' : index + 1}
              </span>
              <div className="closeout-stage__body">
                <StepHeading id={`closeout-stage-${stage}`} className="closeout-stage__title">
                  <span className="closeout-stage__number">
                    {t('suspensionClosure.stages.number', { number: index + 1, total: 2 })}
                  </span>{' '}
                  {t(`suspensionClosure.stages.name.${stage}`)}
                </StepHeading>
                <p className="closeout-stage__state">
                  <span data-badge="stage">
                    <StatusBadge
                      label={t(`suspensionClosure.stages.state.${view.state}`)}
                      tone={stageTone(view.state)}
                    />
                  </span>
                  {highlight === stage && (
                    <span className="cell__aside">{t('suspensionClosure.stages.shown')}</span>
                  )}
                </p>
                <p className="form__note">{t(stateKey(stage, view.state, stages.terminal))}</p>
                {closeoutCase !== null && (
                  <p className="closeout-stage__case">
                    <Link to={closeoutCasePath(stage, closeoutCase.id)}>
                      {t(`suspensionClosure.stages.caseLink.${stage}`, {
                        revision: closeoutCase.revisionNo,
                      })}
                    </Link>{' '}
                    <GovernedStatusBadge status={closeoutCase.status} />
                    {closeoutCase.effectedAt !== null && (
                      <span className="cell__aside">
                        {t('suspensionClosure.stages.effectedAt', {
                          date: formatDateTime(closeoutCase.effectedAt),
                        })}
                      </span>
                    )}
                  </p>
                )}
                {actions[stage] !== undefined && actions[stage] !== null && (
                  <div className="closeout-stage__action">{actions[stage]}</div>
                )}
              </div>
            </li>
          );
        })}
      </ol>
    </section>
  );
}
