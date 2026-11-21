import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import {
  type ProjectDetail,
  type ProjectOverallProgress,
  type ProjectStageFigure,
} from '@/features/projects/api/types.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi, page } from '@/test/mockApi.ts';
import { ETAG, projectDetail, withProject, withProjectLookups } from '@/test/projectFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// Review round 8, point 02: the Overview's one overall figure, by the project's method, and the method's setting.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const NO_FIGURE: ProjectOverallProgress = {
  method: null,
  source: null,
  actualPercent: null,
  plannedPercent: null,
  asOf: null,
  currentStageCode: null,
  stages: [],
  needsSetup: true,
  methodChangeNeedsReason: false,
};

function stage(code: string, weight: number, complete: number): ProjectStageFigure {
  return {
    stageCode: code,
    name: { en: code, ar: code },
    weightPercent: weight,
    applies: true,
    plannedPercent: complete,
    percentComplete: complete,
  };
}

function withProgress(progress: Partial<ProjectOverallProgress>, gapPoints: number | null = null) {
  return projectDetail({
    status: 'ACTIVE',
    indicators: {
      gapPoints,
      onPlan: null,
      daysRemaining: null,
      actionDue: null,
      followUpReasons: [],
      progress: { ...NO_FIGURE, ...progress },
    },
  });
}

function openOverview(project: ProjectDetail): MockApi {
  const api = withProject(withProjectLookups(mockApi()), project)
    .on('GET', /^\/approval-instances$/, { body: page([]) })
    .on('GET', /^\/documents$/, { body: page([]) });
  renderApp({ path: `/projects/${project.id}`, session: sessionFor(['R02']) });
  return api;
}

async function overallSection(): Promise<HTMLElement> {
  // The Overview's card (15 Nov 2026: the Overview as a dashboard; its headline tile carries the short title).
  const heading = await screen.findByRole('heading', {
    name: en('projects.overallProgress.cardTitle'),
  });
  return heading.closest('section') ?? heading;
}

describe('the overall progress figure (review round 8, point 02)', () => {
  test("the client's worked example: 55% from the stages, labelled, with each stage's weight and the current stage", async () => {
    openOverview(
      withProgress(
        {
          method: 'STAGES',
          source: 'STAGES',
          actualPercent: 55,
          plannedPercent: 60,
          asOf: '2026-11-10',
          currentStageCode: 'EXECUTION',
          needsSetup: false,
          stages: [
            stage('BUSINESS_CASE', 10, 100),
            stage('DESIGN', 15, 100),
            stage('TENDERING', 10, 100),
            stage('EXECUTION', 50, 40),
            stage('TESTING', 10, 0),
            stage('HANDOVER', 5, 0),
          ],
        },
        -5,
      ),
    );
    const section = await overallSection();
    // The figures are the Overview's headline tile (15 Nov 2026); the card below keeps their source, date and stages.
    const tiles = screen.getByRole('list', { name: en('projects.overview.kpis') });
    expect(within(tiles).getAllByText(/55\.0%/).length).toBeGreaterThan(0);
    expect(within(tiles).getAllByText(/60\.0%/).length).toBeGreaterThan(0);
    expect(within(section).getByText(en('projects.overallProgress.source.STAGES'))).not.toBeNull();
    expect(
      within(section).getByText(en('projects.overallProgress.asOf', { date: '2026-11-10' })),
    ).not.toBeNull();
    expect(within(section).getAllByRole('listitem')).toHaveLength(6);
    expect(section.querySelector('[aria-current="step"]')?.textContent).toContain('EXECUTION');
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('a project with no method shows its declared figure as declared, and asks who may set it up', async () => {
    openOverview(withProgress({ source: 'DECLARED', actualPercent: 20, plannedPercent: 30 }));
    const section = await overallSection();
    expect(
      within(section).getByText(en('projects.overallProgress.source.DECLARED')),
    ).not.toBeNull();
    expect(within(section).getByRole('status').textContent).toContain(
      en('projects.overallProgress.setUp'),
    );
    expect(
      within(section).getByRole('button', { name: en('projects.overallProgress.chooseMethod') }),
    ).not.toBeNull();
  });

  test('after an approved update a change of method asks for a reason, then sends it under the ETag', async () => {
    const api = openOverview(
      withProgress({
        method: 'STAGES',
        source: 'STAGES',
        actualPercent: 40,
        needsSetup: false,
        methodChangeNeedsReason: true,
      }),
    );
    api.on('POST', /^\/projects\/[^/]+\/set-progress-method$/, {
      body: withProgress({ method: 'SCHEDULE', source: 'SCHEDULE', needsSetup: false }),
      headers: { ETag: '"12"' },
    });
    const section = await overallSection();
    await userEvent.click(
      within(section).getByRole('button', { name: en('projects.progressMethod.change') }),
    );
    const dialog = await screen.findByRole('dialog');
    await userEvent.click(
      within(dialog).getByRole('radio', { name: en('projects.progressMethod.methods.SCHEDULE') }),
    );
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('projects.progressMethod.confirm') }),
    );
    expect(api.requestsTo('POST', /set-progress-method$/)).toHaveLength(0);

    await userEvent.type(
      within(dialog).getByRole('textbox', {
        name: new RegExp(en('projects.progressMethod.reason')),
      }),
      'The schedule is baselined',
    );
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('projects.progressMethod.confirm') }),
    );
    const sent = api.requestsTo('POST', /set-progress-method$/)[0];
    expect(sent?.headers.get('If-Match')).toBe(ETAG);
    expect(sent?.body).toMatchObject({
      progressMethod: 'SCHEDULE',
      reason: { text: 'The schedule is baselined' },
    });
  });
});
