import { useSearchParams } from 'react-router';

import { pageFrom } from './paging.ts';

export interface ListParams {
  params: URLSearchParams;
  page: number;
  /** Sets one filter (an empty value removes it) and returns to the first page. */
  setFilter: (name: string, value: string) => void;
  goToPage: (page: number) => void;
}

/** A list screen's filters and page, kept in the URL so a reload or a shared link shows the same rows. */
export function useListParams(): ListParams {
  const [params, setParams] = useSearchParams();

  const setFilter = (name: string, value: string) => {
    const updated = new URLSearchParams(params);
    updated.delete('page');
    if (value === '') {
      updated.delete(name);
    } else {
      updated.set(name, value);
    }
    setParams(updated);
  };

  const goToPage = (page: number) => {
    const updated = new URLSearchParams(params);
    updated.set('page', String(page));
    setParams(updated);
  };

  return { params, page: pageFrom(params), setFilter, goToPage };
}
