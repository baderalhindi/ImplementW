import { useOutletContext } from 'react-router';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type Notice } from '@/shared/ui/PageNotice.tsx';

import { type ProjectAccess } from '../access.ts';
import { type ProjectDetail } from '../api/types.ts';
import { type ProjectLookups } from '../useProjectLookups.ts';

/** What the workspace shell (SCR-040) lends each tab: the project read once, and what the person may do with it. */
export interface WorkspaceContext {
  user: SessionUser;
  project: ProjectDetail;
  /** Sent back as If-Match by every change, so a change made meanwhile is 412, not overwritten. */
  etag: string | null;
  access: ProjectAccess;
  lookups: ProjectLookups;
  /** Reads the project again, e.g. after a tab changed it. */
  reload: () => void;
  notify: (notice: Notice) => void;
}

export function useWorkspace(): WorkspaceContext {
  return useOutletContext<WorkspaceContext>();
}
