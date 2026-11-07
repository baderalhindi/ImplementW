// A number as it is typed (review round 5, item 6): shown with thousands separators, kept and sent as the plain number.

const ARABIC_DIGIT_ZERO = 0x0660;
const EASTERN_ARABIC_DIGIT_ZERO = 0x06f0;

export interface AmountRules {
  /** Places kept after the point; further digits are dropped as they are typed. */
  maxDecimals: number;
  /** Whether the field takes a negative number, such as a change's cost impact. */
  allowNegative: boolean;
}

/** Arabic-Indic digits and the Arabic decimal separator become their Latin forms, so a phone keyboard or a paste is read the same. */
function latinDigits(text: string): string {
  return text.replace(/[\u0660-\u0669\u06f0-\u06f9\u066b]/g, (char) => {
    if (char === '\u066b') {
      return '.';
    }
    const code = char.charCodeAt(0);
    return String(
      code >= EASTERN_ARABIC_DIGIT_ZERO
        ? code - EASTERN_ARABIC_DIGIT_ZERO
        : code - ARABIC_DIGIT_ZERO,
    );
  });
}

/** The plain number in what was typed or pasted: separators and anything else that is not part of a number are dropped. */
export function plainAmount(typed: string, rules: AmountRules): string {
  const text = latinDigits(typed).trim();
  const negative = rules.allowNegative && text.startsWith('-');
  const [whole = '', ...rest] = text.replace(/[^0-9.]/g, '').split('.');
  const hasPoint = rest.length > 0 && rules.maxDecimals > 0;
  const decimals = rest.join('').slice(0, rules.maxDecimals);
  return `${negative ? '-' : ''}${whole}${hasPoint ? `.${decimals}` : ''}`;
}

/** The plain number with its thousands separators: 1250000.5 reads 1,250,000.5. */
export function formatAmount(plain: string): string {
  const negative = plain.startsWith('-');
  const [whole = '', decimals] = (negative ? plain.slice(1) : plain).split('.');
  const grouped = whole.replace(/\B(?=(\d{3})+(?!\d))/g, ',');
  return `${negative ? '-' : ''}${grouped}${decimals === undefined ? '' : `.${decimals}`}`;
}

/** How many characters of the number, separators apart, lie before `caret`: what stays put when the separators move. */
export function significantBefore(display: string, caret: number): number {
  return display.slice(0, caret).replace(/,/g, '').length;
}

/** The caret position in `display` with `significant` characters of the number before it. */
export function caretAfter(display: string, significant: number): number {
  let seen = 0;
  let index = 0;
  while (index < display.length && seen < significant) {
    if (display[index] !== ',') {
      seen += 1;
    }
    index += 1;
  }
  return index;
}
