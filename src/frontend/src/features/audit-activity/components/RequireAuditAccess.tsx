import { type ReactElement, type ReactNode } from 'react';

import { RequireSession } from '@/features/identity-access/session/RequireSession.tsx';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { type AuditAccessView } from '../api/types.ts';
import { auditProblemMessage } from '../problems.ts';
import { useAuditAccess } from '../useAuditAccess.ts';

interface RequireAuditAccessProps {
  /** Whether the person's audit permissions open the screen. */
  allows: (access: AuditAccessView) => boolean;
  /** Why the screen is closed to them: which permission it needs. */
  deniedReason: TranslationKey;
  children: (access: AuditAccessView) => ReactNode;
}

/**
 * Shows an FG-06 administration screen only to a person whose audit permissions open it — not to every R01, and to
 * a non-R01 reviewer who holds them (BR-AUD-023). Nothing of the screen is read for anyone else. Navigation, not
 * protection: the API refuses each read the permissions do not cover.
 */
export function RequireAuditAccess(props: RequireAuditAccessProps): ReactElement {
  return (
    <RequireSession>
      <AuditAccessGate {...props} />
    </RequireSession>
  );
}

function AuditAccessGate({
  allows,
  deniedReason,
  children,
}: RequireAuditAccessProps): ReactElement {
  const { t } = useI18n();
  const access = useAuditAccess();
  if (access.loading) {
    return <LoadingState />;
  }
  if (access.error !== null || access.data === undefined || access.data === null) {
    return <ErrorState error={access.error} message={auditProblemMessage(access.error, t)} onRetry={access.reload} />;
  }
  if (!allows(access.data)) {
    return (
      <div data-audit-denied="true">
        <PageHeader title={t('common.forbidden.title')} description={t(deniedReason)} />
      </div>
    );
  }
  return <>{children(access.data)}</>;
}
