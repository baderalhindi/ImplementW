import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type DocumentSummary } from '../api/types.ts';
import { documentTone, languageTag, shortId } from '../presentation.ts';
import { type DocumentLookups } from '../useDocumentLookups.ts';

import { ScanStateBadge } from './ScanState.tsx';

interface DocumentTableProps {
  caption: string;
  documents: DocumentSummary[];
  lookups: DocumentLookups;
  /** False on SCR-121, where every row is the page's project. */
  showProject: boolean;
}

/** SCR-120–122's rows: each document with its latest version's scan state, so a quarantined file is seen in the list. */
export function DocumentTable({
  caption,
  documents,
  lookups,
  showProject,
}: DocumentTableProps): ReactElement {
  const { t, formatDateTime } = useI18n();
  const person = usePersonNames(documents.map((document) => document.ownerUserId));
  return (
    <TableContainer caption={caption}>
      <thead>
        <tr>
          <th scope="col">{t('documents.fields.title')}</th>
          <th scope="col">{t('documents.fields.documentType')}</th>
          <th scope="col">{t('documents.fields.classification')}</th>
          {showProject && <th scope="col">{t('documents.fields.project')}</th>}
          <th scope="col">{t('documents.fields.latestVersion')}</th>
          <th scope="col">{t('documents.fields.status')}</th>
          <th scope="col">{t('documents.fields.owner')}</th>
          <th scope="col">{t('documents.fields.updatedAt')}</th>
        </tr>
      </thead>
      <tbody>
        {documents.map((document) => (
          <tr
            key={document.id}
            className={document.latestScanState === 'QUARANTINED' ? 'row--flagged' : undefined}
          >
            <td>
              <Link
                to={`/documents/${document.id}`}
                lang={languageTag(document.title.language)}
                dir="auto"
              >
                {document.title.text}
              </Link>
            </td>
            <td>{lookups.label(document.documentTypeItemId)}</td>
            <td>{lookups.label(document.dataClassificationItemId)}</td>
            {showProject && (
              <td>
                {document.projectId === null ? (
                  t('documents.values.noProject')
                ) : (
                  <Link to={`/documents/projects/${document.projectId}`}>
                    {t('documents.values.project', { id: shortId(document.projectId) })}
                  </Link>
                )}
              </td>
            )}
            <td>
              {document.latestVersionNo === null || document.latestScanState === null ? (
                '—'
              ) : (
                <span className="version-cell">
                  {t('documents.fields.versionNo', { number: document.latestVersionNo })}{' '}
                  <ScanStateBadge state={document.latestScanState} />
                </span>
              )}
            </td>
            <td>
              <StatusBadge
                label={t(`documents.status.${document.status}`)}
                tone={documentTone(document.status)}
              />
            </td>
            <td>{person(document.ownerUserId)}</td>
            <td>{formatDateTime(document.updatedAt)}</td>
          </tr>
        ))}
      </tbody>
    </TableContainer>
  );
}
