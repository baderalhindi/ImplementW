import { type EvidenceReferenceDetail } from '@/features/documents/api/types.ts';
import { apiRequest } from '@/shared/api/httpClient.ts';
import { readAllPages } from '@/shared/api/paging.ts';

import {
  type MilestoneAchievementCreateRequest,
  type MilestoneAchievementDetail,
  type MilestoneAchievementRequest,
  type MilestoneEvidenceDetail,
  type MilestoneEvidenceRequest,
  type ProjectMilestoneCreateRequest,
  type ProjectMilestoneDetail,
  type ProjectMilestoneRequest,
} from './types.ts';

// WF-03's side of the shared milestone (`/project-milestones`) and WF-05's achievement claims (`/milestone-achievements`),
// TASK-050 milestone-achievement.md §4. A collection of a project the caller may not see is an empty page. Every POST
// and PUT is a sensitive write with an Idempotency-Key, never retried here: a retried milestone create creates a second
// milestone (TASK-050 F-15). The PUTs require If-Match (428 without); the other commands honour it when sent.

export const milestonesApi = {
  /** A project's milestones, earliest forecast first, cancelled ones included. */
  milestones: (projectId: string, signal?: AbortSignal) =>
    readAllPages<ProjectMilestoneDetail>('/project-milestones', { projectId }, signal),
  /** One milestone with the ETag its PUT and cancel send back. */
  milestone: (id: string, signal?: AbortSignal) =>
    apiRequest<ProjectMilestoneDetail>(`/project-milestones/${id}`, { signal }),
  /** MOD-016 create: PLANNED, in the project's schedule. */
  createMilestone: (request: ProjectMilestoneCreateRequest) =>
    apiRequest<ProjectMilestoneDetail>('/project-milestones', { method: 'POST', body: request }),
  /** MOD-016 edit: a PLANNED milestone's inputs, as a whole. */
  updateMilestone: (id: string, request: ProjectMilestoneRequest, etag: string | null) =>
    apiRequest<ProjectMilestoneDetail>(`/project-milestones/${id}`, {
      method: 'PUT',
      body: request,
      ifMatch: etag,
    }),
  /** PLANNED → CANCELLED, final. */
  cancelMilestone: (id: string, etag: string | null) =>
    apiRequest<ProjectMilestoneDetail>(`/project-milestones/${id}/cancel`, {
      method: 'POST',
      ifMatch: etag,
    }),
  /** Every revision of every milestone of a project, newest first. */
  achievements: (projectId: string, signal?: AbortSignal) =>
    readAllPages<MilestoneAchievementDetail>('/milestone-achievements', { projectId }, signal),
  /** One milestone's revisions, newest first. */
  revisions: (projectMilestoneId: string, signal?: AbortSignal) =>
    readAllPages<MilestoneAchievementDetail>(
      '/milestone-achievements',
      { projectMilestoneId },
      signal,
    ),
  /** One revision with the ETag its commands send back. */
  achievement: (id: string, signal?: AbortSignal) =>
    apiRequest<MilestoneAchievementDetail>(`/milestone-achievements/${id}`, { signal }),
  /** The next revision, DRAFT. 409 MILESTONE_ACHIEVEMENT_OPEN while one is DRAFT or SUBMITTED. */
  createAchievement: (request: MilestoneAchievementCreateRequest) =>
    apiRequest<MilestoneAchievementDetail>('/milestone-achievements', {
      method: 'POST',
      body: request,
    }),
  /** A DRAFT's claim, as a whole. */
  updateAchievement: (id: string, request: MilestoneAchievementRequest, etag: string | null) =>
    apiRequest<MilestoneAchievementDetail>(`/milestone-achievements/${id}`, {
      method: 'PUT',
      body: request,
      ifMatch: etag,
    }),
  /** Only a DRAFT; 204, also when it is already gone (R-40). Its evidence links end with it. */
  deleteAchievement: (id: string, etag: string | null) =>
    apiRequest<undefined>(`/milestone-achievements/${id}`, { method: 'DELETE', ifMatch: etag }),
  /** DRAFT → SUBMITTED (a WF-11 run), once the evidence EVIDENCE_POLICY makes mandatory is held. */
  submitAchievement: (id: string, etag: string | null) =>
    apiRequest<MilestoneAchievementDetail>(`/milestone-achievements/${id}/submit`, {
      method: 'POST',
      ifMatch: etag,
    }),
  /** The revision's evidence and the policy in force for its milestone's category. */
  evidence: (id: string, signal?: AbortSignal) =>
    apiRequest<MilestoneEvidenceDetail>(`/milestone-achievements/${id}/evidence`, { signal }),
  /**
   * Links the document and pins the version as evidence of a type, in one transaction. It changes the revision's
   * ETag, so the revision is read again after it.
   */
  attachEvidence: (id: string, request: MilestoneEvidenceRequest, etag: string | null) =>
    apiRequest<EvidenceReferenceDetail>(`/milestone-achievements/${id}/evidence`, {
      method: 'POST',
      body: request,
      ifMatch: etag,
    }),
  /** Withdraws a piece of evidence (it stays as history). It changes the revision's ETag. */
  withdrawEvidence: (id: string, evidenceReferenceId: string, etag: string | null) =>
    apiRequest<EvidenceReferenceDetail>(
      `/milestone-achievements/${id}/evidence/${evidenceReferenceId}/withdraw`,
      { method: 'POST', ifMatch: etag },
    ),
};
