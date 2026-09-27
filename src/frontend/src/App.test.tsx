import { render, screen } from '@testing-library/react';
import { expect, test } from 'vitest';

import { App } from './App.tsx';

test('without a session the application opens on sign-in, inside the main landmark, in Arabic by default', async () => {
  render(<App />);

  const main = screen.getByRole('main');
  expect(await screen.findByRole('heading', { level: 1, name: 'تسجيل الدخول' })).toBeTruthy();
  expect(main.contains(screen.getByRole('heading', { level: 1 }))).toBe(true);
  expect(document.documentElement.dir).toBe('rtl');
});
