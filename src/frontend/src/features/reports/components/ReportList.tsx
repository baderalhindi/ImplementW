import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type ReportCatalogueEntry } from '../api/types.ts';
import { reportPath } from '../presentation.ts';

/** A report sheet: the card's mark, drawn in the current text colour. */
function ReportIcon(): ReactElement {
  return (
    <svg viewBox="0 0 24 24" width="22" height="22" aria-hidden="true" focusable="false">
      <path
        d="M7 3h7l5 5v12a1 1 0 0 1-1 1H7a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1Zm7 0v5h5M9 17v-3m3 3v-5m3 5v-2"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

/**
 * Catalogue entries as cards: each report's name, opening it, and what it is for (FG-02 §13.1) — never business data.
 * The whole card opens the report; the link's accessible name stays the report's name alone.
 */
export function ReportList({ entries }: { entries: ReportCatalogueEntry[] }): ReactElement {
  const { t, language } = useI18n();
  return (
    <ul className="report-list">
      {entries.map((entry) => (
        <li key={entry.code} className="report-list__item" data-report={entry.code}>
          <span className="report-list__icon">
            <ReportIcon />
          </span>
          <div className="report-list__body">
            <Link className="report-list__link" to={reportPath(entry.audienceFamily, entry.code)}>
              {entry.name[language]}
            </Link>
            {entry.description !== null && (
              <p className="report-list__description">{entry.description[language]}</p>
            )}
            <p className="report-list__meta">
              <span className="report-list__tag">
                {t('reports.header.version', { version: entry.versionNo })}
              </span>
              {entry.allowsSavedViews && (
                <span className="report-list__tag report-list__tag--accent">
                  {t('reports.center.savedViewsAllowed')}
                </span>
              )}
            </p>
          </div>
          <span className="report-list__chevron" aria-hidden="true">
            ›
          </span>
        </li>
      ))}
    </ul>
  );
}
