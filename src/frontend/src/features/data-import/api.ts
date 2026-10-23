import {
  apiDownload,
  apiUpload,
  type ApiResponse,
  type DownloadedFile,
} from '@/shared/api/httpClient.ts';

// TASK-102: a register's migration workbook. Each register serves its template at `<collection>/import-template/content`
// and takes the filled workbook at `<collection>/imports`, validated unless `commit=true` (data-import.md D-4).

export type ImportRowStatus = 'READY' | 'CREATED' | 'SKIPPED' | 'REJECTED';

/** Why the workbook cannot be loaded: the worksheet row (1 is the header), the column header, and the code. */
export interface ImportIssue {
  row: number;
  column: string;
  code: string;
}

export interface ImportRowOutcome {
  row: number;
  legacyId: string;
  status: ImportRowStatus;
  entityId: string | null;
}

/** Nothing is committed unless every row passed: `committed` is true only with no issues. */
export interface ImportReport {
  registerCode: string;
  committed: boolean;
  batchId: string | null;
  rowCount: number;
  createdCount: number;
  skippedCount: number;
  issues: ImportIssue[];
  rows: ImportRowOutcome[];
}

function workbookForm(file: File): FormData {
  const form = new FormData();
  form.append('file', file);
  return form;
}

async function data<T>(response: Promise<ApiResponse<T>>): Promise<T> {
  return (await response).data;
}

export const importApi = {
  template: (collectionPath: string): Promise<DownloadedFile> =>
    apiDownload(`${collectionPath}/import-template/content`),
  /** Every row is written and rolled back: the report says what an import would do. */
  validate: (collectionPath: string, file: File) =>
    data(apiUpload<ImportReport>(`${collectionPath}/imports`, workbookForm(file))),
  /** All rows or none: with any issue the report comes back with `committed: false`. */
  commit: (collectionPath: string, file: File) =>
    data(apiUpload<ImportReport>(`${collectionPath}/imports?commit=true`, workbookForm(file))),
};
