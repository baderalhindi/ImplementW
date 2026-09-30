import { type ReactElement, useCallback } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { documentsApi, evidenceApi } from '../api/documentsApi.ts';
import { type BusinessLinkDetail } from '../api/types.ts';
import { DownloadButton } from '../components/DownloadButton.tsx';
import { ScanStateBadge } from '../components/ScanState.tsx';
import { shortId } from '../presentation.ts';
import { documentProblemMessage } from '../problems.ts';
import { type DocumentLookups } from '../useDocumentLookups.ts';

/**
 * Where the document is used: every link, ended ones included, and the evidence pinned on each (TASK-037 D-8, D-12).
 * A record is named by its module, type and id: no module publishes a title for it yet (TASK-038 F-2).
 */
export function DocumentLinks({
  documentId,
  lookups,
}: {
  documentId: string;
  lookups: DocumentLookups;
}): ReactElement {
  const { t } = useI18n();
  const load = useCallback(
    (signal: AbortSignal) => documentsApi.listLinks(documentId, signal),
    [documentId],
  );
  const links = useApiResource(load);
  return (
    <section className="section" aria-labelledby="document-links">
      <h2 id="document-links">{t('documents.links.title')}</h2>
      {links.loading && <LoadingState label={t('documents.links.loading')} />}
      {links.error !== null && (
        <ErrorState message={documentProblemMessage(links.error, t)} onRetry={links.reload} />
      )}
      {links.data !== undefined &&
        (links.data.length === 0 ? (
          <EmptyState title={t('documents.links.empty')} />
        ) : (
          <LinkTable links={links.data} lookups={lookups} />
        ))}
    </section>
  );
}

function LinkTable({
  links,
  lookups,
}: {
  links: BusinessLinkDetail[];
  lookups: DocumentLookups;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  const person = usePersonNames(links.map((link) => link.linkedByUserId));
  return (
    <TableContainer caption={t('documents.links.title')}>
      <thead>
        <tr>
          <th scope="col">{t('documents.links.target')}</th>
          <th scope="col">{t('documents.links.role')}</th>
          <th scope="col">{t('documents.links.linked')}</th>
          <th scope="col">{t('documents.links.ended')}</th>
          <th scope="col">{t('documents.links.evidence')}</th>
        </tr>
      </thead>
      <tbody>
        {links.map((link) => {
          const targetId = `link-${link.id}`;
          return (
            <tr key={link.id}>
              <td id={targetId} dir="ltr">
                {link.target.module} · {link.target.type} · {shortId(link.target.id)}
              </td>
              <td>{t(`documents.linkRole.${link.linkRole}`)}</td>
              <td>
                {person(link.linkedByUserId)}
                <span className="cell__aside">{formatDateTime(link.linkedAt)}</span>
              </td>
              <td>{link.unlinkedAt === null ? '—' : formatDateTime(link.unlinkedAt)}</td>
              <td>
                {link.evidence.length === 0 ? (
                  t('common.values.none')
                ) : (
                  <ul className="evidence-list">
                    {link.evidence.map((evidence) => (
                      <li key={evidence.id}>
                        <span>
                          {lookups.label(evidence.evidenceTypeItemId)} ·{' '}
                          {t('documents.fields.versionNo', { number: evidence.versionNo })}{' '}
                          <ScanStateBadge state={evidence.scanState} />{' '}
                          <StatusBadge
                            label={t(
                              evidence.satisfies
                                ? 'documents.evidence.satisfies'
                                : `documents.evidence.${evidence.status}`,
                            )}
                            tone={evidence.satisfies ? 'positive' : 'neutral'}
                          />
                        </span>
                        <DownloadButton
                          label={t('documents.evidence.download')}
                          scanState={evidence.scanState}
                          fileName={`evidence-${shortId(evidence.id)}`}
                          download={() => evidenceApi.download(evidence.id)}
                          describedBy={targetId}
                        />
                      </li>
                    ))}
                  </ul>
                )}
              </td>
            </tr>
          );
        })}
      </tbody>
    </TableContainer>
  );
}
