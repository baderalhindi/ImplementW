import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, test, vi } from 'vitest';

import { I18nProvider } from '@/shared/i18n/I18nProvider.tsx';
import { translate } from '@/shared/i18n/i18n.ts';

import { LocationsMap, type MapPoint } from './LocationsMap.tsx';

const fake = vi.hoisted(() => {
  const state = {
    maps: [] as FakeMap[],
    workerUrl: null as string | null,
    failToCreate: false,
  };

  class FakeMap {
    listeners = new Map<string, (event: unknown) => void>();
    canvas = document.createElement('div');
    layout: [string, string, unknown][] = [];
    paint: [string, string, unknown][] = [];
    flyTo = vi.fn();
    fitBounds = vi.fn();
    remove = vi.fn();
    options: { container: HTMLElement; style: string };

    constructor(options: { container: HTMLElement; style: string }) {
      if (state.failToCreate) {
        throw new Error('Failed to initialize WebGL');
      }
      this.options = options;
      options.container.append(this.canvas);
      state.maps.push(this);
    }
    addControl = vi.fn();
    getZoom(): number {
      return 6;
    }
    getLayer(id: string): object | undefined {
      return id === 'water' ? {} : undefined;
    }
    getStyle(): object {
      return {
        layers: [
          { id: 'water', type: 'fill' },
          { id: 'label_city', type: 'symbol', layout: { 'text-field': '{name}' } },
          { id: 'highway-shield-non-us', type: 'symbol', layout: { 'text-field': '{ref}' } },
          { id: 'road_one_way_arrow', type: 'symbol', layout: {} },
        ],
      };
    }
    on(name: string, listener: (event: unknown) => void): void {
      this.listeners.set(name, listener);
    }
    fire(name: string, event?: unknown): void {
      this.listeners.get(name)?.(event);
    }
    setLayoutProperty(layer: string, property: string, value: unknown): void {
      this.layout.push([layer, property, value]);
    }
    setPaintProperty(layer: string, property: string, value: unknown): void {
      this.paint.push([layer, property, value]);
    }
  }

  class FakeMarker {
    element: HTMLElement;
    constructor({ element }: { element: HTMLElement }) {
      this.element = element;
    }
    setLngLat(): this {
      return this;
    }
    addTo(map: FakeMap): this {
      map.canvas.append(this.element);
      return this;
    }
    remove(): void {
      this.element.remove();
    }
  }

  class FakePopup {
    node = document.createElement('div');
    setLngLat(): this {
      return this;
    }
    setText(text: string): this {
      this.node.textContent = text;
      this.node.setAttribute('role', 'tooltip');
      return this;
    }
    addTo(map: FakeMap): this {
      map.canvas.append(this.node);
      return this;
    }
    remove(): void {
      this.node.remove();
    }
  }

  class FakeBounds {
    points: [number, number][] = [];
    extend(point: [number, number]): this {
      this.points.push(point);
      return this;
    }
  }

  class FakeNavigationControl {
    position = 'top-left';
  }

  return { state, FakeMap, FakeMarker, FakePopup, FakeBounds, FakeNavigationControl };
});

vi.mock('maplibre-gl', () => ({
  Map: fake.FakeMap,
  Marker: fake.FakeMarker,
  Popup: fake.FakePopup,
  LngLatBounds: fake.FakeBounds,
  NavigationControl: fake.FakeNavigationControl,
  setWorkerUrl: (url: string) => {
    fake.state.workerUrl = url;
  },
}));

const HOFUF: MapPoint = {
  id: 'hofuf',
  name: 'Hofuf',
  latitude: 25.38,
  longitude: 49.59,
  projectCount: 8,
};
const ABQAIQ: MapPoint = {
  id: 'abqaiq',
  name: 'Abqaiq',
  latitude: 25.93,
  longitude: 49.67,
  projectCount: 1,
};

function renderMap(
  options: { language?: 'ar' | 'en'; selectedId?: string | null } = {},
): ReturnType<typeof vi.fn> {
  const onSelect = vi.fn();
  render(
    <I18nProvider initialLanguage={options.language ?? 'en'}>
      <LocationsMap
        points={[HOFUF, ABQAIQ]}
        selectedId={options.selectedId ?? null}
        label="Map"
        onSelect={onSelect}
      />
    </I18nProvider>,
  );
  return onSelect;
}

async function shownMap(): Promise<InstanceType<typeof fake.FakeMap>> {
  await screen.findByRole('button', { name: 'Hofuf: 8' });
  const map = fake.state.maps.at(-1);
  if (map === undefined) {
    throw new Error('No map was created.');
  }
  return map;
}

