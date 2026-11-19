import {
  type CSSProperties,
  type KeyboardEvent,
  type PointerEvent,
  type ReactElement,
  useCallback,
  useEffect,
  useRef,
  useState,
} from 'react';
import { Link } from 'react-router';

import {
  ConfirmDialog,
  type Confirmation,
} from '@/features/identity-access/components/ConfirmDialog.tsx';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
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
  containerMinSize,
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
import { designApi, type Visual, type VisualResult, type VisualType } from './designApi.ts';
import { lineStart, normalized } from './fields.ts';
import { SectionBody } from './SectionBody.tsx';
import { sectionShown, type SectionSources } from './sections.ts';
import { VisualBody } from './VisualBody.tsx';
import { edgeScroller } from './edgeScroll.ts';
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
  MAX_HEIGHT,
  MAX_VISUALS,
  newVisual,
  packed,
  rowsOf,
  VISUAL_TYPES,
} from './visuals.ts';

const NO_FILTERS: PortfolioFilters = {};

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
  /** The cell of the visual under the pointer when it was grabbed, so the visual keeps that cell under the pointer. */
  grabColumn: number;
  grabRow: number;
}

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
    groupDateUnit: visual.groupDateUnit,
    filters: visual.filters,
    axes: visual.axes,
    ...(visual.children === undefined || visual.children === null
      ? {}
      : { children: visual.children.map(withoutFigures) }),
  });
}

