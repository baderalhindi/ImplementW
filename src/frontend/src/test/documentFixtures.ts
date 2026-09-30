import {
  type BusinessLinkDetail,
  type DocumentDetail,
  type DocumentSummary,
  type DocumentVersionDetail,
  type MasterDataItemSummary,
} from '@/features/documents/api/types.ts';
import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { DATE_LOCALES } from '@/shared/i18n/i18n.ts';

import { sessionFor } from './identityAccessFixtures.ts';
import { type MockApi, problem } from './mockApi.ts';

// Test data only: invented names and fixed ids. Each id starts differently, so a shortened id is recognisable.

export const SELF_ID = '4f4f4f4f-0000-4000-8000-000000000010';
export const UPLOADER_ID = '5a5a5a5a-0000-4000-8000-000000000011';
export const OTHER_UPLOADER_ID = '6b6b6b6b-0000-4000-8000-000000000012';
export const PROJECT_ID = '7c7c7c7c-0000-4000-8000-000000000013';
export const DOCUMENT_ID = '8d8d8d8d-0000-4000-8000-000000000014';
export const VERSION_ID = '9e9e9e9e-0000-4000-8000-000000000015';
export const DOCUMENT_TYPE_ID = 'af000000-0000-4000-8000-000000000021';
export const RETIRED_TYPE_ID = 'af000000-0000-4000-8000-000000000022';
export const CLASSIFICATION_ID = 'bf000000-0000-4000-8000-000000000031';
export const EVIDENCE_TYPE_ID = 'cf000000-0000-4000-8000-000000000041';

const PEOPLE: Record<string, string> = {
  [UPLOADER_ID]: 'Salma Uploader',
  [OTHER_UPLOADER_ID]: 'Omar Reviewer',
};

/** An entity Project Manager (R04), the one role holding DOCUMENT_VIEW and DOCUMENT_UPLOAD (TASK-037 D-7). */
export function documentSession(userId = SELF_ID): Session {
  const session = sessionFor(['R04']);
  return {
    ...session,
    user: { ...session.user, id: userId, displayName: PEOPLE[userId] ?? 'Test Manager' },
  };
}

/** The interface's date format (I18nProvider), so a test asserts the timestamp a person reads. */
export function shownAt(iso: string): string {
  return new Intl.DateTimeFormat(DATE_LOCALES.en, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(iso));
}

export function version(overrides: Partial<DocumentVersionDetail> = {}): DocumentVersionDetail {
  return {
    id: VERSION_ID,
    documentId: DOCUMENT_ID,
    versionNo: 1,
    fileName: 'design-review.pdf',
    contentType: 'application/pdf',
    sizeBytes: 2_516_582,
    checksumSha256: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855',
    uploadedByUserId: UPLOADER_ID,
    uploadedAt: '2026-09-28T09:15:00Z',
    scanState: 'CLEAN',
    scanCompletedAt: '2026-09-28T09:16:00Z',
    ...overrides,
  };
}

export function documentDetail(overrides: Partial<DocumentDetail> = {}): DocumentDetail {
  return {
    id: DOCUMENT_ID,
    title: { text: 'Design review minutes', language: 'EN' },
    description: { text: 'Minutes of the stage 2 design review.', language: 'EN' },
    documentTypeItemId: DOCUMENT_TYPE_ID,
    dataClassificationItemId: CLASSIFICATION_ID,
    projectId: PROJECT_ID,
    ownerUserId: UPLOADER_ID,
    status: 'ACTIVE',
    latestVersion: version(),
    createdAt: '2026-09-28T09:15:00Z',
    createdBy: UPLOADER_ID,
    updatedAt: '2026-09-28T09:16:00Z',
    updatedBy: UPLOADER_ID,
    ...overrides,
  };
}

export function documentSummary(overrides: Partial<DocumentSummary> = {}): DocumentSummary {
  return {
    id: DOCUMENT_ID,
    title: { text: 'Design review minutes', language: 'EN' },
    documentTypeItemId: DOCUMENT_TYPE_ID,
    dataClassificationItemId: CLASSIFICATION_ID,
    projectId: PROJECT_ID,
    ownerUserId: UPLOADER_ID,
    status: 'ACTIVE',
    latestVersionNo: 1,
    latestScanState: 'CLEAN',
    updatedAt: '2026-09-28T09:16:00Z',
    ...overrides,
  };
}

