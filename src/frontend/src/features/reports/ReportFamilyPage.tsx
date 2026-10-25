import { type ReactElement } from 'react';
import { Link, useParams } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { reportsApi } from './api/reportsApi.ts';
import { ReportList } from './components/ReportList.tsx';
import { FAMILIES, familyOfSlug } from './presentation.ts';
import { reportProblemMessage } from './problems.ts';

// SCR-131 Executive, SCR-132 Portfolio, SCR-133 Department, SCR-134 Project, SCR-135 Financial, SCR-136 Progress and
// SCR-137 Risk & Issue Reports (FG-02 §13.2): each experience lists the reports of the catalogue it presents, and each
// report opens in the shared report shell. ADR-006 fixes the reports at ten: an experience is a way in, not a report.

export function ReportFamilyPage(): ReactElement {
  const { t } = useI18n();
  const { family: slug } = useParams();
  const family = familyOfSlug(slug);
  const catalogue = useApiResource(reportsApi.catalogue);
  if (family === null) {
    return (
      <>
        <PageHeader title={t('reports.notFound.title')} />
        <EmptyState title={t('reports.notFound.description')}>
          <Link to="/reports">{t('reports.notFound.back')}</Link>
        </EmptyState>
      </>
    );
  }
  const entries = (catalogue.data ?? []).filter((entry) => entry.audienceFamily === family);
  return (
    <>
      <PageHeader title={t(FAMILIES[family].label)} description={t(FAMILIES[family].description)} />
      <p className="report-header__meta">
        <Link to="/reports">{t('reports.nav.center')}</Link>
      </p>
      {catalogue.loading ? (
        <LoadingState />
      ) : catalogue.error !== null ? (
        <ErrorState
          error={catalogue.error}
          message={reportProblemMessage(catalogue.error, t)}
          onRetry={catalogue.reload}
        />
      ) : entries.length === 0 ? (
        <EmptyState title={t('reports.family.none')} />
      ) : (
        <ReportList entries={entries} />
      )}
    </>
  );
}
