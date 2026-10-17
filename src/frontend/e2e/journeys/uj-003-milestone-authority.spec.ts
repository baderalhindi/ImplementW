import { type Page } from '@playwright/test';

import {
  approveInTheInbox,
  baselineAndActivate,
  plannedProject,
  runOn,
  scheduleWithOneActivity,
} from '../support/execution.ts';
import { eventually } from '../support/api.ts';
import { itemId } from '../support/reference.ts';
import { expect, expectText, test, unique, utcDate } from '../support/journey.ts';

// UJ-003 — Milestone authority (Step 18; WF-03, WF-05; BPK-015, BPK-017; ICD-04).
// Participants: the schedule planner (the Project Manager, WF-03) and the milestone achievement authority (AHDA's portfolio
// manager, deciding the WF-11 run WF-05 opens).
// Procedure: create the scheduled milestone in WF-03; perform achievement in WF-05; inspect both views and attempt an
// unauthorized cross-owner change.
// Expected: schedule and achievement retain their split authority; governed contracts reconcile the shared milestone.
// Negative extension: denied scope and a stale/replayed request fail without unauthorized mutation.

interface Milestone {
  id: string;
  status: 'PLANNED' | 'ACHIEVED' | 'CANCELLED';
  forecastDate: string;
  baselineId: string | null;
  baselinePlannedDate: string | null;
}

interface Achievement {
  id: string;
  projectMilestoneId: string;
  revisionNo: number;
  status: string;
  claimedAchievementDate: string;
  acceptedActualAchievementDate: string | null;
}

