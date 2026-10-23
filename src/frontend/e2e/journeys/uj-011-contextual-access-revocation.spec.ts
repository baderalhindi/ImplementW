import { type Download, type Page } from '@playwright/test';

import {
  accessRelationships,
  untilScanned,
  uploadDocument,
  type DocumentDetail,
} from '../support/access-audit.ts';
import { type PlatformApi } from '../support/api.ts';
import { credentialOf } from '../support/environment.ts';
import { expect, expectText, test, unique } from '../support/journey.ts';
import { activeProject } from '../support/projects.ts';
import { DATA_SET, departmentId } from '../support/reference.ts';

// UJ-011 — Contextual access and revocation (Step 18; FG-03; BPK-006; ICD-12).
// Procedure: open an authorized record; revoke the relevant context; retry the action, direct link, download and export
// with a denied account. Expected: backend contextual authorization denies the operation and prevents restricted disclosure
// after context changes. Negative extension: denied scope and a stale/replayed request fail without unauthorized mutation.
//
// The context is one the journey gives and takes away itself: the access administrator assigns the other department's
// manager the department-manager profile limited to this journey's project (MOD-081/082), and later ends exactly that
// assignment (ADM-003). The person's own assignment on their department is never touched. The person stays signed in with
// one session throughout, so every refusal comes from the grants read on the next request (authorization-engine.md D-2),
// not from a new sign-in. Gap G-3 (test-strategy.md §7): the operation, the direct link, the download and the export are
// retried together after one revocation.

const NOT_FOUND = 'The record was not found, or you do not have access to it.';
const PROJECT_NOT_FOUND = 'The project was not found, or you do not have access to it.';
const DOWNLOAD_DENIED =
  'You may no longer see some of what this file holds, so it cannot be downloaded.';

