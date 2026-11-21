import { type CSSProperties, type ReactElement, useId } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { NoteList } from '@/shared/ui/NoteList.tsx';

import { type PortfolioFilters } from '../api/dashboardsApi.ts';
import { type DashboardView, type DashboardWidgetResult } from '../api/types.ts';
import { widgetValuePath } from '../drillLinks.ts';
import { drillPath } from '../drill.ts';
import { drawnSpan, filledSpans, visibleWidgets } from '../layout.ts';

import {
  CoverageLine,
  ProjectionOrigin,
  ProjectionStatus,
  UnknownValue,
} from './ProjectionStatus.tsx';
import { ProgressTrend } from './ProgressTrend.tsx';
import { DistributionBody, MetricBody, ProgressBody } from './WidgetBodies.tsx';

// A dashboard's widgets, each its own named region. Every one says which semantic state it presents and how fresh
// and complete it is before it says anything else, so no number is ever unlabelled (acceptance criterion 2). A widget
// whose source cannot say draws its reason instead of a body. `data-semantic-state` and `data-freshness` name the
// projection metadata for tests and for a reviewer reading the DOM.

function WidgetBody({
  widget,
  title,
  filters,
  projectId,
}: {
  widget: DashboardWidgetResult;
  title: string;
  filters: PortfolioFilters;
  projectId: string | null;
}): ReactElement {
  const { t } = useI18n();
  if (widget.data === null) {
    return <UnknownValue reason={widget.unknownReason ?? 'MISSING'} />;
  }
  // A value opens its records only where the widget may drill at all (a restricted widget has no drill target).
  const linkOf =
    widget.drillTargetScreenId === null
      ? undefined
      : (key: string, count: number) => {
          const path = widgetValuePath(widget.projectionCode, key, filters, projectId);
          return path === null
            ? null
            : { href: path.href, hint: t(`dashboards.drill.${path.kind}`, { count }) };
        };
  const props = {
    projectionCode: widget.projectionCode,
    widgetType: widget.widgetType,
    data: widget.data,
    title,
    linkOf,
  };
  switch (widget.widgetType) {
    case 'METRIC_CARD':
      return <MetricBody {...props} />;
    case 'PROGRESS_INDICATOR':
      return <ProgressBody {...props} />;
    case 'LINE_TREND':
      return <ProgressTrend {...props} />;
    case 'STATUS_DISTRIBUTION':
    case 'BAR_COLUMN':
    case 'DONUT_PIE':
      return <DistributionBody {...props} />;
  }
}

/**
 * Where a widget card is drawn: on a dashboard's widget grid, with the columns it takes in the drawn order (`filledSpans`) and its own heading;
 * or `visual`, filling a designed visual (19 Nov 2026) whose frame carries the title, so the card draws none.
 */
export type WidgetPlacement = { span: number; headingLevel: 2 | 3 } | 'visual';

export function WidgetCard({
  widget,
  placement,
  projectId,
  filters,
}: {
  widget: DashboardWidgetResult;
  placement: WidgetPlacement;
  projectId: string | null;
  filters: PortfolioFilters;
}): ReactElement {
  const { t, language } = useI18n();
  const titleId = useId();
  const title = widget.title[language];
  const drill = drillPath(widget.drillTargetScreenId, projectId);
  const stale = widget.projection.freshness === 'STALE';
  const status = (
    <>
      <ProjectionStatus widget={widget} />
      {drill !== null && (
        <Link className="widget__drill" to={drill}>
          {t('dashboards.widget.open')} <span className="visually-hidden">{title}</span>
        </Link>
      )}
    </>
  );
  const content = (
    <>
      {/* Always present, so the cards of a row keep the same four rows (header, notice, body, footer). */}
      <div className="widget__notice">
        {stale && (
          <p className="widget__explanation">
            {widget.coverageDetail === null
              ? t('dashboards.freshness.staleValue')
              : t('dashboards.freshness.staleAggregate')}
          </p>
        )}
      </div>
      <WidgetBody widget={widget} title={title} filters={filters} projectId={projectId} />
      <NoteList as="div" className="widget__footer">
        {widget.coverageDetail !== null && <CoverageLine coverage={widget.coverageDetail} />}
        <ProjectionOrigin widget={widget} />
      </NoteList>
    </>
  );
  const data = {
    'data-widget': widget.code,
    'data-widget-type': widget.widgetType,
    'data-semantic-state': widget.projection.semanticState,
    'data-freshness': widget.projection.freshness,
  };
  if (placement === 'visual') {
    // The visual's frame is the named region; the card is its body.
    return (
      <div className="widget widget--in-visual" {...data}>
        <div className="widget__header">{status}</div>
        {content}
      </div>
    );
  }
  const Heading = placement.headingLevel === 2 ? 'h2' : 'h3';
  return (
    <section
      className="widget"
      aria-labelledby={titleId}
      {...data}
      style={{ '--widget-span': placement.span } as CSSProperties}
    >
      <div className="widget__header">
        <Heading id={titleId} className="widget__title">
          {title}
        </Heading>
        {status}
      </div>
      {content}
    </section>
  );
}

export function DashboardCanvas({
  view,
  headingLevel,
  filters = {},
}: {
  view: DashboardView;
  headingLevel: 2 | 3;
  /** The dashboard's filters, which a value's link carries to the list it opens. */
  filters?: PortfolioFilters;
}): ReactElement {
  const widgets = visibleWidgets(view.widgets);
  const spans = filledSpans(widgets.map(drawnSpan));
  return (
    <div className="dashboard__grid">
      {widgets.map((widget, index) => (
        <WidgetCard
          key={widget.code}
          widget={widget}
          placement={{ span: spans[index] ?? widget.layoutSpan, headingLevel }}
          projectId={view.projectId}
          filters={filters}
        />
      ))}
    </div>
  );
}
