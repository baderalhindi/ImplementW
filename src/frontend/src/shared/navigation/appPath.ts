/**
 * A path the SPA may navigate to or render as a link: a route on this origin (TASK-077). It starts with one slash and
 * holds no backslash, which browsers read as a slash (`/\evil.example` is `//evil.example`, another host), no scheme
 * and no control character. The API refuses notification deep links that break the same rules at intake (TASK-039
 * D-3); the SPA checks again rather than follow a value it cannot vouch for.
 */
export function isAppPath(value: unknown): value is string {
  return (
    typeof value === 'string' &&
    value.startsWith('/') &&
    !value.startsWith('//') &&
    !value.includes('\\') &&
    !value.includes('://') &&
    !hasControlCharacter(value)
  );
}

function hasControlCharacter(value: string): boolean {
  for (let i = 0; i < value.length; i++) {
    const code = value.charCodeAt(i);
    if (code < 0x20 || (code >= 0x7f && code <= 0x9f)) {
      return true;
    }
  }
  return false;
}
