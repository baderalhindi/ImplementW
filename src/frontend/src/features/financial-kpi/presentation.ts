import { formatSar } from '@/features/projects/presentation.ts';
import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import {
  type ApprovedVersionStatus,
  type Decimal,
  type FinancialStatus,
  type FinancialUpdateStatus,
  type KpiAssignmentStatus,
  type KpiMeasurementStatus,
  type KpiRagStatus,
} from './api/types.ts';
import { toNumber } from './financialKpiRules.ts';

// How WF-14 values read on screen. Each state has its own word; the colour only repeats it (WCAG 1.4.1). Green is
// reserved for a GREEN rating the API gave from a measured figure: a workflow state (published, in force) is blue, so
// a row whose value is "No data" never carries a success colour.

/** UNKNOWN is grey and says so: a status without its figures is never a colour (TASK-052 D-2). */
const FINANCIAL_STATUS_TONES: Record<FinancialStatus, StatusTone> = {
  GREEN: 'positive',
  AMBER: 'warning',
  RED: 'negative',
  UNKNOWN: 'neutral',
};

export function financialStatusTone(status: FinancialStatus): StatusTone {
  return FINANCIAL_STATUS_TONES[status];
}

const RAG_TONES: Record<KpiRagStatus, StatusTone> = {
  GREEN: 'positive',
  AMBER: 'warning',
  RED: 'negative',
  UNKNOWN: 'neutral',
  NOT_APPLICABLE: 'neutral',
};

export function ragTone(rag: KpiRagStatus): StatusTone {
  return RAG_TONES[rag];
}

const UPDATE_TONES: Record<FinancialUpdateStatus, StatusTone> = {
  DRAFT: 'neutral',
  SUBMITTED: 'info',
  UNDER_REVIEW: 'info',
  RETURNED: 'warning',
  PUBLISHED: 'info',
};

export function updateTone(status: FinancialUpdateStatus): StatusTone {
  return UPDATE_TONES[status];
}

const VERSION_TONES: Record<ApprovedVersionStatus, StatusTone> = {
  DRAFT: 'neutral',
  SUBMITTED: 'info',
  UNDER_REVIEW: 'info',
  RETURNED: 'warning',
  ACTIVE: 'info',
  SUPERSEDED: 'neutral',
  REJECTED: 'negative',
  WITHDRAWN: 'neutral',
};

export function versionTone(status: ApprovedVersionStatus): StatusTone {
  return VERSION_TONES[status];
}

const MEASUREMENT_TONES: Record<KpiMeasurementStatus, StatusTone> = {
  DRAFT: 'neutral',
  SUBMITTED: 'info',
  PUBLISHED: 'info',
};

export function measurementTone(status: KpiMeasurementStatus): StatusTone {
  return MEASUREMENT_TONES[status];
}

const ASSIGNMENT_TONES: Record<KpiAssignmentStatus, StatusTone> = {
  ACTIVE: 'info',
  SUSPENDED: 'warning',
  RETIRED: 'neutral',
};

export function assignmentTone(status: KpiAssignmentStatus): StatusTone {
  return ASSIGNMENT_TONES[status];
}

/** A financial update still in the workflow; RETURNED and PUBLISHED are final. */
export function isUpdateInProgress(status: FinancialUpdateStatus): boolean {
  return status === 'DRAFT' || status === 'SUBMITTED' || status === 'UNDER_REVIEW';
}

/** Four places is the API's precision; Latin digits in both languages (TASK-032 D-5). */
const KPI_FORMAT = new Intl.NumberFormat('en', { maximumFractionDigits: 4 });

export function formatKpiValue(value: Decimal): string {
  return KPI_FORMAT.format(toNumber(value));
}

/** "1,250,000.00": the API's string, grouped, exact. The currency is SAR only and said once beside it (ADR-008). */
export function formatAmount(value: Decimal): string {
  return formatSar(typeof value === 'number' ? value.toFixed(2) : value);
}
