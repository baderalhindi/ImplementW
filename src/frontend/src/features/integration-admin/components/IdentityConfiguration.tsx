import { type ReactElement, type ReactNode, useCallback } from 'react';

import { Detail } from '@/features/projects/components/Detail.tsx';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { integrationAdminApi } from '../api/integrationAdminApi.ts';
import {
  type DirectoryIntegrationStatus,
  type IdentityIntegrationStatus,
  type IdentityVerificationIntegrationStatus,
  type SingleSignOnIntegrationStatus,
} from '../api/types.ts';
import { integrationProblemMessage } from '../problems.ts';

import { Code, Section } from './Display.tsx';

// ADM-041 and ADM-043's configuration reference (TASK-028 D-10, TASK-068 D-11): which settings are in effect, read from
// the secret and configuration stores by the API. A secret reaches the browser only as whether it is set; each field
// below is named, so nothing else the response might carry is ever rendered. Editing stays in the approved stores
// (FG-05 §8.4), so this is a reference, not a form.

/** Whether a setting is present: the only thing shown of a secret. */
function Presence({ set }: { set: boolean }): ReactElement {
  const { t } = useI18n();
  return (
    <StatusBadge
      label={t(
        set ? 'integrationAdmin.configuration.set' : 'integrationAdmin.configuration.notSet',
      )}
      tone={set ? 'info' : 'neutral'}
    />
  );
}

function YesNo({ value }: { value: boolean }): ReactElement {
  const { t } = useI18n();
  return <>{t(value ? 'common.values.yes' : 'common.values.no')}</>;
}

function Value({ value }: { value: string | null }): ReactElement {
  const { t } = useI18n();
  return value === null || value === '' ? <>{t('common.values.none')}</> : <Code>{value}</Code>;
}

function Scopes({ scopes }: { scopes: readonly string[] }): ReactElement {
  return <Value value={scopes.length === 0 ? null : scopes.join(' ')} />;
}

/** Reads `GET /identity-integration` once and renders the part `children` names. */
export function IdentityConfiguration({
  children,
}: {
  children: (status: IdentityIntegrationStatus) => ReactNode;
}): ReactElement {
  const { t } = useI18n();
  const load = useCallback(
    (signal: AbortSignal) => integrationAdminApi.identityIntegration(signal),
    [],
  );
  const status = useApiResource(load);
  if (status.loading) {
    return <LoadingState />;
  }
  if (status.error !== null || status.data === undefined) {
    return (
      <ErrorState
        error={status.error}
        message={integrationProblemMessage(status.error, t)}
        onRetry={status.reload}
      />
    );
  }
  return <>{children(status.data)}</>;
}

export function DirectorySettings({
  status,
}: {
  status: DirectoryIntegrationStatus;
}): ReactElement {
  const { t } = useI18n();
  return (
    <Section title={t('integrationAdmin.configuration.directory')}>
      <dl className="details" data-configuration="directory">
        <Detail term={t('integrationAdmin.configuration.configured')}>
          <YesNo value={status.isConfigured} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.directoryUrl')}>
          <Presence set={status.urlConfigured} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.bindDn')}>
          <Presence set={status.bindDnConfigured} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.bindPassword')}>
          <Presence set={status.bindPasswordConfigured} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.transportSecurity')}>
          <YesNo value={status.usesTransportSecurity} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.usernameAttribute')}>
          <Value value={status.usernameAttribute} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.subjectAttribute')}>
          <Value value={status.subjectAttribute} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.jobTitleAttribute')}>
          <Value value={status.jobTitleAttribute} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.departmentAttribute')}>
          <Value value={status.departmentAttribute} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.managerAttribute')}>
          <Value value={status.managerAttribute} />
        </Detail>
      </dl>
    </Section>
  );
}

export function SingleSignOnSettings({
  status,
}: {
  status: SingleSignOnIntegrationStatus;
}): ReactElement {
  const { t } = useI18n();
  return (
    <Section title={t('integrationAdmin.configuration.singleSignOn')}>
      <dl className="details" data-configuration="single-sign-on">
        <Detail term={t('integrationAdmin.configuration.configured')}>
          <YesNo value={status.isConfigured} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.authority')}>
          <Value value={status.authority} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.clientId')}>
          <Value value={status.clientId} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.clientSecret')}>
          <Presence set={status.clientSecretConfigured} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.callbackUrl')}>
          <Value value={status.callbackUrl} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.subjectClaim')}>
          <Value value={status.subjectClaim} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.scopes')}>
          <Scopes scopes={status.scopes} />
        </Detail>
      </dl>
    </Section>
  );
}

export function NafathSettings({
  status,
}: {
  status: IdentityVerificationIntegrationStatus;
}): ReactElement {
  const { t } = useI18n();
  return (
    <Section title={t('integrationAdmin.configuration.nafath')}>
      {!status.isEnabled && (
        <p className="integration-note" data-nafath-disabled="true">
          {t('integrationAdmin.nafath.switchedOff')}
        </p>
      )}
      <dl className="details" data-configuration="nafath">
        <Detail term={t('integrationAdmin.configuration.nafathEnabled')}>
          <YesNo value={status.isEnabled} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.configured')}>
          <YesNo value={status.isConfigured} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.authority')}>
          <Value value={status.authority} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.clientId')}>
          <Presence set={status.clientIdConfigured} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.clientSecret')}>
          <Presence set={status.clientSecretConfigured} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.callbackUrl')}>
          <Value value={status.callbackUrl} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.applicationId')}>
          <Value value={status.applicationId} />
        </Detail>
        <Detail term={t('integrationAdmin.configuration.scopes')}>
          <Scopes scopes={status.scopes} />
        </Detail>
      </dl>
    </Section>
  );
}
