import { type CSSProperties, type ReactElement, useState } from 'react';
import { Link } from 'react-router';

import { OnPlanBadge } from '@/features/projects/components/FollowUpBadges.tsx';
import { useProjectLookups } from '@/features/projects/useProjectLookups.ts';
import { formatPoints } from '@/features/projects/presentation.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { sarExactLabel } from '@/shared/format/money.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';

import { type PortfolioFilters } from '../api/dashboardsApi.ts';
import { bucketColors, type ChartBucket } from '../charts.ts';
import { DonutChart, GaugeChart } from '../components/Charts.tsx';
import { drawnAxes, valueScale } from './axes.ts';
import { AxisBarChart, AxisColumnChart, type AxisProps } from './AxisCharts.tsx';
import { LineChart } from './LineChart.tsx';
import { Coverage } from '../insights/figures.tsx';
import { isStatusScale, labelOfValue } from '../presentation.ts';
import {
  type FieldAggregation,
  type FieldDescriptor,
  type FieldType,
  type VisualGroup,
  type VisualResult,
} from './designApi.ts';
import {
  fieldName,
  fieldParts,
  fieldValueText,
  formatFieldValue,
  isDateType,
  isPercentValue,
  valueTypeOf,
} from './fields.ts';
import { drillPath, fieldGroupText, formatValue, groupText, isPercent } from './visuals.ts';
import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';
import { useCan } from '@/features/identity-access/permissions/permissions.ts';

const ON_PLAN_TONE: Record<string, StatusTone> = {
  ON_TRACK: 'positive',
  LATE: 'warning',
  STALLED: 'negative',
};

/** The slices a pie or donut draws: past this, the smallest go together as one. */
const SHARES = 6;

/** One value a visual draws: a Portfolio figure or a field's calculation, with its words, its format and the records it opens. */
interface Series {
  key: string;
  text: string;
  format: (value: number | null) => string;
  percent: boolean;
  /** What its figures are, so a value axis writes its ticks as they read where every series reads alike: PERCENT, SAR, DATE, a count... */
  unit: string;
  /** The figure's exact amount, for a card's tooltip; undefined where the short form is exact. */
  exact: (value: number) => string | undefined;
  drill: (key: string | null) => string | undefined;
}

const SERIES_COLORS = [
  'var(--chart-category-1)',
  'var(--chart-category-2)',
  'var(--chart-category-3)',
];

/**
 * A visual's figures drawn as its type says. With `filters` (on the dashboard, not in the designer) each figure opens the records it counts, as
 * round 8 point 01 has every count do; a field's value opens nothing, as no list filters by it. A field value reads by the type the API
 * returns with it (`valueTypes`); `fields`, the designer's catalogue, stands in where none came.
 */
