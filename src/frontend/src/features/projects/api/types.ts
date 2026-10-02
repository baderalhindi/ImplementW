// The WF-01 representations (TASK-041, project-registration.md §4) as the API serialises them: enums in
// SNAKE_CASE_UPPER, ids as strings, dates as ISO 8601, money as R-16 decimal strings.

/** TASK-041 owns DRAFT to ACTIVE; SUSPENDED, COMPLETED and CLOSED are TASK-062's and TASK-063's. */
export type ProjectStatus =
  | 'DRAFT'
  | 'SUBMITTED'
  | 'UNDER_REVIEW'
  | 'RETURNED'
  | 'APPROVED_PLANNED'
  | 'ACTIVE'
  | 'SUSPENDED'
  | 'COMPLETED'
  | 'CLOSED';

/** ADR-013: who delivers the project. */
export type ParticipationMode = 'ENTITY_MANAGED' | 'AHDA_MANAGED';

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

/** A row of SCR-025 / SCR-026. */
export interface ProjectSummary {
  id: string;
  /** Null until AHDA approves the registration (ADR-013). */
  formalProjectId: string | null;
  title: NarrativeText;
  classificationItemId: string;
  departmentId: string;
  externalEntityId: string | null;
  projectManagerUserId: string | null;
  status: ProjectStatus;
  participationMode: ParticipationMode;
  /** Set only on a project that entered by the legacy intake path (ADR-014, TASK-104). */
  legacyIntakeDate: string | null;
  updatedAt: string;
}

/** The project the workspace (SCR-040–043) shows, with the ETag every change sends back. */
export interface ProjectDetail extends ProjectSummary {
  description: NarrativeText | null;
  revisionNo: number;
  governanceProfileItemId: string;
  registrationBudgetSar: string | null;
  plannedStartDate: string | null;
  plannedEndDate: string | null;
  regionItemId: string | null;
  cityItemId: string | null;
  latitude: number | null;
  longitude: number | null;
  activatedAt: string | null;
  createdAt: string;
  createdBy: string;
  updatedBy: string;
}

/** The whole registration (R-5): what SCR-033 creates and SCR-034 / SCR-035 replace. No Formal Project ID. */
export interface ProjectRequest {
  title: NarrativeTextRequest;
  description: NarrativeTextRequest | null;
  classificationItemId: string;
  departmentId: string;
  externalEntityId: string | null;
  participationMode: ParticipationMode;
  governanceProfileItemId: string;
  registrationBudgetSar: string | null;
  plannedStartDate: string | null;
  plannedEndDate: string | null;
  regionItemId: string | null;
  cityItemId: string | null;
  latitude: number | null;
  longitude: number | null;
}

export interface ProjectQuery {
  /** A set, comma-separated (R-31). */
  status?: string | undefined;
  departmentId?: string | undefined;
  externalEntityId?: string | undefined;
  /** SCR-026 My Projects. */
  projectManagerUserId?: string | undefined;
  q?: string | undefined;
  page?: number | undefined;
  pageSize?: number | undefined;
}

/** ADR-015 as published in GOVERNANCE_PROFILE (TASK-034): what a profile asks of a project. */
export interface GovernanceProfileSettings {
  governanceProfileItemId: string;
  assignmentMinBudgetSar: number | null;
  assignmentMinDurationDays: number | null;
  mandatoryFieldCodes: string[];
}

export interface GovernanceProfileResolution {
  versionId: string;
  content: { governanceProfiles: GovernanceProfileSettings[] };
}
