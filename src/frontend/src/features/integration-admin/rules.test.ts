import { describe, expect, test } from 'vitest';

import { internalSystem, seededCatalogue } from '@/test/integrationAdminFixtures.ts';

import {
  hasConnectionTest,
  internalSystems,
  logFilterProblems,
  logFilterValues,
  MESSAGING_CODES,
  metricsWindowFrom,
  plainCode,
  SSO_DIRECTORY_CODES,
  toInvocationQuery,
  withCodes,
} from './catalogue.ts';
import { availabilityTone, freshnessTone } from './presentation.ts';

// TASK-076's rules: which integrations each provider screen shows, which codes a connection test may display, the
// ADM-048 filters sent only when the API accepts them, and the tones that keep Unknown from reading as healthy.

const EMPTY_FILTERS = logFilterValues(new URLSearchParams());

describe('screen membership', () => {
  test('ADM-041 and ADM-042 show their providers in a fixed order, and ADM-044 everything else', () => {
    const items = [...seededCatalogue(), internalSystem()];

    expect(withCodes(items, SSO_DIRECTORY_CODES).map((item) => item.code)).toEqual([
      'DIRECTORY',
      'SSO',
      'MFA',
    ]);
    expect(withCodes(items, MESSAGING_CODES).map((item) => item.code)).toEqual([
      'EXCHANGE_EMAIL',
      'SMS',
    ]);
    expect(internalSystems(items).map((item) => item.code)).toEqual(['FINANCE_LEDGER']);
    expect(internalSystems(seededCatalogue())).toEqual([]);
  });

  test('only the directory, SSO and Nafath have a connection test', () => {
    expect(
      seededCatalogue()
        .map((item) => item.code)
        .filter(hasConnectionTest),
    ).toEqual(['DIRECTORY', 'SSO', 'NAFATH']);
  });
});

describe('plainCode (acceptance criterion 1)', () => {
  test('keeps a fixed code and drops any other text, which could carry a credential', () => {
    expect(plainCode('BIND_REJECTED')).toBe('BIND_REJECTED');
    expect(plainCode('UNREACHABLE')).toBe('UNREACHABLE');
    expect(plainCode(null)).toBeNull();
    expect(plainCode('invalid credentials: password=hunter2')).toBeNull();
    expect(plainCode('Bearer eyJhbGciOi')).toBeNull();
    expect(plainCode('bind_rejected')).toBeNull();
    expect(plainCode('A'.repeat(65))).toBeNull();
  });
});

describe('ADM-048 filters', () => {
  test('a malformed id, an over-long key and a backwards range are refused before any request', () => {
    expect(
      logFilterProblems({
        ...EMPTY_FILTERS,
        integrationId: 'directory',
        correlationId: 'not-a-uuid',
        idempotencyKey: 'k'.repeat(201),
        from: '2026-10-14',
        to: '2026-10-13',
      }),
    ).toEqual({
      integrationId: 'malformed',
      correlationId: 'malformed',
      idempotencyKey: 'tooLong',
      to: 'dateBeforeStart',
    });
    expect(logFilterProblems(EMPTY_FILTERS)).toEqual({});
  });

  test('only values the API accepts are sent, and a status is one of its states', () => {
    const correlationId = '7c000000-0000-4000-8000-000000000001';
    expect(
      toInvocationQuery({
        ...EMPTY_FILTERS,
        status: 'DEAD_LETTER',
        correlationId,
        operationCode: 'RECONCILE',
      }),
    ).toMatchObject({
      statuses: ['DEAD_LETTER'],
      correlationId,
      operationCode: 'RECONCILE',
      integrationId: undefined,
    });
    expect(
      toInvocationQuery({ ...EMPTY_FILTERS, status: 'BROKEN', correlationId: 'x' }),
    ).toMatchObject({ statuses: [], correlationId: undefined });
  });
});

test('the measures window is 1, 7 or 30 days, and 1 when anything else is asked', () => {
  expect(metricsWindowFrom(new URLSearchParams('window=7'))).toBe(7);
  expect(metricsWindowFrom(new URLSearchParams('window=30'))).toBe(30);
  expect(metricsWindowFrom(new URLSearchParams('window=365'))).toBe(1);
  expect(metricsWindowFrom(new URLSearchParams())).toBe(1);
});

test('Unknown never reads as healthy, in either indicator', () => {
  expect(availabilityTone('UNKNOWN')).toBe('neutral');
  expect(freshnessTone('UNKNOWN')).toBe('neutral');
  expect(availabilityTone('UP')).toBe('positive');
  expect(freshnessTone('STALE')).toBe('warning');
});
