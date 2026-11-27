import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

// jsdom has no HTMLDialogElement.showModal/close; enough of them for Dialog.tsx: `open` reflects the state.
if (typeof HTMLDialogElement.prototype.showModal !== 'function') {
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.setAttribute('open', '');
  };
  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    this.removeAttribute('open');
  };
}

// Node's BroadcastChannel reaches every store in the worker, so a session left by one test file would answer another's.
// The app's store therefore works as a lone tab here; sessionStore.test.ts gives its simulated tabs their own channel.
Reflect.deleteProperty(globalThis, 'BroadcastChannel');

afterEach(() => {
  cleanup();
  sessionStorage.clear();
  localStorage.clear();
  delete document.documentElement.dataset.theme;
});

// jsdom has no pointer capture; the Gantt's drag (SCR-045) captures the pointer on its bar. The stub records which.
if (typeof Element.prototype.setPointerCapture !== 'function') {
  Element.prototype.setPointerCapture = function setPointerCapture(
    this: Element,
    pointerId: number,
  ) {
    this.setAttribute('data-pointer-capture', String(pointerId));
  };
  Element.prototype.releasePointerCapture = function releasePointerCapture(this: Element) {
    this.removeAttribute('data-pointer-capture');
  };
}

// jsdom lays nothing out, so it has no scrollIntoView; a save that finds fields to complete moves to the first one (review round 8, point 16).
if (typeof Element.prototype.scrollIntoView !== 'function') {
  Element.prototype.scrollIntoView = function scrollIntoView() {
    // Nothing to scroll in a page that is not laid out.
  };
}
