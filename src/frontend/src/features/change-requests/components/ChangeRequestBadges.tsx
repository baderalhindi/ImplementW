import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type ChangeRequestStatus } from '../api/types.ts';
import { type ApprovalState, type ImplementationState } from '../changeRequestRules.ts';
import {
  approvalStyle,
  bandTone,
  type ChangeBadgeStyle,
  implementationStyle,
  statusStyle,
} from '../presentation.ts';

/** A tick for Approved, a double tick for Implemented: the shapes differ as the words do. */
function BadgeIcon({ style }: { style: 'approved' | 'implemented' }): ReactElement {
  return (
    <svg
      className="badge__icon"
      viewBox="0 0 16 16"
      width="12"
      height="12"
      aria-hidden="true"
      focusable="false"
    >
      {style === 'approved' ? (
        <path d="M2.5 8.5l3.5 3.5 7.5-8" fill="none" stroke="currentColor" strokeWidth="2" />
      ) : (
        <path
          d="M1 8.5l3 3 6-7M6 11.5l1 1 7.5-8.5"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
        />
      )}
    </svg>
  );
}

/** A badge in one of WF-08's styles; `name` says which state dimension it reports, for tests and styling hooks. */
function ChangeBadge({
  label,
  style,
  name,
}: {
  label: string;
  style: ChangeBadgeStyle;
  name: string;
}): ReactElement {
  if (style !== 'approved' && style !== 'implemented') {
    return (
      <span data-badge={name}>
        <StatusBadge label={label} tone={style} />
      </span>
    );
  }
  return (
    <span data-badge={name}>
      <span className={`badge badge--${style}`}>
        <BadgeIcon style={style} />
        {label}
      </span>
    </span>
  );
}

/** The request's lifecycle status, in its own words. */
export function ChangeRequestStatusBadge({
  status,
}: {
  status: ChangeRequestStatus;
}): ReactElement {
  const { t } = useI18n();
  return (
    <ChangeBadge
      name="status"
      label={t(`changeRequests.status.${status}`)}
      style={statusStyle(status)}
    />
  );
}

/** WF-11's decision on it, apart from whether the change is made. */
export function ApprovalStateBadge({ state }: { state: ApprovalState }): ReactElement {
  const { t } = useI18n();
  return (
    <ChangeBadge
      name="approval"
      label={t(`changeRequests.approvalState.${state}`)}
      style={approvalStyle(state)}
    />
  );
}

/** Whether the approved change has been made in its target modules. */
export function ImplementationStateBadge({ state }: { state: ImplementationState }): ReactElement {
  const { t } = useI18n();
  return (
    <ChangeBadge
      name="implementation"
      label={t(`changeRequests.implementationState.${state}`)}
      style={implementationStyle(state)}
    />
  );
}

export function BandBadge({ bandNo }: { bandNo: number }): ReactElement {
  const { t } = useI18n();
  return (
    <StatusBadge
      label={t('changeRequests.materiality.band', { band: bandNo })}
      tone={bandTone(bandNo)}
    />
  );
}
