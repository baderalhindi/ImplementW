import { type Locator, type Page } from '@playwright/test';

import { eventually, type PlatformApi } from '../support/api.ts';
import { approveInTheInbox, download, runOn } from '../support/execution.ts';
import { activeProject } from '../support/projects.ts';
import { itemId } from '../support/reference.ts';
import { expect, expectText, test, unique, utcDate } from '../support/journey.ts';

// UJ-006 — Exact evidence version (Step 18; WF-12; BPK-010; ICD-08).
// Participants: the source submitter (the Project Manager claiming a milestone achievement in WF-05, with its evidence filed in
// WF-12) and the evidence reviewer (AHDA's portfolio manager, deciding the claim's WF-11 run).
// Procedure: attach a CLEAN document version to a submission; add a newer version; reopen submitted evidence and attempt a
// non-CLEAN attachment.
// Expected: submitted/approved evidence retains the exact immutable CLEAN version; later uploads cannot replace its identity.
// Negative extension: denied scope and a stale/replayed request fail without unauthorized mutation.

interface EvidencePiece {
  id: string;
  documentId: string;
  documentVersionId: string;
  versionNo: number;
  status: string;
  scanState: string;
  satisfies: boolean;
}

interface Evidence {
  links: { evidence: EvidencePiece[] }[];
}

interface DocumentDetail {
  id: string;
  latestVersion: { id: string; versionNo: number; scanState: string };
}

// The scanner stand-in decides by file name (stack/standins/standins.mjs): "held" stays SCAN_PENDING, "infected" is
// QUARANTINED, anything else is CLEAN.
const SIGNED = Buffer.from(`UJ-006 completion certificate, signed ${new Date().toISOString()}\n`);
const AMENDED = Buffer.from(`UJ-006 completion certificate, amended ${new Date().toISOString()}\n`);
const PENDING = Buffer.from(`UJ-006 completion certificate, still being scanned\n`);
const MALWARE = Buffer.from(`UJ-006 completion certificate, flagged by the scanner\n`);

