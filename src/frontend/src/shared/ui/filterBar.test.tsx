import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { describe, expect, test, vi } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { I18nProvider } from '@/shared/i18n/I18nProvider.tsx';

import { FilterBar, type FilterBarField } from './FilterBar.tsx';

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

function Harness({ onChange }: { onChange: (name: string, value: string | undefined) => void }) {
  const [values, setValues] = useState<Record<string, string | undefined>>({});
  return (
    <I18nProvider initialLanguage="en">
      <FilterBar
        fields={FIELDS}
        values={values}
        onChange={(name, value) => {
          onChange(name, value);
          setValues((current) => ({ ...current, [name]: value }));
        }}
        onClearAll={() => {
          setValues({});
        }}
      />
    </I18nProvider>
  );
}

/** The one filter bar (the 2 Nov 2026 review, R3-1). */
describe('FilterBar', () => {
  test('each option carries its label and count, and a filter with nothing to offer is not shown', () => {
    render(<Harness onChange={vi.fn()} />);

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

  test('a filter applies as it is chosen and shows as a chip that removes it; Clear all removes every one', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);

    await user.selectOptions(screen.getByRole('combobox', { name: 'Sector' }), 'health');
    expect(onChange).toHaveBeenLastCalledWith('sector', 'health');
    const chip = screen.getByRole('button', {
      name: en('common.filters.remove', {
        filter: en('common.filters.inline', { label: 'Sector', value: 'Health' }),
      }),
    });
    await user.click(chip);
    expect(onChange).toHaveBeenLastCalledWith('sector', undefined);

    await user.selectOptions(screen.getByRole('combobox', { name: 'Sector' }), 'water');
    await user.click(screen.getByRole('button', { name: en('common.filters.clearAll') }));
    expect(screen.queryByRole('button', { name: en('common.filters.clearAll') })).toBeNull();
  });

  test('the rest is under More filters, which counts those set', async () => {
    const user = userEvent.setup();
    render(<Harness onChange={vi.fn()} />);

    expect(screen.queryByRole('combobox', { name: 'Status' })).toBeNull();
    await user.click(screen.getByRole('button', { name: en('common.filters.more') }));
    await user.selectOptions(screen.getByRole('combobox', { name: 'Status' }), 'ACTIVE');

    expect(
      screen.getByRole('button', { name: en('common.filters.moreCount', { count: 1 }) }),
    ).toBeTruthy();
  });
});
