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

afterEach(() => {
  cleanup();
  sessionStorage.clear();
  localStorage.clear();
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
