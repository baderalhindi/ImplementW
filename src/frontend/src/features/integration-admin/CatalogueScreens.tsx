import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type IntegrationSummary } from './api/types.ts';
import { integrationDetailPath, internalSystems } from './catalogue.ts';
import { CataloguePage } from './components/CataloguePage.tsx';
import { Code } from './components/Display.tsx';
import { IntegrationTable } from './components/IntegrationTable.tsx';
import { activationTone, labelIn } from './presentation.ts';

/**
 * ADM-040 Integrations Overview (FG-05 §13): the catalogue's runtime summary — each integration's activation, its
 * availability and freshness, last success and failure, and live alerts — each opening ADM-046.
 */
export function IntegrationsOverviewPage(): ReactElement {
  const { t } = useI18n();
  const title = t('integrationAdmin.overview.title');
  return (
    <CataloguePage title={title} description={t('integrationAdmin.overview.description')}>
      {(catalogue) => (
        <IntegrationTable
          items={catalogue.items}
          caption={title}
          emptyTitle={t('integrationAdmin.overview.empty')}
        />
      )}
    </CataloguePage>
  );
}

/**
 * ADM-044 Internal-System Integrations: every registered integration that is not one of the provider screens'
 * (ADM-041 to ADM-043). None is registered yet (TASK-075 D-15); one registered later appears here with no code change.
 */
export function InternalSystemsPage(): ReactElement {
  const { t } = useI18n();
  const title = t('integrationAdmin.internalSystems.title');
  return (
    <CataloguePage title={title} description={t('integrationAdmin.internalSystems.description')}>
      {(catalogue) => (
        <IntegrationTable
          items={internalSystems(catalogue.items)}
          caption={title}
          emptyTitle={t('integrationAdmin.internalSystems.empty')}
        />
      )}
    </CataloguePage>
  );
}

const DIRECTION_ORDER = ['INBOUND', 'OUTBOUND', 'BIDIRECTIONAL'];

function byDirectionThenCode(a: IntegrationSummary, b: IntegrationSummary): number {
  return (
    DIRECTION_ORDER.indexOf(a.direction) - DIRECTION_ORDER.indexOf(b.direction) ||
    a.code.localeCompare(b.code)
  );
}

/**
 * ADM-045 API Integration Catalogue: each integration as a contract reference — direction, kind, its instances and the
 * configuration key naming each endpoint (never the endpoint itself), and its activation. Health is ADM-047's.
 */
export function ApiCataloguePage(): ReactElement {
  const { t, language } = useI18n();
  const title = t('integrationAdmin.apiCatalogue.title');
  return (
    <CataloguePage title={title} description={t('integrationAdmin.apiCatalogue.description')}>
      {(catalogue) =>
        catalogue.items.length === 0 ? (
          <EmptyState title={t('integrationAdmin.overview.empty')} />
        ) : (
          <TableContainer caption={title}>
            <thead>
              <tr>
                <th scope="col">{t('integrationAdmin.fields.integration')}</th>
                <th scope="col">{t('integrationAdmin.fields.direction')}</th>
                <th scope="col">{t('integrationAdmin.fields.kind')}</th>
                <th scope="col">{t('integrationAdmin.fields.instance')}</th>
                <th scope="col">{t('integrationAdmin.fields.endpointKey')}</th>
                <th scope="col">{t('integrationAdmin.fields.activation')}</th>
                <th scope="col">{t('common.table.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {[...catalogue.items].sort(byDirectionThenCode).map((item) => {
                const name = labelIn(item.name, language);
                return (
                  <tr key={item.id} data-integration={item.code}>
                    <th scope="row">
                      {name}
                      <span className="cell__aside">
                        <Code>{item.code}</Code>
                      </span>
                    </th>
                    <td>{t(`integrationAdmin.direction.${item.direction}`)}</td>
                    <td>{t(`integrationAdmin.kind.${item.integrationKind}`)}</td>
                    <td>
                      {item.instances.length === 0
                        ? t('integrationAdmin.values.noInstance')
                        : item.instances.map((instance) => (
                            <Code key={instance.id}>{instance.code}</Code>
                          ))}
                    </td>
                    <td>
                      {item.instances.map((instance) => (
                        <Code key={instance.id}>{instance.endpointConfigurationKey}</Code>
                      ))}
                    </td>
                    <td>
                      <StatusBadge
                        label={t(`integrationAdmin.activationStatus.${item.status}`)}
                        tone={activationTone(item.status)}
                      />
                    </td>
                    <td className="cell--actions">
                      <Link to={integrationDetailPath(item.id)}>
                        {t('integrationAdmin.list.open')}{' '}
                        <span className="visually-hidden">{name}</span>
                      </Link>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </TableContainer>
        )
      }
    </CataloguePage>
  );
}
