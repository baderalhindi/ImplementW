import { cleanup, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import {
  type NotificationPreferenceChange,
  type NotificationPreferenceSet,
} from '@/features/notifications/api/types.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import { notificationSession, preferenceSet, withInbox } from '@/test/notificationFixtures.ts';
import { type MockApi, mockApi, problem } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// SCR-154 Notification Preferences: TASK-040's third acceptance criterion and the ADR-004 amendment.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

function toggle(family: string, channel: 'IN_APP' | 'EMAIL' | 'SMS'): HTMLInputElement {
  return screen.getByRole('checkbox', {
    name: en('notifications.preferences.toggle', {
      family,
      channel: en(`notifications.channel.${channel}`),
    }),
  });
}

/** A stored preference set that PUT changes and GET reads back, as the API keeps it (TASK-039 D-11). */
function storedPreferences(initial = preferenceSet()): {
  api: MockApi;
  stored: () => NotificationPreferenceSet;
} {
  let stored = initial;
  const api = withInbox(mockApi())
    .on('GET', /^\/notification-preferences$/, () => ({ body: stored }))
    .on('PUT', /^\/notification-preferences$/, (request) => {
      const { preferences } = request.body as { preferences: NotificationPreferenceChange[] };
      stored = {
        families: stored.families.map((family) => ({
          ...family,
          channels: family.channels.map((channel) => {
            const change = preferences.find(
              (candidate) =>
                candidate.eventFamilyCode === family.eventFamilyCode &&
                candidate.channel === channel.channel,
            );
            return change === undefined ? channel : { ...channel, isEnabled: change.isEnabled };
          }),
        })),
      };
      return { body: stored };
    });
  return { api, stored: () => stored };
}

describe('SCR-154 Notification Preferences', () => {
  test('in-app is always on, a mandatory family is fixed, and the rest can be changed', async () => {
    storedPreferences();
    renderApp({ path: '/notifications/preferences', session: notificationSession() });
    await screen.findByRole('table', { name: en('notifications.preferences.title') });

    for (const family of [
      'Approval tasks',
      'Security alerts',
      'Budget alerts',
      'Concern escalations',
    ]) {
      const inApp = toggle(family, 'IN_APP');
      expect(inApp.checked).toBe(true);
      expect(inApp.disabled).toBe(true);
      expect(
        document.getElementById(inApp.getAttribute('aria-describedby') ?? '')?.textContent,
      ).toBe(en('notifications.preferences.alwaysOn'));
    }
    // ADR-004 as amended: SMS of a family that is not mandatory can be turned off.
    expect(toggle('Approval tasks', 'SMS').disabled).toBe(false);
    expect(toggle('Approval tasks', 'EMAIL').disabled).toBe(false);
    expect(toggle('Budget alerts', 'EMAIL').checked).toBe(false);
    // Security and critical escalation families are mandatory on every channel.
    const mandatorySms = toggle('Security alerts', 'SMS');
    expect(mandatorySms.disabled).toBe(true);
    expect(mandatorySms.checked).toBe(true);
    expect(
      document.getElementById(mandatorySms.getAttribute('aria-describedby') ?? '')?.textContent,
    ).toBe(en('notifications.preferences.fixedByMandatory'));
    expect(toggle('Concern escalations', 'EMAIL').disabled).toBe(true);
    expect(screen.getAllByText(en('notifications.preferences.mandatory'))).toHaveLength(2);
    // A channel the family is not routed to has no control.
    const securityRow = screen.getByRole('rowheader', { name: /Security alerts/ }).closest('tr');
    expect(
      within(securityRow as HTMLElement).getByText(en('notifications.preferences.notRouted')),
    ).toBeTruthy();
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('in-app stays locked even if the API were to offer it', async () => {
    const set = preferenceSet();
    const approval = set.families[0];
    if (approval?.channels[0] !== undefined) {
      approval.channels[0].isConfigurable = true;
    }
    storedPreferences(set);
    renderApp({ path: '/notifications/preferences', session: notificationSession() });
    await screen.findByRole('table');
    expect(toggle('Approval tasks', 'IN_APP').disabled).toBe(true);
  });

  test('turning SMS off sends only that change, and the choice persists on the next visit', async () => {
    const { api, stored } = storedPreferences();
    renderApp({ path: '/notifications/preferences', session: notificationSession() });
    const user = userEvent.setup();
    await screen.findByRole('table');

    await user.click(toggle('Approval tasks', 'SMS'));
    await user.click(toggle('Budget alerts', 'EMAIL'));
    await user.click(toggle('Budget alerts', 'EMAIL')); // back as it was: not a change
    await user.click(screen.getByRole('button', { name: en('notifications.preferences.save') }));

    expect(await screen.findByText(en('notifications.preferences.saved'))).toBeTruthy();
    const [put] = api.requestsTo('PUT', /^\/notification-preferences$/);
    expect(put?.body).toEqual({
      preferences: [{ eventFamilyCode: 'APPROVAL_TASK', channel: 'SMS', isEnabled: false }],
    });
    expect(toggle('Approval tasks', 'SMS').checked).toBe(false);
    expect(
      stored().families[0]?.channels.find((channel) => channel.channel === 'SMS')?.isEnabled,
    ).toBe(false);

    // A new visit reads the stored choice back from the API.
    cleanup();
    renderApp({ path: '/notifications/preferences', session: notificationSession() });
    await screen.findByRole('table');
    expect(toggle('Approval tasks', 'SMS').checked).toBe(false);
    expect(toggle('Approval tasks', 'EMAIL').checked).toBe(true);
  });

  test('saving with nothing changed sends nothing', async () => {
    const { api } = storedPreferences();
    renderApp({ path: '/notifications/preferences', session: notificationSession() });
    const user = userEvent.setup();
    await screen.findByRole('table');
    await user.click(screen.getByRole('button', { name: en('notifications.preferences.save') }));
    expect(await screen.findByText(en('notifications.preferences.unchanged'))).toBeTruthy();
    expect(api.requestsTo('PUT', /^\/notification-preferences$/)).toHaveLength(0);
  });

  test('a refused change is explained and the stored choices are shown again', async () => {
    const { api } = storedPreferences();
    api.on(
      'PUT',
      /^\/notification-preferences$/,
      problem(422, 'NOTIFICATION_PREFERENCE_NOT_CONFIGURABLE'),
    );
    renderApp({ path: '/notifications/preferences', session: notificationSession() });
    const user = userEvent.setup();
    await screen.findByRole('table');

    await user.click(toggle('Approval tasks', 'EMAIL'));
    expect(toggle('Approval tasks', 'EMAIL').checked).toBe(false);
    await user.click(screen.getByRole('button', { name: en('notifications.preferences.save') }));

    expect((await screen.findByRole('alert')).textContent).toBe(
      en('notifications.problems.preferenceNotConfigurable'),
    );
    expect(toggle('Approval tasks', 'EMAIL').checked).toBe(true);
    expect(api.requestsTo('GET', /^\/notification-preferences$/).length).toBeGreaterThanOrEqual(2);
  });

  test('undo restores the stored choices without sending anything', async () => {
    const { api } = storedPreferences();
    renderApp({ path: '/notifications/preferences', session: notificationSession() });
    const user = userEvent.setup();
    await screen.findByRole('table');
    await user.click(toggle('Approval tasks', 'SMS'));
    await user.click(screen.getByRole('button', { name: en('notifications.preferences.reset') }));
    expect(toggle('Approval tasks', 'SMS').checked).toBe(true);
    expect(api.requestsTo('PUT', /^\/notification-preferences$/)).toHaveLength(0);
  });

  test('without a routing configuration the screen says preferences are not available yet', async () => {
    withInbox(mockApi()).on(
      'GET',
      /^\/notification-preferences$/,
      problem(422, 'CONFIGURATION_MISSING'),
    );
    renderApp({ path: '/notifications/preferences', session: notificationSession() });
    expect(await screen.findByText(en('notifications.preferences.notConfigured'))).toBeTruthy();
    expect(screen.queryByRole('checkbox')).toBeNull();
  });

  test('in Arabic the families read in Arabic and the layout is right-to-left', async () => {
    storedPreferences();
    renderApp({
      path: '/notifications/preferences',
      session: notificationSession(),
      language: 'ar',
    });
    expect(await screen.findByRole('rowheader', { name: /تنبيهات أمنية/ })).toBeTruthy();
    expect(document.documentElement.dir).toBe('rtl');
  });
});
