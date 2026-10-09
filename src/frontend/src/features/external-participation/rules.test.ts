import { describe, expect, test } from 'vitest';

import { assigneeValue, OTHER } from '@/features/tasks/assignee.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import {
  attempt,
  infoRequest,
  progressRequest,
  revision,
} from '@/test/externalParticipationFixtures.ts';

import { type ContributionFieldDefinition, type ExternalUpdateRequestDetail } from './api/types.ts';
import {
  CONTRIBUTION_INTERNAL_ONLY_FIELDS,
  disclosedContribution,
  disclosedRequest,
  REQUEST_INTERNAL_ONLY_FIELDS,
} from './projection.ts';
import { checkRequestForm, emptyRequestForm, toRequestBody } from './requestForm.ts';
import {
  applicationActionOf,
  applicationStateOf,
  externalOutcomeOf,
  participationRows,
  responseFieldCode,
  responseFieldsToSend,
  responseIssuesOf,
  schemaNeedsTarget,
  versionOfEtag,
} from './rules.ts';

const PERCENT: ContributionFieldDefinition = {
  fieldCode: 'actualPercentComplete',
  fieldType: 'NUMBER',
  required: true,
  minimum: 0,
  maximum: 100,
};
const NOTE: ContributionFieldDefinition = {
  fieldCode: 'progressNote',
  fieldType: 'NARRATIVE',
  required: false,
  minimum: null,
  maximum: null,
};
const AS_OF: ContributionFieldDefinition = {
  fieldCode: 'asOfDate',
  fieldType: 'DATE',
  required: false,
  minimum: null,
  maximum: null,
};

/**
 * The fields of a request any caller may read: everything else is internal-only. A field added to the representation
 * fails this test until it is classified one way or the other, as `ExternalProjectionTests` does on the server.
 */
const REQUEST_EXTERNAL_FIELDS = [
  'id',
  'projectId',
  'formalProjectId',
  'externalEntityId',
  'origin',
  'contributionTypeItemId',
  'contributionSchemaCode',
  'applicationMode',
  'targetModule',
  'targetType',
  'targetId',
  'targetLabel',
  'responseFields',
  'instructions',
  'responsibleUserId',
  'dueDate',
  'dueCondition',
  'status',
  'issuedAt',
  'cancelledAt',
  'cancellationReason',
  'closedAt',
  'createdAt',
  'updatedAt',
  'projection',
  'maskedFields',
];

const CONTRIBUTION_EXTERNAL_FIELDS = [
  'id',
  'externalUpdateRequestId',
  'projectId',
  'externalEntityId',
  'revisionNo',
  'previousRevisionId',
  'status',
  'contributorUserId',
  'fields',
  'submittedAt',
  'reviewedAt',
  'reviewReason',
  'createdAt',
  'updatedAt',
  'projection',
  'maskedFields',
];

describe('the least-disclosure projection (projection.ts)', () => {
  test('every field of a request and of a revision is classified, external or internal-only, and the lists mirror the server’s', () => {
    expect(Object.keys(progressRequest()).sort()).toEqual(
      [...REQUEST_EXTERNAL_FIELDS, ...REQUEST_INTERNAL_ONLY_FIELDS].sort(),
    );
    expect(Object.keys(revision()).sort()).toEqual(
      [...CONTRIBUTION_EXTERNAL_FIELDS, ...CONTRIBUTION_INTERNAL_ONLY_FIELDS].sort(),
    );
    // ExternalParticipationViews.RequestInternalOnlyFields and ContributionInternalOnlyFields (TASK-066 D-6).
    expect(REQUEST_INTERNAL_ONLY_FIELDS).toEqual([
      'reviewerUserId',
      'participationConfigurationVersionId',
      'issuedByUserId',
      'createdBy',
      'updatedBy',
    ]);
    expect(CONTRIBUTION_INTERNAL_ONLY_FIELDS).toEqual([
      'targetVersion',
      'targetState',
      'reviewedByUserId',
      'reviewStartedAt',
      'reviewInternalNote',
      'createdBy',
      'updatedBy',
    ]);
  });

  test('an internal record keeps every field; an external one keeps none of the internal-only fields, even when sent them', () => {
    const internal = progressRequest();
    expect(disclosedRequest(internal)).toEqual(internal);

    const leaking: ExternalUpdateRequestDetail = { ...internal, projection: 'EXTERNAL' };
    expect(Object.keys(disclosedRequest(leaking)).sort()).toEqual(
      [...REQUEST_EXTERNAL_FIELDS].sort(),
    );

    const note = { text: 'internal', language: 'EN' };
    const shown = disclosedContribution({
      ...revision({ reviewInternalNote: note }),
      projection: 'EXTERNAL',
    });
    expect('reviewInternalNote' in shown).toBe(false);
    expect('targetVersion' in shown).toBe(false);
  });

  test('a field the record itself names as masked is dropped whatever the audience', () => {
    const masked = disclosedRequest({ ...progressRequest(), maskedFields: ['dueDate'] });
    expect('dueDate' in masked).toBe(false);
  });
});

