import { type Locator, type Page } from '@playwright/test';

import { type PlatformApi } from './api.ts';
import { expect } from './journey.ts';

// What the governance journeys (UJ-004, UJ-007) share: WF-08's records as the API returns them, and the confirmation dialog
// SCR-107 opens for a command without input (ConfirmCommandDialog: its title and its confirm button are the command's name).

export interface ChangeAuthorization {
  id: string;
  authorizationScope: string;
  targetId: string;
  targetRevisionNo: number;
  status: 'ISSUED' | 'APPLIED' | 'EXPIRED' | 'REVOKED';
  appliedAt: string | null;
  appliedByUserId: string | null;
  appliedReference: string | null;
}

export interface ChangeRequestDetail {
  id: string;
  status: string;
  revisionNo: number;
  authorizations: ChangeAuthorization[];
}

export interface BaselineDetail {
  id: string;
  versionNo: number;
  revisionNo: number;
  status: string;
  changeAuthorizationId: string | null;
  baselineFinishDate: string | null;
}

export function changeRequest(api: PlatformApi, id: string): Promise<ChangeRequestDetail> {
  return api.get<ChangeRequestDetail>(`/change-requests/${id}`);
}

/** The project's baselines as "<version> <status>", oldest first. */
export async function baselineStates(api: PlatformApi, projectId: string): Promise<string[]> {
  const page = await api.get<{ items: BaselineDetail[] }>(
    `/project-baselines?projectId=${projectId}&pageSize=50`,
  );
  return page.items
    .map((b) => ({ version: b.versionNo, status: b.status }))
    .sort((a, b) => a.version - b.version)
    .map((b) => `${String(b.version)} ${b.status}`);
}

/** Opens a command of SCR-107 and confirms it in its dialog. */
export async function confirmCommand(page: Page, command: string): Promise<void> {
  await page
    .getByRole('group', { name: 'Change request actions' })
    .getByRole('button', { name: command, exact: true })
    .click();
  const dialog = page.getByRole('dialog', { name: command });
  await dialog.getByRole('button', { name: command, exact: true }).click();
  await expect(dialog).toBeHidden();
}

/** The value a details list (dt/dd) shows for a term, e.g. SCR-107's "Approval" or SCR-115's outcome row. */
export function definition(scope: Page | Locator, term: string): Locator {
  return scope
    .locator('dt')
    .filter({ hasText: new RegExp(`^${term}$`) })
    .locator('xpath=following-sibling::dd[1]');
}
