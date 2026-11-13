/** E.164 (ADR-004): a plus sign and up to fifteen digits, the first not zero. */
export const MOBILE_PATTERN = /^\+[1-9][0-9]{1,14}$/;

/**
 * A number as the person types it, in E.164: spaces, dashes and brackets go, and a Saudi mobile number in its local form
 * (05xxxxxxxx) gains +966. Anything else is sent as typed and checked as E.164.
 */
export function toE164(typed: string): string {
  const compact = typed.replace(/[\s\-()]/g, '');
  return /^05[0-9]{8}$/.test(compact) ? `+966${compact.slice(1)}` : compact;
}