test('UJ-011 a revoked context denies the operation, direct link, download and export', async ({
  actor,
}) => {
  const department = await actor('departmentManager');
  const portfolio = await actor('portfolioManager');
  const manager = await actor('projectManager');
  const administrator = await actor('administrator');
  const person = await actor('otherDepartmentManager');

  const project = await activeProject({
    creator: department.api,
    reviewer: department.api,
    approver: portfolio.api,
    manager: manager.api,
  });
  const title = project.title.text;
  const fileContent = `UJ-011 site survey of ${title}`;
  const document = await uploadDocument(department.api, {
    projectId: project.id,
    title: unique('UJ-011 Site survey'),
    fileName: 'uj-011-site-survey.pdf',
    content: `%PDF-1.4\n${fileContent}\n%%EOF`,
  });
  const scanned = await untilScanned(department.api, document.id);
  expect(scanned.latestVersion?.scanState).toBe('CLEAN');
  const versionId = scanned.latestVersion?.id ?? '';
  const versions = async (): Promise<number[]> =>
    // SCR-124's list: every version, newest first, unpaged.
    (await department.api.get<{ versionNo: number }[]>(`/documents/${document.id}/versions`)).map(
      (v) => v.versionNo,
    );
  const baseAssignment = (await accessRelationships(administrator.api, person.api.userId)).find(
    (a) => a.status === 'ACTIVE' && a.projectId === null,
  );
  expect(baseAssignment).toBeDefined();
  // The department-manager profile version the person already holds on their own department (E2E-R03).
  const profileVersion = baseAssignment?.permissionProfileVersionId ?? '';

  const page = person.page;
  // A second tab of the same signed-in browser, holding the export.
  const reportPage = await page.context().newPage();

  await test.step('before any grant the project, the document and its content are out of reach', async () => {
    await page.goto(`/projects/${project.id}`);
    await expectText(page.getByRole('main'), PROJECT_NOT_FOUND);
    expect((await person.api.send('GET', `/documents/${document.id}`)).status).toBe(404);
  });

  await test.step('denied scope: someone who is not an access administrator cannot grant the context', async () => {
    const attempt = await department.api.send('POST', '/access-relationships', {
      userId: person.api.userId,
      permissionProfileVersionId: profileVersion,
      departmentId: project.departmentId,
      externalEntityId: null,
      projectId: project.id,
      sponsorUserId: null,
      startsAt: null,
      endsAt: null,
    });
    expect([attempt.status, attempt.code]).toEqual([403, 'PERMISSION_DENIED']);
    expect(await projectAssignments(administrator.api, person.api.userId, project.id)).toEqual([]);
  });

  await test.step('the access administrator assigns the department-manager profile limited to this project', async () => {
    const admin = administrator.page;
    await admin.goto(`/admin/users/${person.api.userId}`);
    await expect(
      admin.getByRole('heading', { level: 1, name: person.api.session.user.displayName }),
    ).toBeVisible();
    await admin.getByRole('button', { name: 'Assign role' }).click();
    const dialog = admin.getByRole('dialog', { name: 'Assign role' });
    await dialog
      .getByLabel('Role and permission profile')
      .selectOption({ label: 'R03 — E2E test authority — Sector Manager' });
    await dialog.getByLabel('Profile version').selectOption(profileVersion);
    await dialog.getByRole('button', { name: 'Next' }).click();
    await dialog
      .getByLabel('Sector', { exact: true })
      .selectOption({ label: 'E2E Journeys Department' });
    await dialog.getByLabel('Project ID').fill(project.id);
    await dialog.getByRole('button', { name: 'Assign role' }).click();
    await confirmWithSecondFactor(admin, 'The role was assigned.');
    await expect(assignmentRow(admin, project.id)).toContainText('Active');
  });

  const granted = await projectAssignments(administrator.api, person.api.userId, project.id);
  expect(granted.map((a) => [a.status, a.roleCode, a.departmentId])).toEqual([
    ['ACTIVE', 'R03', await departmentId(administrator.api, DATA_SET.department)],
  ]);
  const grantId = granted[0]?.id ?? '';

  try {
    await test.step('a replayed grant is refused: the same assignment is already held', async () => {
      await administrator.api.stepUp();
      const replay = await administrator.api.send('POST', '/access-relationships', {
        userId: person.api.userId,
        permissionProfileVersionId: profileVersion,
        departmentId: project.departmentId,
        externalEntityId: null,
        projectId: project.id,
        sponsorUserId: null,
        startsAt: null,
        endsAt: null,
      });
      expect([replay.status, replay.code]).toEqual([409, 'IDENTITY_ACCESS_ALREADY_ASSIGNED']);
      expect(
        (await projectAssignments(administrator.api, person.api.userId, project.id)).length,
      ).toBe(1);
    });

    await test.step('with the context, the same session opens the project by its direct link', async () => {
      await page.goto(`/projects/${project.id}`);
      await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible();
    });

    await test.step('…downloads the document and adds a version to it', async () => {
      await page.goto(`/documents/${document.id}`);
      await expect(
        page.getByRole('heading', { level: 1, name: document.title.text }),
      ).toBeVisible();
      const file = await downloadOf(page, () =>
        page
          .getByRole('region', { name: 'Latest version' })
          .getByRole('button', { name: 'Download' })
          .click(),
      );
      expect(await text(file)).toContain(fileContent);

      await addVersion(page, 'uj-011-site-survey-v2.pdf');
      const upload = page.getByRole('dialog', { name: 'Upload new version' });
      await expectText(upload, 'No malware found');
      await expect.poll(versions).toEqual([2, 1]);
      await page
        .getByRole('dialog', { name: 'Upload new version' })
        .getByRole('button', { name: 'Close' })
        .click();
    });

    await test.step('…and exports the Project Report and downloads the file', async () => {
      await reportPage.goto('/reports/project/PROJECT_REPORT');
      await reportPage.getByLabel('Project (required)').selectOption(project.id);
      await reportPage.getByRole('button', { name: 'Run report' }).click();
      await reportPage.getByRole('button', { name: 'Export' }).click();
      const dialog = reportPage.getByRole('dialog', { name: 'Export report' });
      await dialog.getByRole('radio', { name: 'CSV' }).check();
      await dialog.getByRole('button', { name: 'Start export' }).click();
      await expect(dialog.getByRole('status')).toContainText('Ready', { timeout: 90_000 });
      const file = await downloadOf(reportPage, () =>
        dialog.getByRole('button', { name: /^Download/ }).click(),
      );
      expect(await text(file)).toContain(title);
    });

    await test.step('the access administrator ends exactly that assignment', async () => {
      const admin = administrator.page;
      await admin.goto(`/admin/users/${person.api.userId}`);
      await assignmentRow(admin, project.id)
        .getByRole('button', { name: /^End assignment/ })
        .click();
      const dialog = admin.getByRole('dialog', { name: 'End assignment' });
      await expectText(dialog, 'The user will lose the R03 role from their next action.');
      await dialog.getByRole('button', { name: 'End assignment' }).click();
      await confirmWithSecondFactor(admin, 'The assignment was ended.');
      await expect(assignmentRow(admin, project.id)).toContainText(
        'Ended — ended by an administrator',
      );

      const after = await accessRelationships(administrator.api, person.api.userId);
      expect(after.find((a) => a.id === grantId)?.status).toBe('ENDED');
      expect(after.find((a) => a.id === grantId)?.endReason).toBe('MANUAL');
      // The person's own assignment on their department is untouched.
      expect(after.find((a) => a.id === baseAssignment?.id)?.status).toBe('ACTIVE');
    });

    await test.step('retried in the open pages: the download and the new version are refused', async () => {
      const download = page
        .getByRole('region', { name: 'Latest version' })
        .getByRole('button', { name: 'Download' });
      await download.click();
      await expectText(
        page.getByRole('region', { name: 'Latest version' }).getByRole('alert'),
        NOT_FOUND,
      );

      await addVersion(page, 'uj-011-site-survey-v3.pdf');
      await expectText(
        page.getByRole('dialog', { name: 'Upload new version' }).getByRole('alert'),
        NOT_FOUND,
      );
      expect(await versions()).toEqual([2, 1]);
    });

    await test.step('retried in the open export: the file it already made is refused, and no new one is made', async () => {
      const dialog = reportPage.getByRole('dialog', { name: 'Export report' });
      await dialog.getByRole('button', { name: /^Download/ }).click();
      await expectText(dialog.getByRole('alert'), DOWNLOAD_DENIED);

      const jobs = await person.api.get<{ items: { id: string; reportCode: string }[] }>(
        '/report-jobs?pageSize=200',
      );
      const job = jobs.items.find((j) => j.reportCode === 'PROJECT_REPORT');
      const content = await person.api.send('GET', `/report-jobs/${job?.id ?? ''}/content`);
      expect([content.status, content.code]).toEqual([403, 'PERMISSION_DENIED']);
      const again = await person.api.send('POST', '/reports/PROJECT_REPORT/export', {
        parameters: [{ code: 'PROJECT', value: project.id }],
        format: 'CSV',
        language: 'en',
      });
      expect([again.status, again.code]).toEqual([422, 'REPORT_PARAMETER_UNAUTHORIZED']);
      expect(
        (await person.api.get<{ items: unknown[] }>('/report-jobs?pageSize=200')).items.length,
      ).toBe(jobs.items.length);
    });

    await test.step('retried by direct link: the project and the document are not found, nor is the content', async () => {
      await page.goto(`/projects/${project.id}`);
      await expectText(page.getByRole('main'), PROJECT_NOT_FOUND);
      await page.goto(`/documents/${document.id}`);
      await expectText(page.getByRole('main'), NOT_FOUND);

      const read = await person.api.send('GET', `/documents/${document.id}`);
      expect([read.status, read.code]).toEqual([404, 'NOT_FOUND']);
      const content = await person.api.send(
        'GET',
        `/documents/${document.id}/versions/${versionId}/content`,
      );
      expect([content.status, content.code]).toEqual([404, 'NOT_FOUND']);
      const project404 = await person.api.send('GET', `/projects/${project.id}`);
      expect([project404.status, project404.code]).toEqual([404, 'NOT_FOUND']);
      // Nothing changed for the people who still hold the context.
      expect(await versions()).toEqual([2, 1]);
      expect(
        (await department.api.get<DocumentDetail>(`/documents/${document.id}`)).latestVersion
          ?.versionNo,
      ).toBe(2);
    });

    await test.step('a replayed revocation is refused and changes nothing', async () => {
      await administrator.api.stepUp();
      const replay = await administrator.api.send(
        'POST',
        `/access-relationships/${grantId}/end`,
        {},
      );
      expect([replay.status, replay.code]).toEqual([409, 'TERMINAL_STATE']);
      const after = await accessRelationships(administrator.api, person.api.userId);
      expect(after.find((a) => a.id === grantId)?.endReason).toBe('MANUAL');
      expect(after.find((a) => a.id === baseAssignment?.id)?.status).toBe('ACTIVE');
    });
  } finally {
    // Leaves no grant behind if a step above failed before the revocation.
    const left = await projectAssignments(administrator.api, person.api.userId, project.id);
    if (left.some((a) => a.id === grantId && a.status === 'ACTIVE')) {
      await administrator.api.stepUp();
      await administrator.api.send('POST', `/access-relationships/${grantId}/end`, {});
    }
  }
});

