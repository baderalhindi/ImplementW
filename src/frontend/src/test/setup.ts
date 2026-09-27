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
