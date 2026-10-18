import { resolve } from 'node:path';

import { type Page } from '@playwright/test';

import { credentialOf, type Persona } from '../support/environment.ts';

// The audited screen set (docs/testing/accessibility-audit.md §2). Traffic ranks 1–10 are the ten screens TASK-086 found the
// SPA's navigation leads to most (load-test.md §4.2), ranked by the index Σ(people active at peak × visits per 100) over its
// persona groups, and each opened as the group contributing most to that index; sign-in is
// every session's first screen; the rest are the screens of the two tasks TASK-087 depends on (FG-03 administration, WF-01
// registration). A screen is opened with the data the audit's fixture created (`AuditData`), so registers show rows.

/** Where audit-data.setup.ts leaves the ids of the records it created, beside the run's records. */
export const AUDIT_DATA_FILE = resolve(
  import.meta.dirname,
  '../../test-results/accessibility/audit-data.json',
);

export interface AuditData {
  projectId: string;
  userId: string;
  roleId: string;
}

export interface Screen {
  id: string;
  name: string;
  /** Step 16 screen id(s) (02_Navigation_Routes). */
  scr: string;
  /** TASK-086 traffic rank (1 = most visited), or why the screen is in the set. */
  basis: string;
  /** Counts toward "the 10 highest-traffic screens" of the keyboard criterion. */
  topTen: boolean;
  persona: Persona | null;
  path: (data: AuditData) => string;
  /**
   * A state reached by acting on the screen (FE-AX-001/002): a dialog, a second step, a form's errors. Selectors are
   * language-neutral, so the same steps run in both locales.
   */
  state?: {
    enter: (page: Page) => Promise<void>;
    /** A modal: the Tab walk must stay inside it, and Escape must close it and give focus back to its opener. */
    modal?: boolean;
  };
}

/** Submits a form empty: the FG-03 forms validate on submit (noValidate) and show their error summary and inline errors. */
async function submitEmpty(page: Page): Promise<void> {
  await page.locator('form button[type="submit"]').click();
  await page.locator('[aria-invalid="true"]').first().waitFor();
}

