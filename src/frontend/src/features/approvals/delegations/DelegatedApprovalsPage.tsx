import { type ReactElement, useCallback, useState } from 'react';

import {
  type Confirmation,
  ConfirmDialog,
} from '@/features/identity-access/components/ConfirmDialog.tsx';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';
import { PageNotice, type Notice } from '@/shared/ui/PageNotice.tsx';

import { approvalDelegationsApi } from '../api/approvalsApi.ts';
import { type ApprovalDelegationDetail } from '../api/types.ts';
import { DelegateDialog } from '../dialogs/DelegateDialog.tsx';
import { delegationTone } from '../presentation.ts';
import { approvalProblemMessage } from '../problems.ts';
import { type ApprovalNames, useApprovalNames } from '../useApprovalNames.ts';

interface DelegationTableProps {
  caption: string;
  /** The column naming the other person: the delegate for a given delegation, the delegator for a received one. */
  personLabel: string;
  person: (delegation: ApprovalDelegationDetail) => string;
  delegations: ApprovalDelegationDetail[];
  names: ApprovalNames;
  onRevoke?: (delegation: ApprovalDelegationDetail) => void;
}

function DelegationTable({
  caption,
  personLabel,
  person,
  delegations,
  names,
  onRevoke,
}: DelegationTableProps): ReactElement {
  const { t, formatDateTime } = useI18n();
  return (
    <TableContainer caption={caption}>
      <thead>
        <tr>
          <th scope="col">{personLabel}</th>
          <th scope="col">{t('approvals.fields.routingKey')}</th>
          <th scope="col">{t('approvals.fields.validFrom')}</th>
          <th scope="col">{t('approvals.fields.validTo')}</th>
          <th scope="col">{t('approvals.fields.status')}</th>
          {onRevoke !== undefined && <th scope="col">{t('common.table.actions')}</th>}
        </tr>
      </thead>
      <tbody>
        {delegations.map((delegation) => (
          <tr key={delegation.id}>
            <td>{names.user(person(delegation))}</td>
            <td>
              {delegation.routingKey === null ? (
                t('approvals.delegations.allRoutingKeys')
              ) : (
                <span dir="ltr">{delegation.routingKey}</span>
              )}
            </td>
            <td>{formatDateTime(delegation.validFrom)}</td>
            <td>{formatDateTime(delegation.validTo)}</td>
            <td>
              <StatusBadge
                label={t(`approvals.delegationStatus.${delegation.status}`)}
                tone={delegationTone(delegation.status)}
              />
            </td>
            {onRevoke !== undefined && (
              <td className="cell--actions">
                {delegation.status === 'ACTIVE' && (
                  <button
                    type="button"
                    className="button"
                    onClick={() => {
                      onRevoke(delegation);
                    }}
                  >
                    {t('approvals.revoke.action')}
                  </button>
                )}
              </td>
            )}
          </tr>
        ))}
      </tbody>
    </TableContainer>
  );
}

/**
 * SCR-114 Delegated Approvals: the delegations the caller gave, which they may revoke, and those given to them. Tasks
 * the caller may decide under a delegation appear in their inbox, marked with the delegator (SCR-100).
 */
export function DelegatedApprovalsPage(): ReactElement {
  const { t } = useI18n();
  const load = useCallback((signal: AbortSignal) => approvalDelegationsApi.list(signal), []);
  const delegations = useApiResource(load);
  const names = useApprovalNames(
    [...(delegations.data?.given ?? []), ...(delegations.data?.received ?? [])].flatMap((d) => [
      d.delegatorUserId,
      d.delegateUserId,
    ]),
  );
  const [delegating, setDelegating] = useState(false);
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);

  const settle = (message: string) => {
    setDelegating(false);
    setConfirmation(null);
    setNotice({ tone: 'success', message });
    delegations.reload();
  };

  const revoke = (delegation: ApprovalDelegationDetail) => {
    setNotice(null);
    setConfirmation({
      title: t('approvals.revoke.title'),
      body: t('approvals.revoke.body', { name: names.user(delegation.delegateUserId) }),
      confirmLabel: t('approvals.revoke.confirm'),
      destructive: true,
      action: () => approvalDelegationsApi.revoke(delegation.id),
      describeProblem: approvalProblemMessage,
    });
  };

  return (
    <>
      <PageHeader
        title={t('approvals.delegations.title')}
        description={t('approvals.delegations.description')}
        actions={
          <button
            type="button"
            className="button button--primary"
            onClick={() => {
              setNotice(null);
              setDelegating(true);
            }}
          >
            {t('approvals.delegate.action')}
          </button>
        }
      />
      <PageNotice notice={notice} />

      {delegations.loading && <LoadingState label={t('approvals.delegations.loading')} />}
      {delegations.error !== null && (
        <ErrorState
          message={approvalProblemMessage(delegations.error, t)}
          onRetry={delegations.reload}
        />
      )}
      {delegations.data !== undefined && (
        <>
          <section className="section" aria-labelledby="delegations-given">
            <h2 id="delegations-given">{t('approvals.delegations.given')}</h2>
            {delegations.data.given.length === 0 ? (
              <EmptyState title={t('approvals.delegations.givenEmpty')} />
            ) : (
              <DelegationTable
                caption={t('approvals.delegations.givenCaption')}
                personLabel={t('approvals.fields.delegate')}
                person={(d) => d.delegateUserId}
                delegations={delegations.data.given}
                names={names}
                onRevoke={revoke}
              />
            )}
          </section>
          <section className="section" aria-labelledby="delegations-received">
            <h2 id="delegations-received">{t('approvals.delegations.received')}</h2>
            {delegations.data.received.length === 0 ? (
              <EmptyState title={t('approvals.delegations.receivedEmpty')} />
            ) : (
              <DelegationTable
                caption={t('approvals.delegations.receivedCaption')}
                personLabel={t('approvals.fields.delegator')}
                person={(d) => d.delegatorUserId}
                delegations={delegations.data.received}
                names={names}
              />
            )}
          </section>
        </>
      )}

      <DelegateDialog
        open={delegating}
        onClose={() => {
          setDelegating(false);
        }}
        onDone={() => {
          settle(t('approvals.done.delegate'));
        }}
      />
      <ConfirmDialog
        confirmation={confirmation}
        onClose={() => {
          setConfirmation(null);
        }}
        onDone={() => {
          settle(t('approvals.done.revoke'));
        }}
      />
    </>
  );
}
