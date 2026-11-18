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
import {
  designApi,
  type Section,
  type Visual,
  type VisualResult,
  type VisualType,
} from './designApi.ts';
import { SectionBody } from './SectionBody.tsx';
import { sectionShown, type SectionSources } from './sections.ts';
import { VisualBody } from './VisualBody.tsx';
import { VisualIcon } from './VisualIcon.tsx';
import { VisualSettings } from './VisualSettings.tsx';
import {
  COLUMNS,
  gridPlace,
  newSection,
  MAX_HEIGHT,
  MAX_VISUALS,
  moved,
  newVisual,
  packed,
  rowsOf,
  SECTIONS,
  VISUAL_TYPES,
  visualAt,
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
}

let nextKey = 0;
const keyed = (visual: Visual): Draft => ({ ...visual, key: `visual-${String((nextKey += 1))}` });

/** A design as the API takes it: the keys are the designer's own. */
function plain(drafts: Draft[]): Visual[] {
  return drafts.map(({ key: _key, ...visual }) => visual);
}

function withoutFigures(visual: VisualResult): Visual {
  return {
    title: visual.title,
    visualType: visual.visualType,
    measures: visual.measures,
    groupBy: visual.groupBy,
    layoutColumn: visual.layoutColumn,
    layoutRow: visual.layoutRow,
    layoutWidth: visual.layoutWidth,
    layoutHeight: visual.layoutHeight,
    ...(visual.section === undefined || visual.section === null ? {} : { section: visual.section }),
  };
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
 * The Portfolio dashboard's designer (17 and 18 Nov 2026), under Dashboards and Reports: the Portfolio Manager adds visuals and sections from the
 * gallery, drags them by their bar to another place in the order and by their corner to size, and sets each one's values and grouping; the figures
 * are calculated live. The grid is always packed (the user: no white space): every move, size or removal lays the others out again at once.
 * Saving changes the dashboard for everyone, with no approval. A keyboard moves a visual earlier or later with the arrow keys on its move handle
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
  const showsCompletion = useShowsCompletion();
  const sources: SectionSources = {
    insights: insights.data,
    view: view.data,
    filters: NO_FILTERS,
    showsCompletion,
  };

  /** Puts a loaded design in the designer: packed, so a design saved before packing reads without gaps too. */
  const adopt = useCallback((loaded: VisualResult[]) => {
    const drafts = packed(loaded.map((visual) => keyed(withoutFigures(visual))));
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

  /**
   * Lays the design out again after a change, from the latest design (pointer events come faster than renders): the others close up at once.
   * `keep` is the visual whose size was just set, so the room it leaves is taken by the others.
   */
  const relayout = (change: (all: Draft[]) => Draft[], keep: string | null = null) => {
    setPlaceProblem(null);
    setVisuals((all) => {
      const next = change(all);
      return packed(next, keep === null ? null : next.findIndex((visual) => visual.key === keep));
    });
  };

  /** Changes the visual at `index` (its settings or size), the others reflowing around it. */
  const place = (index: number, next: Visual) => {
    const key = visuals[index]?.key ?? null;
    relayout(
      (all) => all.map((visual) => (visual.key === key ? { ...visual, ...next } : visual)),
      key,
    );
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
    append(newVisual(type, visuals));
  };

  const addSection = (section: Section) => {
    append(newSection(section, visuals));
  };

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
    drag.current = { gesture, key: start.key, startX: event.clientX, startY: event.clientY, start };
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
      relayout(
        (all) =>
          all.map((visual) =>
            visual.key === active.key
              ? {
                  ...visual,
                  // Sizing stops at the grid's end edge; it never moves the visual.
                  layoutWidth: Math.min(
                    COLUMNS - visual.layoutColumn,
                    Math.max(1, active.start.layoutWidth + columns),
                  ),
                  layoutHeight: Math.min(MAX_HEIGHT, Math.max(1, active.start.layoutHeight + rows)),
                }
              : visual,
          ),
        active.key,
      );
      return;
    }
    // Moving changes the order: over another visual, the dragged one takes that visual's place and every other moves along at once.
    const column = Math.floor(
      (size.rtl ? size.right - clientX : clientX - size.left) / size.column,
    );
    const row = Math.floor((clientY - size.top) / size.row);
    relayout((all) => {
      const from = all.findIndex((visual) => visual.key === active.key);
      const over = visualAt(all, column, row);
      return over >= 0 && over !== from ? moved(all, from, over) : all;
    });
  };

  /** Arrow keys on a handle: earlier or later in the order on the move handle, smaller or larger on the size handle; Delete removes the visual. */
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
      relayout((all) => moved(all, index, index + (across !== 0 ? across : down)));
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
    return calculated?.groups !== undefined &&
      calculated.visualType === visual.visualType &&
      calculated.groupBy === visual.groupBy &&
      JSON.stringify(calculated.measures) === JSON.stringify(visual.measures)
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
                      <VisualBody visual={figuresOf(visual)} />
                    ) : sectionShown(visual.section, sources) ? (
                      <SectionBody section={visual.section} sources={sources} />
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
            <h3 className="designer__subheading">{t('dashboards.designer.sections')}</h3>
            <p className="designer__hint">{t('dashboards.designer.sectionsHint')}</p>
            <ul className="designer__sections">
              {SECTIONS.map((section) => {
                const placed = visuals.some((visual) => visual.section === section);
                return (
                  <li key={section}>
                    <button
                      type="button"
                      className="designer__section"
                      disabled={placed}
                      onClick={() => {
                        addSection(section);
                      }}
                    >
                      <span>{t(`dashboards.designer.section.${section}`)}</span>
                      {placed ? (
                        <span className="designer__section-placed">
                          {t('dashboards.designer.sectionPlaced')}
                        </span>
                      ) : (
                        <span className="designer__section-add" aria-hidden="true">
                          {t('dashboards.designer.sectionAdd')}
                        </span>
                      )}
                    </button>
                  </li>
                );
              })}
            </ul>
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
