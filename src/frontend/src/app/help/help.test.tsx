import { screen, within } from '@testing-library/react';
import { describe, expect, test } from 'vitest';

import { helpContent, LANDING_ARTICLE, ROLE_CODES } from '@/content/help/help.ts';
import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { type Language, translate } from '@/shared/i18n/i18n.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { mockApi } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';
import { findOpenMenu, openMenuTargets } from '@/test/shell.ts';

// Help & Knowledge (TASK-096): SCR-011 User Guide, SCR-012/SCR-126 FAQ and SCR-125 Knowledge Base, in both languages.

const en = (key: string) => translate('en', key);

function externalSession(): Session {
  const session = sessionFor(['R08']);
  return { ...session, user: { ...session.user, userType: 'EXTERNAL' } };
}

async function expectAccessible(): Promise<void> {
  const violations = await accessibilityViolations();
  expect(violations, describeViolations(violations)).toEqual([]);
}

describe('the Help and Knowledge menu', () => {
  test('every signed-in person has it, with the guide, the FAQ and the knowledge base', async () => {
    mockApi();
    renderApp({ path: '/help/guide', session: sessionFor(['R06']) });

    await findOpenMenu('common.menu.help');
    expect(openMenuTargets('common.menu.help')).toEqual([
      '/help/guide',
      '/help/faq',
      '/help/knowledge-base',
    ]);
  });
});

describe('SCR-011 User Guide', () => {
  test.each(ROLE_CODES)('offers %s its landing-page article, under its own role', async (role) => {
    mockApi();
    const session = role === 'R08' ? externalSession() : sessionFor([role]);
    renderApp({ path: '/help/guide', session });

    const forYou = await screen.findByRole('region', { name: en('common.help.guide.forYou') });
    const title = helpContent.en.guide[LANDING_ARTICLE[role]]?.title ?? '';
    expect(within(forYou).getByRole('link', { name: title }).getAttribute('href')).toBe(
      `/help/guide/${LANDING_ARTICLE[role]}`,
    );
  });

  test('an external user is offered only the articles written for them', async () => {
    mockApi();
    renderApp({ path: '/help/guide', session: externalSession() });

    await screen.findByRole('region', { name: en('common.help.guide.forYou') });
    expect(screen.queryByRole('region', { name: en('common.help.guide.others') })).toBeNull();
    expect(
      screen.queryByRole('link', { name: helpContent.en.guide['decide-an-approval']?.title ?? '' }),
    ).toBeNull();
  });

  test('an internal user can also read the other roles’ articles', async () => {
    mockApi();
    renderApp({ path: '/help/guide', session: sessionFor(['R06']) });

    const others = await screen.findByRole('region', { name: en('common.help.guide.others') });
    expect(
      within(others).getByRole('link', {
        name: helpContent.en.guide['register-a-project']?.title ?? '',
      }),
    ).toBeTruthy();
  });

  test.each<Language>(['en', 'ar'])(
    'an article names each control by the label the screen shows, in %s',
    async (language) => {
      mockApi();
      renderApp({ path: '/help/guide/register-a-project', language, session: sessionFor(['R04']) });

      const article = helpContent[language].guide['register-a-project'];
      await screen.findByRole('heading', { level: 1, name: article?.title ?? '' });
      const main = screen.getByRole('main');
      expect(main.textContent).not.toContain('[[');
      expect(
        within(main).getAllByText(translate(language, 'projects.create.submit')).length,
      ).toBeGreaterThan(0);
      expect(screen.getAllByRole('heading', { level: 2 }).length).toBeGreaterThanOrEqual(
        article?.sections.length ?? 0,
      );
      await expectAccessible();
    },
  );

  test('an unknown article, or one not written for an external user, is not found', async () => {
    mockApi();
    renderApp({ path: '/help/guide/decide-an-approval', session: externalSession() });
    expect(await screen.findByRole('heading', { name: en('common.notFound.title') })).toBeTruthy();
  });
});

describe('SCR-012/SCR-126 FAQ and SCR-125 Knowledge Base', () => {
  test.each<Language>(['en', 'ar'])('the FAQ lists every question, in %s', async (language) => {
    mockApi();
    renderApp({ path: '/help/faq', language, session: sessionFor(['R02']) });

    await screen.findByRole('heading', {
      level: 1,
      name: translate(language, 'common.help.faq.title'),
    });
    const questions = Object.values(helpContent[language].faq).flatMap((group) =>
      Object.keys(group.entries),
    );
    expect(document.querySelectorAll('details.help-faq')).toHaveLength(questions.length);
    expect(screen.getByRole('main').textContent).not.toContain('[[');
    await expectAccessible();
  });

  test.each<Language>(['en', 'ar'])(
    'the knowledge base defines every term, in %s',
    async (language) => {
      mockApi();
      renderApp({ path: '/help/knowledge-base', language, session: sessionFor(['R02']) });

      await screen.findByRole('heading', {
        level: 1,
        name: translate(language, 'common.help.knowledgeBase.title'),
      });
      expect(document.querySelectorAll('.help-glossary dt')).toHaveLength(
        Object.keys(helpContent[language].knowledgeBase).length,
      );
      expect(screen.getByRole('main').textContent).not.toContain('[[');
      await expectAccessible();
    },
  );
});
