import { type ReactElement, useRef } from 'react';

import { ConfirmDialog } from '@/features/identity-access/components/ConfirmDialog.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { notificationsApi } from '../api/notificationsApi.ts';
import { notificationProblemMessage } from '../problems.ts';

interface MarkAllReadDialogProps {
  open: boolean;
  onClose: () => void;
  /** How many notifications the API marked read. */
  onDone: (markedCount: number) => void;
}

/**
 * MOD-071 Mark All Read: every notification sent until now becomes READ. There is no way back: no screen marks a
 * notification unread, so only a new notification makes the badge count again (TASK-039 D-12).
 */
export function MarkAllReadDialog({ open, onClose, onDone }: MarkAllReadDialogProps): ReactElement {
  const { t } = useI18n();
  const marked = useRef(0);
  return (
    <ConfirmDialog
      confirmation={
        open
          ? {
              title: t('notifications.markAll.title'),
              body: t('notifications.markAll.body'),
              confirmLabel: t('notifications.markAll.confirm'),
              destructive: false,
              action: async () => {
                marked.current = (await notificationsApi.markAllRead()).markedCount;
              },
              describeProblem: notificationProblemMessage,
            }
          : null
      }
      onClose={onClose}
      onDone={() => {
        onDone(marked.current);
      }}
    />
  );
}
