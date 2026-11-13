import { type ReactElement, type ReactNode } from 'react';
import { Link } from 'react-router';

import { languageTag } from '@/features/projects/presentation.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { SelectAllHeader, SelectRowCell } from '@/shared/ui/rowSelection.tsx';
import { type RowSelection, useRowSelection } from '@/shared/ui/useRowSelection.ts';

import { type ConcernEscalationDetail } from '../api/types.ts';
import { escalationAgeDays, type EscalationEntry } from '../concernRules.ts';
import { concernPath, escalationPath } from '../paths.ts';
import { type RoleNames } from '../useConcernData.ts';

import { EscalationStatusBadge, SeverityBadge } from './ConcernBadges.tsx';

interface EscalationTableProps {
  caption: string;
  /** SCR-087 names each escalation's concern and project; a concern's own list does not. */
  entries: (Pick<EscalationEntry, 'escalation'> & Partial<EscalationEntry>)[];
  roles: RoleNames;
  itemLabel: (id: string) => string;
  personName: (id: string | null) => string;
  /** The commands offered on a row (a concern's own list); none on SCR-087, where SCR-088 offers them. */
  actions?: (escalation: ConcernEscalationDetail) => ReactNode;
  /** The chosen rows, when the screen acts on them; the table keeps its own otherwise. */
  selection?: RowSelection;
  /** The commands on the chosen rows. */
  bulkActions?: ReactNode;
}

/**
 * Escalations as rows: the number (which opens SCR-088), the source concern and project when listed across them, its
 * severity, the role it is addressed to, when and by whom it was raised and for how long it has been open, the reason,
 * and its status in words. An ended escalation's row is muted and says when it ended and who ended it, so open and
 * ended ones are told apart by text, tone and weight, never colour alone.
 */
export function EscalationTable({
  caption,
  entries,
  roles,
  itemLabel,
  personName,
  actions,
  selection: givenSelection,
  bulkActions,
}: EscalationTableProps): ReactElement {
  const { t, formatDateTime } = useI18n();
  const ownSelection = useRowSelection();
  const selection = givenSelection ?? ownSelection;
  const showSource = entries.some((entry) => entry.concern !== undefined);
  return (
    <TableContainer
      caption={caption}
      selection={{ selection, exportName: 'escalations', actions: bulkActions }}
    >
      <thead>
        <tr>
          <SelectAllHeader
            ids={entries.map(({ escalation }) => escalation.id)}
            selection={selection}
          />
          <th scope="col">{t('issuesChallenges.escalations.table.escalation')}</th>
          {showSource && (
            <>
              <th scope="col">{t('issuesChallenges.escalations.table.concern')}</th>
              <th scope="col">{t('issuesChallenges.table.project')}</th>
              <th scope="col">{t('issuesChallenges.table.severity')}</th>
            </>
          )}
          <th scope="col">{t('issuesChallenges.escalations.table.addressedTo')}</th>
          <th scope="col">{t('issuesChallenges.escalations.table.raised')}</th>
          <th scope="col">{t('issuesChallenges.escalations.table.reason')}</th>
          <th scope="col">{t('issuesChallenges.table.status')}</th>
          {actions !== undefined && (
            <th scope="col">{t('issuesChallenges.escalations.table.actions')}</th>
          )}
        </tr>
      </thead>
      <tbody>
        {entries.map(({ escalation, concern, project }) => {
          const open = escalation.status === 'OPEN';
          return (
            <tr
              key={escalation.id}
              data-escalation={escalation.id}
              data-status={escalation.status}
              className={open ? undefined : 'row--muted'}
            >
              <SelectRowCell
                id={escalation.id}
                name={t('issuesChallenges.escalations.number', { number: escalation.escalationNo })}
                selection={selection}
              />
              <td>
                <Link to={escalationPath(escalation.id)}>
                  {t('issuesChallenges.escalations.number', { number: escalation.escalationNo })}
                </Link>
              </td>
              {showSource && (
                <>
                  <td>
                    {concern !== undefined && (
                      <>
                        <Link
                          to={concernPath(concern.projectId, concern.id)}
                          lang={languageTag(concern.title.language)}
                          dir="auto"
                        >
                          {concern.title.text}
                        </Link>
                        <span className="cell__aside">
                          {t(`issuesChallenges.type.${concern.concernType}`)}
                        </span>
                      </>
                    )}
                  </td>
                  <td
                    dir="auto"
                    lang={project === undefined ? undefined : languageTag(project.title.language)}
                  >
                    {project?.title.text}
                  </td>
                  <td>
                    {concern !== undefined && (
                      <SeverityBadge concern={concern} itemLabel={itemLabel} />
                    )}
                  </td>
                </>
              )}
              <td>{roles.name(escalation.escalatedToRoleId)}</td>
              <td>
                {formatDateTime(escalation.escalatedAt)}
                <span className="cell__aside">{personName(escalation.escalatedByUserId)}</span>
                <span className="cell__aside">
                  {t(
                    open
                      ? 'issuesChallenges.escalations.openFor'
                      : 'issuesChallenges.escalations.wasOpenFor',
                    { days: escalationAgeDays(escalation) },
                  )}
                </span>
              </td>
              <td dir="auto">{escalation.reason.text}</td>
              <td>
                <EscalationStatusBadge status={escalation.status} />
                {!open && escalation.resolvedAt !== null && (
                  <span className="cell__aside">
                    {t('issuesChallenges.escalations.ended', {
                      date: formatDateTime(escalation.resolvedAt),
                      name: personName(escalation.resolvedByUserId),
                    })}
                  </span>
                )}
              </td>
              {actions !== undefined && <td>{actions(escalation)}</td>}
            </tr>
          );
        })}
      </tbody>
    </TableContainer>
  );
}