export function link(overrides: Partial<BusinessLinkDetail> = {}): BusinessLinkDetail {
  return {
    id: 'd0000000-0000-4000-8000-000000000051',
    documentId: DOCUMENT_ID,
    linkRole: 'ATTACHMENT',
    target: { module: 'Milestone', type: 'MILESTONE', id: 'e0000000-0000-4000-8000-000000000061' },
    linkedByUserId: UPLOADER_ID,
    linkedAt: '2026-09-28T10:00:00Z',
    unlinkedAt: null,
    unlinkedByUserId: null,
    evidence: [
      {
        id: 'f0000000-0000-4000-8000-000000000071',
        businessLinkId: 'd0000000-0000-4000-8000-000000000051',
        documentId: DOCUMENT_ID,
        documentVersionId: VERSION_ID,
        versionNo: 1,
        evidenceTypeItemId: EVIDENCE_TYPE_ID,
        designatedByUserId: UPLOADER_ID,
        designatedAt: '2026-09-28T10:00:00Z',
        status: 'VALID',
        scanState: 'CLEAN',
        satisfies: true,
      },
    ],
    ...overrides,
  };
}

function item(
  id: string,
  catalogueId: string,
  code: string,
  en: string,
  ar: string,
  lifecycleState = 'PUBLISHED',
): MasterDataItemSummary {
  return {
    id,
    catalogueId,
    code,
    label: { en, ar },
    parentItemId: null,
    sortOrder: 1,
    lifecycleState,
    isSystem: false,
  };
}

const CATALOGUES = [
  { id: '10000000-0000-4000-8000-0000000000c1', code: 'DOCUMENT_TYPE' },
  { id: '10000000-0000-4000-8000-0000000000c2', code: 'DATA_CLASSIFICATION' },
  { id: '10000000-0000-4000-8000-0000000000c3', code: 'EVIDENCE_TYPE' },
  { id: '10000000-0000-4000-8000-0000000000c4', code: 'PROJECT_TYPE' },
];

const ITEMS: Record<string, MasterDataItemSummary[]> = {
  '10000000-0000-4000-8000-0000000000c1': [
    item(DOCUMENT_TYPE_ID, 'c1', 'MINUTES', 'Meeting minutes', 'محضر اجتماع'),
    item(RETIRED_TYPE_ID, 'c1', 'MEMO', 'Memo', 'مذكرة', 'RETIRED'),
  ],
  '10000000-0000-4000-8000-0000000000c2': [
    item(CLASSIFICATION_ID, 'c2', 'INTERNAL', 'Internal', 'داخلي'),
  ],
  '10000000-0000-4000-8000-0000000000c3': [
    item(EVIDENCE_TYPE_ID, 'c3', 'SIGN_OFF', 'Stage sign-off', 'اعتماد المرحلة'),
  ],
};

/**
 * The FG-03 and FG-04 reads the WF-12 screens make for names. `readable: false` is a caller without USER_VIEW and
 * MASTER_DATA_VIEW — today every uploader (TASK-038 F-1) — who gets 403 for both.
 */
export function withDocumentLookups(api: MockApi, { readable = true } = {}): MockApi {
  if (!readable) {
    return api
      .on('GET', /^\/master-data-catalogues$/, problem(403, 'PERMISSION_DENIED'))
      .on('GET', /^\/users\/[^/]+$/, problem(403, 'PERMISSION_DENIED'));
  }
  return api
    .on('GET', /^\/master-data-catalogues$/, { body: CATALOGUES })
    .on('GET', /^\/master-data-items$/, (request) => {
      const items = ITEMS[request.query.get('catalogueId') ?? ''] ?? [];
      return { body: { items, page: 1, pageSize: 200, totalCount: items.length } };
    })
    .on('GET', /^\/users\/[^/]+$/, (request) => {
      const id = request.path.split('/').at(-1) ?? '';
      const displayName = PEOPLE[id];
      return displayName === undefined
        ? problem(404, 'NOT_FOUND')
        : { body: { id, displayName, username: displayName.toLowerCase(), userType: 'INTERNAL' } };
    });
}