test('UJ-006 submitted evidence keeps its exact immutable CLEAN version', async ({ actor }) => {
  const department = await actor('departmentManager');
  const reviewer = await actor('portfolioManager');
  const submitter = await actor('projectManager');
  const crew = {
    creator: department.api,
    reviewer: department.api,
    approver: reviewer.api,
    manager: submitter.api,
  };
  const project = await activeProject(crew, { title: unique('UJ-006 Water treatment plant') });
  const title = unique('Plant commissioned');
  const documentTitle = unique('Commissioning certificate');
  const milestone = await submitter.api.post<{ id: string }>('/project-milestones', {
    projectId: project.id,
    scheduleActivityId: null,
    title: { text: title, language: 'en' },
    milestoneCategoryItemId: await itemId(submitter.api, 'MILESTONE_CATEGORY', 'E2E_DELIVERY'),
    forecastDate: utcDate(30),
    sortOrder: 0,
  });
  const page = submitter.page;
  const drawer = page.getByRole('dialog', { name: title });
  const claims = () =>
    submitter.api.get<{ items: { id: string; revisionNo: number; status: string }[] }>(
      `/milestone-achievements?projectMilestoneId=${milestone.id}`,
    );
  const evidenceOf = (api: PlatformApi, claimId: string) =>
    api
      .get<Evidence>(`/milestone-achievements/${claimId}/evidence`)
      .then((e) => e.links.flatMap((l) => l.evidence));

  await test.step('the source submitter opens a claim of the achievement as a draft', async () => {
    await openMilestone(page, project.id, title);
    await drawer.getByRole('button', { name: 'Claim the achievement' }).click();
    await drawer.getByLabel('Date achieved').fill(utcDate(0));
    await drawer.getByLabel('What was achieved').fill('Plant commissioned and handed over.');
    await drawer.getByRole('button', { name: 'Save as draft' }).click();
    await expectText(drawer, 'The claim was saved as a draft.');
  });

  const claimId = (await claims()).items[0]?.id ?? '';
  expect(claimId).not.toBe('');

  await test.step('the submitter uploads the signed certificate; the scan finds it clean', async () => {
    await drawer.getByRole('button', { name: 'Upload a document' }).click();
    const upload = page.getByRole('dialog', { name: 'Upload document' });
    await upload.getByLabel('File').setInputFiles({
      name: 'uj-006-certificate-signed.txt',
      mimeType: 'text/plain',
      buffer: SIGNED,
    });
    await upload.getByLabel('Title').fill(documentTitle);
    await upload.getByLabel('Document type').selectOption({ label: 'Report' });
    await upload.getByLabel('Classification').selectOption({ index: 1 });
    await upload.getByRole('button', { name: 'Upload', exact: true }).click();
    await expectText(upload, 'No malware found');
    await upload.getByRole('button', { name: 'Close' }).click();
  });

  const documentId = await eventually(
    () =>
      submitter.api.get<{ items: { id: string; title: { text: string } }[] }>(
        `/documents?projectId=${project.id}&pageSize=100`,
      ),
    (documents) => documents.items.some((d) => d.title.text === documentTitle),
    `the document "${documentTitle}"`,
  ).then((documents) => documents.items.find((d) => d.title.text === documentTitle)?.id ?? '');
  const signed = (await submitter.api.get<DocumentDetail>(`/documents/${documentId}`))
    .latestVersion;
  expect([signed.versionNo, signed.scanState]).toEqual([1, 'CLEAN']);

  await test.step("the submitter attaches the CLEAN version 1 as the claim's completion certificate", async () => {
    await openMilestone(page, project.id, title);
    await attach(page, drawer, documentTitle);
    await expectText(drawer, 'Evidence of: Completion certificate · version 1');
    await expectText(drawer, 'Counts as evidence');
    expect(
      (await evidenceOf(submitter.api, claimId)).map((e) => [
        e.documentVersionId,
        e.scanState,
        e.satisfies,
      ]),
    ).toEqual([[signed.id, 'CLEAN', true]]);
  });

  await test.step('the submitter submits the claim with its evidence', async () => {
    await drawer.getByRole('button', { name: 'Submit for review' }).click();
    await expectText(drawer, 'The claim was submitted for review.');
    await drawer.getByRole('button', { name: 'Close' }).click();
  });

  const pinned = (await evidenceOf(submitter.api, claimId))[0];
  const pinnedId = pinned?.id ?? '';

  await test.step('a replayed submission is refused and opens no second review', async () => {
    const replay = await submitter.api.send(
      'POST',
      `/milestone-achievements/${claimId}/submit`,
      {},
    );
    expect([replay.status, replay.code]).toEqual([409, 'INVALID_TRANSITION']);
    const runs = await reviewer.api.get<{ items: unknown[] }>(
      `/approval-instances?subjectModule=Milestone&subjectType=MilestoneAchievement&subjectId=${claimId}&pageSize=50`,
    );
    expect(runs.items).toHaveLength(1);
  });

  await test.step('the submitter uploads an amended certificate as version 2 of the same document', async () => {
    await page.goto(`/documents/${documentId}`);
    await page.getByRole('button', { name: 'Upload new version' }).click();
    const replace = page.getByRole('dialog', { name: 'Upload new version' });
    await replace.getByLabel('File').setInputFiles({
      name: 'uj-006-certificate-amended.txt',
      mimeType: 'text/plain',
      buffer: AMENDED,
    });
    await replace.getByRole('button', { name: 'Upload version' }).click();
    await expectText(replace, 'No malware found');
    await replace.getByRole('button', { name: 'Close' }).click();
  });

  const amended = (
    await eventually(
      () => submitter.api.get<DocumentDetail>(`/documents/${documentId}`),
      (d) => d.latestVersion.versionNo === 2 && d.latestVersion.scanState === 'CLEAN',
      'version 2 to be scanned CLEAN',
    )
  ).latestVersion;

  await test.step("the evidence reviewer sees the submitted claim's evidence as version 1, and the evidence path serves its bytes", async () => {
    await reviewer.page.goto('/milestones');
    await reviewer.page
      .getByRole('row')
      .filter({ hasText: title })
      .getByRole('button', { name: title })
      .click();
    const reviewed = reviewer.page.getByRole('dialog', { name: title });
    await expectText(reviewed, 'version 1');
    await expect(reviewed).not.toContainText('version 2');
    const reference = await reviewer.api.get<EvidencePiece>(`/evidence-references/${pinnedId}`);
    expect([reference.documentVersionId, reference.versionNo, reference.scanState]).toEqual([
      signed.id,
      1,
      'CLEAN',
    ]);
    const content = await download(reviewer.api, `/evidence-references/${pinnedId}/content`);
    expect(content.status).toBe(200);
    expect(content.body.equals(SIGNED)).toBe(true);
  });

  await test.step('the evidence reviewer accepts the claim in the approval inbox; the accepted evidence is still version 1', async () => {
    const runId = await runOn(reviewer.api, 'Milestone', 'MilestoneAchievement', claimId);
    await approveInTheInbox(reviewer, runId);
    await eventually(
      () => submitter.api.get<{ status: string }>(`/milestone-achievements/${claimId}`),
      (c) => c.status === 'ACCEPTED',
      `claim ${claimId} to be ACCEPTED`,
    );
    expect(
      (await evidenceOf(submitter.api, claimId)).map((e) => [e.id, e.documentVersionId]),
    ).toEqual([[pinnedId, signed.id]]);
  });

  await test.step('the newer version cannot replace the accepted evidence: the accepted revision takes no attachment or withdrawal', async () => {
    const replace = await submitter.api.send(
      'POST',
      `/milestone-achievements/${claimId}/evidence`,
      await evidenceBody(submitter.api, documentId, amended.id),
    );
    expect([replace.status, replace.code]).toEqual([409, 'MILESTONE_ACHIEVEMENT_NOT_EDITABLE']);
    const withdraw = await submitter.api.send(
      'POST',
      `/milestone-achievements/${claimId}/evidence/${pinnedId}/withdraw`,
      {},
    );
    expect([withdraw.status, withdraw.code]).toEqual([409, 'MILESTONE_ACHIEVEMENT_NOT_EDITABLE']);
    expect((await evidenceOf(submitter.api, claimId)).map((e) => e.documentVersionId)).toEqual([
      signed.id,
    ]);
  });

  await test.step('the submitter reopens the evidence in a correction; a version still being scanned cannot be attached', async () => {
    await openMilestone(page, project.id, title);
    await drawer.getByRole('button', { name: 'Correct the achievement' }).click();
    await drawer.getByLabel('Date achieved').fill(utcDate(-1));
    await drawer.getByRole('button', { name: 'Save as draft' }).click();
    await expectText(drawer, 'The claim was saved as a draft.');
    await drawer.getByRole('button', { name: 'Close' }).click();

    await page.goto(`/documents/${documentId}`);
    await page.getByRole('button', { name: 'Upload new version' }).click();
    const replace = page.getByRole('dialog', { name: 'Upload new version' });
    await replace.getByLabel('File').setInputFiles({
      name: 'uj-006-certificate-held.txt',
      mimeType: 'text/plain',
      buffer: PENDING,
    });
    await replace.getByRole('button', { name: 'Upload version' }).click();
    await expectText(replace, 'Checking the file for malware');
    await replace.getByRole('button', { name: 'Close' }).click();

    await openMilestone(page, project.id, title);
    await drawer.getByRole('button', { name: 'Attach a document' }).click();
    const dialog = page.getByRole('dialog', { name: 'Attach a document' });
    const choice = dialog.getByRole('radio', { name: documentTitle });
    await expect(choice).toBeDisabled();
    await expectText(dialog, 'Not clean yet: it can be attached once the scan finds it clean.');
    await dialog.getByRole('button', { name: 'Cancel' }).click();
  });

  const correction = (await claims()).items.find((c) => c.revisionNo === 2);
  expect(correction?.status).toBe('DRAFT');
  const correctionId = correction?.id ?? '';
  const held = (await submitter.api.get<DocumentDetail>(`/documents/${documentId}`)).latestVersion;
  expect([held.versionNo, held.scanState]).toEqual([3, 'SCAN_PENDING']);

  await test.step('a non-CLEAN attachment is refused by the API too, pending or quarantined, and nothing stays linked', async () => {
    const pending = await submitter.api.send(
      'POST',
      `/milestone-achievements/${correctionId}/evidence`,
      await evidenceBody(submitter.api, documentId, held.id),
    );
    expect([pending.status, pending.code]).toEqual([409, 'DOCUMENT_NOT_AVAILABLE']);

    const quarantined = await uploadQuarantined(page, submitter.api, project.id);
    const infected = await submitter.api.send(
      'POST',
      `/milestone-achievements/${correctionId}/evidence`,
      await evidenceBody(submitter.api, quarantined.id, quarantined.latestVersion.id),
    );
    expect([infected.status, infected.code]).toEqual([409, 'DOCUMENT_NOT_AVAILABLE']);
    const served = await download(
      submitter.api,
      `/documents/${quarantined.id}/versions/${quarantined.latestVersion.id}/content`,
    );
    expect([served.status, codeOf(served.body)]).toEqual([409, 'DOCUMENT_NOT_AVAILABLE']);
    expect(await evidenceOf(submitter.api, correctionId)).toEqual([]);
  });

  await test.step('the accepted evidence is still exactly version 1, byte for byte', async () => {
    const reference = await reviewer.api.get<EvidencePiece>(`/evidence-references/${pinnedId}`);
    expect([reference.documentVersionId, reference.versionNo, reference.status]).toEqual([
      signed.id,
      1,
      'VALID',
    ]);
    const content = await download(reviewer.api, `/evidence-references/${pinnedId}/content`);
    expect(content.body.equals(SIGNED)).toBe(true);
  });

  await test.step('denied scope: another department reaches neither the document nor its evidence', async () => {
    const outsider = await actor('otherDepartmentManager');
    const reference = await outsider.api.send('GET', `/evidence-references/${pinnedId}`);
    expect([reference.status, reference.code]).toEqual([404, 'NOT_FOUND']);
    const content = await download(outsider.api, `/evidence-references/${pinnedId}/content`);
    expect(content.status).toBe(404);
    const document = await outsider.api.send('GET', `/documents/${documentId}`);
    expect([document.status, document.code]).toEqual([404, 'NOT_FOUND']);
    await outsider.page.goto(`/documents/${documentId}`);
    await expectText(
      outsider.page.getByRole('main'),
      'The record was not found, or you do not have access to it.',
    );
    await expect(outsider.page.getByRole('main')).not.toContainText(documentTitle);
    expect((await evidenceOf(submitter.api, claimId)).map((e) => e.documentVersionId)).toEqual([
      signed.id,
    ]);
  });
  await test.step('an attachment sent against the correction it no longer is (stale If-Match) is refused and attaches nothing', async () => {
    const read = (await submitter.api.ok('GET', `/milestone-achievements/${correctionId}`)).etag;
    await openMilestone(page, project.id, title);
    await drawer
      .getByLabel('What was achieved')
      .fill('Commissioning date corrected to the day before.');
    await drawer.getByRole('button', { name: 'Save changes' }).click();
    await expectText(drawer, 'The claim was saved.');
    expect(
      (await submitter.api.ok('GET', `/milestone-achievements/${correctionId}`)).etag,
    ).not.toBe(read);
    // milestone-achievement.md §4 and api-conventions R-21: the attachment honours If-Match, 412 when stale.
    const stale = await submitter.api.send(
      'POST',
      `/milestone-achievements/${correctionId}/evidence`,
      await evidenceBody(submitter.api, documentId, amended.id),
      { ifMatch: read },
    );
    expect([stale.status, stale.code]).toEqual([412, 'PRECONDITION_FAILED']);
    expect(await evidenceOf(submitter.api, correctionId)).toEqual([]);
  });
});

