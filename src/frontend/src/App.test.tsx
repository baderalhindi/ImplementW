import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, expect, test } from 'vitest';

import { App } from './App.tsx';

afterEach(cleanup);

test('renders the platform name inside the main landmark', () => {
  render(<App />);

  expect(screen.getByRole('main').textContent).toBe('PMPlatform');
});
