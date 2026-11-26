import {
  type CSSProperties,
  type KeyboardEvent,
  type PointerEvent,
  type ReactElement,
  useCallback,
  useEffect,
  useEffectEvent,
  useRef,
  useState,
} from 'react';
import { Link } from 'react-router';

import {
  ConfirmDialog,
  type Confirmation,
} from '@/features/identity-access/components/ConfirmDialog.tsx';
import { fieldMessage } from '@/features/identity-access/problems.ts';
import { ApiError, type FieldIssue } from '@/shared/api/httpClient.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { HelpTip } from '@/shared/ui/HelpTip.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { PORTFOLIO_DASHBOARD_PATH } from '../paths.ts';
import { dashboardProblemMessage } from '../problems.ts';
import { dashboardsApi, type PortfolioFilters } from '../api/dashboardsApi.ts';
import { usePortfolioInsights, useShowsCompletion } from '../insights/insightSections.ts';
import {
  type Cell,
  changedAt,
  childrenOf,
  CONTAINER_MIN_HEIGHT,
  innerGridPlace,
  isContainer,
  moved,
  nestedReadingOrder,
  newContainer,
  onDashboard,
  pathWhere,
  visualAt,
  type VisualPath,
  visualCount,
  withoutContainer,
  withoutVisual,
} from './containers.ts';
import {
  designApi,
  type FieldDescriptor,
  type Visual,
  type VisualResult,
  type VisualType,
} from './designApi.ts';
import { sourceOf } from './recordSources.ts';
import { countsRisks, RiskMatrixContext, useRiskMatrixWhen } from './riskMatrixContext.ts';
import { lineStart, normalized } from './fields.ts';
import { hiddenColumnsOf } from './sectionColumns.ts';
import { SectionBody } from './SectionBody.tsx';
import { sectionShown, type SectionSources } from './sections.ts';
import {
  frameOf,
  isTextBox,
  TEXT_ISSUE_MESSAGES,
  type TextIssue,
  withoutRefusedText,
} from './textBox.ts';
import { TextBoxBody } from './TextBoxBody.tsx';
import { VisualBody } from './VisualBody.tsx';
import { edgeScroller } from './edgeScroll.ts';
import { type Guide, guidesFor, snappedPlace, snappedSize } from './guides.ts';
import { VisualBodyBox } from './FitToBox.tsx';
import { VisualIcon } from './VisualIcon.tsx';
import { type SizeBounds, VisualSettings } from './VisualSettings.tsx';
import {
  hasWidgetsToSplit,
  newWidgetVisual,
  splitWidgets,
  unplacedWidgets,
} from './widgetVisuals.ts';
import {
  COLUMNS,
  freePlace,
  gridPlace,
  gridRows,
  MAX_HEIGHT,
  MAX_VISUALS,
  newVisual,
  packed,
  rowsOf,
  settled,
  shiftedTogether,
  tenth,
  VISUAL_TYPES,
} from './visuals.ts';

const NO_FILTERS: PortfolioFilters = {};

/** The gallery's tiles before Container: the chart types, then a new map (each time) and a text box. */
const GALLERY_TYPES: readonly VisualType[] = [...VISUAL_TYPES, 'MAP', 'TEXT'];

/** Rows of empty grid under the lowest visual, so a visual can be dragged below the others. */
const SPARE_ROWS = 4;
/** How long the designer waits after the last change before it calculates the figures again. */
const PREVIEW_DELAY_MS = 350;

type Gesture = 'move' | 'resize';

/**
 * A visual as the designer holds it: with a key of its own, so it keeps its element (and the pointer) while the others reflow around it; a
 * container's children are held so too.
 */
interface Draft extends Visual {
  key: string;
  children?: Draft[] | null | undefined;
}

interface Drag {
  gesture: Gesture;
  key: string;
  /** Where the visual was in `before`. */
  from: VisualPath;
  startX: number;
  startY: number;
  start: Draft;
  /** The design when the drag began: each step is laid out from it, so a visual passed over goes back to its place. */
  before: Draft[];
  /** Where on the visual (in columns and rows from its top-start corner) it was grabbed, so that point stays under the pointer. */
  grabColumn: number;
  grabRow: number;
  /** The selected visuals that move with it (top-level ones, itself included); empty when it moves alone. */
  group: readonly string[];
  /** Whether the pointer has moved the visual yet: a press on a selected group that never moves selects that visual alone. */
  moved: boolean;
}

/** Ctrl (Windows), ⌘ (Mac) or Shift held: a press on a visual's bar adds it to the selection, or takes it out. */
const addsToSelection = (event: { ctrlKey: boolean; metaKey: boolean; shiftKey: boolean }) =>
  event.ctrlKey || event.metaKey || event.shiftKey;

let nextKey = 0;
const keyed = ({ children, ...visual }: Visual): Draft => ({
  ...visual,
  ...(children === undefined || children === null ? {} : { children: children.map(keyed) }),
  key: `visual-${String((nextKey += 1))}`,
});

function unkeyed({ key: _key, children, ...visual }: Draft): Visual {
  return normalized({
    ...visual,
    ...(children === undefined || children === null ? {} : { children: children.map(unkeyed) }),
  });
}

/** A design as the API takes it: the keys are the designer's own, and field data only where a visual has some. */
function plain(drafts: Draft[]): Visual[] {
  return drafts.map(unkeyed);
}

function withoutFigures(visual: VisualResult): Visual {
  return normalized({
    title: visual.title,
    visualType: visual.visualType,
    source: visual.source,
    measures: visual.measures,
    groupBy: visual.groupBy,
    layoutColumn: visual.layoutColumn,
    layoutRow: visual.layoutRow,
    layoutWidth: visual.layoutWidth,
    layoutHeight: visual.layoutHeight,
    ...(visual.section === undefined || visual.section === null ? {} : { section: visual.section }),
    widgetCode: visual.widgetCode,
    values: visual.values,
    groupField: visual.groupField,
    columnField: visual.columnField,
    labels: visual.labels,
    groupDateUnit: visual.groupDateUnit,
    filters: visual.filters,
    axes: visual.axes,
    hiddenColumns: visual.hiddenColumns,
    columns: visual.columns,
    showTotals: visual.showTotals,
    text: visual.text,
    map: visual.map,
    ...(visual.children === undefined || visual.children === null
      ? {}
      : { children: visual.children.map(withoutFigures) }),
  });
}

/** What a visual's figures are calculated from: its type, record source, measures or field values (or a table's columns), grouping and filters. */
function figuresBasis(visual: Visual): string {
  const {
    visualType,
    source,
    measures,
    groupBy,
    values,
    groupField,
    columnField,
    groupDateUnit,
    filters,
    columns,
    showTotals,
  } = normalized(visual);
  return JSON.stringify({
    visualType,
    source,
    measures,
    groupBy,
    values,
    groupField,
    columnField,
    groupDateUnit,
    filters,
    columns,
    showTotals,
  });
}

