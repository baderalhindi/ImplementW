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
