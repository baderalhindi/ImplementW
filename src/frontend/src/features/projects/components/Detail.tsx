import { type ReactElement, type ReactNode } from 'react';

/** One term and its value in a `.details` list. */
export function Detail({ term, children }: { term: string; children: ReactNode }): ReactElement {
  return (
    <div className="details__row">
      <dt>{term}</dt>
      <dd>{children}</dd>
    </div>
  );
}
