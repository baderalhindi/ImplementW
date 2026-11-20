import { type CSSProperties, type ReactElement, useCallback, useId } from 'react';
import { Link } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { EmptyState, LoadingState } from '@/shared/ui/States.tsx';

import { type PortfolioFilters } from '../api/dashboardsApi.ts';
import { type DashboardView } from '../api/types.ts';
import {
  projectCountOf,
  usePortfolioInsights,
  useShowsCompletion,
} from '../insights/insightSections.ts';
import { emptyPortfolioReason, hasNoData } from '../presentation.ts';
import { childrenOf, innerGridPlace, isContainer, withoutEmptyInnerRows } from './containers.ts';
import { designApi, type Measure, type VisualGroup, type VisualResult } from './designApi.ts';
import { VisualBodyBox } from './FitToBox.tsx';
import { drawsFields } from './fields.ts';
import { hiddenColumnsOf } from './sectionColumns.ts';
import { SectionBody } from './SectionBody.tsx';
import { sectionShown, type SectionSources } from './sections.ts';
import {
  countsFollowUp,
  drillPath,
  gridPlace,
  gridRows,
  measureSource,
  packed,
  placeKey,
  rowsOf,
  withoutEmptyRows,
} from './visuals.ts';
import { hasColumns } from './tableColumns.ts';
import { frameOf, isTextBox } from './textBox.ts';
import { TextBoxBody } from './TextBoxBody.tsx';
import { VisualBody } from './VisualBody.tsx';
import { hasWidgetsToSplit, splitWidgets } from './widgetVisuals.ts';

/** The words a card's or gauge's link has: how many records it opens, its figure's count for a figure that counts projects of a kind. */
function drillHint(
  measure: Measure,
  whole: VisualGroup | undefined,
  t: ReturnType<typeof useI18n>['t'],
): string {
  const figure = whole?.values[0] ?? 0;
  if (countsFollowUp(measure)) {
    return t('dashboards.drill.followUp', { count: figure });
  }
  return t('dashboards.drill.projects', {
    count: measureSource(measure) === 'ON_PLAN' ? figure : (whole?.projects ?? 0),
  });
}

/** One visual on the dashboard: its title over its figures. A card's or gauge's title opens the records its figure counts, as a headline card's did. */
function VisualFrame({
  visual,
  filters,
  place,
}: {
  visual: VisualResult;
  filters: PortfolioFilters;
  place: CSSProperties;
}): ReactElement {
  const { t, language } = useI18n();
  const title = visual.title[language];
  const whole = visual.groupBy === 'NONE' ? visual.groups?.[0] : undefined;
  // A field's value opens nothing: no list filters by it.
  const href =
    whole === undefined || visual.visualType === 'TABLE' || drawsFields(visual)
      ? undefined
      : drillPath(visual.measures[0] ?? 'PROJECTS', 'NONE', null, filters);
  return (
    <section
      className={href === undefined ? 'widget visual' : 'widget visual home__tile--linked'}
      style={place}
      data-visual={visual.visualType}
    >
      <h3 className="widget__title">
        {href === undefined ? (
          title
        ) : (
          <Link
            className="home__tile-link"
            to={href}
            title={drillHint(visual.measures[0] ?? 'PROJECTS', whole, t)}
          >
            {title}
          </Link>
        )}
      </h3>
      <VisualBodyBox visual={visual} className="visual__body">
        <VisualBody visual={visual} filters={filters} />
      </VisualBodyBox>
    </section>
  );
}

/** A figure the caller may not see at all, such as the value without the amounts permission: left out, as the value card always was. */
function withheld(visual: VisualResult): boolean {
  return (
    visual.measures.length > 0 &&
    visual.measures.every((measure) => measure === 'VALUE_SAR') &&
    (visual.groups ?? []).every((group) => group.values.every((value) => value === null))
  );
}

