import { type ReactElement, type ReactNode } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

export function LoadingState({ label }: { label?: string }): ReactElement {
  const { t } = useI18n();
  return (
    <p className="state state--loading" role="status">
      {label ?? t('common.states.loading')}
    </p>
  );
}

interface EmptyStateProps {
  title: string;
  children?: ReactNode;
}

export function EmptyState({ title, children }: EmptyStateProps): ReactElement {
  return (
    <div className="state state--empty" role="status">
      <p className="state__title">{title}</p>
      {children}
    </div>
  );
}

interface ErrorStateProps {
  message: string;
  onRetry?: () => void;
}

export function ErrorState({ message, onRetry }: ErrorStateProps): ReactElement {
  const { t } = useI18n();
  return (
    <div className="state state--error" role="alert">
      <p className="state__title">{message}</p>
      {onRetry !== undefined && (
        <button type="button" className="button" onClick={onRetry}>
          {t('common.actions.retry')}
        </button>
      )}
    </div>
  );
}

/** The form-level message: a refusal with no field to attach to, or the count of field errors to fix. */
export function FormAlert({ message }: { message: string | null }): ReactElement | null {
  if (message === null) {
    return null;
  }
  return (
    <p className="form-alert" role="alert">
      {message}
    </p>
  );
}
