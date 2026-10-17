import { eventually, type PlatformApi } from '../support/api.ts';
import { mailRelay, runFixture } from '../support/fixtures.ts';
import { expect, expectText, test, unique, utcDate } from '../support/journey.ts';
import { activeProject } from '../support/projects.ts';
import { itemId } from '../support/reference.ts';

// UJ-007 — Reminder ownership (Step 18; WF-15; BPK-012; ICD-09).
// Participants: Source owner and communication operator.
// Procedure: trigger an approved source-domain reminder condition; inspect communication delivery and repeat its technical
// dispatch. Expected: the source owns business conditions; WF-15 owns communication runtime. Delivery or retry does not change
// business state. Negative extension: denied scope and stale/replayed request; confirm source-appropriate failure without
// unauthorized mutation.
// The source condition is WF-07's concern escalation, the one source that publishes a NotificationIntent today
// (notification-runtime.md §5; management-concern.md): WF-07 decides it and routes it to its role, WF-15 delivers it in-app
// and by e-mail. The mail relay stand-in refuses mail (451) to make the e-mail delivery fail; QA's "timeout" fixture
// uj-007-retry-due stands in for the back-off elapsing, so the outage outlasts the retries; the operator then redrives it.

interface Concern {
  id: string;
  status: string;
  revisionNo: number;
  openEscalation: { id: string } | null;
}

interface Delivery {
  id: string;
  recipientUserId: string;
  channel: 'IN_APP' | 'EMAIL' | 'SMS';
  status: string;
  attemptCount: number;
  failureReason: string | null;
}

interface IntentDetail {
  intent: { id: string; sourceReference: string; status: string };
  deliveries: Delivery[];
}