test('UJ-003 schedule and achievement keep split authority over one shared milestone', async ({
  actor,
}) => {
  const department = await actor('departmentManager');
  const portfolio = await actor('portfolioManager');
  const planner = await actor('projectManager');
  const crew = {
    creator: department.api,
    reviewer: department.api,
    approver: portfolio.api,
    manager: planner.api,
  };
  const project = await plannedProject(crew, unique('UJ-003 Harbour bridge'));
  await scheduleWithOneActivity(planner.api, project.id);
  const title = unique('Deck poured');
  const planned = utcDate(40);
  const reforecast = utcDate(45);
  const today = utcDate(0);
  const milestones = () =>
    planner.api.get<{ items: Milestone[] }>(`/project-milestones?projectId=${project.id}`);
  const achievements = () =>
    planner.api.get<{ items: Achievement[] }>(`/milestone-achievements?projectId=${project.id}`);
  const page = planner.page;
  const delivery = await itemId(planner.api, 'MILESTONE_CATEGORY', 'E2E_DELIVERY');
  const row = (p: Page) => p.getByRole('row').filter({ hasText: title });

  await test.step('the schedule planner creates the milestone in WF-03, on the activity it completes', async () => {
    await page.goto(`/projects/${project.id}/milestones`);
    await page.getByRole('button', { name: 'Add milestone' }).click();
    const form = page.getByRole('dialog', { name: 'Add milestone' });
    await form.getByLabel('Title').fill(title);
    await form.getByLabel('Category').selectOption({ label: 'Delivery' });
    await form.getByLabel('Forecast date').fill(planned);
    await form.getByLabel('Schedule activity it completes').selectOption({ index: 1 });
    await form.getByRole('button', { name: 'Save', exact: true }).click();
    await expectText(page.getByRole('main'), 'The milestone was added.');
    await expect(row(page)).toContainText('Planned');
    await expect(row(page)).toContainText('Not claimed');
    await expect(row(page)).toContainText('Not in the active baseline');
  });

  const milestone = (await milestones()).items[0];
  expect(milestone?.status).toBe('PLANNED');
  const milestoneId = milestone?.id ?? '';

  await test.step("WF-03's baseline, approved through WF-11, copies the milestone's date; the project is activated", async () => {
    const baselineId = await baselineAndActivate(crew, project.id);
    const baselined = (await milestones()).items;
    expect(baselined.map((m) => [m.id, m.baselineId, m.baselinePlannedDate])).toEqual([
      [milestoneId, baselineId, planned],
    ]);
    await page.reload();
    await expectText(row(page), `Baseline ${planned}`);
  });

  const beforeReforecast = (await planner.api.ok('GET', `/project-milestones/${milestoneId}`)).etag;

  await test.step('the schedule planner reforecasts the milestone five days later; the baseline keeps its date', async () => {
    await row(page).getByRole('button', { name: title }).click();
    const drawer = page.getByRole('dialog', { name: title });
    await drawer.getByRole('button', { name: 'Edit milestone' }).click();
    const form = page.getByRole('dialog', { name: 'Edit milestone' });
    await form.getByLabel('Forecast date').fill(reforecast);
    await form.getByRole('button', { name: 'Save', exact: true }).click();
    await expectText(page.getByRole('main'), 'The milestone was saved.');
    const after = await planner.api.get<Milestone>(`/project-milestones/${milestoneId}`);
    expect([after.forecastDate, after.baselinePlannedDate]).toEqual([reforecast, planned]);
  });

  await test.step('a re-date sent against the milestone it no longer is (stale If-Match) is refused and changes nothing', async () => {
    const stale = await planner.api.send(
      'PUT',
      `/project-milestones/${milestoneId}`,
      milestoneBody(delivery, title, planned),
      { ifMatch: beforeReforecast },
    );
    expect([stale.status, stale.code]).toEqual([412, 'PRECONDITION_FAILED']);
    expect(
      (await planner.api.get<Milestone>(`/project-milestones/${milestoneId}`)).forecastDate,
    ).toBe(reforecast);
  });

  await test.step('the Project Manager claims the achievement in WF-05 and submits it for review', async () => {
    await page.goto(`/projects/${project.id}/milestones`);
    await row(page).getByRole('button', { name: title }).click();
    const drawer = page.getByRole('dialog', { name: title });
    await drawer.getByRole('button', { name: 'Claim the achievement' }).click();
    await drawer.getByLabel('Date achieved').fill(today);
    await drawer.getByLabel('What was achieved').fill('Deck poured and cured; inspection signed.');
    await drawer.getByRole('button', { name: 'Save as draft' }).click();
    await expectText(drawer, 'The claim was saved as a draft.');
    await drawer.getByRole('button', { name: 'Submit for review' }).click();
    await drawer.getByRole('button', { name: 'Yes, submit without evidence' }).click();
    await expectText(drawer, 'The claim was submitted for review.');
    await drawer.getByRole('button', { name: 'Close' }).click();
    await expect(row(page)).toContainText('Claim under review');
    // WF-05's claim changes nothing of WF-03's: the milestone is still PLANNED on its forecast.
    const shared = await planner.api.get<Milestone>(`/project-milestones/${milestoneId}`);
    expect([shared.status, shared.forecastDate]).toEqual(['PLANNED', reforecast]);
  });

  const claim = (await achievements()).items[0];
  expect([claim?.status, claim?.projectMilestoneId, claim?.revisionNo]).toEqual([
    'SUBMITTED',
    milestoneId,
    1,
  ]);
  const claimId = claim?.id ?? '';

  await test.step('a replayed submission is refused and opens no second review', async () => {
    const replay = await planner.api.send('POST', `/milestone-achievements/${claimId}/submit`, {});
    expect([replay.status, replay.code]).toEqual([409, 'INVALID_TRANSITION']);
    const runs = await portfolio.api.get<{ items: unknown[] }>(
      `/approval-instances?subjectModule=Milestone&subjectType=MilestoneAchievement&subjectId=${claimId}&pageSize=50`,
    );
    expect(runs.items).toHaveLength(1);
  });

  await test.step('the achievement authority accepts the claim in the approval inbox', async () => {
    const runId = await runOn(portfolio.api, 'Milestone', 'MilestoneAchievement', claimId);
    await approveInTheInbox(portfolio, runId);
    await eventually(
      () => planner.api.get<Achievement>(`/milestone-achievements/${claimId}`),
      (a) => a.status === 'ACCEPTED',
      `achievement ${claimId} to be ACCEPTED`,
    );
  });

  await test.step("both views reconcile one milestone: WF-03 records it achieved, the date achieved is WF-05's", async () => {
    const shared = (await milestones()).items;
    expect(shared.map((m) => [m.id, m.status, m.forecastDate, m.baselinePlannedDate])).toEqual([
      [milestoneId, 'ACHIEVED', reforecast, planned],
    ]);
    const revisions = (await achievements()).items;
    expect(
      revisions.map((a) => [a.id, a.projectMilestoneId, a.status, a.acceptedActualAchievementDate]),
    ).toEqual([[claimId, milestoneId, 'ACCEPTED', today]]);

    // The schedule planner's view: the project's Milestones tab.
    await page.goto(`/projects/${project.id}/milestones`);
    await expect(row(page)).toContainText('Achieved');
    await expectText(row(page), `Achieved on ${today}`);
    await expectText(row(page), `Baseline ${planned}`);
    // The achievement authority's view: the milestone register, the same one milestone under its project.
    await portfolio.page.goto('/milestones');
    const registered = row(portfolio.page);
    await expect(registered).toHaveCount(1);
    await expect(registered.getByRole('link', { name: project.title.text })).toBeVisible();
    await expectText(registered, `Achieved on ${today}`);
    await registered.getByRole('button', { name: title }).click();
    const drawer = portfolio.page.getByRole('dialog', { name: title });
    await expectText(drawer, `Achieved on ${today} (revision 1), accepted by`);
  });

  await test.step('cross-owner change: the achievement authority cannot claim or correct the achievement', async () => {
    const drawer = portfolio.page.getByRole('dialog', { name: title });
    await expect(drawer.getByRole('button', { name: 'Correct the achievement' })).toHaveCount(0);
    await expectText(
      drawer,
      "Only the project's Project Manager claims its milestones' achievements.",
    );
    const correction = await portfolio.api.send('POST', '/milestone-achievements', {
      projectMilestoneId: milestoneId,
      claimedAchievementDate: utcDate(-1),
      narrative: null,
    });
    expect([correction.status, correction.code]).toEqual([403, 'PERMISSION_DENIED']);
    expect((await achievements()).items.map((a) => a.id)).toEqual([claimId]);
  });

  await test.step('cross-owner change: the schedule planner cannot re-date or cancel what WF-05 decided', async () => {
    await row(page).getByRole('button', { name: title }).click();
    const drawer = page.getByRole('dialog', { name: title });
    await expect(drawer.getByRole('button', { name: 'Edit milestone' })).toHaveCount(0);
    await expect(drawer.getByRole('button', { name: 'Cancel milestone' })).toHaveCount(0);
    const current = await planner.api.ok('GET', `/project-milestones/${milestoneId}`);
    const redate = await planner.api.send(
      'PUT',
      `/project-milestones/${milestoneId}`,
      milestoneBody(delivery, title, utcDate(50)),
      { ifMatch: current.etag },
    );
    expect([redate.status, redate.code]).toEqual([409, 'SCHEDULE_NOT_EDITABLE']);
    const cancel = await planner.api.send('POST', `/project-milestones/${milestoneId}/cancel`, {});
    expect([cancel.status, cancel.code]).toEqual([409, 'INVALID_TRANSITION']);
    const shared = await planner.api.get<Milestone>(`/project-milestones/${milestoneId}`);
    expect([shared.status, shared.forecastDate]).toEqual(['ACHIEVED', reforecast]);
  });

  await test.step('denied scope: another department reaches neither the milestone nor its achievement', async () => {
    const outsider = await actor('otherDepartmentManager');
    await outsider.page.goto(`/projects/${project.id}/milestones`);
    await expectText(
      outsider.page.getByRole('main'),
      'The project was not found, or you do not have access to it.',
    );
    const read = await outsider.api.send('GET', `/project-milestones/${milestoneId}`);
    expect([read.status, read.code]).toEqual([404, 'NOT_FOUND']);
    // A department manager holds no WF-05 permission at all (MILESTONE_VIEW ships to R04 only): refused before any record.
    const claimRead = await outsider.api.send('GET', `/milestone-achievements/${claimId}`);
    expect([claimRead.status, claimRead.code]).toEqual([403, 'PERMISSION_DENIED']);
    const redate = await outsider.api.send(
      'PUT',
      `/project-milestones/${milestoneId}`,
      milestoneBody(delivery, title, utcDate(50)),
    );
    // Nor may they plan another department's schedule: no SCHEDULE_EDIT, refused before the record is looked for.
    expect([redate.status, redate.code]).toEqual([403, 'PERMISSION_DENIED']);
    const after = await planner.api.get<Milestone>(`/project-milestones/${milestoneId}`);
    expect([after.status, after.forecastDate]).toEqual(['ACHIEVED', reforecast]);
  });
});

/** WF-03's milestone as MOD-016 sends it. */
function milestoneBody(
  categoryItemId: string,
  title: string,
  forecastDate: string,
): Record<string, unknown> {
  return {
    scheduleActivityId: null,
    title: { text: title, language: 'en' },
    milestoneCategoryItemId: categoryItemId,
    forecastDate,
    sortOrder: 0,
  };
}