/** Each visual's figures by its key, a container's children's too: the API answers a design in the order it was sent. */
function figuresByKey(drafts: Draft[], results: VisualResult[]): ReadonlyMap<string, VisualResult> {
  return new Map(
    drafts.flatMap((draft, index) => {
      const result = results[index];
      return result === undefined
        ? []
        : [
            [draft.key, result] as const,
            ...figuresByKey(draft.children ?? [], result.children ?? []),
          ];
    }),
  );
}

function sameDesign(a: Visual[], b: Visual[]): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

/**
 * The Portfolio dashboard's designer (17 and 18 Nov 2026), under Dashboards and Reports: the Portfolio Manager adds visuals from the gallery (the
 * dashboard's sections are no longer offered there, 19 Nov 2026: those on the design stay, and come back only with the standard design), drags them by their bar to any place on the grid and by their corner to size, and sets each one's values and grouping; the figures
 * are calculated live. Placement is free (the user, 19 Nov 2026): a visual goes where it is dropped, empty room may stay, and a visual it lands
 * on moves down below it.
 * A container (19 Nov 2026) frames visuals under a title: a visual dropped on its inner grid becomes its child, moving it moves them, and it is
 * sized freely, its visuals scaling with it (containers.ts). A visual's figures larger than its box are drawn smaller to fit (FitToBox).
 * Saving changes the dashboard for everyone, with no approval. A keyboard moves a visual one cell with the arrow keys on its move handle
 * (a child within its container) and sizes it on its size handle.
 */
/**
 * The design without the visuals `keys`: a container with visuals in it goes with them, or (`keepVisuals`) leaves them on the dashboard where
 * they are; a container's child leaves its container.
 */
function withoutVisuals(all: Draft[], keys: readonly string[], keepVisuals: boolean): Draft[] {
  return keys.reduce((rest, key) => {
    const index = rest.findIndex((visual) => visual.key === key);
    const top = rest[index];
    if (top !== undefined && childrenOf(top).length > 0) {
      return withoutContainer(rest, index, keepVisuals);
    }
    const path = pathWhere(rest, (visual) => visual.key === key);
    return path === null ? rest : withoutVisual(rest, path);
  }, all);
}