/** What a visual's figures are calculated from: its type, measures or field values, grouping and filters. */
function figuresBasis(visual: Visual): string {
  const { visualType, measures, groupBy, values, groupField, groupDateUnit, filters } =
    normalized(visual);
  return JSON.stringify({
    visualType,
    measures,
    groupBy,
    values,
    groupField,
    groupDateUnit,
    filters,
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
 * never sized smaller than they need (containers.ts).
 * Saving changes the dashboard for everyone, with no approval. A keyboard moves a visual one cell with the arrow keys on its move handle
 * (a child within its container) and sizes it on its size handle.
 */
export function DashboardDesignerPage(): ReactElement {
  const { t, language } = useI18n();
  const [saved, setSaved] = useState<Visual[] | null>(null);
  const [visuals, setVisuals] = useState<Draft[]>([]);
  const [isStandard, setIsStandard] = useState(true);
  const [loadError, setLoadError] = useState<unknown>(null);
  const [figures, setFigures] = useState<ReadonlyMap<string, VisualResult>>(new Map());
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const [draggingKey, setDraggingKey] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const [placeProblem, setPlaceProblem] = useState<string | null>(null);
  /** The container whose removal is being asked about: with its visuals, or keeping them. */
  const [removingKey, setRemovingKey] = useState<string | null>(null);
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
  }, [adopt]);

  useEffect(load, [load]);

  // The figures follow the design: a short while after the last change, the API calculates the design as it stands, unsaved.
  useEffect(() => {
    if (saved === null || visuals.length === 0) {
      return undefined;
    }
    const controller = new AbortController();
    const sent = visuals;
    const timer = window.setTimeout(() => {
      designApi.preview(plain(sent), controller.signal).then(
        (design) => {
          setFigures(figuresByKey(sent, design.visuals));
          setPreviewError(null);
        },
        (error: unknown) => {
          if (!controller.signal.aborted) {
            setPreviewError(dashboardProblemMessage(error, t));
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
    removingKey === null ? undefined : visuals.find((visual) => visual.key === removingKey);

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
    relayout((all) => withChanged(all, key, (visual) => ({ ...visual, ...settings })));
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

  /** Removes the visual `key`; a container with visuals in it asks first whether they go with it. */
  const remove = (key: string) => {
    const path = pathOf(visuals, key);
    const visual = path === null ? undefined : visualAt(visuals, path);
    if (visual !== undefined && childrenOf(visual).length > 0) {
      setRemovingKey(key);
      return;
    }
    relayout((all) => {
      const at = pathOf(all, key);
      return at === null ? all : withoutVisual(all, at);
    });
    setSelectedKey(null);
  };

  const removeContainer = (keepVisuals: boolean) => {
    const key = removingKey;
    relayout((all) =>
      withoutContainer(
        all,
        all.findIndex((visual) => visual.key === key),
        keepVisuals,
      ),
    );
    setRemovingKey(null);
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

  /** The size of one grid step, read from the drawn canvas: a column's width and a row's height, each with its gap. */
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
  /** The grid cell under the pointer. */
  const cellAt = (clientX: number, clientY: number, size: ReturnType<typeof step>): Cell => ({
    column: Math.floor((size.rtl ? size.right - clientX : clientX - size.left) / size.column),
    row: Math.floor((clientY - size.top) / size.row),
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
    };
    setSelectedKey(start.key);
    setDraggingKey(start.key);
    // Near the window's top or bottom edge the page scrolls, and the visual follows, so it can reach any place on a long dashboard.
    const scroller = edgeScroller(dragTo);
    const onMove = (move: globalThis.PointerEvent) => {
      dragTo(move.clientX, move.clientY);
      scroller.update(move.clientX, move.clientY);
    };
    const onEnd = () => {
      scroller.stop();
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onEnd);
      window.removeEventListener('pointercancel', onEnd);
      drag.current = null;
      setDraggingKey(null);
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
   * Drag and drop from the gallery (19 Nov 2026): pressed and moved a few pixels, a tile drags a new visual; over the dashboard it is placed at
   * the cell under the pointer, or inside the container under it, as a moved visual is (`moved`); dropped elsewhere nothing is added. A press
   * that does not move stays a click, which adds the visual at the first free place.
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
        draft = keyed(made);
        draggedFromGallery.current = true;
        drag.current = {
          gesture: 'move',
          key: draft.key,
          from: { index: before.length, child: null },
          startX,
          startY,
          start: draft,
          before: [...before, draft],
          // The new visual's top-start corner follows the pointer.
          grabColumn: 0,
          grabRow: 0,
        };
        setDraggingKey(draft.key);
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
      setDraggingKey(null);
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
      const columns = Math.round(((clientX - active.startX) * (size.rtl ? -1 : 1)) / size.column);
      const rows = Math.round((clientY - active.startY) / size.row);
      // Sizing stops at the grid's (or the container's) end edge, and a container at what its visuals need; it never moves the visual.
      const across = containerOf(active.before, active.from)?.layoutWidth ?? COLUMNS;
      relayout(() =>
        changedAt(active.before, active.from, {
          ...active.start,
          layoutWidth: Math.min(
            across - active.start.layoutColumn,
            Math.max(1, active.start.layoutWidth + columns),
          ),
          layoutHeight: Math.min(MAX_HEIGHT, Math.max(1, active.start.layoutHeight + rows)),
        }),
      );
      return;
    }
    // Moving is free: the cell it was grabbed by stays under the pointer, and the pointer's cell decides where it goes: into the container
    // whose inner grid it is on, or onto the dashboard (`moved`).
    const pointer = cellAt(clientX, clientY, size);
    const result = moved(active.before, active.from, pointer, {
      column: pointer.column - active.grabColumn,
      row: pointer.row - active.grabRow,
    });
    setPlaceProblem(result.noRoom ? t('dashboards.designer.noRoomInContainer') : null);
    setVisuals(result.visuals);
  };

  /**
   * Arrow keys on a handle: one cell on the move handle (a child within its container), smaller or larger on the size handle; Delete removes
   * the visual.
   */
  const keyOnHandle = (event: KeyboardEvent<HTMLButtonElement>, key: string, gesture: Gesture) => {
    const path = pathOf(visuals, key);
    const visual = path === null ? undefined : visualAt(visuals, path);
    if (path === null || visual === undefined) {
      return;
    }
    if (event.key === 'Delete' || event.key === 'Backspace') {
      event.preventDefault();
      remove(key);
      return;
    }
    const across = containerOf(visuals, path)?.layoutWidth ?? COLUMNS;
    // The arrow pointing to the reading end is forward: right in English, left in Arabic.
    const forward = step().rtl ? 'ArrowLeft' : 'ArrowRight';
    const backward = step().rtl ? 'ArrowRight' : 'ArrowLeft';
    const sideways = event.key === forward ? 1 : event.key === backward ? -1 : 0;
    const down = event.key === 'ArrowDown' ? 1 : event.key === 'ArrowUp' ? -1 : 0;
    if (sideways === 0 && down === 0) {
      return;
    }
    event.preventDefault();
    setSelectedKey(visual.key);
    relayout((all) =>
      withChanged(all, key, (latest) =>
        gesture === 'move'
          ? {
              ...latest,
              layoutColumn: Math.min(
                across - latest.layoutWidth,
                Math.max(0, latest.layoutColumn + sideways),
              ),
              layoutRow: Math.max(0, latest.layoutRow + down),
            }
          : {
              ...latest,
              layoutWidth: Math.min(
                across - latest.layoutColumn,
                Math.max(1, latest.layoutWidth + sideways),
              ),
              layoutHeight: Math.min(MAX_HEIGHT, Math.max(1, latest.layoutHeight + down)),
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
    `${base}${visual.key === selectedKey ? ' is-selected' : ''}${
      visual.key === draggingKey ? ' is-dragging' : ''
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
          onClick={() => {
            setSelectedKey(visual.key);
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

  /** A visual at `place`: on the dashboard's grid, or on its container's inner grid. */
  const renderVisual = (visual: Draft, place: CSSProperties) => (
    // Its own key: a visual keeps its element, and its pointer, while the others reflow around it.
    <section
      key={visual.key}
      className={classOf('widget visual designer__visual', visual)}
      style={place}
      aria-label={visual.title[language]}
      aria-current={visual.key === selectedKey ? 'true' : undefined}
    >
      {renderBar(visual, 'designer__visual-bar', 'widget__title')}
      <div
        className={
          visual.section === undefined || visual.section === null
            ? 'visual__body'
            : 'visual__body visual__body--section'
        }
      >
        {visual.section === undefined || visual.section === null ? (
          <VisualBody visual={figuresOf(visual)} fields={fields.data} />
        ) : sectionShown(visual, sources) ? (
          <SectionBody section={visual.section} widgetCode={visual.widgetCode} sources={sources} />
        ) : (
          <p className="state">{t('dashboards.noData')}</p>
        )}
      </div>
      {renderResizeHandle(visual)}
    </section>
  );

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
        aria-current={container.key === selectedKey ? 'true' : undefined}
        data-visual="CONTAINER"
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

  /** The sizes the selected visual may take where it is. */
  const boundsOf = (visual: Draft): SizeBounds => {
    const least = isContainer(visual) ? containerMinSize(visual) : { width: 1, height: 1 };
    return {
      minWidth: least.width,
      minHeight: least.height,
      maxWidth:
        (selected === null ? undefined : containerOf(visuals, selected))?.layoutWidth ?? COLUMNS,
    };
  };

  return (
    <div className="designer">
      {ghost !== null && (
        <div className="designer__ghost" aria-hidden="true" style={{ left: ghost.x, top: ghost.y }}>
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
          {visuals.length === 0 ? <p className="state">{t('dashboards.designer.empty')}</p> : null}
          <div
            ref={canvas}
            className="visual-grid designer__canvas"
            style={{ '--canvas-rows': String(rowsOf(visuals) + SPARE_ROWS) } as CSSProperties}
          >
            {visuals.map((visual) =>
              isContainer(visual)
                ? renderContainer(visual)
                : renderVisual(visual, gridPlace(visual)),
            )}
          </div>
        </div>
        <aside className="designer__pane">
          <section className="designer__gallery" aria-labelledby="designer-gallery">
            <h2 id="designer-gallery" className="designer__heading">
              {t('dashboards.designer.gallery')}
            </h2>
            <p className="designer__hint">{t('dashboards.designer.galleryHint')}</p>
            <ul className="designer__types">
              {VISUAL_TYPES.map((type) => (
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
            <p className="designer__hint">{t('dashboards.designer.keyboardHint')}</p>
          </section>
          <section className="designer__settings" aria-labelledby="designer-settings">
            <h2 id="designer-settings" className="designer__heading">
              {t('dashboards.designer.properties')}
            </h2>
            {current === undefined ? (
              <p className="designer__hint">{t('dashboards.designer.selectHint')}</p>
            ) : (
              <VisualSettings
                key={current.key}
                visual={current}
                fields={fields}
                bounds={boundsOf(current)}
                onChange={(next) => {
                  place(current.key, next);
                }}
                onDuplicate={() => {
                  duplicate(current);
                }}
                onRemove={() => {
                  remove(current.key);
                }}
              />
            )}
          </section>
        </aside>
      </div>
      <Dialog
        open={removing !== undefined}
        title={t('dashboards.designer.removeContainer')}
        onClose={() => {
          setRemovingKey(null);
        }}
      >
        <p>
          {t('dashboards.designer.removeContainerBody', {
            title: removing?.title[language] ?? '',
          })}
        </p>
        <div className="form__actions">
          <button
            type="button"
            className="button button--danger"
            onClick={() => {
              removeContainer(false);
            }}
          >
            {t('dashboards.designer.removeWithVisuals')}
          </button>
          <button
            type="button"
            className="button button--primary"
            onClick={() => {
              removeContainer(true);
            }}
          >
            {t('dashboards.designer.keepVisuals')}
          </button>
          <button
            type="button"
            className="button"
            onClick={() => {
              setRemovingKey(null);
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
  );
}