export const SCREENS: Screen[] = [
  {
    id: 'A11Y-S01',
    name: 'Sign in',
    scr: 'SCR-001',
    basis: 'every session starts here',
    topTen: false,
    persona: null,
    path: () => '/sign-in',
  },
  {
    id: 'A11Y-S02',
    name: 'Home dashboard',
    scr: 'DSH-002 (Home)',
    basis: 'traffic rank 1 (index 1,360)',
    topTen: true,
    persona: 'portfolioManager',
    path: () => '/',
  },
  {
    id: 'A11Y-S03',
    name: 'Project register',
    scr: 'SCR-025',
    basis: 'traffic rank 3 (index 730, with My projects)',
    topTen: true,
    persona: 'portfolioManager',
    path: () => '/projects',
  },
  {
    id: 'A11Y-S04',
    name: 'Project workspace',
    scr: 'SCR-040, DSH-009',
    basis: 'traffic rank 2 (index 935)',
    topTen: true,
    persona: 'projectManager',
    path: (d) => `/projects/${d.projectId}`,
  },
  {
    id: 'A11Y-S05',
    name: 'Risk register',
    scr: 'SCR-080',
    basis: 'traffic rank 4 (index 390)',
    topTen: true,
    persona: 'projectManager',
    path: () => '/risks',
  },
  {
    id: 'A11Y-S06',
    name: 'Issue register',
    scr: 'SCR-083',
    basis: 'traffic rank 7= (index 305)',
    topTen: true,
    persona: 'projectManager',
    path: () => '/issues-challenges',
  },
  {
    id: 'A11Y-S07',
    name: 'Approval inbox',
    scr: 'SCR-100',
    basis: 'traffic rank 9 (index 150)',
    topTen: true,
    persona: 'portfolioManager',
    path: () => '/approvals/inbox',
  },
  {
    id: 'A11Y-S08',
    name: 'Document library',
    scr: 'SCR-120',
    basis:
      'traffic rank 7= (index 305); as a Department Manager, since the e2e Project Manager profile reads no documents and would see only the empty state',
    topTen: true,
    persona: 'departmentManager',
    path: () => '/documents',
  },
  {
    id: 'A11Y-S09',
    name: 'Notifications',
    scr: 'SCR-150',
    basis: 'traffic rank 5 (index 345)',
    topTen: true,
    persona: 'projectManager',
    path: () => '/notifications',
  },
  {
    id: 'A11Y-S10',
    name: 'Report run',
    scr: 'SCR-133',
    basis: 'traffic rank 6 (index 340)',
    topTen: true,
    persona: 'departmentManager',
    path: () => '/reports/department/PROJECT_REGISTER',
  },
  {
    id: 'A11Y-S11',
    name: 'Change requests',
    scr: 'SCR-105',
    basis: 'traffic rank 10 (index 140)',
    topTen: true,
    persona: 'projectManager',
    path: () => '/change-requests',
  },
  {
    id: 'A11Y-S12',
    name: 'Reports centre',
    scr: 'SCR-130',
    basis: 'entry to rank 6',
    topTen: false,
    persona: 'executive',
    path: () => '/reports',
  },
  {
    id: 'A11Y-S13',
    name: 'My projects',
    scr: 'SCR-026',
    basis: 'traffic rank 3, as Project Managers open it',
    topTen: false,
    persona: 'projectManager',
    path: () => '/projects/mine',
  },
  {
    id: 'A11Y-S14',
    name: 'Register project (form)',
    scr: 'SCR-033',
    basis: 'WF-01 dependency',
    topTen: false,
    persona: 'departmentManager',
    path: () => '/projects/new',
  },
  {
    id: 'A11Y-S15',
    name: 'Users',
    scr: 'ADM-002',
    basis: 'FG-03 dependency',
    topTen: false,
    persona: 'administrator',
    path: () => '/admin/users',
  },
  {
    id: 'A11Y-S16',
    name: 'User detail',
    scr: 'ADM-003',
    basis: 'FG-03 dependency',
    topTen: false,
    persona: 'administrator',
    path: (d) => `/admin/users/${d.userId}`,
  },
  {
    id: 'A11Y-S17',
    name: 'Create user (form)',
    scr: 'ADM-004',
    basis: 'FG-03 dependency',
    topTen: false,
    persona: 'administrator',
    path: () => '/admin/users/new',
  },
  {
    id: 'A11Y-S18',
    name: 'Roles',
    scr: 'ADM-006',
    basis: 'FG-03 dependency',
    topTen: false,
    persona: 'administrator',
    path: () => '/admin/roles',
  },
  {
    id: 'A11Y-S19',
    name: 'Role detail',
    scr: 'ADM-007',
    basis: 'FG-03 dependency',
    topTen: false,
    persona: 'administrator',
    path: (d) => `/admin/roles/${d.roleId}`,
  },
  {
    id: 'A11Y-S20',
    name: 'Permission matrix',
    scr: 'ADM-009',
    basis: 'FG-03 dependency',
    topTen: false,
    persona: 'administrator',
    path: () => '/admin/permission-matrix',
  },
  {
    id: 'A11Y-S21',
    name: 'Role assignments',
    scr: 'ADM-010',
    basis: 'FG-03 dependency',
    topTen: false,
    persona: 'administrator',
    path: () => '/admin/role-assignments',
  },
  {
    id: 'A11Y-S22',
    name: 'Sign in — verification step',
    scr: 'SCR-002',
    basis: 'second step of every privileged sign-in',
    topTen: false,
    persona: null,
    path: () => '/sign-in',
    state: {
      enter: async (page) => {
        const credential = credentialOf('administrator');
        await page.locator('input[name="username"]').fill(credential.username);
        await page.locator('input[name="password"]').fill(credential.password);
        await page.locator('form button[type="submit"]').click();
        await page.locator('input[name="code"]').waitFor();
      },
    },
  },
  {
    id: 'A11Y-S23',
    name: 'Approval inbox — decision dialog',
    scr: 'SCR-100, MOD-040',
    basis: 'state of rank 6',
    topTen: false,
    persona: 'portfolioManager',
    path: () => '/approvals/inbox',
    state: {
      modal: true,
      enter: async (page) => {
        await page.locator('tbody tr').first().locator('.actions-group button').first().click();
        await page.locator('dialog[open]').waitFor();
      },
    },
  },
  {
    id: 'A11Y-S24',
    name: 'Register project — validation errors',
    scr: 'SCR-033',
    basis: 'state of the WF-01 form',
    topTen: false,
    persona: 'departmentManager',
    path: () => '/projects/new',
    // The WF-01 form keeps its submit disabled while a required field is empty and flags a malformed value as it is typed.
    state: {
      enter: async (page) => {
        await page.locator('input[name="registrationBudgetSar"]').fill('12,5 million');
        await page.locator('input[name="plannedStartDate"]').fill('2027-06-01');
        await page.locator('input[name="plannedEndDate"]').fill('2027-01-01');
        await page.locator('[aria-invalid="true"]').nth(1).waitFor();
      },
    },
  },
  {
    id: 'A11Y-S25',
    name: 'Create user — validation errors',
    scr: 'ADM-004',
    basis: 'state of the FG-03 form',
    topTen: false,
    persona: 'administrator',
    path: () => '/admin/users/new',
    state: { enter: submitEmpty },
  },
];