export function VisualBody({
  visual,
  filters,
  fields = [],
}: {
  visual: VisualResult;
  filters?: PortfolioFilters | undefined;
  fields?: readonly FieldDescriptor[] | undefined;
}): ReactElement {
  const { t, language } = useI18n();
  const lookups = useProjectLookups();
  const [active, setActive] = useState<string | null>(null);
  const groups = visual.groups ?? [];

  if (groups.length === 0 || groups.every((group) => group.projects === 0)) {
    return <p className="state">{t('dashboards.noData')}</p>;
  }

  const fieldValues = visual.values ?? [];
  const onFields = fieldValues.length > 0;
  const groupField = visual.groupField ?? null;
  const series: Series[] = onFields
    ? fieldValues.map((value, index) => {
        const type = valueTypeOf(visual, index, fields);
        return {
          key: `${value.field}:${value.aggregation}`,
          text: fieldValueText(value, language),
          format: (figure: number | null) =>
            formatFieldValue(value.aggregation, type, figure, t, language),
          percent: isPercentValue(value.aggregation, type),
          unit: fieldUnit(value.aggregation, type),
          exact: (figure: number) =>
            value.aggregation !== 'COUNT' && type === 'SAR'
              ? sarExactLabel(String(figure), t)
              : undefined,
          drill: () => undefined,
        };
      })
    : visual.measures.map((measure) => ({
        key: measure,
        text: t(`dashboards.designer.measure.${measure}`),
        format: (figure) => formatValue(measure, figure, t),
        percent: isPercent(measure),
        unit: isPercent(measure) ? 'PERCENT' : measure,
        exact: (figure) => (measure === 'VALUE_SAR' ? sarExactLabel(String(figure), t) : undefined),
        drill: (key) =>
          filters === undefined ? undefined : drillPath(measure, visual.groupBy, key, filters),
      }));
  const first = series[0];
  if (first === undefined) {
    return <p className="state">{t('dashboards.noData')}</p>;
  }
  const measure = onFields ? null : (visual.measures[0] ?? null);
  const projectsHint = (count: number) => t('dashboards.drill.projects', { count });
  const text = (index: number) => {
    const group = groups[index];
    if (group === undefined) {
      return '';
    }
    return groupField === null
      ? groupText(visual.groupBy, group, lookups, t, language)
      : fieldGroupText(groupField, visual.groupDateUnit ?? null, group, t, language);
  };
  const rowDrill = (key: string | null) =>
    onFields || filters === undefined
      ? undefined
      : drillPath('PROJECTS', visual.groupBy, key, filters);
  const groupHeading =
    groupField === null
      ? visual.groupBy === 'NONE'
        ? null
        : t(`dashboards.designer.groupBy.${visual.groupBy}`)
      : fieldName(groupField, language);

  /** The axes of a bar, column or line chart over `values`: a percentage from 0 to 100, a date from its earliest, any other from zero. */
  const axisProps = (values: number[]): AxisProps => {
    const axes = drawnAxes(visual);
    const alike = series.every((one) => one.unit === first.unit);
    return {
      axes,
      scale: valueScale(values, axes.value, {
        percent: series.every((one) => one.percent),
        zero: !(alike && first.unit === 'DATE'),
      }),
      tick: (value) => (alike ? first.format(value) : formatPoints(value)),
      categoryTitle: axes.category.title?.[language] ?? null,
      valueTitle: axes.value.title?.[language] ?? null,
    };
  };
  const figuresOf = (rows: VisualGroup[]) =>
    rows.flatMap((group) => group.values.filter((value): value is number => value !== null));

  switch (visual.visualType) {
    case 'CARD': {
      const [whole] = groups;
      if (whole === undefined) {
        return <p className="state">{t('dashboards.noData')}</p>;
      }
      const second = series[1];
      const lead = whole.values[0] ?? null;
      return (
        <div className="visual-card">
          <span
            className="home__tile-value"
            dir="auto"
            title={lead === null ? undefined : first.exact(lead)}
          >
            {first.format(lead)}
          </span>
          {measure === 'GAP_POINTS' && <OnPlanBadge status={whole.onPlan} />}
          {second !== undefined && (
            <FigureText
              text={`${second.text}: ${second.format(whole.values[1] ?? null)}`}
              href={second.drill(null)}
            />
          )}
          {first.percent && (
            <span className="insights__coverage">{t('dashboards.insights.simpleAverage')}</span>
          )}
          {onFields ? (
            <FieldCoverage count={whole.measured} of={whole.projects} />
          ) : measure !== null && (isPercent(measure) || measure === 'GAP_POINTS') ? (
            <Coverage kind="progress" count={whole.measured} of={whole.projects} />
          ) : measure === 'VALUE_SAR' ? (
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
            rings={[{ key: first.key, value, color: 'var(--chart-category-1)' }]}
            lead={first.format(value)}
          />
          {onFields ? (
            <FieldCoverage count={groups[0]?.measured ?? 0} of={groups[0]?.projects ?? 0} />
          ) : (
            <Coverage
              kind="progress"
              count={groups[0]?.measured ?? 0}
              of={groups[0]?.projects ?? 0}
            />
          )}
        </div>
      );
    }
    case 'BAR': {
      const withoutStage =
        visual.groupBy === 'STAGE'
          ? (groups.find((group) => group.key === null)?.projects ?? 0)
          : 0;
      return (
        <>
          <AxisBarChart
            {...axisProps(
              groups.flatMap((group) =>
                (group.values[0] ?? null) === null ? [] : [group.values[0] ?? 0],
              ),
            )}
            buckets={groups.map((group, index) => ({
              key: group.key ?? 'none',
              value: group.values[0] ?? null,
              figure: first.format(group.values[0] ?? null),
              text: text(index),
              color: 'var(--chart-single)',
              href: first.drill(group.key),
              hint: projectsHint(group.projects),
            }))}
            active={active}
            onActive={setActive}
          />
          {withoutStage > 0 && filters !== undefined && (
            // Review round 8, point 09: projects with no stage yet have no plan set up; the chart says where to set it.
            <StagePlanHint
              withoutStage={withoutStage}
              withStage={groups.reduce(
                (sum, group) => sum + (group.key === null ? 0 : group.projects),
                0,
              )}
              href={drillPath('PROJECTS', 'STAGE', null, filters) ?? ''}
            />
          )}
        </>
      );
    }
    case 'PIE':
    case 'DONUT': {
      // A share of a whole: the five largest groups, then the rest together as "Other", so six fills tell them apart.
      const shares = groups
        .map((group, index) => ({
          key: group.key ?? 'none',
          count: Math.max(0, group.values[0] ?? 0),
          text: text(index),
          href: first.drill(group.key),
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
      // The on-plan indicator and a status field on a RAG-like scale keep their own colours; any other grouping takes categorical fills.
      const colors =
        visual.groupBy === 'ON_PLAN'
          ? bucketColors(
              shown.map((share) => ON_PLAN_TONE[share.key] ?? 'neutral'),
              true,
            )
          : groupField !== null && isStatusScale(fieldParts(groupField).projectionCode)
            ? bucketColors(
                shown.map((share) =>
                  share.key === 'none' || share.key === 'other'
                    ? 'neutral'
                    : labelOfValue(
                        fieldParts(groupField).projectionCode,
                        share.key,
                        null,
                        language,
                        t,
                      ).tone,
                ),
                true,
              )
            : bucketColors(
                shown.map(() => 'neutral'),
                false,
              );
      const buckets: ChartBucket[] = shown.map((share, index) => ({
        ...share,
        value: first.format(share.count),
        color: colors[index] ?? 'var(--chart-single)',
      }));
      return (
        <div className="distribution distribution--donut">
          <DonutChart
            buckets={buckets}
            active={active}
            onActive={setActive}
            totalLabel={first.text}
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
    case 'COLUMN':
      return (
        <AxisColumnChart
          {...axisProps(figuresOf(groups))}
          groups={groups.map((group, index) => ({
            key: group.key ?? 'none',
            text: text(index),
            values: group.values,
            href: first.drill(group.key),
            hint: projectsHint(group.projects),
          }))}
          series={series.map((one, index) => ({
            key: one.key,
            text: one.text,
            color: SERIES_COLORS[index] ?? 'var(--chart-single)',
          }))}
          missing={t('dashboards.insights.notAvailable')}
          format={(value, index) => (series[index] ?? first).format(value)}
        />
      );
    case 'SECTION':
    case 'CONTAINER':
      // A section is drawn from the dashboard's own sources (SectionBody), a container as a frame round its visuals: never from figures.
      return <p className="state">{t('dashboards.noData')}</p>;
    case 'LINE': {
      // The groups along the x axis in key order (a period runs left to right); the projects with no value have no place on it and are in the table.
      const points = groups
        .map((group, index) => ({ group, text: text(index) }))
        .filter(
          (point): point is { group: VisualGroup & { key: string }; text: string } =>
            point.group.key !== null,
        )
        .sort((a, b) => a.group.key.localeCompare(b.group.key));
      return (
        <>
          <LineChart
            {...axisProps(figuresOf(points.map((point) => point.group)))}
            title={visual.title[language]}
            points={points.map((point) => ({
              key: point.group.key,
              text: point.text,
              values: point.group.values,
            }))}
            series={series.map((one, index) => ({
              key: one.key,
              text: one.text,
              color: SERIES_COLORS[index] ?? 'var(--chart-single)',
            }))}
            format={(value, index) => (series[index] ?? first).format(value)}
          />
          <details className="widget__data">
            <summary>{t('dashboards.trend.showTable')}</summary>
            <ValuesTable
              caption={visual.title[language]}
              groupHeading={groupHeading}
              groups={groups}
              series={series}
              text={text}
              rowDrill={rowDrill}
            />
          </details>
        </>
      );
    }
    case 'TABLE':
      return (
        <ValuesTable
          caption={visual.title[language]}
          groupHeading={groupHeading}
          groups={groups}
          series={series}
          text={text}
          rowDrill={rowDrill}
        />
      );
  }
}

/** What a field value's figures are: a count, a share in %, a date, or the field's own type. */
function fieldUnit(aggregation: FieldAggregation, type: FieldType): string {
  if (aggregation === 'COUNT' || aggregation === 'COUNT_TRUE') {
    return 'COUNT';
  }
  if (aggregation === 'SHARE_TRUE') {
    return 'PERCENT';
  }
  return isDateType(type) ? 'DATE' : type;
}

/** A visual's figures as a table: a row per group, a column per value; a table visual's body and a line's accessible fallback. */
function ValuesTable({
  caption,
  groupHeading,
  groups,
  series,
  text,
  rowDrill,
}: {
  caption: string;
  groupHeading: string | null;
  groups: VisualGroup[];
  series: Series[];
  text: (index: number) => string;
  rowDrill: (key: string | null) => string | undefined;
}): ReactElement {
  return (
    <TableContainer caption={caption}>
      <thead>
        <tr>
          {groupHeading !== null && <th scope="col">{groupHeading}</th>}
          {series.map((one) => (
            <th key={one.key} scope="col">
              {one.text}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {groups.map((group, index) => (
          <tr key={group.key ?? 'none'}>
            {groupHeading !== null && (
              <th scope="row">
                <FigureText text={text(index)} href={rowDrill(group.key)} />
              </th>
            )}
            {series.map((one, column) => (
              <td key={one.key} dir="auto">
                {one.format(group.values[column] ?? null)}
              </td>
            ))}
          </tr>
        ))}
      </tbody>
    </TableContainer>
  );
}

/** How many of a field visual's projects have its first value revealed (the others are unknown, restricted or masked). */
function FieldCoverage({ count, of }: { count: number; of: number }): ReactElement {
  const { t } = useI18n();
  return (
    <span className="insights__coverage">
      {t('dashboards.designer.fieldCoverage', { count, total: of })}
    </span>
  );
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

/** Round 8, point 09: where projects have no stage yet, the stage chart says so and offers the way to set their plans up. */
function StagePlanHint({
  withoutStage,
  withStage,
  href,
}: {
  withoutStage: number;
  withStage: number;
  href: string;
}): ReactElement {
  const { t } = useI18n();
  const setsStagePlans = useCan('PROJECT_REGISTER', 'PROGRESS_SUBMIT');
  return (
    <div className="insights__empty-hint" role="note">
      <p>
        {withStage === 0
          ? t('dashboards.insights.stagePlanNone')
          : t('dashboards.insights.stagePlanSome', { count: withoutStage })}
      </p>
      <Link className="button" to={href}>
        {t(
          setsStagePlans
            ? 'dashboards.insights.setUpStagePlans'
            : 'dashboards.insights.viewWithoutStagePlan',
        )}
      </Link>
    </div>
  );
}
