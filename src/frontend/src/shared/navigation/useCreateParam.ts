import { useState } from 'react';
import { useSearchParams } from 'react-router';

/** The query parameter that opens a register's new-record form as the page opens: the Home's quick actions link with it (review round 8, point 08). */
export const CREATE_PARAM = 'create';

/**
 * Whether the page's new-record form is open: open from the start when the address carries `?create=1`. Closing it drops the parameter, so a reload
 * or Back does not open it again.
 */
export function useCreateParam(): [creating: boolean, setCreating: (open: boolean) => void] {
  const [searchParams, setSearchParams] = useSearchParams();
  const [creating, setOpen] = useState(() => searchParams.get(CREATE_PARAM) === '1');
  const setCreating = (open: boolean) => {
    setOpen(open);
    if (!open && searchParams.has(CREATE_PARAM)) {
      const next = new URLSearchParams(searchParams);
      next.delete(CREATE_PARAM);
      setSearchParams(next, { replace: true });
    }
  };
  return [creating, setCreating];
}
