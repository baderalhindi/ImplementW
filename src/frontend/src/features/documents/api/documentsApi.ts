import {
  apiDownload,
  apiRequest,
  type ApiResponse,
  apiUpload,
  type DownloadedFile,
} from '@/shared/api/httpClient.ts';

import {
  type BusinessLinkDetail,
  type DocumentDetail,
  type DocumentDraft,
  type DocumentQuery,
  type DocumentSummary,
  type DocumentUpdateRequest,
  type DocumentVersionDetail,
  type Page,
} from './types.ts';

// WF-12 (TASK-037 document-management.md §3). Every POST and PUT is a sensitive write: the client sends an
// Idempotency-Key. Uploads are multipart/form-data with the file in part `file` (R-8).

async function data<T>(request: Promise<ApiResponse<T>>): Promise<T> {
  return (await request).data;
}

/** MOD-050's metadata as the form fields the API reads, named as the JSON properties are (`title.text`, …). */
function draftForm(file: File, draft: DocumentDraft): FormData {
  const form = new FormData();
  form.append('title.text', draft.title.text);
  form.append('title.language', draft.title.language);
  if (draft.description !== null) {
    form.append('description.text', draft.description.text);
    form.append('description.language', draft.description.language);
  }
  form.append('documentTypeItemId', draft.documentTypeItemId);
  form.append('dataClassificationItemId', draft.dataClassificationItemId);
  if (draft.projectId !== null) {
    form.append('projectId', draft.projectId);
  }
  form.append('file', file, file.name);
  return form;
}

function fileForm(file: File): FormData {
  const form = new FormData();
  form.append('file', file, file.name);
  return form;
}

export type UploadProgress = (fraction: number) => void;

export const documentsApi = {
  /** SCR-120, SCR-121 (`projectId`), SCR-122: only documents the caller may read, most recently changed first. */
  list: (query: DocumentQuery, signal?: AbortSignal) =>
    data(apiRequest<Page<DocumentSummary>>('/documents', { query: { ...query }, signal })),
  /** SCR-123, with the ETag MOD-051 and MOD-053 send back as If-Match. */
  get: (id: string, signal?: AbortSignal) =>
    apiRequest<DocumentDetail>(`/documents/${id}`, { signal }),
  /** MOD-050. Version 1 is SCAN_PENDING until the scan decides it. */
  create: (file: File, draft: DocumentDraft, onProgress: UploadProgress, signal?: AbortSignal) =>
    data(apiUpload<DocumentDetail>('/documents', draftForm(file, draft), { onProgress, signal })),
  /** MOD-051. If-Match is required. */
  update: (id: string, request: DocumentUpdateRequest, etag: string | null) =>
    data(
      apiRequest<DocumentDetail>(`/documents/${id}`, {
        method: 'PUT',
        body: request,
        ifMatch: etag,
      }),
    ),
  /** MOD-053: ACTIVE → ARCHIVED. Versions, links and evidence stay readable. */
  archive: (id: string, etag: string | null) =>
    data(apiRequest<DocumentDetail>(`/documents/${id}/archive`, { method: 'POST', ifMatch: etag })),
  /** SCR-124: every version, newest first. Unpaged: a small per-document set (TASK-037 F-11). */
  listVersions: (id: string, signal?: AbortSignal) =>
    data(apiRequest<DocumentVersionDetail[]>(`/documents/${id}/versions`, { signal })),
  getVersion: (id: string, versionId: string, signal?: AbortSignal) =>
    data(apiRequest<DocumentVersionDetail>(`/documents/${id}/versions/${versionId}`, { signal })),
  /** MOD-052 Replace Version. Earlier versions, and evidence pinned to them, do not change. */
  addVersion: (id: string, file: File, onProgress: UploadProgress, signal?: AbortSignal) =>
    data(
      apiUpload<DocumentVersionDetail>(`/documents/${id}/versions`, fileForm(file), {
        onProgress,
        signal,
      }),
    ),
  /** A CLEAN version's bytes; any other state is 409 DOCUMENT_NOT_AVAILABLE. */
  downloadVersion: (id: string, versionId: string): Promise<DownloadedFile> =>
    apiDownload(`/documents/${id}/versions/${versionId}/content`),
  /** SCAN_FAILED → SCAN_PENDING. */
  rescan: (id: string, versionId: string) =>
    data(
      apiRequest<DocumentVersionDetail>(`/documents/${id}/versions/${versionId}/rescan`, {
        method: 'POST',
      }),
    ),
  /** Every link, ended ones included, with its evidence. */
  listLinks: (id: string, signal?: AbortSignal) =>
    data(apiRequest<BusinessLinkDetail[]>(`/documents/${id}/links`, { signal })),
};

export const evidenceApi = {
  /** The version an evidence reference pins, only while CLEAN. */
  download: (evidenceReferenceId: string): Promise<DownloadedFile> =>
    apiDownload(`/evidence-references/${evidenceReferenceId}/content`),
};

/** The catalogues a document names (db/seed codes). */
export type DocumentCatalogueCode = 'DOCUMENT_TYPE' | 'DATA_CLASSIFICATION' | 'EVIDENCE_TYPE';
