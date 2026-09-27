import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import './app/styles.css';

import { App } from './App.tsx';

const container = document.getElementById('root');
if (container === null) {
  throw new Error('index.html has no #root element.');
}

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
