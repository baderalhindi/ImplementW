import { type CSSProperties, type ReactElement } from 'react';

import { Marks } from '../components/Charts.tsx';
import { type Scale } from './axes.ts';
import { type VisualAxes } from './designApi.ts';

// A designed column or bar chart with real axes (docs/architecture/dashboard-designer-axes.md): the value axis in round ticks with gridlines
// and the values' format, the category axis with the groups' words, each axis titled beside it and shown or hidden. A start or end inside the
// figures clips the columns at the edge; each value's own figure is still written by it. Both mirror in Arabic (the first group at the
// right). As the dashboard's other charts, the picture is hidden from assistive technology unless its groups link to their records.

/** What both charts take for their axes: the scale, how a tick reads, and the axes' settings with their titles in the reader's language. */
export interface AxisProps {
  scale: Scale;
  tick: (value: number) => string;
  axes: VisualAxes;
  categoryTitle: string | null;
  valueTitle: string | null;
}

/** A value's place on the scale, in percent of the plot from its low end. */
const percentOf = (scale: Scale, value: number) => `${String(scale.at(value) * 100)}%`;

/** From the base to the value, clipped at the ends: where a bar starts and how long it is, in percent of the plot. */
function span(scale: Scale, value: number): { start: string; size: string; below: boolean } {
  const from = scale.at(scale.base);
  const to = scale.at(value);
  return {
    start: `${String(Math.min(from, to) * 100)}%`,
    size: `${String(Math.abs(to - from) * 100)}%`,
    below: value < scale.base,
  };
}

/** The longest tick's characters: the width the value axis keeps for its labels. */
const tickChars = (props: AxisProps) =>
  Math.max(1, ...props.scale.ticks.map((value) => props.tick(value).length));

/** The plot's lines: gridlines at the ticks, zero where the scale crosses it, and each shown axis's line. */
function Grid({
  props,
  direction,
  style,
}: {
  props: AxisProps;
  direction: 'block' | 'inline';
  style?: CSSProperties;
}): ReactElement {
  const { scale, axes } = props;
  const place = (value: number): CSSProperties =>
    direction === 'block'
      ? { insetBlockEnd: percentOf(scale, value) }
      : { insetInlineStart: percentOf(scale, value) };
  return (
    <div
      className={`axis-chart__grid axis-chart__grid--${direction}${
        axes.category.show ? ' has-category-line' : ''
      }${axes.value.show ? ' has-value-line' : ''}`}
      style={style}
      aria-hidden="true"
    >
      {axes.value.gridlines &&
        scale.ticks.map((value) => (
          <span key={value} className="axis-chart__gridline" style={place(value)} />
        ))}
      {scale.low < 0 && scale.high > 0 && (
        <span className="axis-chart__gridline axis-chart__zero" style={place(0)} />
      )}
    </div>
  );
}

/** The value axis's tick labels; none when the axis is hidden. */
function Ticks({
  props,
  direction,
  style,
}: {
  props: AxisProps;
  direction: 'block' | 'inline';
  style?: CSSProperties;
}): ReactElement | null {
  const { scale, tick, axes } = props;
  if (!axes.value.show) {
    return null;
  }
  return (
    <div
      className={`axis-chart__ticks axis-chart__ticks--${direction}`}
      style={{ ...style, '--tick-chars': String(tickChars(props)) } as CSSProperties}
      aria-hidden="true"
    >
      {scale.ticks.map((value) => (
        <span
          key={value}
          className="axis-chart__tick"
          style={
            direction === 'block'
              ? { insetBlockEnd: percentOf(scale, value) }
              : { insetInlineStart: percentOf(scale, value) }
          }
        >
          <span dir="ltr">{tick(value)}</span>
        </span>
      ))}
    </div>
  );
}

/** An axis's title, when it is shown and has one. */
function AxisTitle({
  text,
  shown,
  className,
  style,
}: {
  text: string | null;
  shown: boolean;
  className: string;
  style?: CSSProperties;
}): ReactElement | null {
  return text === null || !shown ? null : (
    <span className={`axis-chart__title ${className}`} style={style} dir="auto">
      {text}
    </span>
  );
}

/** A group's words under its columns (or beside its bar); kept for a screen reader, unseen, when the category axis is hidden. */
function GroupText({
  text,
  shown,
  className,
}: {
  text: string;
  shown: boolean;
  className: string;
}): ReactElement {
  return <span className={shown ? className : 'visually-hidden'}>{text}</span>;
}

