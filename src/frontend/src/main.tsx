import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

// Self-hosted, as the CSP allows fonts from the app's own origin only; the weights styles.css uses.
import '@fontsource/ibm-plex-sans/400.css';
import '@fontsource/ibm-plex-sans/600.css';
import '@fontsource/ibm-plex-sans/700.css';
import '@fontsource/ibm-plex-sans-arabic/400.css';
import '@fontsource/ibm-plex-sans-arabic/600.css';
import '@fontsource/ibm-plex-sans-arabic/700.css';
import './app/styles.css';

import { App } from './App.tsx';
import { installErrorReporting } from './shared/observability/errorReporting.ts';
import { applyTheme, readStoredTheme } from './shared/theme/theme.ts';

// Before the first render, so a dark-theme reader never sees a light flash.
applyTheme(readStoredTheme());

const container = document.getElementById('root');
if (container === null) {
  throw new Error('index.html has no #root element.');
}

// TASK-090: script errors and unhandled rejections are reported to the APM through the API.
installErrorReporting();

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
