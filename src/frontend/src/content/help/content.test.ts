import { describe, expect, test } from 'vitest';

import { resources } from '@/shared/i18n/resources.ts';

import {
  GUIDE_ARTICLES,
  helpContent,
  isForRoles,
  LABEL_REFERENCE,
  LANDING_ARTICLE,
  ROLE_CODES,
} from './help.ts';

// The help content's own rules (TASK-096): both languages carry the same articles, questions and terms; every role's
// Blueprint §20.2 landing page has a guide article; every interface label the text names exists in both languages.

function strings(node: unknown): string[] {
  if (typeof node === 'string') {
    return [node];
  }
  if (typeof node !== 'object' || node === null) {
    return [];
  }
  return Object.values(node).flatMap(strings);
}

function hasLabel(key: string): boolean {
  return (['en', 'ar'] as const).every((language) => {
    let node: unknown = resources[language];
    for (const segment of key.split('.')) {
      node =
        typeof node === 'object' && node !== null
          ? (node as Record<string, unknown>)[segment]
          : undefined;
    }
    return typeof node === 'string';
  });
}

const { en, ar } = helpContent;

describe('the two languages carry the same help', () => {
  test('the same guide articles, each with as many sections and steps in Arabic as in English', () => {
    expect(Object.keys(ar.guide).sort()).toEqual(Object.keys(en.guide).sort());
    for (const [id, english] of Object.entries(en.guide)) {
      const arabic = ar.guide[id];
      expect(arabic?.sections.length, id).toBe(english.sections.length);
      english.sections.forEach((section, at) => {
        const counterpart = arabic?.sections[at];
        expect(counterpart?.steps?.length, `${id} §${String(at + 1)} steps`).toBe(
          section.steps?.length,
        );
        expect(counterpart?.note === undefined, `${id} §${String(at + 1)} note`).toBe(
          section.note === undefined,
        );
      });
    }
  });

  test('the same FAQ groups and questions, and the same knowledge-base terms', () => {
    expect(Object.keys(ar.faq)).toEqual(Object.keys(en.faq));
    for (const [groupId, group] of Object.entries(en.faq)) {
      expect(Object.keys(ar.faq[groupId]?.entries ?? {}), groupId).toEqual(
        Object.keys(group.entries),
      );
    }
    expect(Object.keys(ar.knowledgeBase).sort()).toEqual(Object.keys(en.knowledgeBase).sort());
  });

  test('no text is empty, and the Arabic text is Arabic', () => {
    for (const text of [...strings(en), ...strings(ar)]) {
      expect(text.trim(), text).not.toBe('');
    }
    for (const text of strings(ar)) {
      const prose = text.replace(LABEL_REFERENCE, '');
      expect(/[؀-ۿ]/.test(prose) || prose.trim() === '', text).toBe(true);
    }
  });
});

describe('the guide covers every role', () => {
  test('every role R01–R08 has a landing-page article, in both languages, written for that role', () => {
    for (const role of ROLE_CODES) {
      const id = LANDING_ARTICLE[role];
      const meta = GUIDE_ARTICLES.find((article) => article.id === id);
      expect(meta, role).toBeDefined();
      expect(meta !== undefined && isForRoles(meta, [role]), role).toBe(true);
      expect(en.guide[id], `${role} en`).toBeDefined();
      expect(ar.guide[id], `${role} ar`).toBeDefined();
    }
  });

  test('every listed article has text, and every text has a listing', () => {
    expect(GUIDE_ARTICLES.map((article) => article.id).sort()).toEqual(
      Object.keys(en.guide).sort(),
    );
  });
});

test('every interface label the help names, and every related screen label, exists in both languages', () => {
  const references = [...strings(en), ...strings(ar)].flatMap((text) =>
    [...text.matchAll(LABEL_REFERENCE)].map((match) => match[1] ?? ''),
  );
  const related = GUIDE_ARTICLES.flatMap((article) => article.related.map((item) => item.label));
  for (const key of [...references, ...related]) {
    expect(hasLabel(key), key).toBe(true);
  }
});
