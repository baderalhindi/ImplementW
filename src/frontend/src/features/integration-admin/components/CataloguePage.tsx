import { type ReactElement, type ReactNode } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { type IntegrationCatalogueView } from '../api/types.ts';
import { integrationProblemMessage } from '../problems.ts';
import { useCatalogue } from '../useCatalogue.ts';
import { useRefresh } from '../useRefresh.ts';

import { EnvironmentBanner } from './Display.tsx';

interface CataloguePageProps {
  title: string;
  description?: string;
  actions?: ReactNode;
  /** Read the catalogue again this often, in milliseconds, keeping what is shown until the next read lands. */
  refreshEvery?: number;
  children: (catalogue: IntegrationCatalogueView) => ReactNode;
}

/**
 * An FG-05 screen: its heading, the environment the catalogue describes (always visible, FG-05 §13.1), then its
 * content. A catalogue that cannot be read is said so with *Try again*, never shown as an empty list: a source failure is
 * not zero (§13.1). With `refreshEvery` the screen stays live (ADM-047, TASK-092).
 */
export function CataloguePage({
  title,
  description,
  actions,
  refreshEvery,
  children,
}: CataloguePageProps): ReactElement {
  const { t } = useI18n();
  const catalogue = useCatalogue();
  useRefresh(catalogue.reload, refreshEvery);
  return (
    <>
      <PageHeader title={title} description={description} actions={actions} />
      {catalogue.data === undefined ? (
        catalogue.error === null ? (
          <LoadingState />
        ) : (
          <ErrorState error={catalogue.error}
            message={integrationProblemMessage(catalogue.error, t)}
            onRetry={catalogue.reload}
          />
        )
      ) : (
        <>
          <EnvironmentBanner environment={catalogue.data.environment} />
          {children(catalogue.data)}
        </>
      )}
    </>
  );
}
