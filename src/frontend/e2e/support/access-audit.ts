import { randomUUID } from 'node:crypto';

import { request } from '@playwright/test';

import { eventually, type PlatformApi } from './api.ts';
import { environment } from './environment.ts';
import { itemId } from './reference.ts';

// What UJ-011 and UJ-014 prepare or read that the shared harness does not: a document uploaded as the SPA uploads one
// (multipart, document-management.md D-2), read back until the scanner decides it, and the access relationships and audit
// events the journeys assert on. Every call goes through the same origin, token and headers as PlatformApi.

export interface DocumentVersion {
  id: string;
  versionNo: number;
  fileName: string;
  scanState: 'SCAN_PENDING' | 'CLEAN' | 'QUARANTINED' | 'SCAN_FAILED';
}

export interface DocumentDetail {
  id: string;
  title: { text: string; language: string };
  projectId: string | null;
  latestVersion: DocumentVersion | null;
}

export interface UploadOptions {
  projectId: string;
  title: string;
  fileName: string;
  content: string;
}

/** POST /documents as the SPA sends it: the file in part `file`, the metadata as form fields (Internal, a Report). */
export async function uploadDocument(
  api: PlatformApi,
  options: UploadOptions,
): Promise<DocumentDetail> {
  const baseURL = environment.baseUrl();
  const http = await request.newContext({
    baseURL,
    extraHTTPHeaders: { Origin: new URL(baseURL).origin },
    ignoreHTTPSErrors: environment.target === 'compose',
  });
  try {
    const response = await http.post('/api/v1/documents', {
      headers: {
        Authorization: `Bearer ${api.session.accessToken}`,
        'Idempotency-Key': randomUUID(),
      },
      multipart: {
        file: {
          name: options.fileName,
          mimeType: 'application/pdf',
          buffer: Buffer.from(options.content),
        },
        'title.text': options.title,
        'title.language': 'en',
        documentTypeItemId: await itemId(api, 'DOCUMENT_TYPE', 'E2E_REPORT'),
        dataClassificationItemId: await itemId(api, 'DATA_CLASSIFICATION', 'E2E_INTERNAL'),
        projectId: options.projectId,
      },
    });
    const text = await response.text();
    if (response.status() !== 201) {
      throw new Error(
        `${api.persona}: POST /documents answered ${String(response.status())} ${text}`,
      );
    }
    return JSON.parse(text) as DocumentDetail;
  } finally {
    await http.dispose();
  }
}

/** The document once the malware scanner has decided its latest version (a stand-in on the stack, AHDA's scanner on SIT). */
export function untilScanned(api: PlatformApi, documentId: string): Promise<DocumentDetail> {
  return eventually(
    () => api.get<DocumentDetail>(`/documents/${documentId}`),
    (d) => d.latestVersion !== null && d.latestVersion.scanState !== 'SCAN_PENDING',
    `document ${documentId} to be scanned`,
  );
}

export interface AccessRelationship {
  id: string;
  userId: string;
  roleCode: string;
  permissionProfileVersionId: string;
  departmentId: string | null;
  projectId: string | null;
  status: 'ACTIVE' | 'ENDED';
  endReason: string | null;
}

export async function accessRelationships(
  administrator: PlatformApi,
  userId: string,
): Promise<AccessRelationship[]> {
  return (
    await administrator.get<{ items: AccessRelationship[] }>(
      `/access-relationships?userId=${userId}&pageSize=100`,
    )
  ).items;
}

export interface AuditEvent {
  id: string;
  category: string;
  eventClass: string;
  eventType: string;
  outcome: string;
  correlationId: string | null;
  subjectId: string | null;
  actorUserId: string | null;
}

/** The formal audit events a reviewer reaches, by a query string the API takes (audit-activity.md §5). */
export async function auditEvents(reviewer: PlatformApi, query: string): Promise<AuditEvent[]> {
  return (await reviewer.get<{ items: AuditEvent[] }>(`/audit-events?${query}&pageSize=200`)).items;
}