async function projectAssignments(
  administrator: PlatformApi,
  userId: string,
  projectId: string,
): Promise<Awaited<ReturnType<typeof accessRelationships>>> {
  return (await accessRelationships(administrator, userId)).filter(
    (a) => a.projectId === projectId,
  );
}

/** The assignment row of ADM-003 that names the project. */
function assignmentRow(page: Page, projectId: string) {
  return page.getByRole('row').filter({ hasText: projectId });
}

/**
 * Creating and ending an assignment are step-up operations (ADR-010, identity-access-administration.md D-11): when the
 * session's last second factor is older than the threshold the SPA asks for a fresh code and sends the request again.
 */
async function confirmWithSecondFactor(page: Page, done: string): Promise<void> {
  const stepUp = page.getByRole('dialog', { name: 'Confirm it is you' });
  const notice = page.getByRole('main').getByText(done);
  await expect(stepUp.or(notice).first()).toBeVisible();
  if (await stepUp.isVisible()) {
    await stepUp.getByLabel('Verification code').fill(credentialOf('administrator').mfaCode ?? '');
    await stepUp.getByRole('button', { name: 'Confirm' }).click();
  }
  await expect(notice).toBeVisible();
}

async function addVersion(page: Page, fileName: string): Promise<void> {
  await page.getByRole('button', { name: 'Upload new version' }).click();
  const dialog = page.getByRole('dialog', { name: 'Upload new version' });
  await dialog.getByLabel('File').setInputFiles({
    name: fileName,
    mimeType: 'application/pdf',
    buffer: Buffer.from(`%PDF-1.4\n${fileName}\n%%EOF`),
  });
  await dialog.getByRole('button', { name: 'Upload version' }).click();
}

async function downloadOf(page: Page, click: () => Promise<void>): Promise<Download> {
  const [download] = await Promise.all([page.waitForEvent('download'), click()]);
  return download;
}

async function text(download: Download): Promise<string> {
  const stream = await download.createReadStream();
  const chunks: Buffer[] = [];
  for await (const chunk of stream) chunks.push(chunk as Buffer);
  return Buffer.concat(chunks).toString('utf8');
}
