import { type CSSProperties, type ReactElement, useState } from 'react';
import { Link } from 'react-router';

import { OnPlanBadge } from '@/features/projects/components/FollowUpBadges.tsx';
import { useProjectLookups } from '@/features/projects/useProjectLookups.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { sarExactLabel } from '@/shared/format/money.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';

import { type PortfolioFilters } from '../api/dashboardsApi.ts';
import { bucketColors, type ChartBucket } from '../charts.ts';
import { BarChart, DonutChart, GaugeChart, GroupedColumnChart } from '../components/Charts.tsx';
import { Coverage } from '../insights/figures.tsx';
import { type Measure, type VisualResult } from './designApi.ts';
import { drillPath, formatValue, groupText, isPercent } from './visuals.ts';

/** The slices a pie or donut draws: past this, the smallest go together as one. */
const SHARES = 6;

const SERIES_COLORS = [
  'var(--chart-category-1)',
  'var(--chart-category-2)',
  'var(--chart-category-3)',
];

/**
 * A visual's figures drawn as its type says. With `filters` (on the dashboard, not in the designer) each figure opens the records it counts, as
 * round 8 point 01 has every count do.
 */
export function VisualBody({
  visual,
  filters,
}: {
  visual: VisualResult;
  filters?: PortfolioFilters | undefined;
}): ReactElement {
  const { t, language } = useI18n();
  const lookups = useProjectLookups();
  const [active, setActive] = useState<string | null>(null);
  const groups = visual.groups ?? [];
  const measureText = (measure: Measure) => t(`dashboards.designer.measure.${measure}`);

  if (groups.length === 0 || groups.every((group) => group.projects === 0)) {
    return <p className="state">{t('dashboards.noData')}</p>;
  }

  const first = visual.measures[0] ?? 'PROJECTS';
  const drill = (measure: Measure, key: string | null) =>
    filters === undefined ? undefined : drillPath(measure, visual.groupBy, key, filters);
  const projectsHint = (count: number) => t('dashboards.drill.projects', { count });
  const text = (index: number) => {
    const group = groups[index];
    return group === undefined ? '' : groupText(visual.groupBy, group, lookups, t, language);
  };

  switch (visual.visualType) {
    case 'CARD': {
      const [whole] = groups;
      if (whole === undefined) {
        return <p className="state">{t('dashboards.noData')}</p>;
      }
      const second = visual.measures[1];
      return (
        <div className="visual-card">
          <span
            className="home__tile-value"
            dir="auto"
            title={
              first === 'VALUE_SAR' && whole.values[0] != null
                ? sarExactLabel(String(whole.values[0]), t)
                : undefined
            }
          >
            {formatValue(first, whole.values[0] ?? null, t)}
          </span>
          {first === 'GAP_POINTS' && <OnPlanBadge status={whole.onPlan} />}
          {second !== undefined && (
            <FigureText
              text={`${measureText(second)}: ${formatValue(second, whole.values[1] ?? null, t)}`}
              href={drill(second, null)}
            />
          )}
          {isPercent(first) && (
            <span className="insights__coverage">{t('dashboards.insights.simpleAverage')}</span>
          )}
          {isPercent(first) || first === 'GAP_POINTS' ? (
            <Coverage kind="progress" count={whole.measured} of={whole.projects} />
          ) : first === 'VALUE_SAR' ? (
            <Coverage kind="value" count={whole.valueEntered} of={whole.projects} />
          ) : null}
        </div>
      );
    }
    case 'GAUGE': {
      const value = groups[0]?.values[0] ?? null;
      return (
        <div className="visual-gauge">
          <GaugeChart
            rings={[{ key: first, value, color: 'var(--chart-category-1)' }]}
            lead={formatValue(first, value, t)}
          />
          <Coverage
            kind="progress"
            count={groups[0]?.measured ?? 0}
            of={groups[0]?.projects ?? 0}
          />
        </div>
      );
    }
    case 'BAR':
      return (
        <BarChart
          buckets={groups.map((group, index) => ({
            key: group.key ?? 'none',
            count: Math.max(0, group.values[0] ?? 0),
            value: formatValue(first, group.values[0] ?? null, t),
            text: text(index),
            color: 'var(--chart-single)',
            href: drill(first, group.key),
            hint: projectsHint(group.projects),
          }))}
          active={active}
          onActive={setActive}
        />
      );
    case 'PIE':
    case 'DONUT': {
      // A share of a whole: the five largest groups, then the rest together as "Other", so six fills tell them apart.
      const shares = groups
        .map((group, index) => ({
          key: group.key ?? 'none',
          count: Math.max(0, group.values[0] ?? 0),
          text: text(index),
          href: drill(first, group.key),
          hint: projectsHint(group.projects),
        }))
        .filter((share) => share.count > 0)
        .sort((a, b) => b.count - a.count);
      const shown =
        shares.length > SHARES
          ? [
              ...shares.slice(0, SHARES - 1),
              {
                key: 'other',
                count: shares.slice(SHARES - 1).reduce((sum, share) => sum + share.count, 0),
                text: t('dashboards.designer.other', { count: shares.length - SHARES + 1 }),
                href: undefined,
                hint: undefined,
              },
            ]
          : shares;
      const colors = bucketColors(
        shown.map(() => 'neutral'),
        false,
      );
      const buckets: ChartBucket[] = shown.map((share, index) => ({
        ...share,
        value: formatValue(first, share.count, t),
        color: colors[index] ?? 'var(--chart-single)',
      }));
      return (
        <div className="distribution distribution--donut">
          <DonutChart
            buckets={buckets}
            active={active}
            onActive={setActive}
            totalLabel={measureText(first)}
            pie={visual.visualType === 'PIE'}
          />
          <ul className="grouped-columns__legend">
            {buckets.map((bucket) => {
              const words = (
                <>
                  <span
                    className="distribution__key"
                    style={{ '--chart-color': bucket.color } as CSSProperties}
                  />
                  {`${bucket.text} (${bucket.value ?? String(bucket.count)})`}
                </>
              );
              return (
                <li key={bucket.key}>
                  {bucket.href === undefined ? (
                    words
                  ) : (
                    <Link className="drill-link" to={bucket.href} title={bucket.hint}>
                      {words}
                    </Link>
                  )}
                </li>
              );
            })}
          </ul>
        </div>
      );
    }
    case 'COLUMN': {
      const percent = visual.measures.every(isPercent);
      const largest = Math.max(
        1,
        ...groups.flatMap((group) => group.values.map((value) => value ?? 0)),
      );
      return (
        <GroupedColumnChart
          groups={groups.map((group, index) => ({
            key: group.key ?? 'none',
            text: text(index),
            values: group.values,
            href: drill(first, group.key),
            hint: projectsHint(group.projects),
          }))}
          series={visual.measures.map((measure, index) => ({
            key: measure,
            text: measureText(measure),
            color: SERIES_COLORS[index] ?? 'var(--chart-single)',
          }))}
          missing={t('dashboards.insights.notAvailable')}
          scale={percent ? 100 : largest}
          format={(value, series) => formatValue(visual.measures[series] ?? first, value, t)}
        />
      );
    }
    case 'TABLE':
      return (
        <TableContainer caption={visual.title[language]}>
          <table className="table">
            <caption className="visually-hidden">{visual.title[language]}</caption>
            <thead>
              <tr>
                {visual.groupBy !== 'NONE' && (
                  <th scope="col">{t(`dashboards.designer.groupBy.${visual.groupBy}`)}</th>
                )}
                {visual.measures.map((measure) => (
                  <th key={measure} scope="col">
                    {measureText(measure)}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {groups.map((group, index) => (
                <tr key={group.key ?? 'none'}>
                  {visual.groupBy !== 'NONE' && (
                    <th scope="row">
                      <FigureText text={text(index)} href={drill('PROJECTS', group.key)} />
                    </th>
                  )}
                  {visual.measures.map((measure, column) => (
                    <td key={measure} dir="auto">
                      {formatValue(measure, group.values[column] ?? null, t)}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </TableContainer>
      );
  }
}

/** A figure's words, as a link to its records when it has one. */
function FigureText({ text, href }: { text: string; href: string | undefined }): ReactElement {
  return href === undefined ? (
    <span className="insights__coverage">{text}</span>
  ) : (
    <Link className="insights__coverage drill-link" to={href}>
      {text}
    </Link>
  );
}