describe('the response form (rules.ts)', () => {
  test.each([
    ['', false, null],
    ['', true, 'REQUIRED'],
    ['42', true, null],
    ['42.1234', true, null],
    ['42.12345', true, 'TOO_PRECISE'],
    ['100', true, null],
    ['100.0001', true, 'OUT_OF_RANGE'],
    ['-1', true, 'OUT_OF_RANGE'],
    ['4a', true, 'MALFORMED'],
    ['1e2', true, 'MALFORMED'],
  ])('a percentage of %j (submitting: %s) is %s', (value, submitting, code) => {
    expect(responseFieldCode(PERCENT, value, submitting)).toBe(code);
  });

  test('a date must be a calendar date; a narrative takes any text; an optional field may be empty on submission', () => {
    expect(responseFieldCode(AS_OF, '2026-02-30', true)).toBe('MALFORMED');
    expect(responseFieldCode(AS_OF, '2026-10-09', true)).toBeNull();
    expect(responseFieldCode(NOTE, 'anything at all', true)).toBeNull();
    expect(responseFieldCode(NOTE, '', true)).toBeNull();
  });

  test('only answered fields are sent, in the schema’s order, a narrative with its language and anything else without', () => {
    expect(
      responseFieldsToSend(
        [PERCENT, NOTE, AS_OF],
        { progressNote: ' Laid. ', actualPercentComplete: '42' },
        'ar',
      ),
    ).toEqual([
      { fieldCode: 'actualPercentComplete', value: '42', language: null },
      { fieldCode: 'progressNote', value: 'Laid.', language: 'ar' },
    ]);
  });

  test('a refusal naming fields[i] lands on the field sent at i; a missing required field names its own code', () => {
    const sent = responseFieldsToSend(
      [PERCENT, NOTE],
      { actualPercentComplete: '42', progressNote: 'x' },
      'en',
    );
    const refused = new ApiError(
      422,
      'CONTRIBUTION_VALIDATION_FAILED',
      [{ field: 'fields[1].value', code: 'NOT_ALLOWED' }],
      null,
    );
    expect(responseIssuesOf(refused, sent)).toEqual({ progressNote: 'NOT_ALLOWED' });

    const missing = new ApiError(
      422,
      'CONTRIBUTION_REQUIRED_ITEM_MISSING',
      [{ field: 'actualPercentComplete', code: 'REQUIRED' }],
      null,
    );
    expect(responseIssuesOf(missing, [])).toEqual({ actualPercentComplete: 'REQUIRED' });
  });

  test('an entity reads every accepted outcome as Accepted', () => {
    expect(externalOutcomeOf('ACCEPTED_PENDING_APPLICATION')).toBe('ACCEPTED');
    expect(externalOutcomeOf('APPLIED')).toBe('ACCEPTED');
    expect(externalOutcomeOf('APPLICATION_FAILED')).toBe('ACCEPTED');
    expect(externalOutcomeOf('RETURNED')).toBe('RETURNED');
  });
});

