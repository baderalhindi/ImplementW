import { type Page } from '@playwright/test';

import {
  createDraft,
  ratingAt,
  readVersion,
  remapCell,
  resolve,
  type ResolvedConfiguration,
  type RiskMatrixContent,
} from '../support/configuration.ts';
import { expect, expectText, test, unique } from '../support/journey.ts';
import { activeProject } from '../support/projects.ts';

// UJ-012 — Published configuration (Step 18; FG-04; BPK-007; ICD-13).
// Participants: configuration author and authorized publisher.
// Procedure: create a draft policy, publish an authorized version and compare new transactions with an existing pinned
// transaction. Expected: resolution is deterministic; drafts do not silently govern live transactions; pinned context is
// retained. Negative extension: denied scope and stale/replayed request; confirm source-appropriate failure without
// unauthorized mutation.
//
// The policy is the RISK_MATRIX and the transaction a WF-06 risk assessment, which pins the matrix version in force when it
// is made (risk-management.md D-4, D-13; RiskIssue/RiskRatingPinningTests). There are no FG-04 screens
// (docs/testing/test-strategy.md §6: "no FG-04 screens built"), so the draft is authored, reviewed and published through the
// FG-04 API by three people (master-data-configuration.md D-2): the Portfolio Manager authors, the Department Manager
// reviews, the administrator (R01) publishes with step-up (D-11). The assessments and their pinned ratings are made and read
// in the browser by the project's Project Manager.
//
// The journey publishes a RISK_MATRIX version on every run. Each version keeps the levels, dimensions and LOW/MEDIUM/HIGH
// ratings of e2e/stack/provision/50-risk-matrix.sql and remaps one cell, so the matrix in force stays valid for every
// journey but no journey may rely on the rating of a particular cell.

/** The cell the journey assesses: probability 3, the first dimension at 4 and the others at 2, so an overall impact of 4. */
const PROBABILITY = 3;
const OVERALL_IMPACT = 4;

interface Assessment {
  id: string;
  versionNo: number;
  matrixConfigurationVersionId: string;
  probabilityLevel: number;
  overallImpactLevel: number;
  rating: { code: string; label: { en: string } };
}

interface Risk {
  id: string;
  status: string;
  currentAssessment: Assessment | null;
}

