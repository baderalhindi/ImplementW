import 'maplibre-gl/dist/maplibre-gl.css';

import type { ExpressionSpecification, Map as MapLibreMap, Marker } from 'maplibre-gl';
import type * as MapLibreLibrary from 'maplibre-gl';
import workerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?url';
import { type ReactElement, useEffect, useRef, useState } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

/**
 * One circle: a place and its figure (a project count, or a map visual's figure). Its area grows with `value`, so a large place does not swallow its
 * neighbours; it is labelled with `figure`, the value as it reads.
 */
export interface MapPoint {
  id: string;
  name: string;
  latitude: number;
  longitude: number;
  value: number;
  figure: string;
}

type MapLibre = typeof MapLibreLibrary;

interface PlacedBubble {
  marker: Marker;
  element: HTMLButtonElement;
  stackOrder: number;
}

/** OpenFreeMap's public instance: free for commercial use, with no key and no limit, and no uptime guarantee either. */
const STYLE_URL = 'https://tiles.openfreemap.org/styles/liberty';
const MIN_DIAMETER = 26;
const MAX_DIAMETER = 50;
const INITIAL_CENTER: [number, number] = [49.8, 25.0];
const INITIAL_ZOOM = 6;
/** Pressing a circle zooms to here at least, so its close neighbours separate. */
const SELECTED_ZOOM = 9;
const FIT_MAX_ZOOM = 10;
const MAX_ZOOM = 16;
/** The longest figure a circle holds as it is; a longer one widens it. */
const SHORT_FIGURE = 3;

/** The style's numbered road signs: on Saudi roads the data often has no number, which draws an empty box. */
const ROAD_SIGN_LAYERS = new Set([
  'highway-shield-non-us',
  'highway-shield-us-interstate',
  'road_shield_us',
]);
/** The style's colours, softened towards the look people know from Google Maps. */
const SOFTER_COLOURS: [
  layer: string,
  property: Parameters<MapLibreMap['setPaintProperty']>[1],
  colour: string,
][] = [
  ['background', 'background-color', '#f5f5f3'],
  ['water', 'fill-color', '#aadaff'],
  ['landcover_sand', 'fill-color', '#f4efe1'],
];

function diameterOf(value: number, largest: number): number {
  return Math.round(
    MIN_DIAMETER +
      (MAX_DIAMETER - MIN_DIAMETER) * Math.sqrt(Math.max(0, value) / Math.max(1, largest)),
  );
}

function labelExpression(language: string): ExpressionSpecification {
  return language === 'ar'
    ? ['coalesce', ['get', 'name:ar'], ['get', 'name']]
    : ['coalesce', ['get', 'name_en'], ['get', 'name:en'], ['get', 'name']];
}

function dressStyle(map: MapLibreMap, language: string): void {
  for (const layer of map.getStyle().layers) {
    if (layer.type !== 'symbol' || layer.layout?.['text-field'] === undefined) {
      continue;
    }
    if (ROAD_SIGN_LAYERS.has(layer.id)) {
      map.setLayoutProperty(layer.id, 'visibility', 'none');
    } else {
      map.setLayoutProperty(layer.id, 'text-field', labelExpression(language));
    }
  }
  for (const [layer, property, colour] of SOFTER_COLOURS) {
    if (map.getLayer(layer) !== undefined) {
      map.setPaintProperty(layer, property, colour);
    }
  }
}

function markSelected(bubbles: Map<string, PlacedBubble>, selectedId: string | null): void {
  for (const [id, { element, stackOrder }] of bubbles) {
    const chosen = id === selectedId;
    element.classList.toggle('map-bubble--selected', chosen);
    element.style.zIndex = String(stackOrder + (chosen ? 1000 : 0));
  }
}

/**
 * The map of the projects' places (MapLibre over OpenFreeMap's vector map), labelled in the person's language. Each place is a circle holding its
 * count; pressing one, by pointer or keyboard, selects it. The smaller circles sit above the larger, so a close neighbour can still be pressed. If the
 * map's style cannot load, the circles still draw on a plain background; a browser without WebGL gets a note instead. The list beside the map says the
 * same to a screen reader. Without `showCounts` the circles hold no figure; each still names it to a screen reader and in its tooltip.
 */
