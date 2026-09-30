import { rolesApi } from '@/features/identity-access/api/identityAccessApi.ts';
import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

export interface ApprovalNames {
  user: (id: string | null) => string;
  role: (id: string) => string;
}

const SHORT_ID_LENGTH = 8;

/** Module-level, so useApiResource reads the roles once per mount. An approver without ROLE_VIEW gets none. */
async function readRoleNames(signal: AbortSignal) {
  try {
    return await rolesApi.list(signal);
  } catch {
    return [];
  }
}

/**
 * Names for the people and roles a WF-11 screen shows. The approval representations carry ids only (TASK-036 F-1), and
 * reading a user or a role needs USER_VIEW or ROLE_VIEW, which an approver may not hold; an id that cannot be read is
 * shown shortened, and the signed-in person is always "you".
 */
export function useApprovalNames(userIds: (string | null)[]): ApprovalNames {
  const { t, language } = useI18n();
  const user = usePersonNames(userIds);
  const roles = useApiResource(readRoleNames).data ?? [];

  return {
    user,
    role: (id) => {
      const role = roles.find((candidate) => candidate.id === id);
      return role === undefined
        ? t('approvals.people.unknownRole', { id: id.slice(0, SHORT_ID_LENGTH) })
        : `${role.name[language]} (${role.code})`;
    },
  };
}
