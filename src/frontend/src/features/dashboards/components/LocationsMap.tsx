import 'leaflet/dist/leaflet.css';

import type { LayerGroup, Map as LeafletMap } from 'leaflet';
import { type ReactElement, useEffect, useRef, useState } from 'react';

/** One circle: a place with projects. Its area grows with the count, so a large place does not swallow its neighbours. */
export interface MapPoint {
  id: string;
  name: string;
  latitude: number;
  longitude: number;
  projectCount: number;
}

const TILE_URL = 'https://tile.openstreetmap.org/{z}/{x}/{y}.png';
const ATTRIBUTION = '© OpenStreetMap';
const MIN_DIAMETER = 26;
const MAX_DIAMETER = 50;
/** Pressing a circle zooms to here at least, so its close neighbours separate. */
const SELECTED_ZOOM = 10;
const MAX_ZOOM = 16;

function diameterOf(count: number, largest: number): number {
  return Math.round(
    MIN_DIAMETER + (MAX_DIAMETER - MIN_DIAMETER) * Math.sqrt(count / Math.max(1, largest)),
  );
}

/**
 * The map of the projects' places (Leaflet over OpenStreetMap tiles). Each place is a circle holding its count; pressing one, by pointer or
 * keyboard, selects it. The smaller circles sit above the larger, so a close neighbour can still be pressed. If the tiles cannot load, the circles
 * still draw on a plain background. The map is drawn for pointer and keyboard users; the list beside it says the same to a screen reader.
 */
export function LocationsMap({
  points,
  selectedId,
  label,
  onSelect,
}: {
  points: MapPoint[];
  selectedId: string | null;
  label: string;
  onSelect: (id: string | null) => void;
}): ReactElement {
  const container = useRef<HTMLDivElement>(null);
  const map = useRef<LeafletMap | null>(null);
  const markers = useRef<LayerGroup | null>(null);
  const [ready, setReady] = useState(false);
  const select = useRef(onSelect);
  useEffect(() => {
    select.current = onSelect;
  }, [onSelect]);
  // The place set, not the array: a re-render with the same places keeps the person's pan and zoom.
  const placed = points.map((point) => `${point.id}:${String(point.projectCount)}`).join(',');

  useEffect(() => {
    let cancelled = false;
    void import('leaflet').then((L) => {
      if (cancelled || container.current === null) {
        return;
      }
      const created = L.map(container.current, { maxZoom: MAX_ZOOM }).setView([25.0, 49.8], 7);
      created.attributionControl.setPrefix(false);
      L.tileLayer(TILE_URL, {
        attribution: ATTRIBUTION,
        maxZoom: MAX_ZOOM,
        // The site's headers require a CORS image (COEP) and send no referrer, which OpenStreetMap's tile policy asks for.
        crossOrigin: 'anonymous',
        referrerPolicy: 'strict-origin-when-cross-origin',
      }).addTo(created);
      created.on('click', () => {
        select.current(null);
      });
      markers.current = L.layerGroup().addTo(created);
      map.current = created;
      setReady(true);
    });
    return () => {
      cancelled = true;
      map.current?.remove();
      map.current = null;
      markers.current = null;
    };
  }, []);

  useEffect(() => {
    if (!ready || map.current === null || markers.current === null) {
      return;
    }
    let cancelled = false;
    void import('leaflet').then((L) => {
      if (cancelled || map.current === null || markers.current === null) {
        return;
      }
      markers.current.clearLayers();
      const largest = Math.max(1, ...points.map((point) => point.projectCount));
      for (const point of points) {
        const diameter = diameterOf(point.projectCount, largest);
        const bubble = document.createElement('span');
        bubble.className = `map-bubble${point.id === selectedId ? ' map-bubble--selected' : ''}`;
        bubble.textContent = String(point.projectCount);
        const name = document.createElement('span');
        name.textContent = `${point.name}: ${String(point.projectCount)}`;
        L.marker([point.latitude, point.longitude], {
          icon: L.divIcon({
            className: 'map-bubble__anchor',
            html: bubble,
            iconSize: [diameter, diameter],
            iconAnchor: [diameter / 2, diameter / 2],
          }),
          title: `${point.name}: ${String(point.projectCount)}`,
          alt: `${point.name}: ${String(point.projectCount)}`,
          keyboard: true,
          zIndexOffset: 1000 - diameter + (point.id === selectedId ? 1000 : 0),
        })
          .bindTooltip(name, { direction: 'top', offset: [0, -diameter / 2] })
          .on('click', () => {
            select.current(point.id);
          })
          .addTo(markers.current);
      }
    });
    return () => {
      cancelled = true;
    };
    // `placed` stands for `points`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ready, placed, selectedId]);

  useEffect(() => {
    if (
      !ready ||
      map.current === null ||
      points.length === 0 ||
      container.current?.clientWidth === 0
    ) {
      return;
    }
    const chosen = points.find((point) => point.id === selectedId);
    if (chosen !== undefined) {
      map.current.flyTo(
        [chosen.latitude, chosen.longitude],
        Math.max(map.current.getZoom(), SELECTED_ZOOM),
        { duration: 0.6 },
      );
      return;
    }
    void import('leaflet').then((L) => {
      map.current?.fitBounds(
        L.latLngBounds(points.map((point) => [point.latitude, point.longitude])),
        { padding: [48, 48], maxZoom: 11 },
      );
    });
    // `placed` stands for `points`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ready, placed, selectedId]);

  return (
    <div ref={container} className="locations-map" dir="ltr" role="region" aria-label={label} />
  );
}
