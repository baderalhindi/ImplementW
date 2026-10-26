import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import {
  type ApprovedVersionStatus,
  type FinancialStatus,
  type FinancialUpdateStatus,
  type KpiAssignmentStatus,
  type KpiMeasurementStatus,
  type KpiRagStatus,
} from '../api/types.ts';
import {
  assignmentTone,
  financialStatusTone,
  measurementTone,
  ragTone,
  updateTone,
  versionTone,
} from '../presentation.ts';

/** GREEN, AMBER and RED only as the API rated them; UNKNOWN says it is unknown, in grey. */
export function FinancialStatusBadge({ status }: { status: FinancialStatus }): ReactElement {
  const { t } = useI18n();
  return (
    <StatusBadge
      label={t(`financialKpi.financialStatus.${status}`)}
      tone={financialStatusTone(status)}
    />
  );
}

/** A measurement's rating against its pinned target. "Not rated" and "Not applicable" are grey. */
export function RagBadge({ rag }: { rag: KpiRagStatus }): ReactElement {
  const { t } = useI18n();
  return <StatusBadge label={t(`financialKpi.rag.${rag}`)} tone={ragTone(rag)} />;
}

export function UpdateStatusBadge({ status }: { status: FinancialUpdateStatus }): ReactElement {
  const { t } = useI18n();
  return <StatusBadge label={t(`financialKpi.updateStatus.${status}`)} tone={updateTone(status)} />;
}

export function VersionStatusBadge({ status }: { status: ApprovedVersionStatus }): ReactElement {
  const { t } = useI18n();
  return (
    <StatusBadge label={t(`financialKpi.versionStatus.${status}`)} tone={versionTone(status)} />
  );
}

export function MeasurementStatusBadge({ status }: { status: KpiMeasurementStatus }): ReactElement {
  const { t } = useI18n();
  return (
    <StatusBadge
      label={t(`financialKpi.measurementStatus.${status}`)}
      tone={measurementTone(status)}
    />
  );
}

export function AssignmentStatusBadge({ status }: { status: KpiAssignmentStatus }): ReactElement {
  const { t } = useI18n();
  return (
    <StatusBadge
      label={t(`financialKpi.assignmentStatus.${status}`)}
      tone={assignmentTone(status)}
    />
  );
}

/** The gate decision: financial widgets are sensitive and masked by audience (ADR-010). */
export function SensitiveNote(): ReactElement {
  const { t } = useI18n();
  return (
    <p className="sensitive-note">
      <span className="badge badge--sensitive">{t('financialKpi.sensitive.badge')}</span>
      <span>{t('financialKpi.sensitive.note')}</span>
    </p>
  );
}
