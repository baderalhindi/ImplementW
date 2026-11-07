import { describe, expect, test } from 'vitest';

import { caretAfter, formatAmount, plainAmount, significantBefore } from './amountInput.ts';

const sar = { maxDecimals: 2, allowNegative: false };
const impact = { maxDecimals: 2, allowNegative: true };

describe('an amount as it is typed', () => {
  test.each([
    ['1250000', '1250000', '1,250,000'],
    ['1,250,000', '1250000', '1,250,000'],
    ['1,250,000.50', '1250000.50', '1,250,000.50'],
    ['1250000.', '1250000.', '1,250,000.'],
    ['999', '999', '999'],
    ['1000', '1000', '1,000'],
    ['', '', ''],
    ['12.345', '12.34', '12.34'],
    ['١٢٥٠٠٠٠٫٥', '1250000.5', '1,250,000.5'],
    ['SAR 1,000', '1000', '1,000'],
    ['-5', '5', '5'],
  ])('%s keeps %s and shows %s', (typed, plain, shown) => {
    expect(plainAmount(typed, sar)).toBe(plain);
    expect(formatAmount(plainAmount(typed, sar))).toBe(shown);
  });

  test('a minus sign stays where the field takes one', () => {
    expect(formatAmount(plainAmount('-1250000', impact))).toBe('-1,250,000');
    expect(plainAmount('-', impact)).toBe('-');
    expect(plainAmount('12-3', impact)).toBe('123');
  });

  test('no decimals are kept where the field takes none', () => {
    expect(plainAmount('12.5', { maxDecimals: 0, allowNegative: false })).toBe('12');
  });
});

describe('the caret', () => {
  test('stays after the same digit when a separator appears before it', () => {
    // 1234|5 typed from 123|5: three digits, then a fourth, before the caret.
    const significant = significantBefore('12345', 4);
    expect(caretAfter(formatAmount('12345'), significant)).toBe(5);
  });

  test('does not jump when a digit in the middle of the number is deleted', () => {
    // 1,2|50,000 with the 2 deleted leaves 1|50,000.
    const significant = significantBefore('1,50,000', 1);
    expect(caretAfter(formatAmount('150000'), significant)).toBe(1);
  });

  test('stays at the end of the number', () => {
    expect(caretAfter('1,250,000', significantBefore('1250000', 7))).toBe(9);
    expect(caretAfter('1,000', 0)).toBe(0);
  });
});
