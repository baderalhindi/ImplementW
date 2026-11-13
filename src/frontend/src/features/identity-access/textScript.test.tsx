import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { sessionFor, withIdentityAccessData } from '@/test/identityAccessFixtures.ts';
import { mockApi } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

import { checkScript, checkText } from './forms.ts';

// The 13 Nov 2026 review: an Arabic name is written in Arabic and an English name in English, as the API checks it.

const en = (key: string) => translate('en', key);

describe('a name in its own language', () => {
  test.each([
    ['مجتمع الواحة', 'ar', null],
    ['خادم البريد Exchange', 'ar', null],
    ['Oasis community', 'ar', 'NOT_ARABIC'],
    ['2026', 'ar', 'NOT_ARABIC'],
    ['Oasis community', 'en', null],
    ['مجتمع الواحة', 'en', 'NOT_ENGLISH'],
    ['Exchange خادم', 'en', 'NOT_ENGLISH'],
  ] as const)('%s as %s checks as %s', (value, script, code) => {
    expect(checkScript(value, script)).toBe(code);
  });

  test('a blank required name is still REQUIRED, not a language error', () => {
    expect(checkText('  ', { required: true, script: 'ar' })).toBe('REQUIRED');
  });

  test('a department named in the wrong languages is not sent, and each field says why', async () => {
    const api = withIdentityAccessData(mockApi());
    renderApp({ path: '/admin/organization/departments', session: sessionFor(['R01']) });
    const user = userEvent.setup();

    await user.click(
      await screen.findByRole('button', { name: en('identityAccess.departments.create') }),
    );
    const dialog = screen.getByRole('dialog');
    await user.type(within(dialog).getByRole('textbox', { name: /^Code/ }), 'DEPT-X');
    await user.type(
      within(dialog).getByRole('textbox', {
        name: new RegExp(en('identityAccess.departments.nameAr')),
      }),
      'Planning',
    );
    await user.type(
      within(dialog).getByRole('textbox', {
        name: new RegExp(en('identityAccess.departments.nameEn')),
      }),
      'التخطيط',
    );
    await user.click(within(dialog).getByRole('button', { name: en('common.actions.save') }));

    expect(within(dialog).getByText(en('common.fieldErrors.notArabic'))).toBeTruthy();
    expect(within(dialog).getByText(en('common.fieldErrors.notEnglish'))).toBeTruthy();
    expect(api.requestsTo('POST', /^\/departments$/)).toEqual([]);
  });
});