export function DashboardDesignerPage(): ReactElement {
  const { t, language } = useI18n();
  const [saved, setSaved] = useState<Visual[] | null>(null);
  const [visuals, setVisuals] = useState<Draft[]>([]);
  const [isStandard, setIsStandard] = useState(true);
  const [loadError, setLoadError] = useState<unknown>(null);
  const [figures, setFigures] = useState<ReadonlyMap<string, VisualResult>>(new Map());
  const [previewError, setPreviewError] = useState<string | null>(null);
  /** The field issues of the last refused preview or save, with the design sent, so a text box's settings can say what was refused. */
  const [refusal, setRefusal] = useState<{ sent: Draft[]; issues: FieldIssue[] } | null>(null);
  // The selected visuals (21 Nov 2026: several may be selected and dragged together); the last one chosen is the one the Properties pane edits.
  const [selectedKeys, setSelectedKeys] = useState<readonly string[]>([]);
  const selectedKey = selectedKeys.at(-1) ?? null;
  /** Selects the visual `key` alone, or nothing. */
  const setSelectedKey = useCallback((key: string | null) => {
    setSelectedKeys(key === null ? [] : [key]);
  }, []);
  const [draggingKeys, setDraggingKeys] = useState<readonly string[]>([]);
  // The selection box drawn by dragging on empty canvas (21 Nov 2026), in pixels from the canvas's padding edge.
  const [marquee, setMarquee] = useState<{
    left: number;
    top: number;
    width: number;
    height: number;
  } | null>(null);
  const draggingKey = draggingKeys.at(0) ?? null;
  // The alignment guides of the visual being moved or sized (20 Nov 2026, Power BI's red lines).
  const [guides, setGuides] = useState<Guide[]>([]);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const [placeProblem, setPlaceProblem] = useState<string | null>(null);
  /** The visuals being removed while the dialog asks whether the visuals in the containers among them go too, or stay; empty when none. */
  const [removingKeys, setRemovingKeys] = useState<readonly string[]>([]);
  // A tile dragged from the gallery: its name follows the pointer until it is dropped.
  const [ghost, setGhost] = useState<{ x: number; y: number; label: string } | null>(null);
  // A click that ends a drag from the gallery adds nothing more.
  const draggedFromGallery = useRef(false);
  const canvas = useRef<HTMLDivElement>(null);
  const drag = useRef<Drag | null>(null);
  // A section is previewed from what the dashboard reads, unfiltered: the insights and the dashboard's view.
  const insights = usePortfolioInsights(NO_FILTERS);
  const view = useApiResource(
    useCallback((signal: AbortSignal) => dashboardsApi.get('PORTFOLIO', {}, signal), []),
  );
  // The governed fields the Data panel offers; until they load (or when they cannot be read) the Portfolio figures still are.
  const fields = useApiResource(useCallback((signal: AbortSignal) => designApi.fields(signal), []));
  // The risk fields (record sources, 20 Nov 2026) are read once a visual counts risks or the Source select is opened, and the risk matrix that
  // names their ratings and levels with them.
  const [risksAsked, setRisksAsked] = useState(false);
  const risksWanted = risksAsked || countsRisks(visuals);
  const riskFields = useApiResource(
    useCallback(
      (signal: AbortSignal) =>
        risksWanted ? designApi.fields(signal, 'RISKS') : Promise.resolve<FieldDescriptor[]>([]),
      [risksWanted],
    ),
  );
  const riskMatrix = useRiskMatrixWhen(risksWanted);
  /** The catalogue of a visual's source. */
  const fieldsOf = (visual: Visual) => (sourceOf(visual) === 'RISKS' ? riskFields : fields);
  const askForRisks = useCallback(() => {
    setRisksAsked(true);
  }, []);
  const showsCompletion = useShowsCompletion();
  const sources: SectionSources = {
    insights: insights.data,
    view: view.data,
    filters: NO_FILTERS,
    showsCompletion,
  };

  /** Puts a loaded design in the designer, each visual where it was placed. */
  const adopt = useCallback((loaded: VisualResult[]) => {
    // Keyed in the order read, so each key takes its own figures, then in reading order.
    const read = loaded.map((visual) => keyed(withoutFigures(visual)));
    const drafts = nestedReadingOrder(read);
    setVisuals(drafts);
    setSaved(plain(drafts));
    setFigures(figuresByKey(read, loaded));
  }, []);

  const load = useCallback(() => {
    designApi.get({}).then(
      (design) => {
        adopt(design.visuals);
        setIsStandard(design.isStandard);
        setSelectedKey(null);
        setLoadError(null);
      },
      (error: unknown) => {
        setLoadError(error);
      },
    );
  }, [adopt, setSelectedKey]);

  useEffect(load, [load]);

  // The figures follow the design: a short while after the last change, the API calculates the design as it stands, unsaved.
  // A text box whose words would be refused is left out (it has no figures), so the rest is still calculated.
  useEffect(() => {
    const sent = withoutRefusedText(visuals);
    if (saved === null || sent.length === 0) {
      return undefined;
    }
    const controller = new AbortController();
    const timer = window.setTimeout(() => {
      designApi.preview(plain(sent), controller.signal).then(
        (design) => {
          setFigures(figuresByKey(sent, design.visuals));
          setPreviewError(null);
          setRefusal(null);
        },
        (error: unknown) => {
          if (!controller.signal.aborted) {
            setPreviewError(dashboardProblemMessage(error, t));
            setRefusal(error instanceof ApiError ? { sent, issues: error.fieldIssues } : null);
          }
        },
      );
    }, PREVIEW_DELAY_MS);
    return () => {
      window.clearTimeout(timer);
      controller.abort();
    };
  }, [visuals, saved, t]);

  // A design with all the widgets in one section (the standard one, or one saved before 19 Nov 2026) becomes one visual per widget once the
  // dashboard's view says which widgets there are: an unsaved change, as any other, until it is saved. A discard goes back to the design as
  // read, so it is split again. Adjusted while rendering, as React has state derived from other state adjusted.
  const viewWidgets = view.data?.widgets;
  if (viewWidgets !== undefined && hasWidgetsToSplit(visuals, viewWidgets)) {
    setVisuals(packed(splitWidgets(visuals, viewWidgets, keyed)));
  }

  /**
   * Removes the visuals `keys` (24 Nov 2026, "I can't delete more than one selection"): at once, unless a container with visuals in it is among
   * them, when the dialog asks first whether its visuals go with it.
   */
  const removeVisuals = (keys: readonly string[]) => {
    const holdsVisuals = keys.some((key) => {
      const visual = visuals.find((candidate) => candidate.key === key);
      return visual !== undefined && childrenOf(visual).length > 0;
    });
    if (holdsVisuals) {
      setRemovingKeys(keys);
      return;
    }
    setPlaceProblem(null);
    setVisuals((all) => withoutVisuals(all, keys, false));
    setSelectedKeys([]);
  };

  // Delete (or Backspace) removes the visuals selected, wherever the focus is but in a box being typed in or a dialog.
  const deleteSelection = useEffectEvent((event: globalThis.KeyboardEvent) => {
    if (
      (event.key !== 'Delete' && event.key !== 'Backspace') ||
      event.defaultPrevented ||
      selectedKeys.length === 0 ||
      (event.target instanceof HTMLElement &&
        (event.target.isContentEditable ||
          event.target.closest('input, textarea, select, dialog, [role="dialog"]') !== null))
    ) {
      return;
    }
    event.preventDefault();
    removeVisuals(selectedKeys);
  });
  useEffect(() => {
    const onKeyDown = (event: globalThis.KeyboardEvent) => {
      deleteSelection(event);
    };
    window.addEventListener('keydown', onKeyDown);
    return () => {
      window.removeEventListener('keydown', onKeyDown);
    };
  }, []);

  if (loadError !== null) {
    return (
      <ErrorState
        message={dashboardProblemMessage(loadError, t)}
        onRetry={load}
        error={loadError}
      />
    );
  }
  if (saved === null) {
    return <LoadingState />;
  }

  const dirty = !sameDesign(plain(visuals), saved);
  const pathOf = (all: Draft[], key: string | null): VisualPath | null =>
    key === null ? null : pathWhere(all, (visual) => visual.key === key);
  const selected = pathOf(visuals, selectedKey);
  const current = selected === null ? undefined : visualAt(visuals, selected);
  /** The container a child is in; undefined for a top-level visual. */
  const containerOf = (all: Draft[], path: VisualPath): Draft | undefined =>
    path.child === null ? undefined : all[path.index];
  const removing =
    removingKeys.length === 1
      ? visuals.find((visual) => visual.key === removingKeys[0])
      : undefined;

  /**
   * Adds the visual `key` to the selection, or takes it out (Ctrl, ⌘ or Shift on its bar). Only visuals on the dashboard are selected together;
   * a container's child is selected alone, and a container selected moves with its visuals.
   */
  const toggleSelected = (key: string) => {
    const onTop = (member: string) => visuals.some((visual) => visual.key === member);
    if (!onTop(key)) {
      setSelectedKey(key);
      return;
    }
    setSelectedKeys((keys) => {
      const tops = keys.filter(onTop);
      return tops.includes(key) ? tops.filter((member) => member !== key) : [...tops, key];
    });
  };

  /** Applies a change to the latest design (pointer events come faster than renders). */
  const relayout = (change: (all: Draft[]) => Draft[]) => {
    setPlaceProblem(null);
    setVisuals((all) => change(all));
  };

  /**
   * The design with the visual `key` changed (`changedAt`): whatever it now covers moves down below it; a child changes within its container,
   * which grows to hold it.
   */
  const withChanged = (all: Draft[], key: string, change: (visual: Draft) => Draft): Draft[] => {
    const path = pathOf(all, key);
    const visual = path === null ? undefined : visualAt(all, path);
    return path === null || visual === undefined ? all : changedAt(all, path, change(visual));
  };

  /** Changes the visual `key` to its settings `next` (all but a container's children, which the settings never change). */
  const place = (key: string, next: Visual) => {
    const { children: _children, ...settings } = next;
    relayout((all) =>
      withChanged(all, key, (visual) => {
        const changed = { ...visual, ...settings };
        // Made wider than the room to the grid's end edge, a visual moves toward the reading start to take its new width (a child is kept
        // within its container by its own rule).
        const top = all.some((other) => other.key === key);
        return top && changed.layoutColumn + changed.layoutWidth > COLUMNS
          ? { ...changed, layoutColumn: Math.max(0, tenth(COLUMNS - changed.layoutWidth)) }
          : changed;
      }),
    );
  };

  /**
   * Replaces the top-level visual `key` by `next`, its children included (Make editable on a box section), keeping its key so it stays
   * selected; what it now covers moves down. Refused when the design would hold more visuals than it may.
   */
  const replace = (key: string, next: Visual) => {
    if (visualCount(visuals) - 1 + visualCount([next]) > MAX_VISUALS) {
      setPlaceProblem(t('dashboards.designer.noRoom'));
      return;
    }
    relayout((all) => {
      const index = all.findIndex((visual) => visual.key === key);
      return index < 0
        ? all
        : settled(
            all.map((visual, at) => (at === index ? { ...keyed(next), key } : visual)),
            index,
          );
    });
  };

  const append = (visual: Visual | null) => {
    if (visual === null || visualCount(visuals) >= MAX_VISUALS) {
      setPlaceProblem(t('dashboards.designer.noRoom'));
      return;
    }
    const draft = keyed(visual);
    setNotice(null);
    relayout((all) => [...all, draft]);
    setSelectedKey(draft.key);
  };

  const add = (type: VisualType) => {
    append(newVisual(type, visuals, fields.data ?? []));
  };

  // The view's widgets no visual draws: each can be placed again from the gallery.
  const unplaced = view.data === undefined ? [] : unplacedWidgets(visuals, view.data.widgets);

  /** Removes the visuals the dialog asked about, the visuals in their containers with them or kept on the dashboard. */
  const removeAsked = (keepVisuals: boolean) => {
    const keys = removingKeys;
    relayout((all) => withoutVisuals(all, keys, keepVisuals));
    setRemovingKeys([]);
    setSelectedKey(null);
  };

  /** A copy of the visual on the dashboard at the first free place. */
  const duplicate = (source: Draft) => {
    const { key: _key, children: _children, ...copy } = source;
    const free = freePlace(visuals, source.layoutWidth, source.layoutHeight);
    append(
      free === null
        ? null
        : { ...copy, section: null, layoutColumn: free.column, layoutRow: free.row },
    );
  };

  /** The size of one grid step, read from the drawn canvas (styles.css sets its gap and row height): a column's width and a row's height, each with its gap. */
  const step = () => {
    const element = canvas.current;
    if (element === null) {
      return { column: 1, row: 1, rtl: false, left: 0, right: 0, top: 0 };
    }
    const style = window.getComputedStyle(element);
    const columnGap = Number.parseFloat(style.columnGap) || 0;
    const rowGap = Number.parseFloat(style.rowGap) || 0;
    const rowHeight = Number.parseFloat(style.gridAutoRows) || 72;
    const box = element.getBoundingClientRect();
    const padding = Number.parseFloat(style.paddingInlineStart) || 0;
    return {
      column: (element.clientWidth - 2 * padding + columnGap) / COLUMNS,
      row: rowHeight + rowGap,
      rtl: style.direction === 'rtl',
      left: box.left + padding,
      right: box.right - padding,
      top: box.top + (Number.parseFloat(style.paddingTop) || 0),
    };
  };

  /**
   * A drag follows the pointer on the window, not by capturing it: the dragged element moves in the page as the others reflow, and a browser
   * drops a capture when its element moves.
   */
  /** Where the pointer is on the grid, in columns from the reading start and rows from the top (decimals: a place may be any tenth). */
  const cellAt = (clientX: number, clientY: number, size: ReturnType<typeof step>): Cell => ({
    column: (size.rtl ? size.right - clientX : clientX - size.left) / size.column,
    row: (clientY - size.top) / size.row,
  });

  const startDrag = (event: PointerEvent<HTMLElement>, key: string, gesture: Gesture) => {
    const from = pathOf(visuals, key);
    const start = from === null ? undefined : visualAt(visuals, from);
    // The bar drags the visual, but its edit button stays a button.
    const onButton =
      gesture === 'move' &&
      (event.target as Element).closest('button:not(.designer__handle--move)') !== null;
    if (from === null || start === undefined || event.button !== 0 || onButton) {
      return;
    }
    event.preventDefault();
    if (gesture === 'move' && addsToSelection(event)) {
      toggleSelected(start.key);
      return;
    }
    // A selected visual pressed while others are selected too takes them along; any other press selects it alone.
    const group =
      gesture === 'move' && selectedKeys.length > 1 && selectedKeys.includes(start.key)
        ? selectedKeys.filter((key) => visuals.some((visual) => visual.key === key))
        : [];
    const size = step();
    const grabbed = cellAt(event.clientX, event.clientY, size);
    // A child is drawn at its container's place plus its own.
    const container = containerOf(visuals, from);
    const drawn =
      container === undefined
        ? { column: start.layoutColumn, row: start.layoutRow }
        : onDashboard(container, start);
    drag.current = {
      gesture,
      key: start.key,
      from,
      startX: event.clientX,
      startY: event.clientY,
      start,
      before: visuals,
      grabColumn: grabbed.column - drawn.column,
      grabRow: grabbed.row - drawn.row,
      group,
      moved: false,
    };
    if (group.length === 0) {
      setSelectedKey(start.key);
    }
    setDraggingKeys(group.length === 0 ? [start.key] : group);
    // Near the window's top or bottom edge the page scrolls, and the visual follows, so it can reach any place on a long dashboard.
    const scroller = edgeScroller(dragTo);
    const onMove = (move: globalThis.PointerEvent) => {
      dragTo(move.clientX, move.clientY);
      scroller.update(move.clientX, move.clientY);
    };
    const onEnd = () => {
      if (drag.current !== null && drag.current.group.length > 0 && !drag.current.moved) {
        setSelectedKey(start.key);
      }
      scroller.stop();
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onEnd);
      window.removeEventListener('pointercancel', onEnd);
      drag.current = null;
      setDraggingKeys([]);
      setGuides([]);
    };
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onEnd);
    window.addEventListener('pointercancel', onEnd);
  };

  /**
   * Box selection (21 Nov 2026, "select with the mouse, as on the desktop"): pressed on empty canvas and dragged, a box is drawn from the press
   * to the pointer and every visual on the dashboard it touches is selected (with Ctrl, ⌘ or Shift, added to those selected already). A press
   * that does not move clears the selection. Near the window's edge the page scrolls, and the box grows with it.
   */
  const startBoxSelection = (event: PointerEvent<HTMLDivElement>) => {
    const element = canvas.current;
    if (element === null || event.button !== 0 || event.target !== element) {
      return;
    }
    event.preventDefault();
    const adding = addsToSelection(event);
    const kept = adding
      ? selectedKeys.filter((key) => visuals.some((visual) => visual.key === key))
      : [];
    // The press, from the canvas's padding edge, so it stays put as the page scrolls.
    const origin = () => {
      const box = element.getBoundingClientRect();
      return { x: box.left + element.clientLeft, y: box.top + element.clientTop };
    };
    const at = origin();
    const startX = event.clientX - at.x;
    const startY = event.clientY - at.y;
    let drawn = false;
    const drawTo = (clientX: number, clientY: number) => {
      const now = origin();
      const x = clientX - now.x;
      const y = clientY - now.y;
      if (!drawn && Math.hypot(x - startX, y - startY) < 4) {
        return;
      }
      drawn = true;
      const area = {
        left: Math.min(startX, x),
        top: Math.min(startY, y),
        width: Math.abs(x - startX),
        height: Math.abs(y - startY),
      };
      setMarquee(area);
      // The visuals on the dashboard (not a container's children) whose drawn box the selection box touches.
      const touched = [...element.querySelectorAll<HTMLElement>(':scope > [data-visual-key]')]
        .filter((visualElement) => {
          const box = visualElement.getBoundingClientRect();
          return (
            box.left - now.x < area.left + area.width &&
            box.right - now.x > area.left &&
            box.top - now.y < area.top + area.height &&
            box.bottom - now.y > area.top
          );
        })
        .flatMap((visualElement) => visualElement.dataset.visualKey ?? []);
      setSelectedKeys([...kept, ...touched.filter((key) => !kept.includes(key))]);
    };
    const scroller = edgeScroller(drawTo);
    const onMove = (move: globalThis.PointerEvent) => {
      drawTo(move.clientX, move.clientY);
      scroller.update(move.clientX, move.clientY);
    };
    const onEnd = () => {
      if (!drawn && !adding) {
        setSelectedKey(null);
      }
      scroller.stop();
      setMarquee(null);
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onEnd);
      window.removeEventListener('pointercancel', onEnd);
    };
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onEnd);
    window.addEventListener('pointercancel', onEnd);
  };

  /** Whether the pointer is over the canvas. */
  const overCanvas = (clientX: number, clientY: number) => {
    const box = canvas.current?.getBoundingClientRect();
    return (
      box !== undefined &&
      clientX >= box.left &&
      clientX <= box.right &&
      clientY >= box.top &&
      clientY <= box.bottom
    );
  };

  /**
   * Drag and drop from the gallery (19 Nov 2026): pressed and moved a few pixels, a tile drags a new visual (`make`); over the dashboard it is
   * placed at the cell under the pointer, or inside the container under it, as a moved visual is (`moved`); dropped elsewhere nothing is added.
   * A press that does not move stays a click.
   */
  const startGalleryDrag = (
    event: PointerEvent<HTMLElement>,
    label: string,
    make: () => Visual | null,
  ) => {
    if (event.button !== 0) {
      return;
    }
    const startX = event.clientX;
    const startY = event.clientY;
    const before = visuals;
    let draft: Draft | null = null;
    draggedFromGallery.current = false;
    const onMove = (move: globalThis.PointerEvent) => {
      if (draft === null) {
        if (Math.hypot(move.clientX - startX, move.clientY - startY) < 6) {
          return;
        }
        const made = visualCount(before) >= MAX_VISUALS ? null : make();
        if (made === null) {
          setPlaceProblem(t('dashboards.designer.noRoom'));
          finish();
          return;
        }
        const dragged = keyed(made);
        draft = dragged;
        draggedFromGallery.current = true;
        drag.current = {
          gesture: 'move',
          key: dragged.key,
          from: { index: before.length, child: null },
          startX,
          startY,
          start: dragged,
          before: [...before, dragged],
          // The visual's top-start corner follows the pointer.
          grabColumn: 0,
          grabRow: 0,
          group: [],
          moved: true,
        };
        setDraggingKeys([draft.key]);
        setNotice(null);
      }
      follow(move.clientX, move.clientY);
      scroller.update(move.clientX, move.clientY);
    };
    /** The new visual at the pointer's place: on the dashboard where it would drop, or not on it while the pointer is elsewhere. */
    const follow = (clientX: number, clientY: number) => {
      setGhost({ x: clientX, y: clientY, label });
      if (overCanvas(clientX, clientY)) {
        dragTo(clientX, clientY);
      } else {
        setPlaceProblem(null);
        setVisuals(before);
      }
    };
    const scroller = edgeScroller((clientX, clientY) => {
      if (draft !== null) {
        follow(clientX, clientY);
      }
    });
    const onEnd = (end: globalThis.PointerEvent) => {
      if (draft !== null) {
        if (overCanvas(end.clientX, end.clientY)) {
          setSelectedKey(draft.key);
        } else {
          setVisuals(before);
        }
      }
      finish();
    };
    function finish() {
      scroller.stop();
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onEnd);
      window.removeEventListener('pointercancel', onEnd);
      drag.current = null;
      setDraggingKeys([]);
      setGuides([]);
      setGhost(null);
    }
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onEnd);
    window.addEventListener('pointercancel', onEnd);
  };

  /** A gallery tile's click: adds the visual, unless the press was a drag that has placed it already. */
  const clickedTile = (addIt: () => void) => {
    if (draggedFromGallery.current) {
      draggedFromGallery.current = false;
      return;
    }
    addIt();
  };

  const dragTo = (clientX: number, clientY: number) => {
    const active = drag.current;
    if (active === null) {
      return;
    }
    const size = step();
    if (active.gesture === 'resize') {
      // Sizing snaps to a tenth of a column and of a row (20 Nov 2026: "4.5 or 4.7, like float numbers").
      const columns = tenth(((clientX - active.startX) * (size.rtl ? -1 : 1)) / size.column);
      const rows = tenth((clientY - active.startY) / size.row);
      // Sizing stops at the grid's (or the container's) end edge, never at what the visual holds (a container's visuals scale with it); it
      // never moves the visual.
      const container = containerOf(active.before, active.from);
      const across = container?.layoutWidth ?? COLUMNS;
      const sized = {
        ...active.start,
        layoutWidth: Math.min(
          tenth(across - active.start.layoutColumn),
          Math.max(1, tenth(active.start.layoutWidth + columns)),
        ),
        layoutHeight: Math.min(MAX_HEIGHT, Math.max(1, tenth(active.start.layoutHeight + rows))),
      };
      // On the dashboard (not inside a container) its end edges are pulled onto the other visuals' lines, which then show.
      const others =
        container === undefined ? active.before.filter((visual) => visual.key !== active.key) : [];
      const pulled = others.length === 0 ? null : snappedSize(sized, others);
      const fitted =
        pulled === null
          ? sized
          : {
              ...sized,
              layoutWidth: Math.min(tenth(across - sized.layoutColumn), Math.max(1, pulled.width)),
              layoutHeight: Math.min(MAX_HEIGHT, Math.max(1, pulled.height)),
            };
      setGuides(others.length === 0 ? [] : guidesFor(fitted, others));
      relayout(() => changedAt(active.before, active.from, fitted));
      return;
    }
    // Moving is free: the point it was grabbed by stays under the pointer, snapped to a tenth of a column and of a row, and where the pointer
    // is decides where it goes: into the container whose inner grid it is on, or onto the dashboard (`moved`).
    const pointer = cellAt(clientX, clientY, size);
    const place = {
      column: tenth(pointer.column - active.grabColumn),
      row: tenth(pointer.row - active.grabRow),
    };
    if (active.group.length > 0) {
      // A selected group moves by the offset the grabbed visual takes, pulled onto the other visuals' lines; it stays on the dashboard.
      const outside = active.before.filter((visual) => !active.group.includes(visual.key));
      const pulled = snappedPlace(
        { ...active.start, layoutColumn: place.column, layoutRow: place.row },
        outside,
      );
      const columns = pulled.column - active.start.layoutColumn;
      const rows = pulled.row - active.start.layoutRow;
      if (!active.moved && columns === 0 && rows === 0) {
        return;
      }
      active.moved = true;
      const result = shiftedTogether(
        active.before,
        active.before.flatMap((visual, index) =>
          active.group.includes(visual.key) ? [index] : [],
        ),
        columns,
        rows,
      );
      const placed = result.find((visual) => visual.key === active.key);
      setGuides(placed === undefined ? [] : guidesFor(placed, outside));
      setPlaceProblem(null);
      setVisuals(result);
      return;
    }
    // On the dashboard its edges and centre near another visual's are pulled onto them, and the lines that then align show.
    const others = active.before.filter((visual) => visual.key !== active.key);
    const pulled = snappedPlace(
      { ...active.start, layoutColumn: place.column, layoutRow: place.row },
      others,
    );
    let result = moved(active.before, active.from, pointer, pulled);
    const placed = result.visuals.find((visual) => visual.key === active.key);
    if (placed === undefined) {
      // Dropped into a container: its own place, not pulled to the dashboard's lines.
      result = moved(active.before, active.from, pointer, place);
    }
    setGuides(placed === undefined ? [] : guidesFor(placed, others));
    setPlaceProblem(result.noRoom ? t('dashboards.designer.noRoomInContainer') : null);
    setVisuals(result.visuals);
  };

  /**
   * Arrow keys on a handle: one cell on the move handle (a child within its container), smaller or larger on the size handle; with Shift, a
   * tenth of a cell. Delete removes the visual.
   */
  const keyOnHandle = (event: KeyboardEvent<HTMLButtonElement>, key: string, gesture: Gesture) => {
    const path = pathOf(visuals, key);
    const visual = path === null ? undefined : visualAt(visuals, path);
    if (path === null || visual === undefined) {
      return;
    }
    if (event.key === 'Delete' || event.key === 'Backspace') {
      event.preventDefault();
      removeVisuals(selectedKeys.includes(key) ? selectedKeys : [key]);
      return;
    }
    const across = containerOf(visuals, path)?.layoutWidth ?? COLUMNS;
    // The arrow pointing to the reading end is forward: right in English, left in Arabic.
    const forward = step().rtl ? 'ArrowLeft' : 'ArrowRight';
    const backward = step().rtl ? 'ArrowRight' : 'ArrowLeft';
    const by = event.shiftKey ? 0.1 : 1;
    const sideways = event.key === forward ? by : event.key === backward ? -by : 0;
    const down = event.key === 'ArrowDown' ? by : event.key === 'ArrowUp' ? -by : 0;
    if (sideways === 0 && down === 0) {
      return;
    }
    event.preventDefault();
    // A selected group moves together on any of its move handles.
    const group = selectedKeys.filter((member) => visuals.some((top) => top.key === member));
    if (gesture === 'move' && group.length > 1 && group.includes(visual.key)) {
      relayout((all) =>
        shiftedTogether(
          all,
          all.flatMap((top, index) => (group.includes(top.key) ? [index] : [])),
          sideways,
          down,
        ),
      );
      return;
    }
    setSelectedKey(visual.key);
    relayout((all) =>
      withChanged(all, key, (latest) =>
        gesture === 'move'
          ? {
              ...latest,
              layoutColumn: Math.min(
                tenth(across - latest.layoutWidth),
                Math.max(0, tenth(latest.layoutColumn + sideways)),
              ),
              layoutRow: Math.max(0, tenth(latest.layoutRow + down)),
            }
          : {
              ...latest,
              layoutWidth: Math.min(
                tenth(across - latest.layoutColumn),
                Math.max(1, tenth(latest.layoutWidth + sideways)),
              ),
              layoutHeight: Math.min(MAX_HEIGHT, Math.max(1, tenth(latest.layoutHeight + down))),
            },
      ),
    );
  };

  const save = async () => {
    setBusy(true);
    setNotice(null);
    try {
      const design = await designApi.save(plain(visuals));
      adopt(design.visuals);
      setIsStandard(false);
      setNotice({ tone: 'success', message: t('dashboards.designer.saved') });
    } catch (error) {
      setPreviewError(dashboardProblemMessage(error, t));
      setRefusal(error instanceof ApiError ? { sent: visuals, issues: error.fieldIssues } : null);
    } finally {
      setBusy(false);
    }
  };

  /** A visual (never a container: its frame draws no figures) with its figures. */
  const figuresOf = ({ children: _children, ...visual }: Draft): VisualResult => {
    const calculated = figures.get(visual.key);
    // Until the new figures arrive, a visual whose values changed shows none rather than figures for other values.
    return calculated?.groups !== undefined && figuresBasis(calculated) === figuresBasis(visual)
      ? { ...calculated, ...visual }
      : { ...visual, groups: [] };
  };

  const classOf = (base: string, visual: Draft) =>
    `${base}${selectedKeys.includes(visual.key) ? ' is-selected' : ''}${
      draggingKeys.includes(visual.key) ? ' is-dragging' : ''
    }`;

  /** A visual's bar: the whole bar drags it, as its handle does; the keyboard moves it on the handle. Its pencil opens its settings. */
  const renderBar = (visual: Draft, className: string, titleClass: string) => {
    const title = visual.title[language];
    const isSelected = visual.key === selectedKey;
    return (
      <div
        className={className}
        onPointerDown={(event) => {
          startDrag(event, visual.key, 'move');
        }}
      >
        <button
          type="button"
          className="designer__handle designer__handle--move"
          aria-label={t('dashboards.designer.moveHandle', { title })}
          title={t('dashboards.designer.moveHandle', { title })}
          onClick={(event) => {
            // A press has selected already (startDrag); Enter or Space on the handle selects here, with Ctrl, ⌘ or Shift adding to the selection.
            if (event.detail === 0) {
              if (addsToSelection(event)) {
                toggleSelected(visual.key);
              } else {
                setSelectedKey(visual.key);
              }
            }
          }}
          onKeyDown={(event) => {
            keyOnHandle(event, visual.key, 'move');
          }}
        >
          <span aria-hidden="true">⠿</span>
        </button>
        <h3 className={titleClass}>{title}</h3>
        <button
          type="button"
          className="designer__handle designer__edit"
          aria-pressed={isSelected}
          aria-label={t('dashboards.designer.editVisual', { title })}
          title={t('dashboards.designer.editVisual', { title })}
          onClick={() => {
            setSelectedKey(isSelected ? null : visual.key);
          }}
        >
          <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <path d="M4 20h4L19 9l-4-4L4 16v4zM14 6l4 4" />
          </svg>
        </button>
      </div>
    );
  };

  const renderResizeHandle = (visual: Draft) => {
    const title = visual.title[language];
    return (
      <button
        type="button"
        className="designer__handle designer__handle--resize"
        aria-label={t('dashboards.designer.resizeHandle', { title })}
        title={t('dashboards.designer.resizeHandle', { title })}
        onPointerDown={(event) => {
          startDrag(event, visual.key, 'resize');
        }}
        onKeyDown={(event) => {
          keyOnHandle(event, visual.key, 'resize');
        }}
      />
    );
  };

  /**
   * A press on a visual's body (24 Nov 2026, "if I press a visual and press Shift to select another visual I can't"): selects it alone, or with
   * Ctrl, ⌘ or Shift adds it to the selection or takes it out, as its bar does. Its bar and size handle keep their own presses, and a link or
   * a box in it keeps its click; a child's press is not its container's too.
   */
  const pressOnVisual = (event: PointerEvent<HTMLElement>, key: string) => {
    const target = event.target as Element;
    if (
      event.button !== 0 ||
      target.closest(
        '.designer__visual-bar, .designer__handle--resize, a, button, input, select, textarea',
      ) !== null
    ) {
      return;
    }
    event.stopPropagation();
    if (addsToSelection(event)) {
      event.preventDefault();
      toggleSelected(key);
    } else if (!(selectedKeys.length === 1 && selectedKeys[0] === key)) {
      setSelectedKey(key);
    }
  };

  /** A visual at `place`: on the dashboard's grid, or on its container's inner grid. */
  const renderVisual = (visual: Draft, place: CSSProperties) => {
    // A text box's background and border (frameOf) are drawn in the designer as on the dashboard.
    const frame = frameOf(visual.text);
    return (
      // Its own key: a visual keeps its element, and its pointer, while the others reflow around it.
      <section
        key={visual.key}
        className={`${classOf('widget visual designer__visual', visual)}${frame.className}`}
        style={{ ...place, ...frame.style }}
        aria-label={visual.title[language]}
        data-visual-key={visual.key}
        aria-current={visual.key === selectedKey ? 'true' : undefined}
        data-visual={visual.visualType}
        onPointerDown={(event) => {
          pressOnVisual(event, visual.key);
        }}
      >
        {renderBar(visual, 'designer__visual-bar', 'widget__title')}
        <VisualBodyBox
          visual={visual}
          label={visual.title[language]}
          className={
            isTextBox(visual)
              ? 'visual__body visual__body--text'
              : (visual.section === undefined || visual.section === null) &&
                  visual.visualType !== 'MAP'
                ? 'visual__body'
                : // A map visual is laid out as the map section is.
                  'visual__body visual__body--section'
          }
        >
          {isTextBox(visual) ? (
            <TextBoxBody text={visual.text} />
          ) : visual.section === undefined || visual.section === null ? (
            <VisualBody
              visual={figuresOf(visual)}
              fields={fieldsOf(visual).data}
              places={view.data?.places}
            />
          ) : sectionShown(visual, sources) ? (
            <SectionBody
              section={visual.section}
              widgetCode={visual.widgetCode}
              hidden={hiddenColumnsOf(visual)}
              sources={sources}
            />
          ) : (
            <p className="state">{t('dashboards.noData')}</p>
          )}
        </VisualBodyBox>
        {renderResizeHandle(visual)}
      </section>
    );
  };

  /** A container: its frame and title bar over its inner grid, where its visuals are drawn as any visual. */
  const renderContainer = (container: Draft) => {
    const children = childrenOf(container);
    return (
      <section
        key={container.key}
        className={`${classOf('visual-container designer__container', container)}${
          // The container a dragged visual would drop into is marked, so it is plain where the drop goes.
          draggingKey !== null && children.some((child) => child.key === draggingKey)
            ? ' is-drop-target'
            : ''
        }`}
        style={gridPlace(container)}
        aria-label={container.title[language]}
        data-visual-key={container.key}
        aria-current={container.key === selectedKey ? 'true' : undefined}
        data-visual="CONTAINER"
        onPointerDown={(event) => {
          pressOnVisual(event, container.key);
        }}
      >
        {renderBar(
          container,
          'designer__visual-bar visual-container__bar',
          'visual-container__title',
        )}
        {children.length === 0 ? (
          <p className="visual-container__empty">{t('dashboards.designer.containerEmpty')}</p>
        ) : (
          children.map((child) => renderVisual(child, innerGridPlace(child)))
        )}
        {renderResizeHandle(container)}
      </section>
    );
  };

  /** What the API refused in a text box's words, by language: its issues at `visuals[i].text.ar` (a child's at `visuals[i].children[j]…`). */
  const textRefusedOf = (key: string): Partial<Record<'ar' | 'en', string>> => {
    const at = refusal === null ? null : pathOf(refusal.sent, key);
    if (refusal === null || at === null) {
      return {};
    }
    const prefix = `visuals[${String(at.index)}]${at.child === null ? '' : `.children[${String(at.child)}]`}.text.`;
    const messageOf = (language: 'ar' | 'en') => {
      const issue = refusal.issues.find((one) => one.field === `${prefix}${language}`);
      return issue === undefined
        ? undefined
        : issue.code in TEXT_ISSUE_MESSAGES
          ? t(TEXT_ISSUE_MESSAGES[issue.code as TextIssue])
          : fieldMessage(issue.code, t);
    };
    const ar = messageOf('ar');
    const en = messageOf('en');
    return { ...(ar === undefined ? {} : { ar }), ...(en === undefined ? {} : { en }) };
  };

  /** The sizes the selected visual may take where it is. */
  const boundsOf = (visual: Draft): SizeBounds => {
    const least = { width: 1, height: isContainer(visual) ? CONTAINER_MIN_HEIGHT : 1 };
    return {
      minWidth: least.width,
      minHeight: least.height,
      maxWidth:
        (selected === null ? undefined : containerOf(visuals, selected))?.layoutWidth ?? COLUMNS,
    };
  };

  return (
    <RiskMatrixContext value={riskMatrix}>
      <div className="designer">
        {ghost !== null && (
          <div
            className="designer__ghost"
            aria-hidden="true"
            style={{ left: ghost.x, top: ghost.y }}
          >
            {ghost.label}
          </div>
        )}
        <PageHeader
          title={t('dashboards.designer.title')}
          description={t('dashboards.designer.intro')}
          actions={
            <>
              <Link className="button button--quiet" to={PORTFOLIO_DASHBOARD_PATH}>
                {t('dashboards.designer.viewDashboard')}
              </Link>
              <button
                type="button"
                className="button"
                disabled={busy || isStandard}
                onClick={() => {
                  setConfirmation({
                    title: t('dashboards.designer.reset'),
                    body: t('dashboards.designer.resetConfirm'),
                    confirmLabel: t('dashboards.designer.reset'),
                    destructive: true,
                    action: designApi.reset,
                    describeProblem: dashboardProblemMessage,
                  });
                }}
              >
                {t('dashboards.designer.reset')}
              </button>
              <button
                type="button"
                className="button"
                disabled={busy || !dirty}
                onClick={() => {
                  relayout(() => saved.map(keyed));
                  setSelectedKey(null);
                  setPlaceProblem(null);
                }}
              >
                {t('dashboards.designer.discard')}
              </button>
              <button
                type="button"
                className="button button--primary"
                disabled={busy || !dirty || visuals.length === 0}
                onClick={() => {
                  void save();
                }}
              >
                {t('dashboards.designer.save')}
              </button>
            </>
          }
        />
        <PageNotice notice={notice} />
        <p className="designer__status" role="status">
          {dirty
            ? t('dashboards.designer.unsaved')
            : isStandard
              ? t('dashboards.designer.standard')
              : t('dashboards.designer.savedState')}
        </p>
        <FormAlert message={previewError ?? placeProblem} />
        <div className="designer__workspace">
          <div className="designer__canvas-frame">
            <h2 className="visually-hidden">{t('dashboards.designer.canvasLabel')}</h2>
            {visuals.length === 0 ? (
              <p className="state">{t('dashboards.designer.empty')}</p>
            ) : null}
            <div
              ref={canvas}
              className="visual-grid designer__canvas"
              style={gridRows(Math.ceil(rowsOf(visuals)) + SPARE_ROWS)}
              onPointerDown={startBoxSelection}
            >
              {visuals.map((visual) =>
                isContainer(visual)
                  ? renderContainer(visual)
                  : renderVisual(visual, gridPlace(visual)),
              )}
              {draggingKey !== null &&
                guides.map((guide) => (
                  <span
                    key={`${guide.axis}-${String(guide.at)}`}
                    className={`designer__guide designer__guide--${guide.axis} designer__guide--${guide.line}`}
                    style={{ '--guide-at': String(guide.at) } as CSSProperties}
                    aria-hidden="true"
                  />
                ))}
              {marquee !== null && (
                <span
                  className="designer__marquee"
                  style={{
                    left: marquee.left,
                    top: marquee.top,
                    width: marquee.width,
                    height: marquee.height,
                  }}
                  aria-hidden="true"
                />
              )}
            </div>
          </div>
          <aside className="designer__pane">
            <section className="designer__gallery" aria-labelledby="designer-gallery">
              <div className="designer__heading-row">
                <h2 id="designer-gallery" className="designer__heading">
                  {t('dashboards.designer.gallery')}
                </h2>
                <HelpTip label={t('dashboards.designer.gallery')} align="end">
                  {t('dashboards.designer.keyboardHint')}
                </HelpTip>
              </div>
              <p className="designer__hint">{t('dashboards.designer.galleryHint')}</p>
              <ul className="designer__types">
                {GALLERY_TYPES.map((type) => (
                  <li key={type}>
                    <button
                      type="button"
                      className="designer__type"
                      // A line draws fields only: it waits for the catalogue to give it one.
                      disabled={type === 'LINE' && lineStart(fields.data ?? []) === null}
                      onPointerDown={(event) => {
                        startGalleryDrag(event, t(`dashboards.designer.type.${type}`), () =>
                          newVisual(type, visuals, fields.data ?? []),
                        );
                      }}
                      onClick={() => {
                        clickedTile(() => {
                          add(type);
                        });
                      }}
                    >
                      <VisualIcon type={type} />
                      <span className="designer__type-name">
                        {t(`dashboards.designer.type.${type}`)}
                      </span>
                    </button>
                  </li>
                ))}
                <li>
                  <button
                    type="button"
                    className="designer__type"
                    onPointerDown={(event) => {
                      startGalleryDrag(event, t('dashboards.designer.type.CONTAINER'), () =>
                        newContainer(visuals),
                      );
                    }}
                    onClick={() => {
                      clickedTile(() => {
                        append(newContainer(visuals));
                      });
                    }}
                  >
                    <VisualIcon type="CONTAINER" />
                    <span className="designer__type-name">
                      {t('dashboards.designer.type.CONTAINER')}
                    </span>
                  </button>
                </li>
              </ul>
              {unplaced.length > 0 && (
                <section className="designer__widgets" aria-labelledby="designer-widgets">
                  <h3 id="designer-widgets" className="designer__subheading">
                    {t('dashboards.designer.widgets')}
                  </h3>
                  <ul className="designer__widget-list">
                    {unplaced.map((widget) => (
                      <li key={widget.code}>
                        <button
                          type="button"
                          className="designer__widget-tile"
                          aria-label={t('dashboards.designer.addWidget', {
                            title: widget.title[language],
                          })}
                          onPointerDown={(event) => {
                            startGalleryDrag(event, widget.title[language], () =>
                              newWidgetVisual(widget, visuals),
                            );
                          }}
                          onClick={() => {
                            clickedTile(() => {
                              append(newWidgetVisual(widget, visuals));
                            });
                          }}
                        >
                          <span className="designer__widget-name">{widget.title[language]}</span>
                          <span className="designer__widget-add" aria-hidden="true">
                            {t('dashboards.designer.add')}
                          </span>
                        </button>
                      </li>
                    ))}
                  </ul>
                </section>
              )}
            </section>
            <section className="designer__settings" aria-labelledby="designer-settings">
              <h2 id="designer-settings" className="designer__heading">
                {t('dashboards.designer.properties')}
              </h2>
              {selectedKeys.length > 1 ? (
                <>
                  <p className="designer__hint" role="status">
                    {t('dashboards.designer.severalSelected', { count: selectedKeys.length })}
                  </p>
                  <button
                    type="button"
                    className="button button--danger"
                    onClick={() => {
                      removeVisuals(selectedKeys);
                    }}
                  >
                    {t('dashboards.designer.removeSelected', { count: selectedKeys.length })}
                  </button>
                </>
              ) : current === undefined ? (
                <p className="designer__hint">{t('dashboards.designer.selectHint')}</p>
              ) : (
                <VisualSettings
                  key={current.key}
                  visual={current}
                  fields={fieldsOf(current)}
                  projectFields={fields.data ?? []}
                  onSourceOpen={askForRisks}
                  bounds={boundsOf(current)}
                  textRefused={textRefusedOf(current.key)}
                  onChange={(next) => {
                    place(current.key, next);
                  }}
                  onReplace={
                    visuals.some((visual) => visual.key === current.key)
                      ? (next) => {
                          replace(current.key, next);
                        }
                      : undefined
                  }
                  onDuplicate={() => {
                    duplicate(current);
                  }}
                  onRemove={() => {
                    removeVisuals([current.key]);
                  }}
                />
              )}
            </section>
          </aside>
        </div>
        <Dialog
          open={removingKeys.length > 0}
          title={
            removing === undefined
              ? t('dashboards.designer.removeSelected', { count: removingKeys.length })
              : t('dashboards.designer.removeContainer')
          }
          onClose={() => {
            setRemovingKeys([]);
          }}
        >
          <p>
            {removing === undefined
              ? t('dashboards.designer.removeSelectedBody', { count: removingKeys.length })
              : t('dashboards.designer.removeContainerBody', { title: removing.title[language] })}
          </p>
          <div className="form__actions">
            <button
              type="button"
              className="button button--danger"
              onClick={() => {
                removeAsked(false);
              }}
            >
              {removing === undefined
                ? t('dashboards.designer.removeSelectedWithVisuals')
                : t('dashboards.designer.removeWithVisuals')}
            </button>
            <button
              type="button"
              className="button button--primary"
              onClick={() => {
                removeAsked(true);
              }}
            >
              {removing === undefined
                ? t('dashboards.designer.keepContainersVisuals')
                : t('dashboards.designer.keepVisuals')}
            </button>
            <button
              type="button"
              className="button"
              onClick={() => {
                setRemovingKeys([]);
              }}
            >
              {t('dashboards.designer.cancel')}
            </button>
          </div>
        </Dialog>
        <ConfirmDialog
          confirmation={confirmation}
          onClose={() => {
            setConfirmation(null);
          }}
          onDone={() => {
            setConfirmation(null);
            setNotice({ tone: 'success', message: t('dashboards.designer.wasReset') });
            load();
          }}
        />
      </div>
    </RiskMatrixContext>
  );
}
