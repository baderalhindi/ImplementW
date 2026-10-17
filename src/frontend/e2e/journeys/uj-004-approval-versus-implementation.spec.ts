import { type Page } from '@playwright/test';

import { eventually } from '../support/api.ts';
import { runFixture } from '../support/fixtures.ts';
import {
  baselineStates,
  changeRequest,
  confirmCommand,
  definition,
  type BaselineDetail,
} from '../support/governance.ts';
import { expect, expectText, test, unique } from '../support/journey.ts';
import { activeProject } from '../support/projects.ts';

// UJ-004 — Approval versus implementation (Step 18; WF-11, WF-08; BPK-011, BPK-020; ICD-05).
// Participants: Requester and assigned approver; QA-supported failure fixture.
// Procedure: submit and approve a source change; observe source application separately; introduce a controlled
// source-application failure then recover. Expected: approval outcome remains distinct from source implementation; failed
// application is visible and cannot be presented as applied. Negative extension: denied scope and stale/replayed request;
// confirm source-appropriate failure without unauthorized mutation.
// The source change is a WF-08 schedule change; its change authorisation is applied by WF-03's rebaseline as WF-11 approves it
// (change-request.md D-7 to D-9). The failure is QA's "failed apply" fixture (Step 18 TD-05): uj-004-failed-apply makes that
// application fail technically, and the outbox retries it once the fault is cleared (test-strategy.md §7, gap G-1).

const SCHEDULE_IMPACT_DAYS = 5;

