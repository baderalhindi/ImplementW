import { translate, type TranslationKey } from '@/shared/i18n/i18n.ts';

import { CONTAINER_MIN_HEIGHT, TITLE_ROWS } from './containers.ts';
import { type Measure, type Section, type Visual } from './designApi.ts';
import { NO_FIELD_DATA } from './fields.ts';
import { MAX_HEIGHT, tenth } from './visuals.ts';

// The sections made of boxes become containers of cards (20 Nov 2026, docs/architecture/dashboard-designer-box-sections.md): the On-plan
// indicator and the follow-up reasons, each box a card of the figure that counts it, titled with the box's words, in the section's order.

/** Each box section's boxes, in its order: the figure a box counts and the words it is labelled with (PortfolioInsightsSection.tsx). */
const BOX_SECTIONS = {
  ON_PLAN_COUNTS: [
    { measure: 'ON_TRACK', label: 'projects.onPlan.ON_TRACK' },
    { measure: 'LATE', label: 'projects.onPlan.LATE' },
    { measure: 'STALLED', label: 'projects.onPlan.STALLED' },
    { measure: 'NOT_MEASURED', label: 'dashboards.insights.notMeasured' },
  ],
  FOLLOW_UP_REASONS: [
    { measure: 'FOLLOW_UP_STALLED', label: 'projects.followUpReason.STALLED' },
    { measure: 'FOLLOW_UP_SUSPENDED', label: 'projects.followUpReason.SUSPENDED' },
    { measure: 'FOLLOW_UP_LATE', label: 'projects.followUpReason.LATE' },
    { measure: 'FOLLOW_UP_ACTION_OVERDUE', label: 'projects.followUpReason.ACTION_OVERDUE' },
    { measure: 'FOLLOW_UP_OPEN_OBSTACLE', label: 'projects.followUpReason.OPEN_OBSTACLE' },
    { measure: 'NEEDING_FOLLOW_UP', label: 'dashboards.insights.total' },
  ],
} as const satisfies Partial<
  Record<Section, readonly { measure: Measure; label: TranslationKey }[]>
>;

export type BoxSection = keyof typeof BOX_SECTIONS;

/** The rows a box's card takes. */
export const BOX_CARD_ROWS = 1.5;
/** The fewest columns a box's card takes before the cards wrap to another row. */
export const BOX_CARD_MIN_COLUMNS = 2;

export function isBoxSection(section: Section | null | undefined): section is BoxSection {
  return section !== null && section !== undefined && section in BOX_SECTIONS;
}

const both = (key: TranslationKey) => ({
  ar: translate('ar', key),
  en: translate('en', key),
});

/**
 * Make editable on a box section: a container at the section's place, size and titles, holding a card per box in the section's order, laid
 * out across the container's columns in equal widths to a tenth, each 2.5 rows tall: one row when each card is two columns wide or more, else
 * as few rows as keep them so, shared out evenly; the container grows to hold them. Any other visual unchanged.
 */
export function boxesContainer(visual: Visual): Visual {
  if (!isBoxSection(visual.section)) {
    return visual;
  }
  const boxes = BOX_SECTIONS[visual.section];
  const width = visual.layoutWidth;
  // As few rows as hold the cards at two columns or more, the cards shared out evenly between them (four in two rows of two, not three and one).
  const rows = Math.ceil(boxes.length / Math.max(1, Math.floor(width / BOX_CARD_MIN_COLUMNS)));
  const perRow = Math.ceil(boxes.length / rows);
  const cardWidth = Math.floor((width / perRow) * 10) / 10;
  const children: Visual[] = boxes.map((box, index) => ({
    title: both(box.label),
    visualType: 'CARD',
    measures: [box.measure],
    groupBy: 'NONE',
    layoutColumn: tenth((index % perRow) * cardWidth),
    layoutRow: tenth(Math.floor(index / perRow) * BOX_CARD_ROWS),
    layoutWidth: cardWidth,
    layoutHeight: BOX_CARD_ROWS,
  }));
  const { section: _section, widgetCode: _widgetCode, hiddenColumns: _hidden, ...rest } = visual;
  return {
    ...rest,
    ...NO_FIELD_DATA,
    visualType: 'CONTAINER',
    measures: [],
    groupBy: 'NONE',
    axes: null,
    columns: null,
    showTotals: null,
    layoutHeight: Math.min(
      MAX_HEIGHT,
      // As tall as its cards need (20 Nov 2026: the cards read like the section's boxes, with no empty room under them).
      Math.max(CONTAINER_MIN_HEIGHT, tenth(TITLE_ROWS + rows * BOX_CARD_ROWS)),
    ),
    children,
  };
}
