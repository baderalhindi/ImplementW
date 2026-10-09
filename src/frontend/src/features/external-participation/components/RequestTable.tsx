import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';

import { type ExternalUpdateRequestDetail } from '../api/types.ts';
import { usePurpose } from '../labels.ts';
import { externalRequestPath } from '../paths.ts';
import { disclosedRequest } from '../projection.ts';
import { useEntityNames } from '../useExternalParticipationData.ts';

import { DueBadge, RequestStatusBadge } from './Badges.tsx';

interface RequestTableProps {
  requests: ExternalUpdateRequestDetail[];
  caption: string;
  /** AHDA's register names the entity, the reviewer and who last changed it; an entity's list does not. */
  audience: 'internal' | 'external';
  /** Within a project's workspace the project column is redundant. */
  showProject?: boolean;
}

/** A row per request: its purpose and source (opening SCR-162), project, status, due condition and people. */
export function RequestTable({
  requests,
  caption,
  audience,
  showProject = true,
}: RequestTableProps): ReactElement {
  const { t, formatDateTime } = useI18n();
  const purpose = usePurpose();
  const shown = requests.map(disclosedRequest);
  const internal = audience === 'internal';
  const entityName = useEntityNames(internal);
  const personName = usePersonNames(
    shown.flatMap((request) => [request.responsibleUserId, request.reviewerUserId ?? null]),
  );
  return (
    <TableContainer caption={caption}>
      <thead>
        <tr>
          <th scope="col">{t('externalParticipation.table.request')}</th>
          {showProject && <th scope="col">{t('externalParticipation.table.project')}</th>}
          {internal && <th scope="col">{t('externalParticipation.table.entity')}</th>}
          <th scope="col">{t('externalParticipation.table.status')}</th>
          <th scope="col">{t('externalParticipation.table.due')}</th>
          <th scope="col">{t('externalParticipation.table.responder')}</th>
          {internal && <th scope="col">{t('externalParticipation.table.reviewer')}</th>}
          <th scope="col">{t('externalParticipation.table.updated')}</th>
        </tr>
      </thead>
      <tbody>
        {shown.map((request) => (
          <tr key={request.id}>
            <td>
              <Link className="cell__link" to={externalRequestPath(request.id)}>
                {purpose(request)}
              </Link>
              {request.targetLabel !== null && (
                <span className="cell__aside" dir="auto">
                  {request.targetLabel.text}
                </span>
              )}
            </td>
            {showProject && (
              <td>
                <ProjectReference formalProjectId={request.formalProjectId} />
              </td>
            )}
            {internal && (
              <td>
                <span dir="auto">{entityName(request.externalEntityId)}</span>
              </td>
            )}
            <td>
              <RequestStatusBadge status={request.status} />
            </td>
            <td>
              <DueBadge dueDate={request.dueDate} condition={request.dueCondition} />
            </td>
            <td>
              {request.responsibleUserId === null
                ? t('externalParticipation.people.notNamed')
                : personName(request.responsibleUserId)}
            </td>
            {internal && (
              <td>
                {request.reviewerUserId === null || request.reviewerUserId === undefined
                  ? t('externalParticipation.people.notNamed')
                  : personName(request.reviewerUserId)}
              </td>
            )}
            <td>{formatDateTime(request.updatedAt)}</td>
          </tr>
        ))}
      </tbody>
    </TableContainer>
  );
}

/** The project as the entity may see it: its Formal Project ID, or that none is issued yet. */
export function ProjectReference({
  formalProjectId,
}: {
  formalProjectId: string | null;
}): ReactElement {
  const { t } = useI18n();
  return formalProjectId === null ? (
    <span className="cell__aside">{t('externalParticipation.project.noFormalId')}</span>
  ) : (
    <span dir="ltr">{formalProjectId}</span>
  );
}
