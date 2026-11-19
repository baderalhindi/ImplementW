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
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { PORTFOLIO_DASHBOARD_PATH } from '../paths.ts';
import { dashboardProblemMessage } from '../problems.ts';
import { dashboardsApi, type PortfolioFilters } from '../api/dashboardsApi.ts';
import { usePortfolioInsights, useShowsCompletion } from '../insights/insightSections.ts';
import { designApi, type Visual, type VisualResult, type VisualType } from './designApi.ts';
import { lineStart, normalized } from './fields.ts';
import { SectionBody } from './SectionBody.tsx';
import { sectionShown, type SectionSources } from './sections.ts';
import { VisualBody } from './VisualBody.tsx';
import { VisualIcon } from './VisualIcon.tsx';
import { VisualSettings } from './VisualSettings.tsx';
import {
  hasWidgetsToSplit,
  newWidgetVisual,
  splitWidgets,
  unplacedWidgets,
} from './widgetVisuals.ts';
import {
  COLUMNS,
  gridPlace,
  MAX_HEIGHT,
  MAX_VISUALS,
  inReadingOrder,
  newVisual,
  packed,
  rowsOf,
  settled,
  VISUAL_TYPES,
} from './visuals.ts';

const NO_FILTERS: PortfolioFilters = {};

/** Rows of empty grid under the lowest visual, so a visual can be dragged below the others. */
const SPARE_ROWS = 4;
/** How long the designer waits after the last change before it calculates the figures again. */
const PREVIEW_DELAY_MS = 350;

type Gesture = 'move' | 'resize';

/** A visual as the designer holds it: with a key of its own, so it keeps its element (and the pointer) while the others reflow around it. */
type Draft = Visual & { key: string };

interface Drag {
  gesture: Gesture;
  key: string;
  startX: number;
  startY: number;
  start: Visual;
  /** The design when the drag began: each step is laid out from it, so a visual passed over goes back to its place. */
  before: Draft[];
  /** The cell of the visual under the pointer when it was grabbed, so the visual keeps that cell under the pointer. */
  grabColumn: number;
  grabRow: number;
}

let nextKey = 0;
const keyed = (visual: Visual): Draft => ({ ...visual, key: `visual-${String((nextKey += 1))}` });

