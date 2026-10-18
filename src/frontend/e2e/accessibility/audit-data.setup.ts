import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';

import { test } from '@playwright/test';

import { uploadDocument, untilScanned } from '../support/access-audit.ts';
import { PlatformApi } from '../support/api.ts';
import { unique, utcDate } from '../support/journey.ts';
import { activeProject, registration } from '../support/projects.ts';
import { itemId } from '../support/reference.ts';
import { AUDIT_DATA_FILE, type AuditData } from './screens.ts';

// The audit's data (docs/testing/accessibility-audit.md §2): one ACTIVE project with a record in every register the screen set
// shows, and a second project waiting in AHDA's review, so the registers, the workspace and the approval inbox are audited
// with rows, not as empty states. Run once before the screens (the `audit-data` project of accessibility.config.ts); the ids
// go to AUDIT_DATA_FILE for the screens to open.

test('audit data', async () => {
  test.setTimeout(10 * 60 * 1000);
  const department = await PlatformApi.signIn('departmentManager');
  const portfolio = await PlatformApi.signIn('portfolioManager');
  const manager = await PlatformApi.signIn('projectManager');
  const administrator = await PlatformApi.signIn('administrator');
  try {
    const project = await activeProject(
      { creator: department, reviewer: department, approver: portfolio, manager },
      { title: unique('A11Y Riyadh metro depot') },
    );
    await manager.post('/risks', {
      projectId: project.id,
      title: { text: 'Flooding of the depot pit', language: 'en' },
      description: { text: 'Heavy rain may flood the excavation.', language: 'en' },
      riskCategoryItemId: await itemId(manager, 'RISK_CATEGORY', 'E2E_WEATHER'),
      ownerUserId: manager.userId,
      identifiedDate: utcDate(0),
      nextReviewDate: utcDate(14),
    });
    await manager.post('/management-concerns', {
      projectId: project.id,
      concernType: 'ISSUE',
      title: { text: 'Access road closed', language: 'en' },
      description: { text: 'The access road is closed for repairs.', language: 'en' },
      categoryItemId: await itemId(portfolio, 'CONCERN_CATEGORY', 'E2E_SITE_ACCESS'),
      priorityItemId: await itemId(portfolio, 'PRIORITY', 'E2E_HIGH'),
      impacts: [],
      targetResolutionDate: null,
    });
    await manager.post('/change-requests', {
      projectId: project.id,
      changeType: 'SCHEDULE',
      title: { text: 'Five more days of earthworks', language: 'en' },
      justification: { text: 'Ground conditions at the northern cut.', language: 'en' },
      costImpactSar: null,
      scheduleImpactDays: 5,
      scopeImpact: null,
      isContractualObligation: false,
      requestedGovernanceProfileItemId: null,
    });
    const document = await uploadDocument(department, {
      projectId: project.id,
      title: unique('A11Y Site survey'),
      fileName: 'a11y-site-survey.pdf',
      content: '%PDF-1.4\nTASK-087 accessibility audit\n%%EOF',
    });
    await untilScanned(department, document.id);
    const waiting = await department.post<{ id: string }>(
      '/projects',
      await registration(portfolio, { title: unique('A11Y Jeddah corniche') }),
    );
    await department.post(`/projects/${waiting.id}/submit`, {
      projectManagerUserId: manager.userId,
    });
    await department.post(`/projects/${waiting.id}/start-review`);
    const roles = await administrator.get<{ id: string }[]>('/roles'); // a bare array, the catalogue of roles
    const data: AuditData = {
      projectId: project.id,
      userId: manager.userId,
      roleId: roles[0]?.id ?? '',
    };
    mkdirSync(dirname(AUDIT_DATA_FILE), { recursive: true });
    writeFileSync(AUDIT_DATA_FILE, JSON.stringify(data, null, 2));
  } finally {
    await Promise.all([department, portfolio, manager, administrator].map((api) => api.dispose()));
  }
});
