// The FG-03 API's representations (identity-access-administration.md §3), as the API serialises them: camelCase
// properties, UPPER_SNAKE_CASE enumerations (api-conventions R-19), ISO 8601 date-times.

export type UserType = 'INTERNAL' | 'EXTERNAL' | 'SERVICE';
export type UserStatus = 'ACTIVE' | 'DISABLED';
export type DataScope = 'ALL' | 'DEPT' | 'OWN' | 'ASSIGNED' | 'ENTITY' | 'READ_ONLY';
export type AccessEndReason = 'PROJECT_CLOSED' | 'ROLE_CHANGE' | 'MIGRATED' | 'MANUAL' | 'EXPIRED';
export type AccessRelationshipStatus = 'ACTIVE' | 'ENDED';
export type ExternalEntityStatus = 'ACTIVE' | 'SUSPENDED' | 'RETIRED';
export type GovernedLifecycleState = 'DRAFT' | 'VALIDATED' | 'PUBLISHED' | 'RETIRED';
export type LanguageCode = 'ar' | 'en';
export type UserSort = 'displayName:asc' | 'displayName:desc' | 'username:asc' | 'username:desc';

export interface BilingualLabel {
  ar: string;
  en: string;
}

export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface UserSummary {
  id: string;
  userType: UserType;
  username: string;
  displayName: string;
  email: string;
  status: UserStatus;
  departmentId: string | null;
  externalEntityId: string | null;
}

export interface UserDetail extends UserSummary {
  directorySubjectId: string | null;
  mobileNumber: string | null;
  mobileVerifiedAt: string | null;
  jobTitle: string | null;
  managerUserId: string | null;
  preferredLanguage: LanguageCode;
  disabledAt: string | null;
  multiFactorEnrolled: boolean;
  nafathVerifiedAt: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
}

export interface UserQuery {
  status?: UserStatus | undefined;
  userType?: UserType | undefined;
  departmentId?: string | undefined;
  externalEntityId?: string | undefined;
  q?: string | undefined;
  sort?: UserSort | undefined;
  page?: number | undefined;
  pageSize?: number | undefined;
}

export interface UserCreateRequest {
  userType: UserType;
  username: string;
  displayName: string;
  email: string;
  mobileNumber: string | null;
  preferredLanguage: LanguageCode;
  directorySubjectId: string | null;
  jobTitle: string | null;
  externalEntityId: string | null;
}

export type UserUpdateRequest = Omit<UserCreateRequest, 'userType' | 'externalEntityId'>;

export interface RoleSummary {
  id: string;
  code: string;
  name: BilingualLabel;
  isSystem: boolean;
  isExternalEligible: boolean;
}

export interface PermissionProfileSummary {
  id: string;
  code: string;
  name: BilingualLabel;
  baseRoleId: string;
  baseRoleCode: string;
  isShippedDefault: boolean;
}

export interface RoleDetail extends RoleSummary {
  profiles: PermissionProfileSummary[];
  updatedAt: string;
  updatedBy: string;
}

export interface PermissionSummary {
  id: string;
  code: string;
  name: BilingualLabel;
  permissionGroup: string;
  isPrivileged: boolean;
  dataClassificationItemId: string | null;
}

export interface PermissionProfileGrant {
  permissionId: string;
  permissionCode: string;
  dataScope: DataScope;
}

export interface PermissionProfileVersion {
  id: string;
  versionNo: number;
  lifecycleState: GovernedLifecycleState;
  publishedAt: string | null;
  retiredAt: string | null;
  grants: PermissionProfileGrant[];
}

export interface PermissionProfileDetail {
  id: string;
  code: string;
  name: BilingualLabel;
  baseRoleId: string;
  baseRoleCode: string;
  isShippedDefault: boolean;
  /** Newest first. */
  versions: PermissionProfileVersion[];
}

export interface AccessRelationshipSummary {
  id: string;
  userId: string;
  roleCode: string;
  permissionProfileVersionId: string;
  departmentId: string | null;
  externalEntityId: string | null;
  projectId: string | null;
  sponsorUserId: string | null;
  startsAt: string;
  endsAt: string | null;
  endReason: AccessEndReason | null;
  status: AccessRelationshipStatus;
}

export interface AccessRelationshipDetail extends AccessRelationshipSummary {
  permissionProfileId: string;
  permissionProfileCode: string;
  permissionProfileVersionNo: number;
}

export interface AccessRelationshipQuery {
  userId?: string | undefined;
  projectId?: string | undefined;
  status?: AccessRelationshipStatus | undefined;
  page?: number | undefined;
  pageSize?: number | undefined;
}

export interface AccessRelationshipCreateRequest {
  userId: string;
  permissionProfileVersionId: string;
  departmentId: string | null;
  externalEntityId: string | null;
  projectId: string | null;
  sponsorUserId: string | null;
  startsAt: string | null;
  endsAt: string | null;
}

export interface DepartmentSummary {
  id: string;
  code: string;
  name: BilingualLabel;
  parentDepartmentId: string | null;
  isActive: boolean;
}

export interface DepartmentDetail extends DepartmentSummary {
  directoryReference: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface DepartmentQuery {
  isActive?: boolean | undefined;
  parentDepartmentId?: string | undefined;
  q?: string | undefined;
  page?: number | undefined;
  pageSize?: number | undefined;
}

export interface DepartmentCreateRequest {
  code: string;
  name: BilingualLabel;
  parentDepartmentId: string | null;
  directoryReference: string | null;
}

export type DepartmentUpdateRequest = Omit<DepartmentCreateRequest, 'code'>;

export interface ExternalEntitySummary {
  id: string;
  code: string;
  name: BilingualLabel;
  entityTypeItemId: string;
  status: ExternalEntityStatus;
  sponsorUserId: string | null;
}

export interface ExternalEntityDetail extends ExternalEntitySummary {
  createdAt: string;
  updatedAt: string;
}

export interface ExternalEntityQuery {
  status?: ExternalEntityStatus | undefined;
  q?: string | undefined;
  page?: number | undefined;
  pageSize?: number | undefined;
}

export interface ExternalEntityCreateRequest {
  code: string;
  name: BilingualLabel;
  entityTypeItemId: string;
  sponsorUserId: string | null;
}

export type ExternalEntityUpdateRequest = Omit<ExternalEntityCreateRequest, 'code'>;
