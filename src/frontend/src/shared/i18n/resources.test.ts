import { expect, test } from 'vitest';

import { translate } from './i18n.ts';
import { resources } from './resources.ts';

function leaves(node: unknown, prefix = ''): [string, unknown][] {
  if (typeof node !== 'object' || node === null) {
    return [[prefix, node]];
  }
  return Object.entries(node).flatMap(([key, value]) =>
    leaves(value, prefix === '' ? key : `${prefix}.${key}`),
  );
}

const PLACEHOLDER = /\{(\w+)\}/g;

test('Arabic and English hold the same keys, every one a non-empty string', () => {
  const ar = new Map(leaves(resources.ar));
  const en = new Map(leaves(resources.en));

  expect([...ar.keys()].sort()).toEqual([...en.keys()].sort());
  for (const [key, value] of [...ar, ...en]) {
    expect(typeof value === 'string' && value.trim() !== '', key).toBe(true);
  }
});

test('each Arabic string uses the same placeholders as its English one', () => {
  const ar = new Map(leaves(resources.ar));
  for (const [key, english] of leaves(resources.en)) {
    const placeholders = (text: unknown) =>
      [...String(text).matchAll(PLACEHOLDER)].map((m) => m[1]).sort();
    expect(placeholders(ar.get(key)), key).toEqual(placeholders(english));
  }
});

test('an interpolated value is bidi-isolated, so a Latin id keeps its order inside Arabic text', () => {
  expect(translate('ar', 'identityAccess.assignments.projectScope', { project: 'a1-b2' })).toBe(
    'المشروع ⁨a1-b2⁩',
  );
});
