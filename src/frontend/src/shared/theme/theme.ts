import { useState } from 'react';

export type Theme = 'light' | 'dark';

export const THEME_STORAGE_KEY = 'pmplatform.theme';

function isTheme(value: unknown): value is Theme {
  return value === 'light' || value === 'dark';
}

/** The stored choice; before anyone chooses, the operating system's preference; light where neither can be read. */
export function readStoredTheme(): Theme {
  try {
    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    if (isTheme(stored)) {
      return stored;
    }
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  } catch {
    return 'light';
  }
}

/** styles.css keys the dark palette on <html data-theme="dark">. */
export function applyTheme(theme: Theme): void {
  document.documentElement.dataset.theme = theme;
}

function currentTheme(): Theme {
  const applied = document.documentElement.dataset.theme;
  return isTheme(applied) ? applied : readStoredTheme();
}

/** The applied theme and a setter that applies and remembers a new one. */
export function useTheme(): { theme: Theme; setTheme: (theme: Theme) => void } {
  const [theme, setThemeState] = useState(currentTheme);
  const setTheme = (next: Theme): void => {
    applyTheme(next);
    try {
      localStorage.setItem(THEME_STORAGE_KEY, next);
    } catch {
      // A blocked store only forgets the choice.
    }
    setThemeState(next);
  };
  return { theme, setTheme };
}
