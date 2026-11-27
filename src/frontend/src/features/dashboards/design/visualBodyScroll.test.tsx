import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, test, vi } from 'vitest';

import { VisualBodyBox } from './FitToBox.tsx';

// Stage QA F-01 (axe scrollable-region-focusable, WCAG 2.1.1): a visual body that scrolls is a named region in the tab order, so the
// keyboard can scroll it; one that fits stays out of the tab order. jsdom lays out nothing, so the sizes are stubbed.

function stubSizes(content: number, box: number) {
  vi.spyOn(HTMLElement.prototype, 'scrollHeight', 'get').mockReturnValue(content);
  vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockReturnValue(box);
  vi.spyOn(HTMLElement.prototype, 'scrollWidth', 'get').mockReturnValue(100);
  vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(100);
}

function renderBody(visualType: string) {
  render(
    <VisualBodyBox visual={{ visualType }} className="visual__body" label="By sector">
      <table>
        <tbody>
          <tr>
            <td>Roads</td>
          </tr>
        </tbody>
      </table>
    </VisualBodyBox>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('a visual body drawn at its box size', () => {
  test('that scrolls is a focusable region named by its visual', () => {
    stubSizes(400, 120);
    renderBody('TABLE');

    const region = screen.getByRole('region', { name: 'By sector' });
    expect(region.classList.contains('visual__body')).toBe(true);
    expect(region.tabIndex).toBe(0);
  });

  test('that fits is neither a region nor a tab stop', () => {
    stubSizes(120, 120);
    renderBody('TABLE');

    expect(screen.queryByRole('region')).toBeNull();
    expect(document.querySelector('.visual__body')?.hasAttribute('tabindex')).toBe(false);
  });
});
