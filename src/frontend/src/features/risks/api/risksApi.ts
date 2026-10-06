import { apiRequest } from '@/shared/api/httpClient.ts';
import { readAllPages } from '@/shared/api/paging.ts';

import {
  type RiskAcceptanceDetail,
  type RiskAcceptCommand,
  type RiskAssessCommand,
  type RiskAssessmentDetail,
  type RiskCloseCommand,
  type RiskCreateRequest,
  type RiskDetail,
  type RiskMatrixResolution,
  type RiskRequest,
  type RiskTreatmentActionCreateRequest,
  type RiskTreatmentActionDetail,
  type RiskTreatmentActionRequest,
} from './types.ts';

// WF-06's register (`/risks`) and its read-only histories and treatment actions (TASK-055 risk-management.md §4). A
// collection of a project the caller may not see is an empty page. Every POST and PUT is a sensitive write with an
// Idempotency-Key, never retried here: a retried create registers a second risk (TASK-055 F-14). The PUTs require
// If-Match (428 without); the commands honour it when sent, and each answers the whole risk with its new ETag.

/** The risk commands that carry no body. */
export type RiskCommand = 'start-treatment' | 'monitor' | 'revoke-acceptance' | 'reopen';

/** A treatment action's own commands: PLANNED → IN_PROGRESS → COMPLETED, or CANCELLED (TASK-055 D-6). */
export type RiskActionCommand = 'start' | 'complete' | 'cancel';

export const risksApi = {
  /** A project's risks, most recently changed first (the API's fixed order). */
  risks: (projectId: string, signal?: AbortSignal) =>
    readAllPages<RiskDetail>('/risks', { projectId }, signal),
  /** One risk with the ETag its PUT and commands send back. */
  risk: (id: string, signal?: AbortSignal) => apiRequest<RiskDetail>(`/risks/${id}`, { signal }),
  /** MOD-030: registered IDENTIFIED. */
  create: (request: RiskCreateRequest) =>
    apiRequest<RiskDetail>('/risks', { method: 'POST', body: request }),
  /** MOD-031 and MOD-033: the register fields as a whole. */
  update: (id: string, request: RiskRequest, etag: string | null) =>
    apiRequest<RiskDetail>(`/risks/${id}`, { method: 'PUT', body: request, ifMatch: etag }),
  /** MOD-032: a new assessment version, rated against the RISK_MATRIX version in force. */
  assess: (id: string, command: RiskAssessCommand, etag: string | null) =>
    apiRequest<RiskDetail>(`/risks/${id}/assess`, { method: 'POST', body: command, ifMatch: etag }),
  accept: (id: string, command: RiskAcceptCommand, etag: string | null) =>
    apiRequest<RiskDetail>(`/risks/${id}/accept`, { method: 'POST', body: command, ifMatch: etag }),
  /** MOD-035: 400 when the rationale is missing or blank. */
  close: (id: string, command: RiskCloseCommand, etag: string | null) =>
    apiRequest<RiskDetail>(`/risks/${id}/close`, { method: 'POST', body: command, ifMatch: etag }),
  command: (id: string, command: RiskCommand, etag: string | null) =>
    apiRequest<RiskDetail>(`/risks/${id}/${command}`, { method: 'POST', body: {}, ifMatch: etag }),
  /** Every assessment version of a risk, newest first. */
  assessments: (riskId: string, signal?: AbortSignal) =>
    readAllPages<RiskAssessmentDetail>('/risk-assessments', { riskId }, signal),
  /** Every acceptance of a risk, newest first. */
  acceptances: (riskId: string, signal?: AbortSignal) =>
    readAllPages<RiskAcceptanceDetail>('/risk-acceptances', { riskId }, signal),
  /** A risk's treatment actions, oldest first. */
  actions: (riskId: string, signal?: AbortSignal) =>
    readAllPages<RiskTreatmentActionDetail>('/risk-treatment-actions', { riskId }, signal),
  action: (id: string, signal?: AbortSignal) =>
    apiRequest<RiskTreatmentActionDetail>(`/risk-treatment-actions/${id}`, { signal }),
  /** MOD-034: PLANNED. */
  createAction: (request: RiskTreatmentActionCreateRequest) =>
    apiRequest<RiskTreatmentActionDetail>('/risk-treatment-actions', {
      method: 'POST',
      body: request,
    }),
  updateAction: (id: string, request: RiskTreatmentActionRequest, etag: string | null) =>
    apiRequest<RiskTreatmentActionDetail>(`/risk-treatment-actions/${id}`, {
      method: 'PUT',
      body: request,
      ifMatch: etag,
    }),
  actionCommand: (id: string, command: RiskActionCommand, etag: string | null) =>
    apiRequest<RiskTreatmentActionDetail>(`/risk-treatment-actions/${id}/${command}`, {
      method: 'POST',
      body: {},
      ifMatch: etag,
    }),
  /**
   * The RISK_MATRIX version in force (FG-04, ADR-011): its levels, ratings and cells. Read on every visit, never kept,
   * so a newly published version shows on the next load. Needs CONFIGURATION_VIEW; 422 CONFIGURATION_MISSING when none
   * is published.
   */
  matrix: async (signal?: AbortSignal) =>
    (
      await apiRequest<RiskMatrixResolution>('/configuration-resolutions/RISK_MATRIX', {
        signal,
      })
    ).data,
};
