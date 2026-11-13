import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { StatusBadge, type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import {
  type ImportIssueLevel,
  type ImportPreview,
  type ImportRowIssue,
} from '../api/importTypes.ts';

const LEVEL_TONES: Record<ImportIssueLevel, StatusTone> = {
  ERROR: 'negative',
  WARNING: 'warning',
  INFO: 'info',
};

/** The issue codes the import names in words; any other is shown as its code. */
const ISSUE_CODES = [
  'NAME_UNMATCHED',
  'OWNER_UNMATCHED',
  'INITIATIVE_OUTSIDE_PORTFOLIO',
  'END_BEFORE_START',
  'NOT_POSITIVE',
  'PERCENT_OUT_OF_RANGE',
  'DUPLICATE_IN_FILE',
  'MANAGER_NOT_FOUND',
  'MANAGER_NOT_R04',
  'OUTSIDE_SCOPE',
  'STATUS_TO_CHOOSE',
  'PROJECT_CLOSED',
  'SAME_NAME_AND_ENTITY',
  'ACTIVE_PAST_END',
  'COMPLETED_BELOW_100',
  'COMPLETED_FUTURE_END',
  'FUTURE_WITH_PROGRESS',
  'EXCEPTIONAL_VALUE',
  'GOVERNED_FIELD_NOT_UPDATED',
  'KEPT_BLANK',
  'PERCENT_SCALED',
  'NO_CHANGE',
  'SKIPPED_BY_IMPORTER',
  'COLUMN_IGNORED',
  'COLUMN_MISSING',
  'TEMPLATE_OUTDATED',
  'REQUIRED',
  'MALFORMED',
  'MAX_LENGTH',
  'NOT_ARABIC',
  'NOT_ENGLISH',
  'ENUM_VALUE',
  'NOT_ALLOWED',
] as const;

type IssueCode = (typeof ISSUE_CODES)[number];

function isIssueCode(code: string): code is IssueCode {
  return (ISSUE_CODES as readonly string[]).includes(code);
}

export function IssueLine({ issue }: { issue: ImportRowIssue }): ReactElement {
  const { t } = useI18n();
  return (
    <li>
      <StatusBadge
        label={t(`projects.import.level.${issue.level}`)}
        tone={LEVEL_TONES[issue.level]}
      />{' '}
      {issue.column !== '' && (
        <code dir="ltr" className="cell--ltr">
          {issue.column}
        </code>
      )}{' '}
      {isIssueCode(issue.code) ? t(`projects.import.issue.${issue.code}`) : issue.code}
    </li>
  );
}

/**
 * What a workbook would do, row by row (R3-4): the action, the issues by level and, for an update, each field before and after. A row an
 * importer may skip carries a checkbox.
 */
export function ImportPreviewView({
  preview,
  skipped,
  onToggleSkip,
}: {
  preview: ImportPreview;
  skipped?: string[];
  onToggleSkip?: (reference: string) => void;
}): ReactElement {
  const { t } = useI18n();
  return (
    <>
      <p role="status">
        {t('projects.import.summary', {
          rows: preview.rowCount,
          create: preview.createCount,
          update: preview.updateCount,
          skip: preview.skipCount,
          errors: preview.errorCount,
          warnings: preview.warningCount,
        })}
      </p>
      {preview.fileIssues.length > 0 && (
        <div className="notice notice--warning">
          <p>{t('projects.import.fileLevel')}</p>
          <ul>
            {preview.fileIssues.map((issue) => (
              <IssueLine key={`${issue.column}-${issue.code}`} issue={issue} />
            ))}
          </ul>
        </div>
      )}
      <TableContainer caption={t('projects.import.rowsCaption')}>
        <thead>
          <tr>
            <th scope="col">{t('projects.import.row')}</th>
            <th scope="col">{t('projects.import.reference')}</th>
            <th scope="col">{t('projects.import.name')}</th>
            <th scope="col">{t('projects.import.action')}</th>
            <th scope="col">{t('projects.import.issues')}</th>
            <th scope="col">{t('projects.import.changes')}</th>
          </tr>
        </thead>
        <tbody>
          {preview.rows.map((row) => (
            <tr key={row.row}>
              <td dir="ltr">{row.row}</td>
              <td dir="ltr" className="cell--ltr">
                {row.legacyReference ?? ''}
              </td>
              <td dir="auto">{row.title ?? ''}</td>
              <td>
                {t(`projects.import.actionName.${row.action}`)}
                {onToggleSkip !== undefined && row.legacyReference !== null && (
                  <label className="cell__aside">
                    <input
                      type="checkbox"
                      checked={skipped?.includes(row.legacyReference) ?? false}
                      onChange={() => {
                        if (row.legacyReference !== null) {
                          onToggleSkip(row.legacyReference);
                        }
                      }}
                    />{' '}
                    {t('projects.import.skip')}
                  </label>
                )}
              </td>
              <td>
                <ul className="issue-list">
                  {row.issues.map((issue, index) => (
                    <IssueLine
                      key={`${issue.column}-${issue.code}-${String(index)}`}
                      issue={issue}
                    />
                  ))}
                </ul>
              </td>
              <td>
                <ul className="issue-list">
                  {row.changes.map((change) => (
                    <li key={change.field}>
                      <code dir="ltr">{change.field}</code>:{' '}
                      <span dir="auto">{change.before ?? '—'}</span> →{' '}
                      <span dir="auto">{change.after ?? '—'}</span>
                    </li>
                  ))}
                </ul>
              </td>
            </tr>
          ))}
        </tbody>
      </TableContainer>
    </>
  );
}
