import { type ReactElement } from 'react';

import { ConfirmDialog } from '@/features/identity-access/components/ConfirmDialog.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { integrationAdminApi } from '../api/integrationAdminApi.ts';
import { type IntegrationSummary } from '../api/types.ts';
import { labelIn } from '../presentation.ts';
import { integrationProblemMessage } from '../problems.ts';

interface ActivationDialogProps {
  /** The integration to enable or disable; null when the dialog is closed. */
  integration: IntegrationSummary | null;
  /** The detail's ETag, sent as If-Match so a change made meanwhile is refused (412), not overwritten. */
  etag: string | null;
  onClose: () => void;
  onDone: () => void;
}

/**
 * MOD-101 Enable / Disable Integration: activation, which is not health (FG-05 §5.2). Disabling stops FG-05's alerts
 * and operations for the integration and changes no adapter: sign-in and delivery go on, and their telemetry is still
 * recorded (TASK-075 D-15, BR-INT-008). The dialog says so before the command is sent. A RETIRED integration is final
 * and is not offered the command.
 */
export function ActivationDialog({
  integration,
  etag,
  onClose,
  onDone,
}: ActivationDialogProps): ReactElement {
  const { t, language } = useI18n();
  const enable = integration?.status === 'DISABLED';
  const name = integration === null ? '' : labelIn(integration.name, language);
  return (
    <ConfirmDialog
      confirmation={
        integration === null
          ? null
          : {
              title: t(
                enable
                  ? 'integrationAdmin.activation.enableTitle'
                  : 'integrationAdmin.activation.disableTitle',
                { name },
              ),
              body: t(
                enable
                  ? 'integrationAdmin.activation.enableBody'
                  : 'integrationAdmin.activation.disableBody',
              ),
              confirmLabel: t(
                enable ? 'integrationAdmin.actions.enable' : 'integrationAdmin.actions.disable',
              ),
              destructive: !enable,
              action: () => integrationAdminApi.setActivation(integration.id, enable, etag),
              describeProblem: integrationProblemMessage,
            }
      }
      onClose={onClose}
      onDone={onDone}
    />
  );
}
