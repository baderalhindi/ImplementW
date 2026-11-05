import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { type ReactElement, type ReactNode, useState } from 'react';
import { beforeEach, describe, expect, test, vi } from 'vitest';

import { sessionStore } from '@/features/identity-access/session/sessionStore.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import { I18nProvider } from '@/shared/i18n/I18nProvider.tsx';
import { sessionFor } from '@/test/identityAccessFixtures.ts';

import { FilterBar, type FilterBarChip, type FilterBarField } from './FilterBar.tsx';

const en = (key: Parameters<typeof translate>[1], params?: Parameters<typeof translate>[2]) =>
  translate('en', key, params);

const FIELDS: FilterBarField[] = [
  {
    name: 'sector',
    label: 'Sector',
    all: 'All',
    options: [
      { value: 'health', label: 'Health', count: 12 },
      { value: 'water', label: 'Water', count: 3 },
    ],
  },
  { name: 'empty', label: 'Empty', all: 'All', options: [] },
  {
    name: 'status',
    label: 'Status',
    all: 'All',
    more: true,
    options: [{ value: 'ACTIVE', label: 'Active' }],
  },
];

const PAGE = 'register';

function Harness({
  onChange = vi.fn(),
  onClearAll = vi.fn(),
  initial = {},
  fields = FIELDS,
  chips = [],
  actions,
  children,
  pageKey = PAGE,
  forceOpen = false,
}: {
  onChange?: (name: string, value: string | undefined) => void;
  onClearAll?: () => void;
  initial?: Record<string, string | undefined>;
  fields?: FilterBarField[];
  chips?: FilterBarChip[];
  actions?: ReactNode;
  children?: ReactNode;
  pageKey?: string;
  forceOpen?: boolean;
}): ReactElement {
  const [values, setValues] = useState(initial);
  return (
    <I18nProvider initialLanguage="en">
      <FilterBar
        fields={fields}
        values={values}
        onChange={(name, value) => {
          onChange(name, value);
          setValues((current) => ({ ...current, [name]: value }));
        }}
        onClearAll={() => {
          onClearAll();
          setValues({});
        }}
        chips={chips}
        actions={actions}
        pageKey={pageKey}
        forceOpen={forceOpen}
      >
        {children}
      </FilterBar>
    </I18nProvider>
  );
}

const toggle = () => screen.getByRole('button', { name: en('common.filters.button') });
const panelOpen = () => screen.queryByRole('search', { name: en('common.filters.label') }) !== null;

