import { formatAmount, withoutZeroFraction } from '@/shared/forms/amountInput.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

// The platform's one way to write a riyal amount (review round 8, point 11), so cards, tables, exports and tooltips agree. Amounts arrive as
// the API's exact decimal strings; Latin digits in both languages (TASK-032 D-5). Three forms:
//   - exact: every halala, for the financial screens and entry forms, and for the hover over a compact figure;
//   - whole: whole riyals with separators, for tables and details;
//   - compact: "64.6 مليار ريال", for cards and charts.

type Translate = (key: TranslationKey, params?: Record<string, string | number>) => string;

const GROUPED = new Intl.NumberFormat('en');

/** "64,553,236,526.17": the amount as the API keeps it, grouped, with no ".00". */
export function sarExact(amount: string): string {
  return formatAmount(withoutZeroFraction(amount));
}

/**
 * "64,553,236,526": whole riyals, rounded half away from zero on the decimal string itself, so an amount beyond a float's precision is not
 * changed by the rounding.
 */
export function sarWhole(amount: string): string {
  const match = /^(-?)(\d+)(?:\.(\d*))?$/.exec(amount.trim());
  if (match === null) {
    return amount;
  }
  const [, sign = '', integer = '0', fraction = ''] = match;
  const rounded = BigInt(integer) + (Number(fraction.charAt(0) || '0') >= 5 ? 1n : 0n);
  return `${rounded === 0n ? '' : sign}${GROUPED.format(rounded)}`;
}

/** One decimal under a hundred ("64.6"), none from a hundred up ("188"); never a trailing ".0". */
function scaled(value: number): string {
  return new Intl.NumberFormat('en', {
    maximumFractionDigits: Math.abs(value) < 100 ? 1 : 0,
  }).format(value);
}

/** "64.6 مليار ريال", "188 مليون ريال", "512,300 ريال": a card's or chart's amount, in billions or millions from a million up. */
export function sarCompact(amount: string, t: Translate): string {
  const value = Number(amount);
  if (!Number.isFinite(value)) {
    return t('common.money.sar', { amount });
  }
  const size = Math.abs(value);
  if (size >= 1e9) {
    return t('common.money.billion', { amount: scaled(value / 1e9) });
  }
  if (size >= 1e6) {
    return t('common.money.million', { amount: scaled(value / 1e6) });
  }
  return t('common.money.sar', { amount: sarWhole(amount) });
}

/** "64,553,236,526.17 ريال": the full figure, as a compact one's hover shows it. */
export function sarExactLabel(amount: string, t: Translate): string {
  return t('common.money.sar', { amount: sarExact(amount) });
}

/** "64,553,236,526 ريال": whole riyals with their unit, for a detail or a table cell that names it. */
export function sarWholeLabel(amount: string, t: Translate): string {
  return t('common.money.sar', { amount: sarWhole(amount) });
}
