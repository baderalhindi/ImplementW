import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { I18nProvider } from '@/shared/i18n/I18nProvider.tsx';

import { HelpTip } from './HelpTip.tsx';

// A note behind a "?" (24 Nov 2026): shown when the button is pressed, hidden again by Escape or a press elsewhere.

describe('the help tip', () => {
  test('shows its note on a press and hides it on Escape, the focus back on its button', async () => {
    const user = userEvent.setup();
    render(
      <I18nProvider initialLanguage="en">
        <p>Outside</p>
        <HelpTip label="Add a visual">Drag a visual by its handle.</HelpTip>
      </I18nProvider>,
    );
    const button = screen.getByRole('button', {
      name: (name) => name.replace(/[\u2068\u2069]/g, '') === 'Help: Add a visual',
    });
    expect(screen.queryByRole('note')).toBeNull();

    await user.click(button);
    expect(screen.getByRole('note').textContent).toBe('Drag a visual by its handle.');
    expect(button.getAttribute('aria-expanded')).toBe('true');

    await user.keyboard('{Escape}');
    expect(screen.queryByRole('note')).toBeNull();
    expect(document.activeElement).toBe(button);

    await user.click(button);
    await user.click(screen.getByText('Outside'));
    expect(screen.queryByRole('note')).toBeNull();
  });
});
