import { Children, Fragment, type ReactElement, type ReactNode } from 'react';

/**
 * Short notes in one run, apart by " · " in the text itself (review round 8, point 11): copied, exported or read aloud, they stay apart, which a
 * separator drawn by CSS does not. Empty notes (false, null) are skipped, so no separator is left dangling.
 */
export function NoteList({
  as: Tag = 'span',
  className,
  children,
}: {
  as?: 'span' | 'p' | 'div';
  className?: string | undefined;
  children: ReactNode;
}): ReactElement {
  const notes = Children.toArray(children);
  return (
    <Tag className={className}>
      {notes.map((note, index) => (
        <Fragment key={index}>
          {index > 0 && <span className="note-list__separator"> · </span>}
          {note}
        </Fragment>
      ))}
    </Tag>
  );
}