/** The one filter bar (the 2 Nov 2026 review, R3-1), folded behind one button (the 5 Nov 2026 review, R4-1). */
describe('FilterBar', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStore.signOut();
  });

  test('is closed by default: the button says so, and no control shows', () => {
    render(<Harness />);

    expect(toggle().getAttribute('aria-expanded')).toBe('false');
    expect(panelOpen()).toBe(false);
    expect(screen.queryByRole('combobox', { name: 'Sector' })).toBeNull();
  });

  test('the button opens the panel below it and closes it again', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(toggle());
    expect(toggle().getAttribute('aria-expanded')).toBe('true');
    expect(toggle().getAttribute('aria-controls')).toBe(
      screen.getByRole('search', { name: en('common.filters.label') }).id,
    );
    expect(screen.getByRole('combobox', { name: 'Sector' })).toBeTruthy();

    await user.click(toggle());
    expect(toggle().getAttribute('aria-expanded')).toBe('false');
    expect(panelOpen()).toBe(false);
  });

  test('the button counts the active filters, its own and those passed as chips', () => {
    render(
      <Harness
        initial={{ sector: 'health' }}
        chips={[{ name: 'q', text: 'Title: park', onRemove: vi.fn() }]}
      />,
    );

    expect(
      screen.getByRole('button', { name: en('common.filters.toggle', { count: 2 }) }),
    ).toBeTruthy();
  });

  test('active filters stay visible as chips while closed; a chip removes its filter', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    const onRemove = vi.fn();
    render(
      <Harness
        onChange={onChange}
        initial={{ sector: 'health' }}
        chips={[{ name: 'q', text: 'Title: park', onRemove }]}
      />,
    );

    expect(panelOpen()).toBe(false);
    await user.click(
      screen.getByRole('button', {
        name: en('common.filters.remove', {
          filter: en('common.filters.inline', { label: 'Sector', value: 'Health' }),
        }),
      }),
    );
    expect(onChange).toHaveBeenLastCalledWith('sector', undefined);

    await user.click(
      screen.getByRole('button', {
        name: en('common.filters.remove', { filter: 'Title: park' }),
      }),
    );
    expect(onRemove).toHaveBeenCalledOnce();
  });

  test('Clear all, while closed, clears every filter; with none active it is not offered', async () => {
    const user = userEvent.setup();
    const onClearAll = vi.fn();
    render(<Harness onClearAll={onClearAll} initial={{ sector: 'health' }} />);

    await user.click(screen.getByRole('button', { name: en('common.filters.clearAll') }));

    expect(onClearAll).toHaveBeenCalledOnce();
    expect(screen.queryByRole('button', { name: en('common.filters.clearAll') })).toBeNull();
    expect(toggle()).toBeTruthy();
  });

  test('open or closed is remembered across a remount, per person and per page', async () => {
    const user = userEvent.setup();
    sessionStore.setSession(sessionFor(['R03']));
    const userId = sessionStore.getSnapshot().session?.user.id ?? '';
    const key = `pmplatform.filterPanel.${userId}.${PAGE}`;

    const first = render(<Harness />);
    await user.click(toggle());
    expect(localStorage.getItem(key)).toBe('open');
    first.unmount();

    const second = render(<Harness />);
    expect(panelOpen()).toBe(true);
    second.unmount();

    const otherPage = render(<Harness pageKey="another-page" />);
    expect(panelOpen()).toBe(false);
    otherPage.unmount();

    sessionStore.signOut();
    render(<Harness />);
    expect(panelOpen()).toBe(false);
  });

  test('Esc closes the panel, returns focus to the button, and is remembered', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(toggle());
    screen.getByRole('combobox', { name: 'Sector' }).focus();
    await user.keyboard('{Escape}');

    expect(panelOpen()).toBe(false);
    expect(document.activeElement).toBe(toggle());
    expect(localStorage.getItem(`pmplatform.filterPanel.anonymous.${PAGE}`)).toBe('closed');
  });

  test('with no filter offered there is no button, and the page’s actions still show', () => {
    render(<Harness fields={[]} actions={<button type="button">Run report</button>} />);

    expect(screen.queryByRole('button', { name: en('common.filters.button') })).toBeNull();
    expect(screen.getByRole('button', { name: 'Run report' })).toBeTruthy();
  });

  test('the page’s actions stay beside the button while the panel is closed', () => {
    render(<Harness actions={<button type="button">Run report</button>} />);

    expect(panelOpen()).toBe(false);
    expect(screen.getByRole('button', { name: 'Run report' })).toBeTruthy();
  });

  test('forceOpen holds the panel open, whatever was remembered, without changing what is remembered', async () => {
    const user = userEvent.setup();
    const key = `pmplatform.filterPanel.anonymous.${PAGE}`;
    localStorage.setItem(key, 'closed');
    const held = render(<Harness forceOpen />);

    expect(toggle().getAttribute('aria-expanded')).toBe('true');
    expect(panelOpen()).toBe(true);
    await user.keyboard('{Escape}');
    expect(panelOpen()).toBe(true);
    held.unmount();

    render(<Harness />);
    expect(panelOpen()).toBe(false);
    expect(localStorage.getItem(key)).toBe('closed');
  });

  test('controls passed as children sit in the panel, under the bar’s own', async () => {
    const user = userEvent.setup();
    render(
      <Harness fields={[]}>
        <label>
          Title
          <input />
        </label>
      </Harness>,
    );

    expect(screen.queryByRole('textbox', { name: 'Title' })).toBeNull();
    await user.click(toggle());
    expect(screen.getByRole('textbox', { name: 'Title' })).toBeTruthy();
  });

  test('each option carries its label and count, and a filter with nothing to offer is not shown', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(toggle());

    const sector = screen.getByRole('combobox', { name: 'Sector' });
    expect(
      within(sector)
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual([
      en('common.filters.inline', { label: 'Sector', value: 'All' }),
      en('common.filters.inline', {
        label: 'Sector',
        value: en('common.filters.optionCount', { label: 'Health', count: 12 }),
      }),
      en('common.filters.inline', {
        label: 'Sector',
        value: en('common.filters.optionCount', { label: 'Water', count: 3 }),
      }),
    ]);
    expect(screen.queryByRole('combobox', { name: 'Empty' })).toBeNull();
  });

  test('a filter applies as it is chosen and shows as a chip', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    await user.click(toggle());

    await user.selectOptions(screen.getByRole('combobox', { name: 'Sector' }), 'health');

    expect(onChange).toHaveBeenLastCalledWith('sector', 'health');
    expect(
      screen.getByRole('button', {
        name: en('common.filters.remove', {
          filter: en('common.filters.inline', { label: 'Sector', value: 'Health' }),
        }),
      }),
    ).toBeTruthy();
    expect(
      screen.getByRole('button', { name: en('common.filters.toggle', { count: 1 }) }),
    ).toBeTruthy();
  });

  test('the rest is under More filters, which counts those set', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(toggle());

    expect(screen.queryByRole('combobox', { name: 'Status' })).toBeNull();
    await user.click(screen.getByRole('button', { name: en('common.filters.more') }));
    await user.selectOptions(screen.getByRole('combobox', { name: 'Status' }), 'ACTIVE');

    expect(
      screen.getByRole('button', { name: en('common.filters.moreCount', { count: 1 }) }),
    ).toBeTruthy();
  });
});
