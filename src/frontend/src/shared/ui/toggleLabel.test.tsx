import { render, screen } from '@testing-library/react';
import { describe, expect, test } from 'vitest';

import { ToggleLabel } from './ToggleLabel.tsx';

describe('a two-word toggle label', () => {
  test('names the button by the shown word only, keeping the other in the layout', () => {
    render(
      <button type="button">
        <ToggleLabel current="Activate" other="Deactivate" />
      </button>,
    );
    expect(screen.getByRole('button', { name: 'Activate' })).toBeTruthy();
    const reserve = screen.getByText('Deactivate');
    expect(reserve.getAttribute('aria-hidden')).toBe('true');
    expect(reserve.className).toBe('toggle-label__reserve');
  });
});