describe('LocationsMap', () => {
  beforeEach(() => {
    fake.state.maps.length = 0;
    fake.state.workerUrl = null;
    fake.state.failToCreate = false;
  });

  test('loads the free map, starts its worker from this origin and credits its sources', async () => {
    renderMap();
    const map = await shownMap();

    expect(map.options.style).toBe('https://tiles.openfreemap.org/styles/liberty');
    expect(fake.state.workerUrl).toEqual(expect.any(String));
    expect(fake.state.workerUrl).not.toMatch(/^blob:/);
    expect(screen.getByRole('link', { name: 'OpenStreetMap' }).getAttribute('href')).toBe(
      'https://www.openstreetmap.org/copyright',
    );
  });

  test('draws a circle for each place, the fuller place larger, with the smaller stacked above', async () => {
    renderMap();
    await shownMap();

    const large = screen.getByRole('button', { name: 'Hofuf: 8' });
    const small = screen.getByRole('button', { name: 'Abqaiq: 1' });
    expect(large.textContent).toBe('8');
    expect(large.style.inlineSize).toBe('50px');
    expect(small.style.inlineSize).toBe('34px');
    expect(Number(small.style.zIndex)).toBeGreaterThan(Number(large.style.zIndex));
  });

  test('pressing a circle selects it, by pointer or keyboard', async () => {
    const onSelect = renderMap();
    await shownMap();
    const user = userEvent.setup();

    await user.click(screen.getByRole('button', { name: 'Abqaiq: 1' }));
    expect(onSelect).toHaveBeenLastCalledWith('abqaiq');

    screen.getByRole('button', { name: 'Hofuf: 8' }).focus();
    await user.keyboard('{Enter}');
    expect(onSelect).toHaveBeenLastCalledWith('hofuf');
  });

  test('pressing the map clears the selection, but its report of a press on a circle does not', async () => {
    const onSelect = renderMap();
    const map = await shownMap();

    map.fire('click', {
      originalEvent: { target: screen.getByRole('button', { name: 'Hofuf: 8' }) },
    });
    expect(onSelect).not.toHaveBeenCalled();

    map.fire('click', { originalEvent: { target: map.canvas } });
    expect(onSelect).toHaveBeenCalledWith(null);
  });

  test('marks the selected place, lifts it above the others and flies to it', async () => {
    // jsdom lays nothing out, and the map does not move while it has no width.
    vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(800);
    renderMap({ selectedId: 'hofuf' });
    const map = await shownMap();

    const chosen = screen.getByRole('button', { name: 'Hofuf: 8' });
    const other = screen.getByRole('button', { name: 'Abqaiq: 1' });
    expect(chosen.classList.contains('map-bubble--selected')).toBe(true);
    expect(other.classList.contains('map-bubble--selected')).toBe(false);
    expect(Number(chosen.style.zIndex)).toBeGreaterThan(Number(other.style.zIndex));
    await waitFor(() => {
      expect(map.flyTo).toHaveBeenCalledWith(
        expect.objectContaining({ center: [49.59, 25.38], zoom: 9 }),
      );
    });
    expect(map.fitBounds).not.toHaveBeenCalled();
  });

  test('names a place when the pointer or focus is on its circle', async () => {
    renderMap();
    await shownMap();
    const user = userEvent.setup();

    await user.hover(screen.getByRole('button', { name: 'Hofuf: 8' }));
    expect(screen.getByRole('tooltip').textContent).toBe('Hofuf: 8');

    await user.unhover(screen.getByRole('button', { name: 'Hofuf: 8' }));
    expect(screen.queryByRole('tooltip')).toBeNull();
  });

  test.each([
    ['ar' as const, ['coalesce', ['get', 'name:ar'], ['get', 'name']]],
    ['en' as const, ['coalesce', ['get', 'name_en'], ['get', 'name:en'], ['get', 'name']]],
  ])(
    'labels the %s map in that language, hides the empty road signs and softens the water',
    async (language, label) => {
      renderMap({ language });
      const map = await shownMap();

      map.fire('style.load');

      await waitFor(() => {
        expect(map.layout).toContainEqual(['label_city', 'text-field', label]);
      });
      expect(map.layout).toContainEqual(['highway-shield-non-us', 'visibility', 'none']);
      expect(map.layout.some(([layer]) => layer === 'road_one_way_arrow')).toBe(false);
      expect(map.paint).toContainEqual(['water', 'fill-color', '#aadaff']);
      expect(map.paint).toHaveLength(1);
    },
  );

  test('says so when the browser cannot draw the map', async () => {
    fake.state.failToCreate = true;
    renderMap();

    expect((await screen.findByRole('status')).textContent).toBe(
      translate('en', 'dashboards.locations.mapUnavailable'),
    );
    expect(screen.queryByRole('link', { name: 'OpenStreetMap' })).toBeNull();
  });
});
