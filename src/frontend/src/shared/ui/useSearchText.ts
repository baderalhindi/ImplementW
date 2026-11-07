import { useEffect, useEffectEvent, useState } from 'react';

/** Long enough to skip the requests between keystrokes, short enough to read as immediate. */
export const SEARCH_TYPING_PAUSE_MS = 300;

/**
 * A search box that applies itself: `onApply` receives the trimmed text once typing pauses, including the empty text when the box is cleared.
 * A change of `applied` from elsewhere (Back, a removed chip, Clear all) resets the box. The answer to its own `onApply` does not, so text
 * typed while that answer travels is kept.
 */
export function useSearchText(
  applied: string,
  onApply: (text: string) => void,
): [string, (text: string) => void] {
  const [text, setText] = useState(applied);
  const [seen, setSeen] = useState(applied);
  const [sent, setSent] = useState<string | null>(null);

  if (applied !== seen) {
    setSeen(applied);
    setSent(null);
    if (applied !== sent) {
      setText(applied);
    }
  }

  const wanted = text.trim();
  const apply = useEffectEvent(() => {
    setSent(wanted);
    onApply(wanted);
  });

  useEffect(() => {
    if (wanted === applied) {
      return undefined;
    }
    const timer = setTimeout(() => {
      apply();
    }, SEARCH_TYPING_PAUSE_MS);
    return () => {
      clearTimeout(timer);
    };
  }, [wanted, applied]);

  return [text, setText];
}