/** A design as the API takes it: the keys are the designer's own, and field data only where a visual has some. */
function plain(drafts: Draft[]): Visual[] {
  return drafts.map(({ key: _key, ...visual }) => normalized(visual));
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

/** Each visual's figures by its key: the API answers a design in the order it was sent. */
function figuresByKey(drafts: Draft[], results: VisualResult[]): ReadonlyMap<string, VisualResult> {
  return new Map(
    drafts.flatMap((draft, index) => {
      const result = results[index];
      return result === undefined ? [] : [[draft.key, result] as const];
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
 * Saving changes the dashboard for everyone, with no approval. A keyboard moves a visual one cell with the arrow keys on its move handle
 * and sizes it on its size handle.
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
    const drafts = inReadingOrder(loaded.map((visual) => keyed(withoutFigures(visual))));
    setVisuals(drafts);
    setSaved(plain(drafts));
    setFigures(figuresByKey(drafts, loaded));
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
  const selected = selectedKey === null ? null : visuals.findIndex((v) => v.key === selectedKey);
  const current = selected === null || selected < 0 ? undefined : visuals[selected];

  /** Applies a change to the latest design (pointer events come faster than renders). */
  const relayout = (change: (all: Draft[]) => Draft[]) => {
    setPlaceProblem(null);
    setVisuals((all) => change(all));
  };

  /** The design with the visual `key` changed to `next`, and whatever it now covers moved down below it. */
  const withChanged = (all: Draft[], key: string | null, next: Partial<Visual>): Draft[] => {
    const changed = all.map((visual) => (visual.key === key ? { ...visual, ...next } : visual));
    return settled(
      changed,
      changed.findIndex((visual) => visual.key === key),
    );
  };

  /** Changes the visual at `index` (its settings or size); a visual it now covers moves down below it. */
  const place = (index: number, next: Visual) => {
    const key = visuals[index]?.key ?? null;
    relayout((all) => withChanged(all, key, next));
  };

  const append = (visual: Visual | null) => {
    if (visual === null) {
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

  const remove = (index: number) => {
    const key = visuals[index]?.key;
    relayout((all) => all.filter((visual) => visual.key !== key));
    setSelectedKey(null);
  };

  const duplicate = (index: number) => {
    const source = visuals[index];
    if (source !== undefined) {
      append(visuals.length >= MAX_VISUALS ? null : { ...source, section: null });
    }
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
  const startDrag = (event: PointerEvent<HTMLElement>, index: number, gesture: Gesture) => {
    const start = visuals[index];
    // The bar drags the visual, but its edit button stays a button.
    const onButton =
      gesture === 'move' &&
      (event.target as Element).closest('button:not(.designer__handle--move)') !== null;
    if (start === undefined || event.button !== 0 || onButton) {
      return;
    }
    event.preventDefault();
    const size = step();
    drag.current = {
      gesture,
      key: start.key,
      startX: event.clientX,
      startY: event.clientY,
      start,
      before: visuals,
      grabColumn:
        Math.floor(
          (size.rtl ? size.right - event.clientX : event.clientX - size.left) / size.column,
        ) - start.layoutColumn,
      grabRow: Math.floor((event.clientY - size.top) / size.row) - start.layoutRow,
    };
    setSelectedKey(start.key);
    setDraggingKey(start.key);
    const onMove = (move: globalThis.PointerEvent) => {
      dragTo(move.clientX, move.clientY);
    };
    const onEnd = () => {
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

  const dragTo = (clientX: number, clientY: number) => {
    const active = drag.current;
    if (active === null) {
      return;
    }
    const size = step();
    if (active.gesture === 'resize') {
      const columns = Math.round(((clientX - active.startX) * (size.rtl ? -1 : 1)) / size.column);
      const rows = Math.round((clientY - active.startY) / size.row);
      relayout(() =>
        withChanged(active.before, active.key, {
          // Sizing stops at the grid's end edge; it never moves the visual.
          layoutWidth: Math.min(
            COLUMNS - active.start.layoutColumn,
            Math.max(1, active.start.layoutWidth + columns),
          ),
          layoutHeight: Math.min(MAX_HEIGHT, Math.max(1, active.start.layoutHeight + rows)),
        }),
      );
      return;
    }
    // Moving is free: the visual goes to the cell under the pointer (the cell it was grabbed by stays under it), within the grid's columns.
    const column = Math.min(
      COLUMNS - active.start.layoutWidth,
      Math.max(
        0,
        Math.floor((size.rtl ? size.right - clientX : clientX - size.left) / size.column) -
          active.grabColumn,
      ),
    );
    const row = Math.max(0, Math.floor((clientY - size.top) / size.row) - active.grabRow);
    relayout(() =>
      withChanged(active.before, active.key, { layoutColumn: column, layoutRow: row }),
    );
  };

  /** Arrow keys on a handle: one cell on the move handle, smaller or larger on the size handle; Delete removes the visual. */
  const keyOnHandle = (
    event: KeyboardEvent<HTMLButtonElement>,
    index: number,
    gesture: Gesture,
  ) => {
    const visual = visuals[index];
    if (visual === undefined) {
      return;
    }
    if (event.key === 'Delete' || event.key === 'Backspace') {
      event.preventDefault();
      remove(index);
      return;
    }
    // The arrow pointing to the reading end is forward: right in English, left in Arabic.
    const forward = step().rtl ? 'ArrowLeft' : 'ArrowRight';
    const backward = step().rtl ? 'ArrowRight' : 'ArrowLeft';
    const across = event.key === forward ? 1 : event.key === backward ? -1 : 0;
    const down = event.key === 'ArrowDown' ? 1 : event.key === 'ArrowUp' ? -1 : 0;
    if (across === 0 && down === 0) {
      return;
    }
    event.preventDefault();
    setSelectedKey(visual.key);
    if (gesture === 'move') {
      relayout((all) =>
        withChanged(all, visual.key, {
          layoutColumn: Math.min(
            COLUMNS - visual.layoutWidth,
            Math.max(0, visual.layoutColumn + across),
          ),
          layoutRow: Math.max(0, visual.layoutRow + down),
        }),
      );
    } else {
      place(index, {
        ...visual,
        layoutWidth: Math.min(COLUMNS - visual.layoutColumn, visual.layoutWidth + across),
        layoutHeight: visual.layoutHeight + down,
      });
    }
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

  const figuresOf = (visual: Draft): VisualResult => {
    const calculated = figures.get(visual.key);
    // Until the new figures arrive, a visual whose values changed shows none rather than figures for other values.
    return calculated?.groups !== undefined && figuresBasis(calculated) === figuresBasis(visual)
      ? { ...calculated, ...visual }
      : { ...visual, groups: [] };
  };

  return (
    <div className="designer">
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
            {visuals.map((visual, index) => {
              const title = visual.title[language];
              const isSelected = visual.key === selectedKey;
              return (
                // Its own key: a visual keeps its element, and its pointer, while the others reflow around it.
                <section
                  key={visual.key}
                  className={`widget visual designer__visual${isSelected ? ' is-selected' : ''}${
                    visual.key === draggingKey ? ' is-dragging' : ''
                  }`}
                  style={gridPlace(visual)}
                  aria-label={title}
                  aria-current={isSelected ? 'true' : undefined}
                >
                  {/* The whole bar drags the visual, as its handle does; the keyboard moves it on the handle. */}
                  <div
                    className="designer__visual-bar"
                    onPointerDown={(event) => {
                      startDrag(event, index, 'move');
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
                        keyOnHandle(event, index, 'move');
                      }}
                    >
                      <span aria-hidden="true">⠿</span>
                    </button>
                    <h3 className="widget__title">{title}</h3>
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
                      <SectionBody
                        section={visual.section}
                        widgetCode={visual.widgetCode}
                        sources={sources}
                      />
                    ) : (
                      <p className="state">{t('dashboards.noData')}</p>
                    )}
                  </div>
                  <button
                    type="button"
                    className="designer__handle designer__handle--resize"
                    aria-label={t('dashboards.designer.resizeHandle', { title })}
                    title={t('dashboards.designer.resizeHandle', { title })}
                    onPointerDown={(event) => {
                      startDrag(event, index, 'resize');
                    }}
                    onKeyDown={(event) => {
                      keyOnHandle(event, index, 'resize');
                    }}
                  />
                </section>
              );
            })}
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
                    onClick={() => {
                      add(type);
                    }}
                  >
                    <VisualIcon type={type} />
                    <span className="designer__type-name">
                      {t(`dashboards.designer.type.${type}`)}
                    </span>
                  </button>
                </li>
              ))}
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
                        onClick={() => {
                          append(newWidgetVisual(widget, visuals));
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
            {selected === null || selected < 0 || current === undefined ? (
              <p className="designer__hint">{t('dashboards.designer.selectHint')}</p>
            ) : (
              <VisualSettings
                key={current.key}
                visual={current}
                fields={fields}
                onChange={(next) => {
                  place(selected, next);
                }}
                onDuplicate={() => {
                  duplicate(selected);
                }}
                onRemove={() => {
                  remove(selected);
                }}
              />
            )}
          </section>
        </aside>
      </div>
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
