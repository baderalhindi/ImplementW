import { type NarrativeText } from './types.ts';

// Project import from RPMO's workbook (the 2 Nov 2026 review, R3-4) as the API serialises it.

export type ImportDimension =
  | 'ENTITY'
  | 'DEPARTMENT'
  | 'SECTOR'
  | 'PORTFOLIO'
  | 'INITIATIVE'
  | 'REGION'
  | 'CITY'
  | 'PRIORITY'
  | 'GOVERNANCE'
  | 'STATUS'
  | 'PROJECT_MANAGER'
  | 'FOLLOW_UP_OWNER';

export type ImportIssueLevel = 'ERROR' | 'WARNING' | 'INFO';

export type ImportRowAction = 'CREATE' | 'UPDATE' | 'SKIP';

export type ImportBatchStatus = 'SUBMITTED' | 'APPROVED' | 'RETURNED' | 'ROLLED_BACK';

export interface ImportMapping {
  dimension: ImportDimension;
  sourceText: string;
  targetValue: string;
}

/** The importer's decisions: name mappings, and the rows (by legacy reference) to skip. */
export interface ImportPlan {
  mappings: ImportMapping[];
  skippedRows: string[];
}

export interface ImportRowIssue {
  column: string;
  code: string;
  level: ImportIssueLevel;
}

export interface ImportChange {
  field: string;
  before: string | null;
  after: string | null;
}

export interface ImportRowPreview {
  row: number;
  legacyReference: string | null;
  title: string | null;
  action: ImportRowAction;
  projectId: string | null;
  issues: ImportRowIssue[];
  changes: ImportChange[];
}

export interface ImportOption {
  value: string;
  label: { ar: string; en: string };
}

export interface ImportUnmatchedName {
  dimension: ImportDimension;
  sourceText: string;
  rowCount: number;
  options: ImportOption[];
}

export interface ImportPreview {
  fileSha256: string;
  rowCount: number;
  createCount: number;
  updateCount: number;
  skipCount: number;
  errorCount: number;
  warningCount: number;
  submittable: boolean;
  fileIssues: ImportRowIssue[];
  unmatched: ImportUnmatchedName[];
  rows: ImportRowPreview[];
}

export interface ImportBatch {
  id: string;
  batchNo: number;
  status: ImportBatchStatus;
  fileName: string;
  fileSha256: string;
  submissionNote: NarrativeText | null;
  rowCount: number;
  createCount: number;
  updateCount: number;
  skipCount: number;
  warningCount: number;
  submittedBy: string;
  submittedAt: string;
  decidedByUserId: string | null;
  decidedAt: string | null;
  returnReason: NarrativeText | null;
  rolledBackByUserId: string | null;
  rolledBackAt: string | null;
  mayDecide: boolean;
  mayRollBack: boolean;
}

export type ImportValidationStatus = 'QUEUED' | 'RUNNING' | 'COMPLETED' | 'FAILED';

/** A workbook of more than 200 rows, validated in the background; its importer is notified when it is done. */
export interface ImportValidation {
  id: string;
  status: ImportValidationStatus;
  fileName: string;
  rowCount: number;
  requestedAt: string;
  completedAt: string | null;
  failureCode: string | null;
  preview: ImportPreview | null;
}

export type ImportValueRequestStatus = 'OPEN' | 'ADDED' | 'DECLINED';

/** An importer's request that an administrator add a value the lists lack. */
export interface ImportValueRequest {
  id: string;
  dimension: ImportDimension;
  sourceText: string;
  status: ImportValueRequestStatus;
  requestedBy: string;
  requestedAt: string;
  resolvedByUserId: string | null;
  resolvedAt: string | null;
  resolutionNote: NarrativeText | null;
}
