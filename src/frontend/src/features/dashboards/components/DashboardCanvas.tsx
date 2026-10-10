import { type CSSProperties, type ReactElement, useId } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type DashboardView, type DashboardWidgetResult } from '../api/types.ts';
import { drillPath } from '../drill.ts';
import { visibleWidgets } from '../layout.ts';

import { CoverageLine, ProjectionStatus, UnknownValue } from './ProjectionStatus.tsx';
import { ProgressTrend } from './ProgressTrend.tsx';
import { DistributionBody, MetricBody, ProgressBody } from './WidgetBodies.tsx';

// A dashboard's widgets, each its own named region. Every one says which semantic state it presents and how fresh
// and complete it is before it says anything else, so no number is ever unlabelled (acceptance criterion 2). A widget
// whose source cannot say draws its reason instead of a body. `data-semantic-state` and `data-freshness` name the
// projection metadata for tests and for a reviewer reading the DOM.

function WidgetBody({
  widget,
  title,
}: {
  widget: DashboardWidgetResult;
  title: string;
}): ReactElement {
  if (widget.data === null) {
    return <UnknownValue reason={widget.unknownReason ?? 'MISSING'} />;
  }
  const props = { projectionCode: widget.projectionCode, data: widget.data, title };
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

function WidgetCard({
  widget,
  projectId,
  headingLevel,
}: {
  widget: DashboardWidgetResult;
  projectId: string | null;
  headingLevel: 2 | 3;
}): ReactElement {
  const { t, language } = useI18n();
  const titleId = useId();
  const title = widget.title[language];
  const Heading = headingLevel === 2 ? 'h2' : 'h3';
  const drill = drillPath(widget.drillTargetScreenId, projectId);
  const stale = widget.projection.freshness === 'STALE';
  return (
    <section
      className={stale ? 'widget widget--stale' : 'widget'}
      aria-labelledby={titleId}
      data-widget={widget.code}
      data-semantic-state={widget.projection.semanticState}
      data-freshness={widget.projection.freshness}
      style={{ '--widget-span': widget.layoutSpan } as CSSProperties}
    >
      <div className="widget__header">
        <Heading id={titleId} className="widget__title">
          {title}
        </Heading>
        {drill !== null && (
          <Link className="widget__drill" to={drill}>
            {t('dashboards.widget.open')} <span className="visually-hidden">{title}</span>
          </Link>
        )}
      </div>
      <ProjectionStatus widget={widget} />
      {stale && (
        <p className="widget__explanation">
          {widget.coverageDetail === null
            ? t('dashboards.freshness.staleValue')
            : t('dashboards.freshness.staleAggregate')}
        </p>
      )}
      {widget.coverageDetail !== null && <CoverageLine coverage={widget.coverageDetail} />}
      <WidgetBody widget={widget} title={title} />
    </section>
  );
}

export function DashboardCanvas({
  view,
  headingLevel,
}: {
  view: DashboardView;
  headingLevel: 2 | 3;
}): ReactElement {
  return (
    <div className="dashboard__grid">
      {visibleWidgets(view.widgets).map((widget) => (
        <WidgetCard
          key={widget.code}
          widget={widget}
          projectId={view.projectId}
          headingLevel={headingLevel}
        />
      ))}
    </div>
  );
}
