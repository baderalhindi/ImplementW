import { type RouteObject } from 'react-router';

import {
  ApiCataloguePage,
  InternalSystemsPage,
  IntegrationsOverviewPage,
} from './CatalogueScreens.tsx';
import { IntegrationDetailPage } from './IntegrationDetailPage.tsx';
import { InvocationDetailPage, IntegrationLogsPage } from './IntegrationLogsPage.tsx';
import { IntegrationMonitoringPage } from './IntegrationMonitoringPage.tsx';
import { EmailExchangePage, NafathPage, SsoDirectoryPage } from './ProviderScreens.tsx';

/**
 * FG-05's administration screens under /admin, at Step 16's proposed addresses, inside the R01 guard: R01 is the only
 * role that holds FG-05's permissions and IDENTITY_INTEGRATION_MANAGE (TASK-075 D-16, TASK-030), and Step 16's audience
 * for ADM-040 to ADM-048 is "ADM". The guard is navigation; the API checks each permission on every call. MOD-100 and
 * MOD-101 are dialogs on these screens, not routes (Step 16: "UNRESOLVED — do not implement route behavior").
 */
export const integrationAdminRoutes: RouteObject[] = [
  { path: 'integrations-overview', element: <IntegrationsOverviewPage /> }, // ADM-040
  { path: 'sso-directory-integration', element: <SsoDirectoryPage /> }, // ADM-041, MOD-100
  { path: 'email-exchange-integration', element: <EmailExchangePage /> }, // ADM-042
  { path: 'nafath-integration', element: <NafathPage /> }, // ADM-043, MOD-100
  { path: 'internal-system-integrations', element: <InternalSystemsPage /> }, // ADM-044
  { path: 'api-integration-catalogue', element: <ApiCataloguePage /> }, // ADM-045
  { path: 'integration-detail-configuration/:integrationId', element: <IntegrationDetailPage /> }, // ADM-046, MOD-100, MOD-101
  { path: 'integration-monitoring', element: <IntegrationMonitoringPage /> }, // ADM-047
  { path: 'integration-logs', element: <IntegrationLogsPage /> }, // ADM-048
  { path: 'integration-logs/:invocationId', element: <InvocationDetailPage /> }, // ADM-048 attempt and lineage
];
