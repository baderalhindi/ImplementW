import { waitFor } from '@testing-library/react';
import { describe, expect, test } from 'vitest';

import { type NarrativeText } from '@/features/projects/api/types.ts';
import {
  AUDIT_REVIEWER_ACCESS,
  auditEvent,
  BUSINESS_EVENT_ID,
  eventDetail,
  withAudit,
} from '@/test/auditActivityFixtures.ts';
import { changeRequest, PAVING_ID, withChangeRequests } from '@/test/changeRequestFixtures.ts';
import {
  concern,
  concernProject,
  CULVERT_ID,
  managerSession,
  withConcerns,
} from '@/test/concernFixtures.ts';
import {
  DOCUMENT_ID,
  documentDetail,
  documentSession,
  link,
  withDocumentLookups,
} from '@/test/documentFixtures.ts';
import { mockApi } from '@/test/mockApi.ts';
import { notification, notificationSession, withInbox } from '@/test/notificationFixtures.ts';
import { submission, withProgress } from '@/test/progressFixtures.ts';
import {
  ENTITY_USER_ID,
  entitySession,
  projectDetail,
  PROJECT_ID,
  reviewerSession,
  withProject,
  withProjectLookups,
} from '@/test/projectFixtures.ts';
import { renderApp } from '@/test/renderApp.tsx';
import { FLOODING_ID, risk, withRisks } from '@/test/riskFixtures.ts';
import { task, withTasks } from '@/test/taskFixtures.ts';

// TASK-077 (CTL-11), the workbook's check: "manually attempt a stored-XSS payload in a comment field and confirm it renders
// as inert text, not executable markup". Every screen here shows free text a person typed, as the API stores it — exactly as
// entered (ADR-012). Each is rendered with a payload in that text; the payload must be read back as text, and no element or
// event handler it describes may exist in the page. output-encoding-checklist.md lists the render paths this sweeps.

const PAYLOAD =
  '<img src=x onerror=alert(1) data-xss><script data-xss>alert(2)</script><a data-xss href="javascript:alert(3)">x</a>';

const planted = (language: NarrativeText['language'] = 'EN'): NarrativeText => ({
  text: PAYLOAD,
  language,
});

const activeProject = () =>
  projectDetail({
    status: 'ACTIVE',
    projectManagerUserId: ENTITY_USER_ID,
    activatedAt: '2026-10-01T10:00:00Z',
  });

const projectApi = (project = activeProject()) =>
  withProject(withProjectLookups(mockApi()), project);

interface Screen {
  name: string;
  render: () => void;
}

const screens: Screen[] = [
  {
    name: 'SCR-080 Risk Register (risk title)',
    render: () => {
      withRisks(projectApi(), { risks: [risk({ title: planted() })] });
      renderApp({ path: `/projects/${PROJECT_ID}/risks`, session: entitySession() });
    },
  },
  {
    name: 'SCR-082 Risk Detail (risk description)',
    render: () => {
      withRisks(projectApi(), { risks: [risk({ description: planted() })] });
      renderApp({
        path: `/projects/${PROJECT_ID}/risks/${FLOODING_ID}`,
        session: entitySession(),
      });
    },
  },
  {
    name: 'Issue register (concern title)',
    render: () => {
      withConcerns(projectApi(concernProject()), { concerns: [concern({ title: planted() })] });
      renderApp({ path: `/projects/${PROJECT_ID}/issues-challenges`, session: managerSession() });
    },
  },
  {
    name: 'Issue detail (concern description)',
    render: () => {
      withConcerns(projectApi(concernProject()), {
        concerns: [concern({ description: planted() })],
      });
      renderApp({
        path: `/projects/${PROJECT_ID}/issues-challenges/${CULVERT_ID}`,
        session: managerSession(),
      });
    },
  },
  {
    name: 'Change request detail (justification)',
    render: () => {
      withChangeRequests(projectApi(concernProject()), {
        requests: [changeRequest({ justification: planted() })],
      });
      renderApp({ path: `/change-requests/${PAVING_ID}`, session: managerSession() });
    },
  },
  {
    name: 'Task board (task title)',
    render: () => {
      withTasks(projectApi(), { tasks: [task({ title: planted() })] });
      renderApp({ path: `/projects/${PROJECT_ID}/tasks`, session: entitySession() });
    },
  },
  {
    name: 'Progress (narrative, Arabic entry)',
    render: () => {
      withProgress(projectApi(), { submissions: [submission({ narrative: planted('AR') })] });
      renderApp({ path: `/projects/${PROJECT_ID}/progress`, session: entitySession() });
    },
  },
  {
    name: 'Document detail (title and description)',
    render: () => {
      withDocumentLookups(mockApi())
        .on('GET', /^\/documents\/[^/]+$/, {
          body: documentDetail({ title: planted(), description: planted() }),
          headers: { ETag: '"7"' },
        })
        .on('GET', /^\/documents\/[^/]+\/links$/, { body: [link()] });
      renderApp({ path: `/documents/${DOCUMENT_ID}`, session: documentSession() });
    },
  },
  {
    name: 'SCR-150 All Notifications (subject)',
    render: () => {
      withInbox(mockApi(), { items: [notification({ subject: PAYLOAD })] });
      renderApp({ path: '/notifications', session: notificationSession() });
    },
  },
  {
    name: 'ADM-051 audit event (old and new values)',
    render: () => {
      const detail = eventDetail(auditEvent());
      withAudit(projectApi(), { access: AUDIT_REVIEWER_ACCESS }).on(
        'GET',
        /^\/audit-events\/[^/]+$/,
        {
          body: {
            ...detail,
            attributes: [{ name: 'title', oldValue: 'Flooding', newValue: PAYLOAD }],
          },
        },
      );
      renderApp({ path: `/admin/audit-trail/${BUSINESS_EVENT_ID}`, session: reviewerSession() });
    },
  },
];

/** The page's text as a person reads it; translated parameters are wrapped in bidi isolates, which are not part of it. */
const pageText = () => document.body.textContent.replace(/[⁨⁩]/g, '');

/** Every event-handler attribute in the page. React attaches listeners and never writes one, so any found came from data. */
function handlerAttributes(): string[] {
  return Array.from(document.querySelectorAll('*')).flatMap((element) =>
    element
      .getAttributeNames()
      .filter((name) => name.toLowerCase().startsWith('on'))
      .map((name) => `${element.tagName.toLowerCase()}[${name}]`),
  );
}

describe('stored text renders as inert text on every screen that shows it (TASK-077)', () => {
  test.each(screens)('$name', async ({ render }) => {
    render();

    await waitFor(() => {
      expect(pageText()).toContain(PAYLOAD);
    });
    expect(document.querySelectorAll('[data-xss]')).toHaveLength(0);
    expect(document.querySelectorAll('script')).toHaveLength(0);
    expect(
      Array.from(document.querySelectorAll('[href], [src]')).filter((element) =>
        /^\s*javascript:/i.test(element.getAttribute('href') ?? element.getAttribute('src') ?? ''),
      ),
    ).toHaveLength(0);
    expect(handlerAttributes()).toEqual([]);
  });
});
