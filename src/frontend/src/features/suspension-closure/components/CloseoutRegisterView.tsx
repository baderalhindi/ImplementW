import { type ReactElement, useState } from 'react';
import { Link } from 'react-router';

import { ProjectStatusBadge } from '@/features/projects/components/ProjectStatusBadge.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField } from '@/shared/ui/FormFields.tsx';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type CloseoutStage } from '../api/closeoutApi.ts';
import { type ReadinessStatus } from '../api/types.ts';
import { CLOSEOUT_STAGES } from '../closeoutRules.ts';
import { closeoutCasePath } from '../paths.ts';
import { readinessTone } from '../presentation.ts';
import { type CloseoutEntry } from '../useSuspensionClosureData.ts';

import { GovernedStatusBadge } from './GovernedState.tsx';

const READINESS_STATUSES: ReadinessStatus[] = [
  'READY',
  'READY_WITH_CONDITIONS',
  'NOT_READY',
  'INCOMPLETE',
];

/**
 * SCR-111's filters and table (WF-10 §14.1): each case names its stage — Stage 1 Completion or Stage 2 Closure, the
 * latter marked when it closes a suspended project without completion — its status, the server's readiness roll-up with
 * the number of failing criteria, and when it last changed.
 */
export function CloseoutRegisterView({
  entries,
  showProject,
  caption,
}: {
  entries: CloseoutEntry[];
  showProject: boolean;
  caption: string;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  const [stage, setStage] = useState<CloseoutStage | ''>('');
  const [readiness, setReadiness] = useState<ReadinessStatus | ''>('');
  const shown = entries.filter(
    (entry) =>
      (stage === '' || entry.stage === stage) &&
      (readiness === '' || entry.closeoutCase.readiness.status === readiness),
  );

  return (
    <>
      <div className="filters">
        <SelectField
          label={t('suspensionClosure.closeout.filter.stage')}
          name="stage"
          value={stage}
          placeholder={t('suspensionClosure.closeout.filter.allStages')}
          options={CLOSEOUT_STAGES.map((value, index) => ({
            value,
            label: t('suspensionClosure.stages.numbered', {
              number: index + 1,
              name: t(`suspensionClosure.stages.name.${value}`),
            }),
          }))}
          onChange={(value) => {
            setStage(CLOSEOUT_STAGES.find((candidate) => candidate === value) ?? '');
          }}
        />
        <SelectField
          label={t('suspensionClosure.closeout.filter.readiness')}
          name="readiness"
          value={readiness}
          placeholder={t('suspensionClosure.closeout.filter.allReadiness')}
          options={READINESS_STATUSES.map((value) => ({
            value,
            label: t(`suspensionClosure.readiness.status.${value}`),
          }))}
          onChange={(value) => {
            setReadiness(READINESS_STATUSES.find((candidate) => candidate === value) ?? '');
          }}
        />
      </div>
      <p className="list-order" role="status">
        {t('suspensionClosure.closeout.filter.showing', {
          shown: shown.length,
          total: entries.length,
        })}
      </p>
      {shown.length === 0 ? (
        <EmptyState title={t('suspensionClosure.closeout.filter.noMatch')} />
      ) : (
        <TableContainer caption={caption}>
          <thead>
            <tr>
              <th scope="col">{t('suspensionClosure.closeout.table.stage')}</th>
              {showProject && <th scope="col">{t('suspensionClosure.table.project')}</th>}
              {showProject && (
                <th scope="col">{t('suspensionClosure.closeout.table.lifecycle')}</th>
              )}
              <th scope="col">{t('suspensionClosure.closeout.table.status')}</th>
              <th scope="col">{t('suspensionClosure.closeout.table.readiness')}</th>
              <th scope="col">{t('suspensionClosure.table.updated')}</th>
            </tr>
          </thead>
          <tbody>
            {shown.map((entry) => {
              const { closeoutCase, project } = entry;
              const failing = closeoutCase.readiness.checks.filter(
                (check) => check.result === 'FAIL',
              ).length;
              return (
                <tr key={closeoutCase.id} data-stage={entry.stage}>
                  <td>
                    <Link
                      className="cell__link"
                      to={closeoutCasePath(entry.stage, closeoutCase.id)}
                    >
                      {t('suspensionClosure.stages.numbered', {
                        number: entry.stage === 'completion' ? 1 : 2,
                        name: t(`suspensionClosure.stages.name.${entry.stage}`),
                      })}
                    </Link>
                    <span className="cell__aside">
                      {t('suspensionClosure.table.revision', { revision: closeoutCase.revisionNo })}
                    </span>
                    {entry.stage === 'closure' &&
                      entry.closeoutCase.outcome === 'TERMINATED_WITHOUT_COMPLETION' && (
                        <span className="cell__aside">
                          {t('suspensionClosure.closeout.outcome.TERMINATED_WITHOUT_COMPLETION')}
                        </span>
                      )}
                  </td>
                  {showProject && (
                    <td>
                      <span dir="auto">{project.title.text}</span>
                    </td>
                  )}
                  {showProject && (
                    <td>
                      <ProjectStatusBadge status={project.status} legacyIntakeDate={null} />
                    </td>
                  )}
                  <td>
                    <GovernedStatusBadge status={closeoutCase.status} />
                  </td>
                  <td>
                    <StatusBadge
                      label={t(
                        `suspensionClosure.readiness.status.${closeoutCase.readiness.status}`,
                      )}
                      tone={readinessTone(closeoutCase.readiness.status)}
                    />
                    {failing > 0 && (
                      <span className="cell__aside">
                        {t('suspensionClosure.closeout.table.failing', { count: failing })}
                      </span>
                    )}
                  </td>
                  <td>{formatDateTime(closeoutCase.updatedAt)}</td>
                </tr>
              );
            })}
          </tbody>
        </TableContainer>
      )}
    </>
  );
}
