import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type ReadinessCheckDetail, type ReadinessDetail } from '../api/types.ts';
import { CHECK_SOURCES, sortedChecks } from '../closeoutRules.ts';
import { readinessTone, resultTone } from '../presentation.ts';

/**
 * The case's readiness as the server rolled it up (TASK-063 D-5; WF-10 §14.2 "backend roll-up"): the status, when it
 * was evaluated, and each criterion with how many records block it and where they are settled. Nothing is computed
 * here; a waiver is offered on a criterion that failed and may be waived, to whoever may waive it.
 */
export function ReadinessPanel({
  readiness,
  projectId,
  onEvaluate,
  evaluating,
  canWaive,
  onWaive,
}: {
  readiness: ReadinessDetail;
  projectId: string;
  /** Offered while the case is DRAFT or RETURNED, to its raiser. */
  onEvaluate?: (() => void) | undefined;
  evaluating?: boolean;
  canWaive: (check: ReadinessCheckDetail) => boolean;
  onWaive: (check: ReadinessCheckDetail) => void;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  const personName = usePersonNames(readiness.checks.map((check) => check.waivedByUserId));
  const checks = sortedChecks(readiness.checks);

  return (
    <section className="section" aria-labelledby="closeout-readiness">
      <div className="section__header">
        <h2 id="closeout-readiness">{t('suspensionClosure.readiness.title')}</h2>
        {onEvaluate !== undefined && (
          <button type="button" className="button" disabled={evaluating} onClick={onEvaluate}>
            {evaluating === true
              ? t('common.states.loading')
              : t('suspensionClosure.actions.evaluateReadiness')}
          </button>
        )}
      </div>
      <dl className="details">
        <div className="details__row">
          <dt>{t('suspensionClosure.readiness.rollUp')}</dt>
          <dd>
            <span data-badge="readiness">
              <StatusBadge
                label={t(`suspensionClosure.readiness.status.${readiness.status}`)}
                tone={readinessTone(readiness.status)}
              />
            </span>
          </dd>
        </div>
        <div className="details__row">
          <dt>{t('suspensionClosure.readiness.evaluatedAt')}</dt>
          <dd>
            {readiness.evaluatedAt === null
              ? t('suspensionClosure.readiness.neverEvaluated')
              : formatDateTime(readiness.evaluatedAt)}
          </dd>
        </div>
      </dl>
      <p className="form__note">{t(`suspensionClosure.readiness.explain.${readiness.status}`)}</p>
      {checks.length > 0 && (
        <TableContainer caption={t('suspensionClosure.readiness.caption')}>
          <thead>
            <tr>
              <th scope="col">{t('suspensionClosure.readiness.criterion')}</th>
              <th scope="col">{t('suspensionClosure.readiness.result')}</th>
              <th scope="col">{t('suspensionClosure.readiness.blocking')}</th>
              <th scope="col">{t('suspensionClosure.readiness.waiver')}</th>
            </tr>
          </thead>
          <tbody>
            {checks.map((check) => (
              <tr key={check.checkCode} data-check={check.checkCode}>
                <th scope="row">
                  {t(`suspensionClosure.readiness.checks.${check.checkCode}`)}
                  {!check.waivable && (
                    <span className="cell__aside">
                      {t('suspensionClosure.readiness.notWaivable')}
                    </span>
                  )}
                </th>
                <td>
                  <StatusBadge
                    label={t(`suspensionClosure.readiness.results.${check.result}`)}
                    tone={resultTone(check.result)}
                  />
                </td>
                <td>
                  <span dir="ltr">{check.blockingCount}</span>
                  {check.result === 'FAIL' && (
                    <span className="cell__aside">
                      <Link to={`/projects/${projectId}/${CHECK_SOURCES[check.checkCode]}`}>
                        {t('suspensionClosure.readiness.whereSettled')}
                      </Link>
                    </span>
                  )}
                </td>
                <td>
                  {check.result === 'WAIVED' ? (
                    <>
                      <span dir="auto">{check.waiverReason?.text ?? ''}</span>
                      <span className="cell__aside">{personName(check.waivedByUserId)}</span>
                    </>
                  ) : canWaive(check) ? (
                    <button
                      type="button"
                      className="button button--link"
                      aria-label={t('suspensionClosure.waive.named', {
                        criterion: t(`suspensionClosure.readiness.checks.${check.checkCode}`),
                      })}
                      onClick={() => {
                        onWaive(check);
                      }}
                    >
                      {t('suspensionClosure.waive.action')}
                    </button>
                  ) : (
                    t('common.values.none')
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </TableContainer>
      )}
    </section>
  );
}
