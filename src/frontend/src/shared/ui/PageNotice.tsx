import { type ReactElement } from 'react';

export interface Notice {
  /** `warning`: nothing was changed because the record moved on; the list was read again. */
  tone: 'success' | 'warning';
  message: string;
}

/** The outcome of the last action on the page, announced politely to a screen reader. */
export function PageNotice({ notice }: { notice: Notice | null }): ReactElement | null {
  if (notice === null) {
    return null;
  }
  return (
    <p className={notice.tone === 'warning' ? 'notice notice--warning' : 'notice'} role="status">
      {notice.message}
    </p>
  );
}