test('UJ-012 a published configuration governs new transactions; a draft never does; pinned context is kept', async ({
  actor,
}) => {
  const department = await actor('departmentManager');
  const portfolio = await actor('portfolioManager');
  const manager = await actor('projectManager');
  const administrator = await actor('administrator');
  // FG-04's three people (D-2): author, reviewer, publisher.
  const author = portfolio.api;
  const reviewer = department.api;
  const publisher = administrator.api;

  const project = await activeProject({
    creator: department.api,
    reviewer: department.api,
    approver: portfolio.api,
    manager: manager.api,
  });
  const risk = (riskId: string) => manager.api.get<Risk>(`/risks/${riskId}`);

  // Version A: whichever RISK_MATRIX version is in force now (50-risk-matrix.sql's, or a previous run's).
  const inForce = await resolve<RiskMatrixContent>(manager.api, 'RISK_MATRIX');
  const ratedByA = ratingAt(inForce.content, PROBABILITY, OVERALL_IMPACT);
  // Version B rates the same cell otherwise, with the same three ratings.
  const ratingCodeOfB = ratedByA.code === 'HIGH' ? 'LOW' : 'HIGH';
  const ratedByB = inForce.content.riskRatings.find((r) => r.code === ratingCodeOfB);
  if (ratedByB === undefined)
    throw new Error(`Matrix ${inForce.versionId} has no ${ratingCodeOfB} rating.`);

  let pinnedRiskId = '';
  let pinned: Assessment | null = null;

  await test.step(`the Project Manager assesses a risk in the browser: it is rated by version ${String(inForce.versionNo)} in force and pins it`, async () => {
    pinnedRiskId = await registerAndAssess(manager.page, project.id, unique('UJ-012 Pinned risk'));
    await expect(rating(manager.page)).toHaveText(ratedByA.label.en);
    await expectText(
      manager.page.getByRole('main'),
      `Risk matrix version ${String(inForce.versionNo)},`,
    );
    pinned = (await risk(pinnedRiskId)).currentAssessment;
    expect([
      pinned?.matrixConfigurationVersionId,
      pinned?.rating.code,
      pinned?.overallImpactLevel,
    ]).toEqual([inForce.versionId, ratedByA.code, OVERALL_IMPACT]);
  });

  let draftId = '';
  let createdEtag = '';

  await test.step('the author creates a draft from the version in force that rates the assessed cell otherwise', async () => {
    const created = await createDraft<RiskMatrixContent>(
      author,
      'RISK_MATRIX',
      inForce.versionId,
      `UJ-012: ${String(PROBABILITY)} × ${String(OVERALL_IMPACT)} rated ${ratingCodeOfB}`,
    );
    draftId = created.body.id;
    createdEtag = created.etag ?? '';
    expect(created.body.governance.lifecycleState).toBe('DRAFT');
    const written = await author.send(
      'PUT',
      `/configuration-versions/${draftId}`,
      { content: remapCell(created.body.content, PROBABILITY, OVERALL_IMPACT, ratingCodeOfB) },
      { ifMatch: createdEtag },
    );
    expect(written.status).toBe(200);
    const draft = await readVersion<RiskMatrixContent>(author, draftId);
    expect(ratingAt(draft.body.content, PROBABILITY, OVERALL_IMPACT).code).toBe(ratingCodeOfB);
  });

  await test.step('a draft does not govern: resolution still answers the published version, and a new assessment is rated by it', async () => {
    expect((await resolve(manager.api, 'RISK_MATRIX')).versionId).toBe(inForce.versionId);
    const riskId = await registerAndAssess(
      manager.page,
      project.id,
      unique('UJ-012 Draft-time risk'),
    );
    await expect(rating(manager.page)).toHaveText(ratedByA.label.en);
    const assessed = (await risk(riskId)).currentAssessment;
    expect([assessed?.matrixConfigurationVersionId, assessed?.rating.code]).toEqual([
      inForce.versionId,
      ratedByA.code,
    ]);
  });

  await test.step('stale: validating against the draft as it was before the edit (old ETag) is refused and changes nothing', async () => {
    const stale = await reviewer.send(
      'POST',
      `/configuration-versions/${draftId}/validate`,
      {},
      { ifMatch: createdEtag },
    );
    expect([stale.status, stale.code]).toEqual([412, 'PRECONDITION_FAILED']);
    expect((await readVersion(author, draftId)).body.governance.lifecycleState).toBe('DRAFT');
  });

  await test.step('the author cannot review their own draft; the reviewer validates it, and a validated version still does not govern', async () => {
    const own = await author.send('POST', `/configuration-versions/${draftId}/validate`, {});
    expect([own.status, own.code]).toEqual([422, 'MASTER_DATA_CONFIG_SEPARATION_OF_DUTIES']);
    const validated = await reviewer.send(
      'POST',
      `/configuration-versions/${draftId}/validate`,
      {},
    );
    expect(validated.status).toBe(200);
    expect((await readVersion(author, draftId)).body.governance.lifecycleState).toBe('VALIDATED');
    expect((await resolve(manager.api, 'RISK_MATRIX')).versionId).toBe(inForce.versionId);
  });

  await test.step('denied: a person without configuration management can neither publish nor author, and nothing changes', async () => {
    const publish = await manager.api.send(
      'POST',
      `/configuration-versions/${draftId}/publish`,
      {},
    );
    expect([publish.status, publish.code]).toEqual([403, 'PERMISSION_DENIED']);
    const draft = await manager.api.send('POST', '/configuration-versions', {
      familyId: (await readVersion(author, draftId)).body.familyId,
      basedOnVersionId: inForce.versionId,
      changeSummary: { text: 'Not allowed.', language: 'en' },
    });
    expect([draft.status, draft.code]).toEqual([403, 'PERMISSION_DENIED']);
    expect((await readVersion(author, draftId)).body.governance.lifecycleState).toBe('VALIDATED');
    expect((await resolve(manager.api, 'RISK_MATRIX')).versionId).toBe(inForce.versionId);
  });

  const beforePublication = new Date().toISOString();

  await test.step('the authorized publisher steps up and publishes it: resolution now answers the new version', async () => {
    await publisher.stepUp();
    const published = await publisher.send(
      'POST',
      `/configuration-versions/${draftId}/publish`,
      {},
    );
    expect(published.status).toBe(200);
    const version = await readVersion<RiskMatrixContent>(author, draftId);
    expect(version.body.governance.lifecycleState).toBe('PUBLISHED');
    const now = await resolve<RiskMatrixContent>(manager.api, 'RISK_MATRIX');
    expect([now.versionId, ratingAt(now.content, PROBABILITY, OVERALL_IMPACT).code]).toEqual([
      draftId,
      ratingCodeOfB,
    ]);
  });

  await test.step('resolution is deterministic: a moment before the publication still resolves the earlier version, unchanged', async () => {
    const then = await resolve<RiskMatrixContent>(manager.api, 'RISK_MATRIX', beforePublication);
    expect(then).toEqual(inForce);
    const previous = await readVersion(author, inForce.versionId);
    expect([previous.body.governance.lifecycleState, previous.body.effectiveTo]).toEqual([
      'PUBLISHED',
      null,
    ]);
  });

  let published: ResolvedConfiguration<RiskMatrixContent> | null = null;

  await test.step('a new assessment of the same levels is rated by the published version and pins it', async () => {
    published = await resolve<RiskMatrixContent>(manager.api, 'RISK_MATRIX');
    const riskId = await registerAndAssess(manager.page, project.id, unique('UJ-012 New risk'));
    await expect(rating(manager.page)).toHaveText(ratedByB.label.en);
    await expectText(
      manager.page.getByRole('main'),
      `Risk matrix version ${String(published.versionNo)},`,
    );
    const assessed = (await risk(riskId)).currentAssessment;
    expect([assessed?.matrixConfigurationVersionId, assessed?.rating.code]).toEqual([
      draftId,
      ratingCodeOfB,
    ]);
  });

  await test.step('the earlier risk, reopened, keeps the rating and version it pinned', async () => {
    await manager.page.goto(`/projects/${project.id}/risks/${pinnedRiskId}`);
    await expect(rating(manager.page)).toHaveText(ratedByA.label.en);
    await expectText(
      manager.page.getByRole('main'),
      'The rating was given under an earlier matrix version and is kept as given.',
    );
    expect((await risk(pinnedRiskId)).currentAssessment).toEqual(pinned);
  });

  await test.step('replayed: publishing the published version again, or editing it, is refused and changes nothing', async () => {
    const before = await readVersion<RiskMatrixContent>(author, draftId);
    const replay = await publisher.send('POST', `/configuration-versions/${draftId}/publish`, {});
    expect([replay.status, replay.code]).toEqual([409, 'INVALID_TRANSITION']);
    const edit = await author.send(
      'PUT',
      `/configuration-versions/${draftId}`,
      { content: remapCell(before.body.content, PROBABILITY, OVERALL_IMPACT, ratedByA.code) },
      { ifMatch: before.etag },
    );
    expect([edit.status, edit.code]).toEqual([409, 'INVALID_TRANSITION']);
    const after = await readVersion<RiskMatrixContent>(author, draftId);
    expect([after.body, after.etag]).toEqual([before.body, before.etag]);
    expect(await resolve(manager.api, 'RISK_MATRIX')).toEqual(published);
    expect((await risk(pinnedRiskId)).currentAssessment).toEqual(pinned);
  });
});

