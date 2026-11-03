import {
  apiDownload,
  apiRequest,
  apiUpload,
  type DownloadedFile,
} from '@/shared/api/httpClient.ts';

import {
  type ImportBatch,
  type ImportBatchStatus,
  type ImportDimension,
  type ImportPlan,
  type ImportPreview,
  type ImportValidation,
  type ImportValueRequest,
  type ImportValueRequestStatus,
} from './importTypes.ts';
import {
  type FollowUpReason,
  type Page,
  type ProjectDetail,
  type ProjectFollowUpItem,
  type ProjectFollowUpNote,
  type ProjectFollowUpRequest,
} from './types.ts';

// The 2 Nov 2026 review: RPMO's follow-up of a project and its Follow-up list (R3-3, R3-5), and project import batches (R3-4).

export const followUpApi = {
  /** The whole follow-up of a project from APPROVED_PLANNED on, with an optional note. If-Match is required. */
  update: (projectId: string, request: ProjectFollowUpRequest, etag: string | null) =>
    apiRequest<ProjectDetail>(`/projects/${projectId}/update-follow-up`, {
      method: 'POST',
      body: request,
      ifMatch: etag,
    }),
  notes: async (projectId: string, signal?: AbortSignal) =>
    (
      await apiRequest<Page<ProjectFollowUpNote>>('/project-follow-up-notes', {
        query: { projectId, pageSize: 50 },
        signal,
      })
    ).data,
  /** قائمة المتابعة: most urgent first. */
  list: async (
    query: {
      departmentId?: string | undefined;
      reason?: FollowUpReason | undefined;
      page?: number;
      pageSize?: number;
    },
    signal?: AbortSignal,
  ) =>
    (
      await apiRequest<Page<ProjectFollowUpItem>>('/project-follow-ups', {
        query: { ...query },
        signal,
      })
    ).data,
};

const BATCHES = '/project-import-batches';

function importForm(file: File, plan: ImportPlan, note?: string, noteLanguage?: string): FormData {
  const form = new FormData();
  form.append('file', file);
  form.append('plan', JSON.stringify(plan));
  if (note !== undefined && note.trim() !== '') {
    form.append('note', note.trim());
    form.append('noteLanguage', noteLanguage ?? 'ar');
  }
  return form;
}

export const projectImportApi = {
  template: (): Promise<DownloadedFile> => apiDownload(`${BATCHES}/import-template/content`),
  /** A preview at once, or — for more than 200 rows — the queued background validation (202). */
  validate: async (file: File, plan: ImportPlan): Promise<ImportPreview | ImportValidation> =>
    (
      await apiUpload<ImportPreview | ImportValidation>(
        `${BATCHES}/validations`,
        importForm(file, plan),
      )
    ).data,
  validation: async (validationId: string, signal?: AbortSignal) =>
    (await apiRequest<ImportValidation>(`/project-import-validations/${validationId}`, { signal }))
      .data,
  requestValue: async (dimension: ImportDimension, sourceText: string) =>
    (
      await apiRequest<ImportValueRequest>('/project-import-value-requests', {
        method: 'POST',
        body: { dimension, sourceText },
      })
    ).data,
  valueRequests: async (status: ImportValueRequestStatus | undefined, signal?: AbortSignal) =>
    (
      await apiRequest<Page<ImportValueRequest>>('/project-import-value-requests', {
        query: { status, pageSize: 200 },
        signal,
      })
    ).data,
  resolveValue: (requestId: string, status: 'ADDED' | 'DECLINED', note: string, language: string) =>
    apiRequest<ImportValueRequest>(`/project-import-value-requests/${requestId}/resolve`, {
      method: 'POST',
      body: { status, note: note.trim() === '' ? null : { text: note.trim(), language } },
    }),
  submit: async (file: File, plan: ImportPlan, note: string, noteLanguage: string) =>
    (await apiUpload<ImportBatch>(BATCHES, importForm(file, plan, note, noteLanguage))).data,
  list: async (
    query: { status?: ImportBatchStatus | undefined; page?: number; pageSize?: number },
    signal?: AbortSignal,
  ) => (await apiRequest<Page<ImportBatch>>(BATCHES, { query: { ...query }, signal })).data,
  get: (batchId: string, signal?: AbortSignal) =>
    apiRequest<ImportBatch>(`${BATCHES}/${batchId}`, { signal }),
  preview: async (batchId: string, signal?: AbortSignal) =>
    (await apiRequest<ImportPreview>(`/project-import-batch-previews/${batchId}`, { signal })).data,
  file: (batchId: string): Promise<DownloadedFile> => apiDownload(`${BATCHES}/${batchId}/content`),
  approve: (batchId: string, etag: string | null) =>
    apiRequest<ImportBatch>(`${BATCHES}/${batchId}/approve`, {
      method: 'POST',
      body: {},
      ifMatch: etag,
    }),
  returnBatch: (batchId: string, reason: { text: string; language: string }, etag: string | null) =>
    apiRequest<ImportBatch>(`${BATCHES}/${batchId}/return`, {
      method: 'POST',
      body: { reason },
      ifMatch: etag,
    }),
  rollBack: (batchId: string, etag: string | null) =>
    apiRequest<ImportBatch>(`${BATCHES}/${batchId}/rollback`, {
      method: 'POST',
      body: {},
      ifMatch: etag,
    }),
};