/** Values side by side for each group, on the value axis at the start side and the groups along the bottom. */
export function AxisColumnChart({
  groups,
  series,
  missing,
  format,
  ...props
}: AxisProps & {
  groups: {
    key: string;
    text: string;
    values: (number | null)[];
    href?: string | undefined;
    hint?: string | undefined;
  }[];
  series: { key: string; text: string; color: string }[];
  missing: string;
  /** Writes a value of the series at `index`. */
  format: (value: number, index: number) => string;
}): ReactElement {
  const { scale, axes } = props;
  const linked = groups.some((group) => group.href !== undefined);
  return (
    <div className="axis-chart" aria-hidden={linked ? undefined : true}>
      <ul className="grouped-columns__legend">
        {series.map((entry) => (
          <li key={entry.key}>
            <span
              className="distribution__key"
              style={{ '--chart-color': entry.color } as CSSProperties}
            />
            {entry.text}
          </li>
        ))}
      </ul>
      <div className="axis-chart__frame axis-chart__frame--column">
        <AxisTitle
          text={props.valueTitle}
          shown={axes.value.show}
          className="axis-chart__title--value"
        />
        <Ticks props={props} direction="block" />
        <Grid props={props} direction="block" />
        <div
          className="axis-chart__plot"
          style={{ '--groups': String(groups.length) } as CSSProperties}
        >
          {groups.map((group) => (
            <div key={group.key} className="axis-chart__group">
              <Marks href={group.href} hint={group.hint} className="axis-chart__link">
                <span className="axis-chart__columns">
                  {group.values.every((value) => value === null) ? (
                    // A group with no figure at all says so once, across its columns.
                    <span className="axis-chart__missing">{missing}</span>
                  ) : (
                    series.map((entry, index) => {
                      const value = group.values[index] ?? null;
                      const bar = value === null ? null : span(scale, value);
                      return (
                        <span key={entry.key} className="axis-chart__track">
                          {bar !== null && (
                            <span
                              className={`axis-chart__column${bar.below ? ' is-below' : ''}`}
                              style={
                                {
                                  insetBlockEnd: bar.start,
                                  blockSize: bar.size,
                                  '--chart-color': entry.color,
                                } as CSSProperties
                              }
                            />
                          )}
                          {/* A figure missing beside others is a dash, named in its tooltip; a narrow column has no room for the words. */}
                          <span
                            className="axis-chart__value"
                            dir="ltr"
                            title={value === null ? missing : undefined}
                            style={{
                              insetBlockEnd:
                                bar === null || bar.below
                                  ? percentOf(scale, scale.base)
                                  : percentOf(scale, value ?? scale.base),
                            }}
                          >
                            {value === null ? '–' : format(value, index)}
                          </span>
                        </span>
                      );
                    })
                  )}
                </span>
                <GroupText
                  text={group.text}
                  shown={axes.category.show}
                  className="column-chart__label axis-chart__category"
                />
              </Marks>
            </div>
          ))}
        </div>
        <AxisTitle
          text={props.categoryTitle}
          shown={axes.category.show}
          className="axis-chart__title--category"
        />
      </div>
    </div>
  );
}

/** One bar per group, the groups down the start side and the value axis along the bottom; each value's figure at the end of its row. */
export function AxisBarChart({
  buckets,
  active,
  onActive,
  ...props
}: AxisProps & {
  buckets: {
    key: string;
    value: number | null;
    /** The value's words, "not available" when it has none. */
    figure: string;
    text: string;
    color: string;
    href?: string | undefined;
    hint?: string | undefined;
  }[];
  active: string | null;
  onActive: (key: string | null) => void;
}): ReactElement {
  const { scale, axes } = props;
  const linked = buckets.some((bucket) => bucket.href !== undefined);
  const rows = { gridRow: `1 / ${String(buckets.length + 1)}` };
  return (
    <div
      className="axis-chart__frame axis-chart__frame--bar"
      aria-hidden={linked ? undefined : true}
    >
      <AxisTitle
        text={props.categoryTitle}
        shown={axes.category.show}
        className="axis-chart__title--category"
        style={rows}
      />
      <Grid props={props} direction="inline" style={rows} />
      {buckets.map((bucket, index) => {
        const bar = bucket.value === null ? null : span(scale, bucket.value);
        return (
          <span
            key={bucket.key}
            className={`axis-chart__row${
              active === null ? '' : active === bucket.key ? ' is-active' : ' is-dimmed'
            }`}
            style={{ gridRow: String(index + 1) }}
            onPointerEnter={() => {
              onActive(bucket.key);
            }}
            onPointerLeave={() => {
              onActive(null);
            }}
          >
            <Marks href={bucket.href} hint={bucket.hint} className="axis-chart__link">
              <GroupText
                text={bucket.text}
                shown={axes.category.show}
                className="bars__label axis-chart__category"
              />
              <span className="axis-chart__bar-track">
                {bar !== null && (
                  <span
                    className="widget__bar axis-chart__bar"
                    style={
                      {
                        insetInlineStart: bar.start,
                        inlineSize: bar.size,
                        '--chart-color': bucket.color,
                      } as CSSProperties
                    }
                  />
                )}
              </span>
              <span className="bars__value" dir="ltr">
                {bucket.figure}
              </span>
            </Marks>
          </span>
        );
      })}
      <Ticks props={props} direction="inline" style={{ gridRow: String(buckets.length + 1) }} />
      <AxisTitle
        text={props.valueTitle}
        shown={axes.value.show}
        className="axis-chart__title--value"
        style={{ gridRow: String(buckets.length + 2) }}
      />
    </div>
  );
}