/** The risk detail's rating, as SCR-082 shows it. */
function rating(page: Page) {
  return page
    .locator('.details__row')
    .filter({ has: page.locator('dt', { hasText: /^Rating$/ }) })
    .locator('dd');
}

/**
 * MOD-030 then MOD-032 as the Project Manager does them: registers a risk on the project's Risks tab and assesses it at
 * probability 3 with the first impact dimension at 4 and the others at 2. Leaves the page on the risk; answers its id.
 */
async function registerAndAssess(page: Page, projectId: string, title: string): Promise<string> {
  await page.goto(`/projects/${projectId}/risks`);
  await page.getByRole('button', { name: 'Register risk' }).click();
  const form = page.getByRole('dialog', { name: 'Register risk' });
  await form.getByLabel('Title').fill(title);
  await form
    .getByLabel('Description')
    .fill('The contractor may not mobilise by the planned start.');
  await form.getByLabel('Category').selectOption({ label: 'Weather' });
  await form.getByRole('button', { name: 'Save' }).click();
  await expect(page.getByRole('heading', { level: 2, name: title })).toBeVisible();
  const riskId = /\/risks\/([0-9a-f-]{36})/.exec(page.url())?.[1] ?? '';
  expect(riskId).not.toBe('');

  await page
    .getByRole('group', { name: 'Risk actions' })
    .getByRole('button', { name: 'Assess' })
    .click();
  // The dialog's title and the level labels carry translated parameters (U+2068/U+2069); options are chosen by level.
  const dialog = page.getByRole('dialog');
  await expectText(dialog.getByRole('heading', { level: 2 }), `Assess ${title}`);
  await dialog.getByLabel('Probability').selectOption(String(PROBABILITY));
  const dimensions = dialog
    .getByRole('group', { name: 'Impact by dimension' })
    .getByRole('combobox');
  const count = await dimensions.count();
  expect(count).toBeGreaterThan(1);
  for (let i = 0; i < count; i++) {
    await dimensions.nth(i).selectOption(i === 0 ? String(OVERALL_IMPACT) : '2');
  }
  await dialog.getByRole('button', { name: 'Save assessment' }).click();
  await expectText(page.getByRole('main'), 'The assessment was saved.');
  return riskId;
}
