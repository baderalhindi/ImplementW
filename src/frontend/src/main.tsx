import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import './app/styles.css';

import { App } from './App.tsx';
import { installErrorReporting } from './shared/observability/errorReporting.ts';

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
