import { type ReactElement, useCallback, useState } from 'react';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { integrationAdminApi } from './api/integrationAdminApi.ts';
import { type IntegrationSummary } from './api/types.ts';
import { MESSAGING_CODES, NAFATH_CODES, SSO_DIRECTORY_CODES, withCodes } from './catalogue.ts';
import { CataloguePage } from './components/CataloguePage.tsx';
import { Section } from './components/Display.tsx';
import {
  DirectorySettings,
  IdentityConfiguration,
  NafathSettings,
  SingleSignOnSettings,
} from './components/IdentityConfiguration.tsx';
import { IntegrationTable } from './components/IntegrationTable.tsx';
import { MetricsPanel } from './components/MetricsPanel.tsx';
import { TestConnectionDialog } from './components/TestConnectionDialog.tsx';
import { labelIn } from './presentation.ts';
import { integrationProblemMessage } from './problems.ts';

/** The header's MOD-100 button and its dialog. */
function useTestConnectionAction(): { button: ReactElement; dialog: ReactElement } {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  return {
    button: (
      <button
        type="button"
        className="button button--primary"
        onClick={() => {
          setOpen(true);
        }}
      >
        {t('integrationAdmin.actions.testConnection')}
      </button>
    ),
    dialog: (
      <TestConnectionDialog
        open={open}
        onClose={() => {
          setOpen(false);
        }}
      />
    ),
  };
}

/**
 * ADM-041 SSO / Directory Integration: the sign-in identity runtime — directory, SSO and the MFA provider — with each
 * one's availability and freshness, the directory and SSO settings in effect (secrets only as set or not), and MOD-100.
 */
export function SsoDirectoryPage(): ReactElement {
  const { t } = useI18n();
  const title = t('integrationAdmin.ssoDirectory.title');
  const test = useTestConnectionAction();
  return (
    <CataloguePage
      title={title}
      description={t('integrationAdmin.ssoDirectory.description')}
      actions={test.button}
    >
      {(catalogue) => (
        <>
          <Section title={t('integrationAdmin.sections.runtime')}>
            <IntegrationTable
              items={withCodes(catalogue.items, SSO_DIRECTORY_CODES)}
              caption={t('integrationAdmin.sections.runtime')}
              emptyTitle={t('integrationAdmin.overview.empty')}
            />
          </Section>
          <IdentityConfiguration>
            {(status) => (
              <>
                <DirectorySettings status={status.directory} />
                <SingleSignOnSettings status={status.singleSignOn} />
              </>
            )}
          </IdentityConfiguration>
          {test.dialog}
        </>
      )}
    </CataloguePage>
  );
}

/**
 * ADM-042 Email / Exchange Integration: the communication providers WF-15 sends through — the Exchange relay and, per
 * ADR-004, the SMS provider — with their availability and freshness and ADR-004's measures over the last day. WF-15
 * owns notifications, recipients and delivery history; this page is the providers' technical health. No connection
 * test exists for them (F-1).
 */
export function EmailExchangePage(): ReactElement {
  const { t } = useI18n();
  const title = t('integrationAdmin.emailExchange.title');
  return (
    <CataloguePage title={title} description={t('integrationAdmin.emailExchange.description')}>
      {(catalogue) => {
        const providers = withCodes(catalogue.items, MESSAGING_CODES);
        return (
          <>
            <p className="integration-note">{t('integrationAdmin.emailExchange.boundary')}</p>
            <Section title={t('integrationAdmin.sections.runtime')}>
              <IntegrationTable
                items={providers}
                caption={t('integrationAdmin.sections.runtime')}
                emptyTitle={t('integrationAdmin.overview.empty')}
              />
            </Section>
            {providers.map((provider) => (
              <ProviderMeasures key={provider.id} integration={provider} />
            ))}
          </>
        );
      }}
    </CataloguePage>
  );
}

function ProviderMeasures({ integration }: { integration: IntegrationSummary }): ReactElement {
  const { t, language } = useI18n();
  const load = useCallback(
    (signal: AbortSignal) => integrationAdminApi.integration(integration.id, {}, signal),
    [integration.id],
  );
  const detail = useApiResource(load);
  return (
    <Section
      title={t('integrationAdmin.measures.titleFor', { name: labelIn(integration.name, language) })}
    >
      {detail.loading ? (
        <LoadingState />
      ) : detail.error !== null || detail.data === undefined ? (
        <ErrorState
          error={detail.error}
          message={integrationProblemMessage(detail.error, t)}
          onRetry={detail.reload}
        />
      ) : (
        <MetricsPanel
          metrics={detail.data.data.metrics}
          instances={detail.data.data.integration.instances}
        />
      )}
    </Section>
  );
}

/**
 * ADM-043 Nafath Integration ("if implemented"): Nafath is built behind `Identity:Nafath:Enabled` (TASK-068), so the
 * page says whether it is switched on here, then its availability and freshness, its settings and MOD-100.
 */
export function NafathPage(): ReactElement {
  const { t } = useI18n();
  const title = t('integrationAdmin.nafath.title');
  const test = useTestConnectionAction();
  return (
    <CataloguePage
      title={title}
      description={t('integrationAdmin.nafath.description')}
      actions={test.button}
    >
      {(catalogue) => (
        <>
          <Section title={t('integrationAdmin.sections.runtime')}>
            <IntegrationTable
              items={withCodes(catalogue.items, NAFATH_CODES)}
              caption={t('integrationAdmin.sections.runtime')}
              emptyTitle={t('integrationAdmin.overview.empty')}
            />
          </Section>
          <IdentityConfiguration>
            {(status) => <NafathSettings status={status.identityVerification} />}
          </IdentityConfiguration>
          {test.dialog}
        </>
      )}
    </CataloguePage>
  );
}