async function openMilestone(page: Page, projectId: string, title: string): Promise<void> {
  await page.goto(`/projects/${projectId}/milestones`);
  await page
    .getByRole('row')
    .filter({ hasText: title })
    .getByRole('button', { name: title })
    .click();
}

async function attach(page: Page, drawer: Locator, documentTitle: string): Promise<void> {
  await drawer.getByRole('button', { name: 'Attach a document' }).click();
  const dialog = page.getByRole('dialog', { name: 'Attach a document' });
  await dialog.getByRole('radio', { name: documentTitle }).check();
  await dialog.getByLabel('Evidence of').selectOption({ label: 'Completion certificate' });
  await dialog.getByRole('button', { name: 'Attach', exact: true }).click();
}

async function evidenceBody(
  api: PlatformApi,
  documentId: string,
  documentVersionId: string,
): Promise<Record<string, string>> {
  return {
    documentId,
    documentVersionId,
    evidenceTypeItemId: await itemId(api, 'EVIDENCE_TYPE', 'E2E_COMPLETION_CERTIFICATE'),
  };
}

/** A file the scanner finds malware in, uploaded to the project through the library; answers it once QUARANTINED. */
async function uploadQuarantined(
  page: Page,
  api: PlatformApi,
  projectId: string,
): Promise<DocumentDetail> {
  const title = unique('Certificate copy from email');
  await page.goto(`/projects/${projectId}/documents`);
  await page.getByRole('button', { name: 'Upload document' }).click();
  const upload = page.getByRole('dialog', { name: 'Upload document' });
  await upload.getByLabel('File').setInputFiles({
    name: 'uj-006-certificate-infected.txt',
    mimeType: 'text/plain',
    buffer: MALWARE,
  });
  await upload.getByLabel('Title').fill(title);
  await upload.getByLabel('Document type').selectOption({ label: 'Report' });
  await upload.getByLabel('Classification').selectOption({ index: 1 });
  await upload.getByRole('button', { name: 'Upload', exact: true }).click();
  await expectText(upload, 'Malware found: this file is quarantined');
  await upload.getByRole('link', { name: 'Open the document' }).click();
  await expect(page).toHaveURL(/\/documents\/[0-9a-f-]{36}$/);
  await expectText(page.getByRole('main'), 'Malware found: this file is quarantined');
  const documentId = /\/documents\/([0-9a-f-]{36})/.exec(page.url())?.[1] ?? '';
  expect(documentId).not.toBe('');
  const quarantined = await api.get<DocumentDetail>(`/documents/${documentId}`);
  expect(quarantined.latestVersion.scanState).toBe('QUARANTINED');
  return quarantined;
}

function codeOf(body: Buffer): string | null {
  try {
    return (JSON.parse(body.toString()) as { code?: string }).code ?? null;
  } catch {
    return null;
  }
}