describe('source application states (rules.ts, acceptance criterion 2)', () => {
  test.each([
    ['ACCEPTED_PENDING_APPLICATION', [], 'PENDING', 'apply'],
    ['ACCEPTED_PENDING_APPLICATION', [attempt({ status: 'CONFLICT' })], 'CONFLICT', 'revalidate'],
    [
      'ACCEPTED_PENDING_APPLICATION',
      [attempt({ status: 'CONFLICT', revalidatedAt: '2026-10-09T11:00:00Z' })],
      'REVALIDATED',
      'retry',
    ],
    [
      'ACCEPTED_PENDING_APPLICATION',
      [attempt({ status: 'FAILED', failureCode: 'PROJECT_STATE_NOT_PERMITTED' })],
      'RETRYABLE',
      'retry',
    ],
    ['APPLIED', [attempt()], 'APPLIED', null],
    [
      'APPLICATION_FAILED',
      [attempt({ status: 'FAILED', failureCode: 'SOURCE_RECORD_TERMINAL' })],
      'FAILED',
      null,
    ],
  ] as const)('%s after %j is %s, and takes %s', (status, attempts, state, action) => {
    expect(applicationStateOf(status, attempts)).toBe(state);
    expect(applicationActionOf(applicationStateOf(status, attempts))).toBe(action);
  });

  test('only the newest attempt decides: a conflict revalidated, then refused, is a retry', () => {
    expect(
      applicationStateOf('ACCEPTED_PENDING_APPLICATION', [
        attempt({ attemptNo: 2, status: 'FAILED', failureCode: 'SOURCE_RECORD_STATE_INVALID' }),
        attempt({ attemptNo: 1, status: 'CONFLICT', revalidatedAt: '2026-10-09T11:00:00Z' }),
      ]),
    ).toBe('RETRYABLE');
  });

  test('a row version is read from a strong ETag only', () => {
    expect(versionOfEtag('"7002"')).toBe(7002);
    expect(versionOfEtag('W/"7002"')).toBeNull();
    expect(versionOfEtag(null)).toBeNull();
  });
});

describe('SCR-161 and SCR-167 (requestForm.ts, rules.ts)', () => {
  const today = '2026-10-09';

  test('a draft needs a type, an entity and instructions; issuing also the people and a due date not in the past', () => {
    const values = {
      ...emptyRequestForm('6f6f6f6f-0000-4000-8000-000000000107', null),
      contributionTypeItemId: 'd1300000-0000-4000-8000-000000000502',
      instructions: 'Where is the survey?',
    };
    const draft = checkRequestForm(values, {
      needsTarget: false,
      canSearch: false,
      issuing: false,
      today,
    });
    expect(Object.values(draft).filter((code) => code !== null)).toEqual([]);

    const issue = checkRequestForm(
      { ...values, dueDate: '2026-10-08' },
      { needsTarget: false, canSearch: false, issuing: true, today },
    );
    expect(issue).toMatchObject({
      dueDate: 'NOT_ALLOWED',
      responsibleUserId: 'REQUIRED',
      reviewerUserId: 'REQUIRED',
    });
  });

  test('a task-progress type needs its task; a typed person id must be a UUID', () => {
    const values = {
      ...emptyRequestForm('6f6f6f6f-0000-4000-8000-000000000107', null),
      contributionTypeItemId: 'd1300000-0000-4000-8000-000000000501',
      instructions: 'Report it.',
      responder: { ...assigneeValue(null), choice: OTHER, typedId: 'not-an-id' },
    };
    expect(
      checkRequestForm(values, { needsTarget: true, canSearch: false, issuing: false, today }),
    ).toMatchObject({ targetId: 'REQUIRED', responsibleUserId: 'MALFORMED' });
    expect(schemaNeedsTarget('TASK_PROGRESS')).toBe(true);
    expect(schemaNeedsTarget('PROJECT_INFORMATION')).toBe(false);
    expect(schemaNeedsTarget('ANYTHING_ELSE')).toBeUndefined();
  });

  test('the body sends no source for a type that names none, and ids in lower case', () => {
    const body = toRequestBody(
      {
        ...emptyRequestForm('6F6F6F6F-0000-4000-8000-000000000107', null),
        contributionTypeItemId: 'D1300000-0000-4000-8000-000000000502',
        targetId: 'b1300000-0000-4000-8000-000000000401',
        instructions: ' Where is the survey? ',
      },
      { needsTarget: false, canSearch: false },
      'en',
    );
    expect(body).toEqual({
      contributionTypeItemId: 'd1300000-0000-4000-8000-000000000502',
      targetId: null,
      instructions: { text: 'Where is the survey?', language: 'en' },
      responsibleUserId: null,
      reviewerUserId: null,
      dueDate: null,
    });
  });

  test('participation is counted per project and entity, overdue first', () => {
    const otherEntity = '6f6f6f6f-0000-4000-8000-000000000999';
    const rows = participationRows([
      progressRequest({ status: 'IN_PROGRESS' }),
      infoRequest(),
      infoRequest({
        id: 'x1',
        externalEntityId: otherEntity,
        dueCondition: 'NOT_DUE',
        status: 'CLOSED',
      }),
    ]);
    expect(
      rows.map((row) => [
        row.externalEntityId === otherEntity,
        row.drafting,
        row.notStarted,
        row.overdue,
        row.closed,
        row.responders,
      ]),
    ).toEqual([
      [false, 1, 1, 1, 0, 1],
      [true, 0, 0, 0, 1, 0],
    ]);
  });
});
