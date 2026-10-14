import { describe, expect, test } from 'vitest';
import { isAppPath } from './appPath.ts';

describe('isAppPath', () => {
  test('accepts a route on this origin, with its query', () => {
    expect(isAppPath('/projects/7d1c/risks')).toBe(true);
    expect(isAppPath('/risks?status=OPEN')).toBe(true);
    expect(isAppPath('/')).toBe(true);
  });

  test('refuses anything that leaves the origin or runs script', () => {
    for (const value of [
      '//evil.example',
      '/\\evil.example',
      '/\\/evil.example',
      // eslint-disable-next-line no-script-url -- the payload this check must refuse
      'javascript:alert(1)',
      '/redirect?to=https://evil.example',
      'https://evil.example',
      'projects',
      '/projects\u0000',
      '/\tprojects',
      '',
      null,
      undefined,
      42,
    ]) {
      expect(isAppPath(value), String(value)).toBe(false);
    }
  });
});
