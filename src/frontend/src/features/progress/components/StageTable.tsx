import { type ReactElement } from 'react';

import { oneDecimal } from '@/features/projects/stagePlan/stageProgress.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';

import { type ProgressSubmissionDetail } from '../api/types.ts';

/**
 * A period's stage table (review round 7, item 1): each stage's weight, planned % and % complete, its actual start and finish and note, with the
 * current stage marked and the stage version the period was weighed by. The project report and the published history show it for every period.
 */
export function StageTable({
  submission,
  caption,
}: {
  submission: ProgressSubmissionDetail;
  caption: string;
}): ReactElement | null {
  const { t, language } = useI18n();
  const stages = [...(submission.stages ?? [])].sort((a, b) => a.sortOrder - b.sortOrder);
  if (stages.length === 0) {
    return null;
  }
  const none = t('common.values.none');
  const percent = (value: number | string | null) =>
    value === null ? none : `${oneDecimal(Number(value))}%`;
  return (
    <TableContainer
      caption={
        submission.stageVersionNo == null
          ? caption
          : `${caption} · ${t('projects.stageProgress.versionUsed', { version: submission.stageVersionNo })}`
      }
    >
      <thead>
        <tr>
          <th scope="col">{t('progress.stages.stage')}</th>
          <th scope="col">{t('progress.stages.weight')}</th>
          <th scope="col">{t('progress.stages.planned')}</th>
          <th scope="col">{t('progress.stages.percentComplete')}</th>
          <th scope="col">{t('progress.stages.actualStart')}</th>
          <th scope="col">{t('progress.stages.actualFinish')}</th>
          <th scope="col">{t('progress.stages.note')}</th>
        </tr>
      </thead>
      <tbody>
        {stages.map((stage) => (
          <tr
            key={stage.stageCode}
            aria-current={stage.stageCode === submission.currentStageCode ? 'step' : undefined}
          >
            <th scope="row">
              {stage.name?.[language] ?? stage.stageCode}
              {stage.stageCode === submission.currentStageCode && (
                <span className="details__aside">{t('projects.stageProgress.current')}</span>
              )}
            </th>
            <td dir="ltr">{`${oneDecimal(Number(stage.weightPercent))}%`}</td>
            {stage.applies ? (
              <>
                <td dir="ltr">{percent(stage.plannedPercent)}</td>
                <td>
                  <span dir="ltr">{percent(stage.percentComplete)}</span>
                  {stage.isFromSchedule && (
                    <span className="details__aside">{t('progress.stages.fromSchedule')}</span>
                  )}
                  {stage.decreaseReason !== null && (
                    <span className="details__aside" dir="auto">
                      {t('progress.stages.decreasedBecause', { reason: stage.decreaseReason.text })}
                    </span>
                  )}
                </td>
                <td dir="ltr">{stage.actualStartDate ?? none}</td>
                <td dir="ltr">{stage.actualFinishDate ?? none}</td>
                <td dir="auto">{stage.note ?? none}</td>
              </>
            ) : (
              <td colSpan={5}>{t('projects.stageProgress.notApplicable')}</td>
            )}
          </tr>
        ))}
      </tbody>
    </TableContainer>
  );
}
