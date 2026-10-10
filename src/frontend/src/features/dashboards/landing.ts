import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

import { type DashboardCatalogueEntry, type DashboardCode } from './api/types.ts';

// Which dashboard the role-aware Home shows, and what it is called. The API decides the landing (`isDefaultLanding`,
// TASK-069 D-2) and every widget's data; this only names the rendering and picks among what the catalogue offered.
// This is navigation, not protection: a dashboard the caller may not open is 404 whatever the address asks for.

/** A role dashboard of Blueprint §21 (DSH-001 to DSH-008), the landing page Blueprint §20.2 gives one role. */
export interface RoleLanding {
  dashboardId: string;
  /** Blueprint §20.2's name for the landing page. */
  name: TranslationKey;
}

/** Blueprint §20.2, role for role: the landing pages, each the role dashboard of the same audience (§21). */
export const ROLE_LANDINGS: Readonly<Record<string, RoleLanding>> = {
  R01: { dashboardId: 'DSH-001', name: 'dashboards.landing.R01' },
  R02: { dashboardId: 'DSH-002', name: 'dashboards.landing.R02' },
  R03: { dashboardId: 'DSH-003', name: 'dashboards.landing.R03' },
  R04: { dashboardId: 'DSH-004', name: 'dashboards.landing.R04' },
  R05: { dashboardId: 'DSH-005', name: 'dashboards.landing.R05' },
  R06: { dashboardId: 'DSH-006', name: 'dashboards.landing.R06' },
  R07: { dashboardId: 'DSH-007', name: 'dashboards.landing.R07' },
  R08: { dashboardId: 'DSH-008', name: 'dashboards.landing.R08' },
};

/**
 * The role the caller lands as: with several roles, the first in code order that has a landing page — the API's own
 * rule for choosing the landing dashboard (TBC-DSH-003, `DashboardService.ListAsync`), so the name matches the
 * dashboard it chose.
 */
export function landingRole(user: SessionUser): string | null {
  const codes = [...new Set(user.roleAssignments.map((assignment) => assignment.roleCode))]
    .filter((code) => code in ROLE_LANDINGS)
    .sort();
  return codes[0] ?? null;
}

/**
 * The dashboards the Home hosts. The Project Dashboard is not one of them: DSH-009 renders only inside SCR-040
 * Project Overview (Blueprint §21.1, no duplicate page), so it is opened from a project, never from here.
 */
export function hostedEntries(entries: DashboardCatalogueEntry[]): DashboardCatalogueEntry[] {
  return entries.filter((entry) => entry.contextKind === 'PORTFOLIO');
}

/** The entry the caller lands on; a catalogue without one lands on the first dashboard the Home hosts. */
export function landingEntry(entries: DashboardCatalogueEntry[]): DashboardCatalogueEntry | null {
  return entries.find((entry) => entry.isDefaultLanding) ?? hostedEntries(entries)[0] ?? null;
}

/**
 * The entry the Home shows: the hosted dashboard the address asks for (`?dashboard=GOVERNANCE`), else the landing.
 * A code the catalogue does not offer, or the Project Dashboard, is ignored.
 */
export function selectedEntry(
  entries: DashboardCatalogueEntry[],
  requested: string | null,
): DashboardCatalogueEntry | null {
  return hostedEntries(entries).find((entry) => entry.code === requested) ?? landingEntry(entries);
}

/** ADR-019's `LAYOUT_PERSONALIZE` ships to R02, R03 and R07 (OWN, `authorization-engine.md` D-9). */
const PERSONALIZING_ROLES = new Set(['R02', 'R03', 'R07']);

/**
 * Whether the Home offers "Customise layout" on a dashboard: only one the API says is personalisable (the Portfolio
 * Dashboard, by its CHECK) and only to a holder of the permission. The Project Dashboard never is (ADR-019).
 */
export function mayPersonalize(
  user: SessionUser,
  entry: { allowsPersonalization: boolean; code: DashboardCode },
): boolean {
  return (
    entry.allowsPersonalization &&
    entry.code !== 'PROJECT' &&
    user.roleAssignments.some((assignment) => PERSONALIZING_ROLES.has(assignment.roleCode))
  );
}
