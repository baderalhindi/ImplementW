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
  newVisual,
  placeWith,
  rowsOf,
  SECTIONS,
  VISUAL_TYPES,
} from './visuals.ts';

const NO_FILTERS: PortfolioFilters = {};

/** Rows of empty grid under the lowest visual, so a visual can be dragged below the others. */
const SPARE_ROWS = 4;
/** How long the designer waits after the last change before it calculates the figures again. */
const PREVIEW_DELAY_MS = 350;

type Gesture = 'move' | 'resize';

interface Drag {
  gesture: Gesture;
  index: number;
  startX: number;
  startY: number;
  start: Visual;
  /** The whole design when the drag began: each step is placed on it, so visuals pushed aside come back when the pointer moves on. */
  design: Visual[];
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

function sameDesign(a: Visual[], b: Visual[]): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

/**
 * The Portfolio dashboard's designer (17 Nov 2026), under Dashboards and Reports: the Portfolio Manager adds visuals from the gallery, drags
 * them into place and by their corner to size, and sets each one's values and grouping; the figures are calculated live. Saving changes the
 * dashboard for everyone, with no approval, as the business decided. A keyboard moves a visual with the arrow keys on its move handle and sizes it
 * on its size handle.
 */
export function DashboardDesignerPage(): ReactElement {
  const { t, language } = useI18n();
  const [saved, setSaved] = useState<Visual[] | null>(null);
  const [visuals, setVisuals] = useState<Visual[]>([]);
  const [isStandard, setIsStandard] = useState(true);
  const [loadError, setLoadError] = useState<unknown>(null);
  const [figures, setFigures] = useState<VisualResult[] | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [selected, setSelected] = useState<number | null>(null);
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

  const load = useCallback(() => {
    designApi.get({}).then(
      (design) => {
        const loaded = design.visuals.map(withoutFigures);
        setSaved(loaded);
        setVisuals(loaded);
        setFigures(design.visuals);
        setIsStandard(design.isStandard);
        setSelected(null);
        setLoadError(null);
      },
      (error: unknown) => {
        setLoadError(error);
      },
    );
  }, []);

  useEffect(load, [load]);

  // The figures follow the design: a short while after the last change, the API calculates the design as it stands, unsaved.
  useEffect(() => {
    if (saved === null || visuals.length === 0) {
      return undefined;
    }
    const controller = new AbortController();
    const timer = window.setTimeout(() => {
      designApi.preview(visuals, controller.signal).then(
        (design) => {
          setFigures(design.visuals);
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

  const dirty = !sameDesign(visuals, saved);
  const current = selected === null ? undefined : visuals[selected];

  /** Puts `next` at `index`: it takes its place, and the visuals it lands on move down out of its way. */
  const place = (index: number, next: Visual) => {
    setPlaceProblem(null);
    setVisuals((all) =>
      placeWith(
        all.map((visual, at) => (at === index ? next : visual)),
        index,
        next,
      ),
    );
  };

  const add = (type: VisualType) => {
    const visual = newVisual(type, visuals);
    if (visual === null) {
      setPlaceProblem(t('dashboards.designer.noRoom'));
      return;
    }
    setPlaceProblem(null);
    setNotice(null);
    setVisuals((all) => [...all, visual]);
    setSelected(visuals.length);
  };

  const addSection = (section: Section) => {
    const visual = newSection(section, visuals);
    if (visual === null) {
      setPlaceProblem(t('dashboards.designer.noRoom'));
      return;
    }
    setPlaceProblem(null);
    setNotice(null);
    setVisuals((all) => [...all, visual]);
    setSelected(visuals.length);
  };

  const remove = (index: number) => {
    setVisuals((all) => all.filter((_, at) => at !== index));
    setSelected(null);
  };

  const duplicate = (index: number) => {
    const source = visuals[index];
    const copy = source === undefined ? null : newVisual(source.visualType, visuals);
    if (source === undefined || copy === null) {
      setPlaceProblem(t('dashboards.designer.noRoom'));
      return;
    }
    const placed = { ...source, layoutColumn: copy.layoutColumn, layoutRow: copy.layoutRow };
    setVisuals((all) => [...all, placed]);
    setSelected(visuals.length);
  };

  /** The size of one grid step, read from the drawn canvas: a column's width and a row's height, each with its gap. */
  const step = () => {
    const element = canvas.current;
    if (element === null) {
      return { column: 1, row: 1, rtl: false };
    }
    const style = window.getComputedStyle(element);
    const columnGap = Number.parseFloat(style.columnGap) || 0;
    const rowGap = Number.parseFloat(style.rowGap) || 0;
    const rowHeight = Number.parseFloat(style.gridAutoRows) || 72;
    return {
      column: (element.clientWidth + columnGap) / COLUMNS,
      row: rowHeight + rowGap,
      rtl: style.direction === 'rtl',
    };
  };

  const startDrag = (event: PointerEvent<HTMLElement>, index: number, gesture: Gesture) => {
    const start = visuals[index];
    // The bar drags the visual, but its edit button stays a button.
    const onButton = (event.target as Element).closest('button:not(.designer__handle)') !== null;
    if (start === undefined || event.button !== 0 || onButton) {
      return;
    }
    event.preventDefault();
    event.currentTarget.setPointerCapture(event.pointerId);
    drag.current = {
      gesture,
      index,
      startX: event.clientX,
      startY: event.clientY,
      start,
      design: visuals,
    };
    setSelected(index);
  };

  const moveDrag = (event: PointerEvent<HTMLElement>) => {
    const active = drag.current;
    if (active === null) {
      return;
    }
    const size = step();
    const columns = Math.round(
      ((event.clientX - active.startX) * (size.rtl ? -1 : 1)) / size.column,
    );
    const rows = Math.round((event.clientY - active.startY) / size.row);
    const next =
      active.gesture === 'move'
        ? {
            ...active.start,
            layoutColumn: active.start.layoutColumn + columns,
            layoutRow: active.start.layoutRow + rows,
          }
        : {
            ...active.start,
            // Sizing stops at the grid's end edge; it never moves the visual.
            layoutWidth: Math.min(
              COLUMNS - active.start.layoutColumn,
              active.start.layoutWidth + columns,
            ),
            layoutHeight: active.start.layoutHeight + rows,
          };
    const shown = visuals[active.index];
    if (
      shown !== undefined &&
      (shown.layoutColumn !== next.layoutColumn ||
        shown.layoutRow !== next.layoutRow ||
        shown.layoutWidth !== next.layoutWidth ||
        shown.layoutHeight !== next.layoutHeight)
    ) {
      setPlaceProblem(null);
      setVisuals(placeWith(active.design, active.index, next));
    }
  };

  const endDrag = () => {
    drag.current = null;
  };

  /** Arrow keys on a handle: move by a step on the move handle, size by a step on the size handle; Delete removes the visual. */
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
    // The arrow pointing to the reading end grows the column: right in English, left in Arabic.
    const forward = step().rtl ? 'ArrowLeft' : 'ArrowRight';
    const backward = step().rtl ? 'ArrowRight' : 'ArrowLeft';
    const across = event.key === forward ? 1 : event.key === backward ? -1 : 0;
    const down = event.key === 'ArrowDown' ? 1 : event.key === 'ArrowUp' ? -1 : 0;
    if (across === 0 && down === 0) {
      return;
    }
    event.preventDefault();
    setSelected(index);
    place(
      index,
      gesture === 'move'
        ? {
            ...visual,
            layoutColumn: visual.layoutColumn + across,
            layoutRow: visual.layoutRow + down,
          }
        : {
            ...visual,
            layoutWidth: Math.min(COLUMNS - visual.layoutColumn, visual.layoutWidth + across),
            layoutHeight: visual.layoutHeight + down,
          },
    );
  };

  const save = async () => {
    setBusy(true);
    setNotice(null);
    try {
      const design = await designApi.save(visuals);
      const kept = design.visuals.map(withoutFigures);
      setSaved(kept);
      setVisuals(kept);
      setIsStandard(false);
      setNotice({ tone: 'success', message: t('dashboards.designer.saved') });
    } catch (error) {
      setPreviewError(dashboardProblemMessage(error, t));
    } finally {
      setBusy(false);
    }
  };

  const figuresOf = (index: number, visual: Visual): VisualResult => {
    const calculated = figures?.[index];
    // Until the new figures arrive, a visual whose values changed shows none rather than another visual's.
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
                setVisuals(saved);
                setSelected(null);
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
              const isSelected = index === selected;
              return (
                // The index, not the place: a visual keeps its element, and its pointer, while it is dragged.
                <section
                  key={index}
                  className={`widget visual designer__visual${isSelected ? ' is-selected' : ''}`}
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
                    onPointerMove={moveDrag}
                    onPointerUp={endDrag}
                    onPointerCancel={endDrag}
                  >
                    <button
                      type="button"
                      className="designer__handle designer__handle--move"
                      aria-label={t('dashboards.designer.moveHandle', { title })}
                      title={t('dashboards.designer.moveHandle', { title })}
                      onClick={() => {
                        setSelected(index);
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
                        setSelected(isSelected ? null : index);
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
                      <VisualBody visual={figuresOf(index, visual)} />
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
                    onPointerMove={moveDrag}
                    onPointerUp={endDrag}
                    onPointerCancel={endDrag}
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
                      {placed && (
                        <span className="designer__section-placed">
                          {t('dashboards.designer.sectionPlaced')}
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
            {selected === null || current === undefined ? (
              <p className="designer__hint">{t('dashboards.designer.selectHint')}</p>
            ) : (
              <VisualSettings
                key={selected}
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
