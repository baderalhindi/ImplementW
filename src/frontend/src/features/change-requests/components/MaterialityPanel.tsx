import { type ReactElement, useId } from 'react';

import { formatSar, shortId } from '@/features/projects/presentation.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';

import { type ChangeRequestDetail, type MaterialityAssessment } from '../api/types.ts';

import { BandBadge } from './ChangeRequestBadges.tsx';

/** The approval path each band enters (ADR-016: three bands; band 3 is the elevated route). */
const PATHS: Record<number, TranslationKey> = {
  1: 'changeRequests.materiality.path.band1',
  2: 'changeRequests.materiality.path.band2',
  3: 'changeRequests.materiality.path.band3',
};

type StatedImpacts = Pick<
  ChangeRequestDetail,
  'costImpactSar' | 'scheduleImpactDays' | 'scopeImpact' | 'isContractualObligation'
>;

interface MaterialityPanelProps {
  assessment: MaterialityAssessment;
  /** The impacts the classification was computed from: the request's as saved. */
  impacts: StatedImpacts;
  headingLevel: 2 | 3;
}

/**
 * A materiality classification as the server computed it (CHG-GP-08: the SPA never computes one): the resulting band and
 * the approval path it enters, each dimension's band beside the impact stated and the cumulative position since the
 * active baseline, and its basis. A preview (no evaluation id) says it is advisory; a recorded one, what it is pinned to.
 */
export function MaterialityPanel({
  assessment,
  impacts,
  headingLevel,
}: MaterialityPanelProps): ReactElement {
  const { t, formatDateTime } = useI18n();
  const headingId = useId();
  const Heading = headingLevel === 2 ? 'h2' : 'h3';
  const preview = assessment.evaluationId === null;
  const notApplicable = t('changeRequests.materiality.notApplicable');
  const band = (bandNo: number | null) =>
    bandNo === null ? notApplicable : <BandBadge bandNo={bandNo} />;
  const pinned = [
    t('changeRequests.materiality.pinnedVersion', {
      id: shortId(assessment.materialityConfigurationVersionId),
    }),
    ...(assessment.projectBaselineId === null
      ? []
      : [t('changeRequests.materiality.pinnedBaseline')]),
    ...(assessment.financialCommitmentId === null
      ? []
      : [t('changeRequests.materiality.pinnedBudget')]),
  ].join(t('changeRequests.materiality.listSeparator'));

  return (
    <section
      className="section materiality"
      aria-labelledby={headingId}
      data-materiality={preview ? 'preview' : 'recorded'}
    >
      <Heading id={headingId}>
        {t(
          preview
            ? 'changeRequests.materiality.previewTitle'
            : 'changeRequests.materiality.recordedTitle',
        )}
      </Heading>
      <p className="materiality__result">
        <span>{t('changeRequests.materiality.resulting')}</span>{' '}
        <span data-field="resultingBand">
          <BandBadge bandNo={assessment.resultingBandNo} />
        </span>
      </p>
      <p className="materiality__path">
        <strong>{t('changeRequests.materiality.pathLabel')}</strong>{' '}
        {t(PATHS[assessment.resultingBandNo] ?? 'changeRequests.materiality.path.band3')}
      </p>
      <TableContainer caption={t('changeRequests.materiality.caption')}>
        <thead>
          <tr>
            <th scope="col">{t('changeRequests.materiality.dimension')}</th>
            <th scope="col">{t('changeRequests.materiality.bandColumn')}</th>
            <th scope="col">{t('changeRequests.materiality.thisRequest')}</th>
            <th scope="col">{t('changeRequests.materiality.cumulative')}</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <th scope="row">{t('changeRequests.fields.costImpactSar')}</th>
            <td>{band(assessment.costBandNo)}</td>
            <td>
              {impacts.costImpactSar === null ? (
                t('changeRequests.impacts.none')
              ) : (
                <span dir="ltr">
                  {t('changeRequests.impacts.sar', { amount: formatSar(impacts.costImpactSar) })}
                </span>
              )}
            </td>
            <td>
              <span dir="ltr">
                {t('changeRequests.impacts.sar', {
                  amount: formatSar(assessment.cumulativeCostImpactSar),
                })}
              </span>
            </td>
          </tr>
          <tr>
            <th scope="row">{t('changeRequests.fields.scheduleImpactDays')}</th>
            <td>{band(assessment.scheduleBandNo)}</td>
            <td>
              {impacts.scheduleImpactDays === null
                ? t('changeRequests.impacts.none')
                : t('changeRequests.impacts.days', { days: impacts.scheduleImpactDays })}
            </td>
            <td>
              {t('changeRequests.impacts.days', { days: assessment.cumulativeScheduleImpactDays })}
            </td>
          </tr>
          <tr>
            <th scope="row">{t('changeRequests.fields.scopeImpact')}</th>
            <td>{band(assessment.scopeBandNo)}</td>
            <td>
              {impacts.scopeImpact === null
                ? t('changeRequests.impacts.none')
                : t('changeRequests.impacts.stated')}
            </td>
            <td>{t('changeRequests.materiality.notCumulative')}</td>
          </tr>
        </tbody>
      </TableContainer>
      {impacts.isContractualObligation && (
        <p className="form__note">{t('changeRequests.materiality.contractual')}</p>
      )}
      <p className="form__note">
        {preview
          ? t('changeRequests.materiality.previewBasis', {
              time: formatDateTime(assessment.evaluatedAt),
              pinned,
            })
          : t('changeRequests.materiality.recordedBasis', {
              time: formatDateTime(assessment.evaluatedAt),
              revision: assessment.revisionNo,
              pinned,
            })}
      </p>
    </section>
  );
}