test('UJ-007 the source owns the escalation, WF-15 owns its delivery, and neither failure nor retry moves the record', async ({
  actor,
}) => {
  test.setTimeout(10 * 60_000);
  const owner = await actor('projectManager');
  const department = await actor('departmentManager');
  const portfolio = await actor('portfolioManager');
  const operator = await actor('administrator');
  const project = await activeProject({
    creator: department.api,
    reviewer: department.api,
    approver: portfolio.api,
    manager: owner.api,
  });
  const title = unique('UJ-007 Access road closed');
  const concernBody = {
    title: { text: title, language: 'en' },
    description: {
      text: 'The utility contractor closed the only access road to the site.',
      language: 'en',
    },
    categoryItemId: await itemId(portfolio.api, 'CONCERN_CATEGORY', 'E2E_SITE_ACCESS'),
    priorityItemId: await itemId(portfolio.api, 'PRIORITY', 'E2E_HIGH'),
    targetResolutionDate: utcDate(30),
  };
  const concern = await owner.api.post<Concern>('/management-concerns', {
    projectId: project.id,
    concernType: 'ISSUE',
    impacts: null,
    ...concernBody,
  });
  const concernPath = `/projects/${project.id}/issues-challenges/${concern.id}`;
  const read = (api: PlatformApi = owner.api) =>
    api.ok<Concern>('GET', `/management-concerns/${concern.id}`);
  const before = await read();

  let escalationId = '';
  let intentId = '';
  let email: Delivery | undefined;
  const intent = () => operator.api.get<IntentDetail>(`/notification-intents/${intentId}`);
  const emailDelivery = async () => (await intent()).deliveries.find((d) => d.channel === 'EMAIL');

  try {
    await mailRelay.refuse();

    await test.step('the source owner escalates the issue: WF-07 decides the condition and routes it to its role', async () => {
      const page = owner.page;
      await page.goto(concernPath);
      await page.getByRole('button', { name: 'Escalate', exact: true }).click();
      // The dialog carries no accessible name (its heading is not referenced), so it is found by what it says.
      const dialog = page.getByRole('dialog').filter({ hasText: 'Escalation is not approval' });
      await expectText(dialog.getByRole('heading'), `Escalate “${title}”`);
      await dialog
        .getByLabel(/^Reason for escalation/)
        .fill('Blocked for two weeks; we need the utility owner to reopen the road.');
      await dialog.getByRole('button', { name: 'Escalate' }).click();
      await expectText(page.getByRole('main'), 'It was escalated.');
      const escalated = (await read()).body;
      escalationId = escalated.openEscalation?.id ?? '';
      expect(escalationId).not.toBe('');
      // An escalation is an overlay: the issue keeps its status and revision.
      expect([escalated.status, escalated.revisionNo]).toEqual([
        before.body.status,
        before.body.revisionNo,
      ]);
    });

    await test.step('WF-15 receives the intent, sends in-app to the Department Manager, and the e-mail fails at the relay', async () => {
      const intents = await eventually(
        () =>
          operator.api.get<{ items: IntentDetail['intent'][] }>(
            '/notification-intents?sourceModule=ManagementConcern&eventFamilyCode=CONCERN_ESCALATION&pageSize=100',
          ),
        (page) => page.items.some((i) => i.sourceReference === escalationId),
        `the notification intent of escalation ${escalationId}`,
      );
      intentId = intents.items.find((i) => i.sourceReference === escalationId)?.id ?? '';
      const routed = await eventually(
        intent,
        (detail) => detail.deliveries.some((d) => d.channel === 'EMAIL' && d.attemptCount >= 1),
        'the first e-mail attempt',
      );
      expect(
        routed.deliveries
          .map((d) => [d.channel, d.recipientUserId, d.status])
          .sort((a, b) => String(a[0]).localeCompare(String(b[0]))),
      ).toEqual([
        ['EMAIL', department.api.userId, 'FAILED'],
        ['IN_APP', department.api.userId, 'SENT'],
      ]);
      email = routed.deliveries.find((d) => d.channel === 'EMAIL');
      expect(email?.failureReason).not.toBeNull();
    });

    await test.step('the recipient sees the in-app notification, and the e-mail as failed and retrying', async () => {
      const page = department.page;
      await page.goto('/notifications');
      await expect(
        page.getByRole('row').filter({ hasText: 'Escalation 1 of an issue or challenge' }).first(),
      ).toBeVisible();
      await page.goto('/notifications/history?channel=EMAIL');
      await expect(
        page.getByRole('row').filter({ hasText: 'Failed, will retry' }).first(),
      ).toBeVisible();
    });

    await test.step('the communication operator inspects the failed attempt: WF-15 retries it, not the source', async () => {
      const page = operator.page;
      const invocations = await eventually(
        () =>
          operator.api.get<{ items: { id: string; status: string }[] }>(
            `/integration-invocations?idempotencyKey=${String(email?.id)}&pageSize=50`,
          ),
        (p) => p.items.length >= 1,
        'the e-mail attempt in integration monitoring',
      );
      await page.goto(`/admin/integration-logs/${invocations.items[0]?.id ?? ''}`);
      await expectText(page.getByRole('main'), 'Retry scheduled');
      const early = await operator.api.send(
        'POST',
        `/notification-deliveries/${String(email?.id)}/redrive`,
      );
      expect([early.status, early.code]).toEqual([409, 'INVALID_TRANSITION']);
    });

    await test.step('the outage outlasts the retries (QA fixture uj-007-retry-due): the e-mail is dead-lettered', async () => {
      const dead = await eventually(
        async () =>
          (await runFixture('uj-007-retry-due', { deliveryId: String(email?.id) }))[0] ?? '',
        (row) => row.startsWith('DEAD_LETTER|'),
        'the e-mail delivery to be dead-lettered',
        240_000,
      );
      expect(dead).toBe('DEAD_LETTER|5');
      await department.page.goto('/notifications/history?channel=EMAIL');
      await expect(
        department.page.getByRole('row').filter({ hasText: 'Not delivered' }).first(),
      ).toBeVisible();
    });

    await test.step('delivery failure changed no business state: the issue and its escalation are as the source left them', async () => {
      const now = await read();
      expect([now.body.status, now.body.revisionNo, now.etag]).toEqual([
        before.body.status,
        before.body.revisionNo,
        before.etag,
      ]);
      const escalation = await owner.api.get<{ status: string }>(
        `/concern-escalations/${escalationId}`,
      );
      expect(escalation.status).toBe('OPEN');
      expect(await deliveredMail(concern.id)).toEqual([]);
    });

    await test.step('the relay recovers and the operator repeats the technical dispatch: the e-mail is sent once', async () => {
      await mailRelay.accept();
      const redriven = await operator.api.send<Delivery>(
        'POST',
        `/notification-deliveries/${String(email?.id)}/redrive`,
      );
      expect([redriven.status, redriven.body.status]).toEqual([200, 'PENDING']);
      await eventually(
        emailDelivery,
        (d) => d?.status === 'SENT',
        'the redriven e-mail to be sent',
      );
      const mail = await eventually(
        () => deliveredMail(concern.id),
        (messages) => messages.length > 0,
        'the e-mail at the relay',
      );
      // The relay records each RCPT TO path as given, <address>.
      expect(mail.map((m) => m.to)).toEqual([['<e2e.department@pmplatform.local>']]);
      expect((await intent()).intent.status).toBe('COMPLETED');
      await department.page.goto('/notifications/history?channel=EMAIL');
      await expect(
        department.page.getByRole('row').filter({ hasText: 'Sent' }).first(),
      ).toBeVisible();
    });
  } finally {
    await mailRelay.accept();
  }

  await test.step('replayed: a second redrive and the repeated escalation request publish nothing more', async () => {
    const again = await operator.api.send(
      'POST',
      `/notification-deliveries/${String(email?.id)}/redrive`,
    );
    expect([again.status, again.code]).toEqual([409, 'INVALID_TRANSITION']);
    const another = await owner.api.send('POST', '/concern-escalations', {
      managementConcernId: concern.id,
      reason: { text: 'Still blocked.', language: 'en' },
    });
    expect([another.status, another.code]).toEqual([409, 'CONCERN_ESCALATION_OPEN']);
    const intents = await operator.api.get<{ items: IntentDetail['intent'][] }>(
      '/notification-intents?sourceModule=ManagementConcern&eventFamilyCode=CONCERN_ESCALATION&pageSize=100',
    );
    expect(intents.items.filter((i) => i.sourceReference === escalationId)).toHaveLength(1);
    expect((await intent()).deliveries.map((d) => d.status).sort()).toEqual(['SENT', 'SENT']);
    expect(await deliveredMail(concern.id)).toHaveLength(1);
  });

  await test.step('stale: an edit of the issue against a version it no longer is (old If-Match) is refused', async () => {
    const current = await read();
    await owner.api.ok(
      'PUT',
      `/management-concerns/${concern.id}`,
      {
        ...concernBody,
        description: {
          text: 'The road stays closed until the utility owner acts.',
          language: 'en',
        },
      },
      { ifMatch: current.etag },
    );
    const stale = await owner.api.send(
      'PUT',
      `/management-concerns/${concern.id}`,
      { ...concernBody, title: { text: `${title} (stale)`, language: 'en' } },
      { ifMatch: current.etag },
    );
    expect([stale.status, stale.code]).toEqual([412, 'PRECONDITION_FAILED']);
    const after = await owner.api.get<{ title: { text: string } }>(
      `/management-concerns/${concern.id}`,
    );
    expect(after.title.text).toBe(title);
  });

  await test.step('denied scope: another department is neither told nor let in, and only the operator sees or repeats delivery', async () => {
    const outsider = await actor('otherDepartmentManager');
    expect((await intent()).deliveries.map((d) => d.recipientUserId)).not.toContain(
      outsider.api.userId,
    );
    await outsider.page.goto(concernPath);
    await expectText(
      outsider.page.getByRole('main'),
      'The project was not found, or you do not have access to it.',
    );
    const hidden = await outsider.api.send('GET', `/management-concerns/${concern.id}`);
    expect([hidden.status, hidden.code]).toEqual([404, 'NOT_FOUND']);
    const ops = await department.api.send('GET', `/notification-intents/${intentId}`);
    expect([ops.status, ops.code]).toEqual([403, 'PERMISSION_DENIED']);
    const redrive = await owner.api.send(
      'POST',
      `/notification-deliveries/${String(email?.id)}/redrive`,
    );
    expect([redrive.status, redrive.code]).toEqual([403, 'PERMISSION_DENIED']);
    const escalation = await owner.api.get<{ status: string }>(
      `/concern-escalations/${escalationId}`,
    );
    expect(escalation.status).toBe('OPEN');
  });
});

/** The messages the relay accepted for this concern: the e-mail's deep link names it. */
async function deliveredMail(concernId: string) {
  return (await mailRelay.messages()).filter((m) => mailText(m.data).includes(concernId));
}

/** The message as text: quoted-printable soft breaks and escapes undone, base64 parts decoded. */
function mailText(data: string): string {
  const unfolded = data
    .replace(/=\r?\n/g, '')
    .replace(/=([0-9A-F]{2})/g, (_, hex: string) => String.fromCharCode(parseInt(hex, 16)));
  const base64 =
    /Content-Transfer-Encoding: base64\r?\n(?:.*\r?\n)*?\r?\n([A-Za-z0-9+/=\r\n]+)/i.exec(data);
  return base64?.[1] === undefined
    ? unfolded
    : `${unfolded}\n${Buffer.from(base64[1].replace(/\s/g, ''), 'base64').toString('utf8')}`;
}
