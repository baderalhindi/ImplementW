import { act, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest';
import { type ReactElement } from 'react';

import { SEARCH_TYPING_PAUSE_MS, useSearchText } from './useSearchText.ts';

function Box({
  applied,
  onApply,
}: {
  applied: string;
  onApply: (text: string) => void;
}): ReactElement {
  const [text, setText] = useSearchText(applied, onApply);
  return (
    <input
      aria-label="search"
      value={text}
      onChange={(event) => {
        setText(event.target.value);
      }}
    />
  );
}

function type(value: string) {
  const box = screen.getByRole<HTMLInputElement>('textbox');
  act(() => {
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set?.call(box, value);
    box.dispatchEvent(new Event('input', { bubbles: true }));
  });
}

function pause(ms = SEARCH_TYPING_PAUSE_MS) {
  act(() => {
    vi.advanceTimersByTime(ms);
  });
}

function boxValue(): string {
  return screen.getByRole<HTMLInputElement>('textbox').value;
}

describe('useSearchText', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  test('applies once typing pauses, not on each keystroke', () => {
    const onApply = vi.fn();
    render(<Box applied="" onApply={onApply} />);

    type('m');
    pause(SEARCH_TYPING_PAUSE_MS - 1);
    type('mo');
    pause(SEARCH_TYPING_PAUSE_MS - 1);
    expect(onApply).not.toHaveBeenCalled();

    pause(1);
    expect(onApply).toHaveBeenCalledExactlyOnceWith('mo');
  });

  test('applies the empty text when the box is cleared, and trims', () => {
    const onApply = vi.fn();
    render(<Box applied="mo" onApply={onApply} />);

    type('');
    pause();
    expect(onApply).toHaveBeenLastCalledWith('');

    onApply.mockClear();
    type('  mo ');
    pause();
    expect(onApply).not.toHaveBeenCalled();
  });

  test('a change from elsewhere resets the box and drops what was waiting', () => {
    const onApply = vi.fn();
    const { rerender } = render(<Box applied="mo" onApply={onApply} />);

    type('mohammad');
    rerender(<Box applied="" onApply={onApply} />);
    expect(boxValue()).toBe('');

    pause();
    expect(onApply).not.toHaveBeenCalled();
  });

  test('its own answer arriving late keeps what was typed since', () => {
    const onApply = vi.fn();
    const { rerender } = render(<Box applied="" onApply={onApply} />);

    type('mo');
    pause();
    expect(onApply).toHaveBeenLastCalledWith('mo');

    type('moh');
    rerender(<Box applied="mo" onApply={onApply} />);
    expect(boxValue()).toBe('moh');

    pause();
    expect(onApply).toHaveBeenLastCalledWith('moh');
  });

  test('after its answer, returning to that value from elsewhere still resets the box', () => {
    const onApply = vi.fn();
    const { rerender } = render(<Box applied="" onApply={onApply} />);

    type('mo');
    pause();
    rerender(<Box applied="mo" onApply={onApply} />);
    rerender(<Box applied="other" onApply={onApply} />);
    expect(boxValue()).toBe('other');
    rerender(<Box applied="mo" onApply={onApply} />);
    expect(boxValue()).toBe('mo');
  });
});