/** A section of the dashboard in its design's place, titled as the design names it. */
function SectionFrame({
  visual,
  sources,
  place,
}: {
  visual: VisualResult;
  sources: SectionSources;
  place: CSSProperties;
}): ReactElement | null {
  const { language } = useI18n();
  const titleId = useId();
  // A widget's visual is the region its card was, named by the visual's title.
  const isWidget = visual.section === 'DASHBOARD_WIDGET';
  return visual.section === undefined || visual.section === null ? null : (
    <section
      className={
        isWidget ? 'widget visual visual--section visual--widget' : 'widget visual visual--section'
      }
      style={place}
      aria-labelledby={isWidget ? titleId : undefined}
      data-visual="SECTION"
      data-section-code={visual.section}
    >
      <h3 id={titleId} className="widget__title">
        {visual.title[language]}
      </h3>
      <VisualBodyBox visual={visual} className="visual__body visual__body--section">
        <SectionBody
          section={visual.section}
          widgetCode={visual.widgetCode}
          hidden={hiddenColumnsOf(visual)}
          sources={sources}
        />
      </VisualBodyBox>
    </section>
  );
}

/**
 * A text box on the dashboard: its words only, with no title bar and no card round them, so it reads as text (its title is its accessible
 * name); longer than its visual, it scrolls.
 */
function TextFrame({
  visual,
  place,
}: {
  visual: VisualResult;
  place: CSSProperties;
}): ReactElement {
  const { language } = useI18n();
  const frame = frameOf(visual.text);
  return (
    <section
      className={`visual visual--text${frame.className}`}
      style={{ ...place, ...frame.style }}
      aria-label={visual.title[language]}
      data-visual="TEXT"
    >
      <div className="visual__body visual__body--text">
        <TextBoxBody text={visual.text} />
      </div>
    </section>
  );
}

/** A visual or a section at `place`: on the dashboard's grid, or on its container's inner grid. */
function PlacedVisual({
  visual,
  filters,
  sources,
  place,
}: {
  visual: VisualResult;
  filters: PortfolioFilters;
  sources: SectionSources;
  place: CSSProperties;
}): ReactElement {
  return visual.visualType === 'SECTION' ? (
    <SectionFrame visual={visual} sources={sources} place={place} />
  ) : isTextBox(visual) ? (
    <TextFrame visual={visual} place={place} />
  ) : (
    <VisualFrame visual={visual} filters={filters} place={place} />
  );
}

/** A container: its frame and title over its visuals, each at its place on the container's inner grid. */
function ContainerFrame({
  container,
  filters,
  sources,
}: {
  container: VisualResult;
  filters: PortfolioFilters;
  sources: SectionSources;
}): ReactElement {
  const { language } = useI18n();
  const titleId = useId();
  return (
    <section
      className="visual-container"
      style={gridPlace(container)}
      aria-labelledby={titleId}
      data-visual="CONTAINER"
    >
      <div className="visual-container__bar">
        <h3 id={titleId} className="visual-container__title">
          {container.title[language]}
        </h3>
      </div>
      {childrenOf(container).map((child) => (
        // No two children share a place in their container.
        <PlacedVisual
          key={placeKey(child)}
          visual={child}
          filters={filters}
          sources={sources}
          place={innerGridPlace(child)}
        />
      ))}
    </section>
  );
}

/**
 * The Portfolio dashboard as the Portfolio Manager designed it (17 and 18 Nov 2026): every card, chart and section in its design's place, under
 * the dashboard's filters, the saved design or the standard one. What the caller may not see, or what has nothing, is left out and the rest rises
 * into its room. No project in reach reads "no data", never a grid of zeros.
 */
