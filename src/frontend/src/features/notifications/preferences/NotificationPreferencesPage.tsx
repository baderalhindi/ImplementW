import { type ReactElement, type SyntheticEvent, useId, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader, TableContainer } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { notificationsApi } from '../api/notificationsApi.ts';
import {
  type NotificationChannel,
  type NotificationChannelPreference,
  type NotificationFamilyPreference,
  type NotificationPreferenceChange,
  type NotificationPreferenceSet,
} from '../api/types.ts';
import { canChangePreference, CHANNELS } from '../presentation.ts';
import { isConfigurationMissing, notificationProblemMessage } from '../problems.ts';

/** Module-level, so useApiResource reads once per mount. */
function loadPreferences(signal: AbortSignal): Promise<NotificationPreferenceSet> {
  return notificationsApi.getPreferences(signal);
}

/** Refusals after which the stored choices may differ from what the screen shows, so the set is read again. */
const STALE_CODES = new Set([
  'NOTIFICATION_PREFERENCE_NOT_CONFIGURABLE',
  'NOTIFICATION_PREFERENCE_INVALID',
  'PRECONDITION_FAILED',
]);

/**
 * SCR-154 Notification Preferences: one row per family of the routing in force, one column per channel. In-app is
 * always on and a mandatory family's channels are fixed (ADR-004 as amended); the rest — e-mail and SMS of a family that
 * is not mandatory — the recipient turns on or off. Only the cells changed are sent, all or none; WF-15 reads the stored
 * choice at routing and at every send attempt, so a change applies to the next notification (TASK-039 D-11).
 */
export function NotificationPreferencesPage(): ReactElement {
  const { t } = useI18n();
  const preferences = useApiResource(loadPreferences);
  return (
    <>
      <PageHeader
        title={t('notifications.preferences.title')}
        description={t('notifications.preferences.description')}
      />
      {preferences.loading && <LoadingState label={t('notifications.preferences.loading')} />}
      {preferences.error !== null &&
        (isConfigurationMissing(preferences.error) ? (
          <EmptyState title={t('notifications.preferences.notConfigured')} />
        ) : (
          <ErrorState
            message={notificationProblemMessage(preferences.error, t)}
            onRetry={preferences.reload}
          />
        ))}
      {preferences.data !== undefined && <PreferencesForm initial={preferences.data} />}
    </>
  );
}

function cellKey(eventFamilyCode: string, channel: NotificationChannel): string {
  return `${eventFamilyCode}:${channel}`;
}

function PreferencesForm({ initial }: { initial: NotificationPreferenceSet }): ReactElement {
  const { t } = useI18n();
  const save = useSaveAction(notificationProblemMessage);
  const [stored, setStored] = useState(initial);
  const [edits, setEdits] = useState<Partial<Record<string, boolean>>>({});
  const [notice, setNotice] = useState<Notice | null>(null);

  const changes: NotificationPreferenceChange[] = stored.families.flatMap((family) =>
    family.channels
      .filter((preference) => {
        const edited = edits[cellKey(family.eventFamilyCode, preference.channel)];
        return edited !== undefined && edited !== preference.isEnabled;
      })
      .map((preference) => ({
        eventFamilyCode: family.eventFamilyCode,
        channel: preference.channel,
        isEnabled: !preference.isEnabled,
      })),
  );

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    setNotice(null);
    if (changes.length === 0) {
      setNotice({ tone: 'warning', message: t('notifications.preferences.unchanged') });
      return;
    }
    const result = await save.run(() => notificationsApi.updatePreferences(changes));
    if (result.ok) {
      setStored(result.value);
      setEdits({});
      setNotice({ tone: 'success', message: t('notifications.preferences.saved') });
    } else if (result.error instanceof ApiError && STALE_CODES.has(result.error.code)) {
      // The refusal stays on screen; the choices under it are the stored ones again.
      try {
        setStored(await notificationsApi.getPreferences());
        setEdits({});
      } catch {
        // The refusal already says what failed; the screen keeps the choices it had.
      }
    }
  };

  return (
    <form className="form form--wide" onSubmit={(event) => void submit(event)} noValidate>
      <PageNotice notice={notice} />
      <FormAlert message={save.formError} />
      <TableContainer caption={t('notifications.preferences.title')}>
        <thead>
          <tr>
            <th scope="col">{t('notifications.preferences.family')}</th>
            {CHANNELS.map((channel) => (
              <th key={channel} scope="col">
                {t(`notifications.channel.${channel}`)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {stored.families.map((family) => (
            <FamilyRow
              key={family.eventFamilyCode}
              family={family}
              edits={edits}
              disabled={save.saving}
              onToggle={(channel, isEnabled) => {
                setNotice(null);
                setEdits((current) => ({
                  ...current,
                  [cellKey(family.eventFamilyCode, channel)]: isEnabled,
                }));
              }}
            />
          ))}
        </tbody>
      </TableContainer>
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('notifications.preferences.save')}
        </button>
        <button
          type="button"
          className="button"
          disabled={save.saving || changes.length === 0}
          onClick={() => {
            setEdits({});
          }}
        >
          {t('notifications.preferences.reset')}
        </button>
      </div>
    </form>
  );
}

function FamilyRow({
  family,
  edits,
  disabled,
  onToggle,
}: {
  family: NotificationFamilyPreference;
  edits: Partial<Record<string, boolean>>;
  disabled: boolean;
  onToggle: (channel: NotificationChannel, isEnabled: boolean) => void;
}): ReactElement {
  const { t, language } = useI18n();
  const name = family.label[language];
  return (
    <tr>
      <th scope="row">
        <span className="preference__family">{name}</span>
        {family.isMandatory && (
          <StatusBadge label={t('notifications.preferences.mandatory')} tone="info" />
        )}
      </th>
      {CHANNELS.map((channel) => {
        const preference = family.channels.find((candidate) => candidate.channel === channel);
        return (
          <td key={channel}>
            {preference === undefined ? (
              <span className="cell__aside">{t('notifications.preferences.notRouted')}</span>
            ) : (
              <PreferenceToggle
                family={family}
                preference={preference}
                checked={edits[cellKey(family.eventFamilyCode, channel)] ?? preference.isEnabled}
                label={t('notifications.preferences.toggle', {
                  family: name,
                  channel: t(`notifications.channel.${channel}`),
                })}
                disabled={disabled}
                onChange={(isEnabled) => {
                  onToggle(channel, isEnabled);
                }}
              />
            )}
          </td>
        );
      })}
    </tr>
  );
}

function PreferenceToggle({
  family,
  preference,
  checked,
  label,
  disabled,
  onChange,
}: {
  family: NotificationFamilyPreference;
  preference: NotificationChannelPreference;
  checked: boolean;
  label: string;
  disabled: boolean;
  onChange: (isEnabled: boolean) => void;
}): ReactElement {
  const { t } = useI18n();
  const reasonId = useId();
  const changeable = canChangePreference(family, preference);
  const reason = changeable
    ? null
    : preference.channel === 'IN_APP'
      ? t('notifications.preferences.alwaysOn')
      : family.isMandatory
        ? t('notifications.preferences.fixedByMandatory')
        : t('notifications.preferences.fixed');
  return (
    <div className="preference">
      <input
        type="checkbox"
        aria-label={label}
        checked={checked}
        disabled={disabled || !changeable}
        aria-describedby={reason === null ? undefined : reasonId}
        onChange={(event) => {
          onChange(event.target.checked);
        }}
      />
      {reason !== null && (
        <span id={reasonId} className="cell__aside">
          {reason}
        </span>
      )}
    </div>
  );
}