test('UJ-004 approval stays distinct from implementation, and a failed application is never shown as applied', async ({
  actor,
}) => {
  test.setTimeout(10 * 60_000);
  const requester = await actor('projectManager');
  const reviewer = await actor('departmentManager');
  const approver = await actor('portfolioManager');
  const project = await activeProject({
    creator: reviewer.api,
    reviewer: reviewer.api,
    approver: approver.api,
    manager: requester.api,
  });
  const title = unique('UJ-004 Extend the earthworks');
  const baselinesPath = `/projects/${project.id}/schedule/baselines`;
  const firstBaseline = (
    await approver.api.get<{ items: BaselineDetail[] }>(
      `/project-baselines?projectId=${project.id}&pageSize=50`,
    )
  ).items[0];
  if (firstBaseline === undefined) throw new Error('activeProject() left no baseline');
  const firstBaselineVersion = (
    await approver.api.ok('GET', `/project-baselines/${firstBaseline.id}`)
  ).etag;

  await test.step('the requester raises a schedule change, sees its classification and submits it', async () => {
    const page = requester.page;
    await page.goto(`/change-requests/new?projectId=${project.id}`);
    await page.getByLabel(/^Change type/).selectOption({ label: 'Schedule change' });
    await page.getByLabel(/^Title/).fill(title);
    await page
      .getByLabel(/^Justification/)
      .fill('Ground conditions at the northern cut need five more days of earthworks.');
    await page.getByLabel(/^Schedule impact/).fill(String(SCHEDULE_IMPACT_DAYS));
    const submit = page.getByRole('button', { name: 'Submit for review' });
    await expect(submit).toBeDisabled();
    await page.getByRole('button', { name: 'Save and check materiality' }).click();
    await expectText(page.getByRole('main'), 'Band 1 of 3');
    await expect(submit).toBeEnabled();
    await submit.click();
    await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible();
    await expectText(page.getByRole('main'), 'The change request is submitted.');
  });

  const requestId = /\/change-requests\/([0-9a-f-]{36})/.exec(requester.page.url())?.[1] ?? '';
  expect(requestId).not.toBe('');
  const requestPath = `/change-requests/${requestId}`;

  await test.step("the department manager starts AHDA's review; the classification is recorded", async () => {
    await reviewer.page.goto(requestPath);
    await confirmCommand(reviewer.page, 'Start review');
    await expectText(reviewer.page.getByRole('main'), 'The review has started.');
    expect((await changeRequest(approver.api, requestId)).status).toBe('UNDER_REVIEW');
  });

  await test.step('the assigned approver approves the change in WF-11', async () => {
    await approver.page.goto(requestPath);
    await approver.page
      .getByRole('group', { name: 'Change request actions' })
      .getByRole('button', { name: 'Approve', exact: true })
      .click();
    await approver.page
      .getByRole('dialog', { name: 'Approve request' })
      .getByRole('button', { name: 'Approve' })
      .click();
    await expectText(approver.page.getByRole('main'), 'Your approval is recorded.');
    await eventually(
      () => changeRequest(approver.api, requestId),
      (r) => r.status === 'APPROVED',
      `change request ${requestId} to be APPROVED`,
    );
  });

  const issued = (await changeRequest(approver.api, requestId)).authorizations;
  expect(
    issued.map((a) => [a.authorizationScope, a.targetId, a.targetRevisionNo, a.status]),
  ).toEqual([['REBASELINE', firstBaseline.id, 1, 'ISSUED']]);
  const authorizationId = issued[0]?.id ?? '';

  await test.step('approval is not implementation: the request reads Approved, Not started, and the schedule is unchanged', async () => {
    const page = requester.page;
    await page.goto(requestPath);
    const state = page.getByRole('region', { name: 'Approval and implementation' });
    await expectText(definition(state, 'Approval'), 'Approved');
    await expectText(definition(state, 'Implementation'), 'Not started');
    await expectText(state, '0 of 1 authorisations applied');
    await expectText(authorizationRow(page), 'Issued, not applied');
    // WF-03 holds the baseline it had: approval changed nothing in the source.
    expect(await baselineStates(approver.api, project.id)).toEqual(['1 ACTIVE']);
    expect((await approver.api.ok('GET', `/project-baselines/${firstBaseline.id}`)).etag).toBe(
      firstBaselineVersion,
    );
    await page.goto(baselinesPath);
    await expect(baselineRow(page, 1)).toContainText('Active');
  });

  const approvedVersion = (await requester.api.ok('GET', `/change-requests/${requestId}`)).etag;

  await test.step('the requester opens the implementation', async () => {
    await requester.page.goto(requestPath);
    await confirmCommand(requester.page, 'Start implementation');
    await expectText(requester.page.getByRole('main'), 'The implementation has started.');
    expect((await changeRequest(approver.api, requestId)).status).toBe('IMPLEMENTATION');
  });

  // The schedule change the authorisation governs: the works take five days longer in the working schedule.
  const activities = await requester.api.get<{
    items: {
      id: string;
      wbsCode: string;
      name: { text: string };
      requestedStartDate: string;
      plannedDurationDays: number;
      sortOrder: number;
    }[];
  }>(`/schedule-activities?projectId=${project.id}&pageSize=50`);
  const works = activities.items[0];
  if (works === undefined) throw new Error('activeProject() left no activity');
  const workVersion = (await requester.api.ok('GET', `/schedule-activities/${works.id}`)).etag;
  await requester.api.ok(
    'PUT',
    `/schedule-activities/${works.id}`,
    {
      parentActivityId: null,
      wbsCode: works.wbsCode,
      name: { text: works.name.text, language: 'en' },
      requestedStartDate: works.requestedStartDate,
      plannedDurationDays: works.plannedDurationDays + SCHEDULE_IMPACT_DAYS,
      sortOrder: works.sortOrder,
    },
    { ifMatch: workVersion },
  );

  await test.step('the requester submits the rebaseline that applies the authorisation; WF-03 sends it for approval', async () => {
    const page = requester.page;
    await page.goto(baselinesPath);
    await page.getByRole('button', { name: 'Create baseline candidate' }).click();
    await expectText(page.getByRole('main'), 'A draft baseline candidate was created');
    await baselineRow(page, 2).getByRole('button', { name: 'Submit' }).click();
    const dialog = page.getByRole('dialog', { name: 'Submit baseline' });
    await dialog.getByLabel(/^Change authorisation ID/).fill(authorizationId);
    await dialog.getByRole('button', { name: 'Submit baseline' }).click();
    await expectText(page.getByRole('main'), 'The baseline was submitted to AHDA for review.');
    await expect(baselineRow(page, 2)).toContainText('With AHDA for review');
    expect(await baselineStates(approver.api, project.id)).toEqual(['1 ACTIVE', '2 SUBMITTED']);
  });

  const candidateId =
    (
      await approver.api.get<{ items: BaselineDetail[] }>(
        `/project-baselines?projectId=${project.id}&pageSize=50`,
      )
    ).items.find((b) => b.versionNo === 2)?.id ?? '';
  expect(candidateId).not.toBe('');

  let runId = '';
  // QA's failure fixture is cleared whatever happens, so no fault outlives the journey.
  try {
    await test.step('a controlled failure: the application of the authorisation will fail (QA fixture uj-004-failed-apply)', async () => {
      expect(
        await runFixture('uj-004-failed-apply', {
          step: 'inject',
          changeAuthorizationId: authorizationId,
        }),
      ).toEqual(['injected']);
    });

    await test.step('the approver approves the rebaseline; WF-11 records the approval', async () => {
      const page = approver.page;
      await page.goto(baselinesPath);
      await baselineRow(page, 2).getByRole('button', { name: 'Decide' }).click();
      const dialog = page.getByRole('dialog', { name: 'Decide on baseline' });
      await dialog.getByRole('radio', { name: 'Approve' }).check();
      await dialog.getByRole('button', { name: 'Record decision' }).click();
      await expectText(page.getByRole('main'), 'Your approval was recorded.');
      const runs = await approver.api.get<{ items: { id: string; status: string }[] }>(
        `/approval-instances?subjectModule=Schedule&subjectType=ProjectBaseline&subjectId=${candidateId}&pageSize=50`,
      );
      expect(runs.items.map((r) => r.status)).toEqual(['APPROVED']);
      runId = runs.items[0]?.id ?? '';
    });

    await test.step('the application fails: the outcome is not delivered and the dispatch counts a failed attempt', async () => {
      const [attempts, error, dispatched] =
        (
          await eventually(
            () => runFixture('uj-004-failed-apply', { step: 'observe', runId }),
            (rows) => Number((rows[0] ?? '0').split('|')[0]) >= 1,
            'a failed application attempt',
          )
        )[0]?.split('|') ?? [];
      expect([Number(attempts) >= 1, error, dispatched]).toEqual([
        true,
        'InvalidOperationException',
        'false',
      ]);
    });

    await test.step('the failed application is visible and is not presented as applied', async () => {
      // WF-11: the decision is Approved; its outcome has not reached the source record.
      await approver.page.goto(`/approvals/instances/${runId}`);
      await expect(definition(approver.page, 'Status')).toHaveText('Approved');
      await expect(definition(approver.page, 'Outcome sent to the source record')).toHaveText(
        'Not yet',
      );
      // WF-03: the candidate is not active; the baseline the change was approved against still is.
      await approver.page.goto(baselinesPath);
      await expect(baselineRow(approver.page, 2)).toContainText('With AHDA for review');
      await expect(baselineRow(approver.page, 1)).toContainText('Active');
      // WF-08: the authorisation is not applied, so the request is not implemented and cannot be marked so.
      const page = requester.page;
      await page.goto(requestPath);
      const state = page.getByRole('region', { name: 'Approval and implementation' });
      await expectText(definition(state, 'Approval'), 'Approved');
      await expectText(definition(state, 'Implementation'), 'In progress');
      await expectText(state, '0 of 1 authorisations applied');
      await expectText(authorizationRow(page), 'Issued, not applied');
      await expect(authorizationRow(page)).not.toContainText('Applied');
      await expect(page.getByRole('button', { name: 'Mark implemented' })).toHaveCount(0);
      await expectText(
        page.getByRole('main'),
        'Marking it implemented becomes available when every authorisation below has been applied in its module.',
      );
      const early = await requester.api.send(
        'POST',
        `/change-requests/${requestId}/mark-implemented`,
      );
      expect([early.status, early.code]).toEqual([409, 'CHANGE_REQUEST_AUTHORIZATION_PENDING']);
      const authorization = (await changeRequest(approver.api, requestId)).authorizations[0];
      expect([authorization?.status, authorization?.appliedAt]).toEqual(['ISSUED', null]);
      expect(await baselineStates(approver.api, project.id)).toEqual(['1 ACTIVE', '2 SUBMITTED']);
    });

    await test.step('recovery: the fault is cleared and the retried application applies the authorisation once', async () => {
      expect(
        await runFixture('uj-004-failed-apply', {
          step: 'clear',
          changeAuthorizationId: authorizationId,
        }),
      ).toEqual(['cleared']);
      const delivered =
        (
          await eventually(
            () => runFixture('uj-004-failed-apply', { step: 'observe', runId }),
            (rows) => (rows[0] ?? '').endsWith('|true'),
            'the retried application to be delivered',
            180_000,
          )
        )[0]?.split('|') ?? [];
      // Delivered on a later attempt than the one that failed, with nothing left of the failure.
      expect([Number(delivered[0]) >= 2, delivered[1]]).toEqual([true, '-']);
      expect(await baselineStates(approver.api, project.id)).toEqual(['1 SUPERSEDED', '2 ACTIVE']);
      const applied = (await changeRequest(approver.api, requestId)).authorizations;
      expect(applied.map((a) => [a.status, a.appliedByUserId, a.appliedReference])).toEqual([
        ['APPLIED', approver.api.userId, `Schedule.ProjectBaseline:${candidateId}`],
      ]);
      const events = await approver.api.get<{ items: { eventType: string }[] }>(
        `/audit-events?subjectId=${requestId}&eventType=ChangeRequest.ChangeAuthorizationApplied&pageSize=50`,
      );
      expect(events.items.map((e) => e.eventType)).toEqual([
        'ChangeRequest.ChangeAuthorizationApplied',
      ]);
    });
  } finally {
    await runFixture('uj-004-failed-apply', {
      step: 'clear',
      changeAuthorizationId: authorizationId,
    });
  }

  await test.step('the screens now show the application, and only now', async () => {
    await approver.page.goto(`/approvals/instances/${runId}`);
    await expect(definition(approver.page, 'Outcome sent to the source record')).not.toHaveText(
      'Not yet',
    );
    const page = requester.page;
    await page.goto(requestPath);
    const state = page.getByRole('region', { name: 'Approval and implementation' });
    await expectText(state, '1 of 1 authorisations applied');
    await expect(authorizationRow(page).getByRole('cell').nth(2)).toHaveText('Applied');
    await page.goto(baselinesPath);
    await expect(baselineRow(page, 2)).toContainText('Active');
    await expect(baselineRow(page, 1)).toContainText('Superseded');
  });

  await test.step('stale: marking it implemented against the request as it was approved (an old If-Match) is refused', async () => {
    const stale = await requester.api.send(
      'POST',
      `/change-requests/${requestId}/mark-implemented`,
      {},
      { ifMatch: approvedVersion },
    );
    expect([stale.status, stale.code]).toEqual([412, 'PRECONDITION_FAILED']);
    expect((await changeRequest(approver.api, requestId)).status).toBe('IMPLEMENTATION');
  });

  await test.step('the requester marks the change implemented; Approved and Implemented read apart', async () => {
    const page = requester.page;
    await page.goto(requestPath);
    await confirmCommand(page, 'Mark implemented');
    await expectText(page.getByRole('main'), 'The change request is marked implemented.');
    const state = page.getByRole('region', { name: 'Approval and implementation' });
    await expectText(definition(state, 'Approval'), 'Approved');
    await expectText(definition(state, 'Implementation'), 'Implemented');
    expect((await changeRequest(approver.api, requestId)).status).toBe('IMPLEMENTED');
  });

  await test.step('replayed: the consumed authorisation applies to no other rebaseline, and marking again is refused', async () => {
    const before = await baselineStates(approver.api, project.id);
    const again = await requester.api.send(
      'POST',
      `/change-requests/${requestId}/mark-implemented`,
    );
    expect([again.status, again.code]).toEqual([409, 'INVALID_TRANSITION']);
    const another = await requester.api.post<{ id: string }>('/project-baselines', {
      projectId: project.id,
    });
    const reused = await requester.api.send('POST', `/project-baselines/${another.id}/submit`, {
      changeAuthorizationId: authorizationId,
    });
    expect([reused.status, reused.code]).toEqual([422, 'SCHEDULE_CHANGE_AUTHORIZATION_REQUIRED']);
    expect(await baselineStates(approver.api, project.id)).toEqual([...before, '3 DRAFT']);
    const authorization = (await changeRequest(approver.api, requestId)).authorizations[0];
    expect(authorization?.appliedReference).toBe(`Schedule.ProjectBaseline:${candidateId}`);
  });

  await test.step('denied scope: another department reaches neither the change request nor its commands', async () => {
    const outsider = await actor('otherDepartmentManager');
    await outsider.page.goto(requestPath);
    await expectText(
      outsider.page.getByRole('main'),
      'The record was not found, or you do not have access to it.',
    );
    const read = await outsider.api.send('GET', `/change-requests/${requestId}`);
    expect([read.status, read.code]).toEqual([404, 'NOT_FOUND']);
    // A command the outsider's role holds (CHANGE_REQUEST_REVIEW) meets the record's scope; one it does not hold, the permission.
    const review = await outsider.api.send('POST', `/change-requests/${requestId}/start-review`);
    expect([review.status, review.code]).toEqual([404, 'NOT_FOUND']);
    const close = await outsider.api.send('POST', `/change-requests/${requestId}/close`);
    expect([close.status, close.code]).toEqual([403, 'PERMISSION_DENIED']);
    expect((await changeRequest(approver.api, requestId)).status).toBe('IMPLEMENTED');
  });
});

function authorizationRow(page: Page) {
  return page
    .getByRole('table', { name: 'The authorisations the approval issued' })
    .getByRole('row')
    .filter({ hasText: 'Rebaseline' });
}

function baselineRow(page: Page, version: number) {
  // The version is a translated parameter, wrapped in U+2068/U+2069.
  const name = new RegExp(`^Version \u2068?${String(version)}\u2069? ·`);
  return page
    .getByRole('table', { name: 'Baselines of the project, latest first' })
    .getByRole('row')
    .filter({ has: page.getByRole('link', { name }) });
}
