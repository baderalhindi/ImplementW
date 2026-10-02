// The WF-12 representations (TASK-037, document-management.md §3) as the API serialises them: enums in
// SNAKE_CASE_UPPER, ids as strings, instants as ISO 8601.

export type DocumentStatus = 'ACTIVE' | 'ARCHIVED';

/** CTL-20. Only CLEAN content is served or can be evidence; CLEAN and QUARANTINED are final. */
export type ScanState = 'SCAN_PENDING' | 'CLEAN' | 'QUARANTINED' | 'SCAN_FAILED';

export type BusinessLinkRole = 'ATTACHMENT' | 'REFERENCE';

export type EvidenceReferenceStatus = 'VALID' | 'WITHDRAWN';

/** Free text with its entry language. The API writes the language as `EN`/`AR` and reads it as `en`/`ar`. */
export interface NarrativeText {
  text: string;
  language: string;
}

export interface NarrativeTextRequest {
  text: string;
  language: 'ar' | 'en';
}

export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** SCR-124: one version. The storage key is never part of any representation. */
export interface DocumentVersionDetail {
  id: string;
  documentId: string;
  versionNo: number;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  checksumSha256: string;
  uploadedByUserId: string;
  uploadedAt: string;
  scanState: ScanState;
  scanCompletedAt: string | null;
}

/** SCR-123, with the latest version (null only before the first version commits). */
export interface DocumentDetail {
  id: string;
  title: NarrativeText;
  description: NarrativeText | null;
  documentTypeItemId: string;
  dataClassificationItemId: string;
  projectId: string | null;
  ownerUserId: string;
  status: DocumentStatus;
  latestVersion: DocumentVersionDetail | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
}

/** A document in a list (SCR-120–122), with its latest version's number and scan state. */
export interface DocumentSummary {
  id: string;
  title: NarrativeText;
  documentTypeItemId: string;
  dataClassificationItemId: string;
  projectId: string | null;
  ownerUserId: string;
  status: DocumentStatus;
  latestVersionNo: number | null;
  latestScanState: ScanState | null;
  updatedAt: string;
}

/** `satisfies`: VALID, on a link not ended, pinned to a CLEAN version (TASK-037 D-12). */
export interface EvidenceReferenceDetail {
  id: string;
  businessLinkId: string;
  documentId: string;
  documentVersionId: string;
  versionNo: number;
  evidenceTypeItemId: string;
  designatedByUserId: string;
  designatedAt: string;
  status: EvidenceReferenceStatus;
  scanState: ScanState;
  satisfies: boolean;
}

/** The record of another module a document is linked to, by identity only (M-4). */
export interface BusinessTarget {
  module: string;
  type: string;
  id: string;
}

/** Every link, ended ones included: unlinking never deletes (TASK-037 D-8). */
export interface BusinessLinkDetail {
  id: string;
  documentId: string;
  linkRole: BusinessLinkRole;
  target: BusinessTarget;
  linkedByUserId: string;
  linkedAt: string;
  unlinkedAt: string | null;
  unlinkedByUserId: string | null;
  evidence: EvidenceReferenceDetail[];
}

export interface DocumentQuery {
  projectId?: string | undefined;
  status?: DocumentStatus | undefined;
  q?: string | undefined;
  page?: number;
  pageSize?: number;
}

/** MOD-050: the metadata sent as form fields beside the file. */
export interface DocumentDraft {
  title: NarrativeTextRequest;
  description: NarrativeTextRequest | null;
  documentTypeItemId: string;
  dataClassificationItemId: string;
  projectId: string | null;
}

/** MOD-051: the whole editable set (R-5). The project and owner never change. */
export interface DocumentUpdateRequest {
  title: NarrativeTextRequest;
  description: NarrativeTextRequest | null;
  documentTypeItemId: string;
  dataClassificationItemId: string;
}

export type { MasterDataCatalogue, MasterDataItemSummary } from '@/shared/api/masterData.ts';