export function LocationsMap({
  points,
  selectedId,
  label,
  onSelect,
  showCounts = true,
}: {
  points: MapPoint[];
  selectedId: string | null;
  label: string;
  onSelect: (id: string | null) => void;
  showCounts?: boolean;
}): ReactElement {
  const { language, t } = useI18n();
  const container = useRef<HTMLDivElement>(null);
  const library = useRef<MapLibre | null>(null);
  const map = useRef<MapLibreMap | null>(null);
  const bubbles = useRef(new Map<string, PlacedBubble>());
  const [ready, setReady] = useState(false);
  const [styleLoaded, setStyleLoaded] = useState(false);
  const [unavailable, setUnavailable] = useState(false);
  const select = useRef(onSelect);
  useEffect(() => {
    select.current = onSelect;
  }, [onSelect]);
  // The place set, not the array: a re-render with the same places keeps the person's pan and zoom.
  const placed = `${String(showCounts)};${points
    .map((point) => `${point.id}:${String(point.value)}:${point.figure}`)
    .join(',')}`;

  useEffect(() => {
    let cancelled = false;
    void import('maplibre-gl')
      .then((maplibre) => {
        if (cancelled || container.current === null) {
          return;
        }
        // Without this MapLibre starts its worker from a blob: URL, which the CSP refuses.
        maplibre.setWorkerUrl(workerUrl);
        const created = new maplibre.Map({
          container: container.current,
          style: STYLE_URL,
          center: INITIAL_CENTER,
          zoom: INITIAL_ZOOM,
          maxZoom: MAX_ZOOM,
          dragRotate: false,
          pitchWithRotate: false,
          touchPitch: false,
          // The credit is drawn by React below: MapLibre's own control writes the style's text as HTML.
          attributionControl: false,
        });
        created.addControl(new maplibre.NavigationControl({ showCompass: false }), 'top-left');
        created.on('click', (event) => {
          // The map hears a press on a circle as well; only a press on the map itself clears the selection.
          if ((event.originalEvent.target as Element).closest('.map-bubble') === null) {
            select.current(null);
          }
        });
        created.on('style.load', () => {
          setStyleLoaded(true);
        });
        library.current = maplibre;
        map.current = created;
        setReady(true);
      })
      .catch(() => {
        if (!cancelled) {
          setUnavailable(true);
        }
      });
    const placedBubbles = bubbles.current;
    return () => {
      cancelled = true;
      map.current?.remove();
      map.current = null;
      placedBubbles.clear();
    };
  }, []);

  useEffect(() => {
    if (styleLoaded && map.current !== null) {
      dressStyle(map.current, language);
    }
  }, [styleLoaded, language]);

  useEffect(() => {
    const shown = map.current;
    const maplibre = library.current;
    if (!ready || shown === null || maplibre === null) {
      return;
    }
    for (const { marker } of bubbles.current.values()) {
      marker.remove();
    }
    bubbles.current.clear();
    const largest = Math.max(1, ...points.map((point) => point.value));
    for (const point of points) {
      const diameter = diameterOf(point.value, largest);
      const name = `${point.name}: ${point.figure}`;
      const element = document.createElement('button');
      element.type = 'button';
      element.className = 'map-bubble';
      element.textContent = showCounts ? point.figure : '';
      element.setAttribute('aria-label', name);
      element.style.blockSize = `${String(diameter)}px`;
      if (showCounts && point.figure.length > SHORT_FIGURE) {
        // A longer figure (an amount, "1.2M SAR") widens its circle into a pill rather than spilling out of it.
        element.classList.add('map-bubble--wide');
        element.style.minInlineSize = `${String(diameter)}px`;
      } else {
        element.style.inlineSize = `${String(diameter)}px`;
      }
      element.addEventListener('click', () => {
        select.current(point.id);
      });
      const position: [number, number] = [point.longitude, point.latitude];
      const tooltip = new maplibre.Popup({
        anchor: 'bottom',
        closeButton: false,
        closeOnClick: false,
        offset: diameter / 2,
      })
        .setLngLat(position)
        .setText(name);
      const showTooltip = (): void => {
        tooltip.addTo(shown);
      };
      const hideTooltip = (): void => {
        tooltip.remove();
      };
      element.addEventListener('mouseenter', showTooltip);
      element.addEventListener('focus', showTooltip);
      element.addEventListener('mouseleave', hideTooltip);
      element.addEventListener('blur', hideTooltip);
      const marker = new maplibre.Marker({ element }).setLngLat(position).addTo(shown);
      bubbles.current.set(point.id, { marker, element, stackOrder: 1000 - diameter });
    }
    // `placed` stands for `points`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ready, placed]);

  useEffect(() => {
    markSelected(bubbles.current, selectedId);
  }, [ready, placed, selectedId]);

  useEffect(() => {
    const shown = map.current;
    const maplibre = library.current;
    if (
      !ready ||
      shown === null ||
      maplibre === null ||
      points.length === 0 ||
      container.current?.clientWidth === 0
    ) {
      return;
    }
    const chosen = points.find((point) => point.id === selectedId);
    if (chosen !== undefined) {
      shown.flyTo({
        center: [chosen.longitude, chosen.latitude],
        zoom: Math.max(shown.getZoom(), SELECTED_ZOOM),
        duration: 600,
      });
      return;
    }
    const bounds = new maplibre.LngLatBounds();
    for (const point of points) {
      bounds.extend([point.longitude, point.latitude]);
    }
    shown.fitBounds(bounds, { padding: 48, maxZoom: FIT_MAX_ZOOM });
    // `placed` stands for `points`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ready, placed, selectedId]);

  return (
    <div className="locations-map-frame">
      <div ref={container} className="locations-map" dir="ltr" role="region" aria-label={label} />
      {unavailable ? (
        <p className="locations-map__unavailable" role="status">
          {t('dashboards.locations.mapUnavailable')}
        </p>
      ) : (
        // The credit reads in the page's language and direction; only the sources' names stay as they are (stage QA F-03).
        <p className="locations-map__credit">
          <a href="https://openfreemap.org" target="_blank" rel="noreferrer">
            OpenFreeMap
          </a>{' '}
          ©{' '}
          <a href="https://www.openmaptiles.org/" target="_blank" rel="noreferrer">
            OpenMapTiles
          </a>{' '}
          {t('dashboards.locations.dataFrom')}{' '}
          <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noreferrer">
            OpenStreetMap
          </a>
        </p>
      )}
    </div>
  );
}
