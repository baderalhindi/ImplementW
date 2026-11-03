import {
  apiDownload,
  apiRequest,
  apiUpload,
  type DownloadedFile,
} from '@/shared/api/httpClient.ts';

import {
  type ImportBatch,
  type ImportBatchStatus,
  type ImportPlan,
  type ImportPreview,
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
  validate: async (file: File, plan: ImportPlan) =>
    (await apiUpload<ImportPreview>(`${BATCHES}/validations`, importForm(file, plan))).data,
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