export function DesignedVisuals({
  filters,
  view,
  map,
  onClearFilters,
}: {
  filters: PortfolioFilters;
  view: DashboardView;
  map: SectionSources['map'];
  onClearFilters: () => void;
}): ReactElement | null {
  const { t } = useI18n();
  const insights = usePortfolioInsights(filters);
  const showsCompletion = useShowsCompletion();
  const {
    departmentId,
    externalEntityId,
    classificationItemId,
    regionItemId,
    cityItemId,
    status,
    priorityItemId,
    strategicPortfolioItemId,
    strategicInitiativeItemId,
    period,
  } = filters;
  const design = useApiResource(
    useCallback(
      (signal: AbortSignal) =>
        designApi.get(
          {
            departmentId,
            externalEntityId,
            classificationItemId,
            regionItemId,
            cityItemId,
            status,
            priorityItemId,
            strategicPortfolioItemId,
            strategicInitiativeItemId,
            period,
          },
          signal,
        ),
      [
        departmentId,
        externalEntityId,
        classificationItemId,
        regionItemId,
        cityItemId,
        status,
        priorityItemId,
        strategicPortfolioItemId,
        strategicInitiativeItemId,
        period,
      ],
    ),
    { keepWhileReloading: true },
  );
  if (design.data === undefined || insights.data === undefined) {
    return design.loading || insights.loading ? (
      <LoadingState />
    ) : (
      <p className="state">{t('dashboards.insights.unavailable')}</p>
    );
  }
  // No project in reach: the insights count none, or count nothing the caller may see and the dashboard's own view has no data either.
  const projects = projectCountOf(insights.data);
  if (projects === 0 || (projects === null && hasNoData(view))) {
    const reason = emptyPortfolioReason(filters);
    return (
      <EmptyState
        title={t('dashboards.noData')}
        reason={reason}
        action={
          reason === 'scope'
            ? undefined
            : { label: t('common.filters.clear'), onClick: onClearFilters }
        }
      />
    );
  }
  const sources: SectionSources = { insights: insights.data, view, filters, showsCompletion, map };
  // Each visual where the designer placed it; what the caller may not see is left out, and a row it leaves empty is taken out. A design saved
  // with all the widgets in one section draws each widget as its own visual, packed as the designer packs that split.
  const split = hasWidgetsToSplit(design.data.visuals, view.widgets);
  const isShown = (visual: VisualResult) =>
    // A text box is the designer's words, for everyone.
    isTextBox(visual) ||
    (visual.section === undefined || visual.section === null
      ? visual.groups !== null &&
        !withheld(visual) &&
        // A column table the caller may not see comes back with no table.
        (!hasColumns(visual) || (visual.table !== null && visual.table !== undefined))
      : sectionShown(visual, sources));
  // A container keeps the visuals the caller may see, with the inner rows they leave empty taken out; one with none left is left out.
  const visible = splitWidgets(
    design.data.visuals,
    view.widgets,
    ({ children: _children, ...visual }) => ({
      ...visual,
      groups: [],
    }),
  ).flatMap((visual) => {
    if (!isContainer(visual)) {
      return isShown(visual) ? [visual] : [];
    }
    const children = childrenOf(visual).filter(isShown);
    return children.length === 0 ? [] : [withoutEmptyInnerRows(visual, children)];
  });
  const shown = split ? packed(visible) : withoutEmptyRows(visible);
  return shown.length === 0 ? null : (
    <section className="insights" aria-labelledby="designed-visuals">
      <h2 id="designed-visuals" className="visually-hidden">
        {t('dashboards.insights.headlineLabel')}
      </h2>
      <div className="visual-grid" data-section="designed" style={gridRows(rowsOf(shown))}>
        {shown.map((visual) =>
          // No two visuals share a place, so the place names one.
          isContainer(visual) ? (
            <ContainerFrame
              key={placeKey(visual)}
              container={visual}
              filters={filters}
              sources={sources}
            />
          ) : (
            <PlacedVisual
              key={placeKey(visual)}
              visual={visual}
              filters={filters}
              sources={sources}
              place={gridPlace(visual)}
            />
          ),
        )}
      </div>
    </section>
  );
}
