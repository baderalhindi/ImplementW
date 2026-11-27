import { type ReactElement, type ReactNode, useEffect, useState } from 'react';

import { ResourceScopeContext, ResourceScopeStore } from './resourceScope.ts';

/** One screen's shared reads (see useScopedResource): what is read inside it is read once while it stays mounted. */
export function ResourceScope({ children }: { children: ReactNode }): ReactElement {
  const [store] = useState(() => new ResourceScopeStore());
  useEffect(
    () => () => {
      store.dispose();
    },
    [store],
  );
  return <ResourceScopeContext value={store}>{children}</ResourceScopeContext>;
}
