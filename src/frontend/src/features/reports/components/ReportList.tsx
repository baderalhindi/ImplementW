import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type ReportCatalogueEntry } from '../api/types.ts';
import { reportPath } from '../presentation.ts';

/** Catalogue entries: each report's name, opening it, and what it is for (FG-02 §13.1) — never business data. */
export function ReportList({ entries }: { entries: ReportCatalogueEntry[] }): ReactElement {
  const { t, language } = useI18n();
  return (
    <ul className="report-list">
      {entries.map((entry) => (
        <li key={entry.code} className="report-list__item" data-report={entry.code}>
          <Link to={reportPath(entry.audienceFamily, entry.code)}>{entry.name[language]}</Link>
          {entry.description !== null && <p>{entry.description[language]}</p>}
          <p className="report-list__meta">
            {t('reports.header.version', { version: entry.versionNo })}
            {entry.allowsSavedViews && ` · ${t('reports.center.savedViewsAllowed')}`}
          </p>
        </li>
      ))}
    </ul>
  );
}
