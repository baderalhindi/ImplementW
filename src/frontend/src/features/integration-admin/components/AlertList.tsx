import { type ReactElement, type SyntheticEvent, useCallback, useState } from 'react';
import { Link } from 'react-router';

import { checkText, useSaveAction } from '@/features/identity-access/forms.ts';
import { ConfirmDialog } from '@/features/identity-access/components/ConfirmDialog.tsx';
import { PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { TextAreaField } from '@/shared/ui/FormFields.tsx';
import { Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { integrationAdminApi } from '../api/integrationAdminApi.ts';
import { type OperationalAlertQuery, type OperationalAlertView } from '../api/types.ts';
import { invocationPath } from '../catalogue.ts';
import { alertStatusTone, severityTone } from '../presentation.ts';
import { integrationProblemMessage } from '../problems.ts';

import { Code, Moment } from './Display.tsx';

/** The API's limit for a resolution note (NarrativeTextRequest.TextLength). */
const NOTE_LENGTH = 2000;

type Closing = { alert: OperationalAlertView; outcome: 'resolve' | 'dismiss' } | null;

interface AlertListProps {
  query: OperationalAlertQuery;
  caption: string;
  emptyTitle: string;
}

/**
 * Operational alerts, newest first, with their handling (FG-05 §10.2): an operator acknowledges an OPEN alert, then
 * resolves or dismisses it with a note; RESOLVED and DISMISSED are final. Handling an alert changes no integration's
 * availability or freshness, and the integration recovering closes no alert (TASK-075 D-9), so the page says neither.
 * Each command is the API's to allow: a refusal is shown, and the list is read again.
 */
export function AlertList({ query, caption, emptyTitle }: AlertListProps): ReactElement {
  const { t } = useI18n();
  const [page, setPage] = useState(1);
  const [acknowledging, setAcknowledging] = useState<OperationalAlertView | null>(null);
  const [closing, setClosing] = useState<Closing>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const key = JSON.stringify(query);
  const load = useCallback(
    (signal: AbortSignal) =>
      integrationAdminApi.alerts(
        JSON.parse(key) as OperationalAlertQuery,
        { page, pageSize: PAGE_SIZE },
        signal,
      ),
    [key, page],
  );
  const alerts = useApiResource(load, { keepWhileReloading: true });

  const done = (message: string) => {
    setAcknowledging(null);
    setClosing(null);
    setNotice({ tone: 'success', message });
    alerts.reload();
  };

  let content: ReactElement;
  if (alerts.data === undefined) {
    content =
      alerts.error === null ? (
        <LoadingState />
      ) : (
        <ErrorState message={integrationProblemMessage(alerts.error, t)} onRetry={alerts.reload} />
      );
  } else if (alerts.data.items.length === 0) {
    content = <EmptyState title={emptyTitle} />;
  } else {
    content = (
      <>
        <TableContainer caption={caption}>
          <thead>
            <tr>
              <th scope="col">{t('integrationAdmin.fields.raisedAt')}</th>
              <th scope="col">{t('integrationAdmin.fields.integration')}</th>
              <th scope="col">{t('integrationAdmin.fields.alertType')}</th>
              <th scope="col">{t('integrationAdmin.fields.severity')}</th>
              <th scope="col">{t('integrationAdmin.fields.alertStatus')}</th>
              <th scope="col">{t('integrationAdmin.fields.alertDetail')}</th>
              <th scope="col">{t('common.table.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {alerts.data.items.map((alert) => (
              <tr key={alert.id} data-alert={alert.id} data-alert-status={alert.status}>
                <td>
                  <Moment value={alert.raisedAt} />
                </td>
                <td>
                  <Code>{alert.integrationCode}</Code>
                </td>
                <td>{t(`integrationAdmin.alertType.${alert.alertType}`)}</td>
                <td>
                  <StatusBadge
                    label={t(`integrationAdmin.severity.${alert.severity}`)}
                    tone={severityTone(alert.severity)}
                  />
                </td>
                <td>
                  <StatusBadge
                    label={t(`integrationAdmin.alertStatus.${alert.status}`)}
                    tone={alertStatusTone(alert.status)}
                  />
                  {alert.acknowledgedAt !== null && (
                    <span className="cell__aside">
                      {t('integrationAdmin.alerts.acknowledgedAt')}{' '}
                      <Moment value={alert.acknowledgedAt} />
                    </span>
                  )}
                </td>
                <td>
                  {alert.detail ?? t('common.values.none')}
                  {alert.resolutionNote !== null && (
                    <span className="cell__aside" dir="auto">
                      {alert.resolutionNote.text}
                    </span>
                  )}
                  {alert.invocationId !== null && (
                    <span className="cell__aside">
                      <Link to={invocationPath(alert.invocationId)}>
                        {t('integrationAdmin.alerts.openInvocation')}
                      </Link>
                    </span>
                  )}
                </td>
                <td className="cell--actions cell--actions-wrap">
                  <AlertActions
                    alert={alert}
                    onAcknowledge={() => {
                      setNotice(null);
                      setAcknowledging(alert);
                    }}
                    onClose={(outcome) => {
                      setNotice(null);
                      setClosing({ alert, outcome });
                    }}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </TableContainer>
        <Pagination
          page={alerts.data.page}
          pageSize={alerts.data.pageSize}
          totalCount={alerts.data.totalCount}
          onPageChange={setPage}
        />
      </>
    );
  }

  return (
    <>
      <PageNotice notice={notice} />
      {content}
      <ConfirmDialog
        confirmation={
          acknowledging === null
            ? null
            : {
                title: t('integrationAdmin.alerts.acknowledgeTitle'),
                body: t('integrationAdmin.alerts.acknowledgeBody'),
                confirmLabel: t('integrationAdmin.actions.acknowledge'),
                destructive: false,
                action: () => integrationAdminApi.acknowledgeAlert(acknowledging.id),
                describeProblem: integrationProblemMessage,
              }
        }
        onClose={() => {
          setAcknowledging(null);
        }}
        onDone={() => {
          done(t('integrationAdmin.alerts.acknowledged'));
        }}
      />
      <CloseAlertDialog
        closing={closing}
        onClose={() => {
          setClosing(null);
        }}
        onDone={(outcome) => {
          done(
            t(
              outcome === 'resolve'
                ? 'integrationAdmin.alerts.resolved'
                : 'integrationAdmin.alerts.dismissed',
            ),
          );
        }}
      />
    </>
  );
}

function AlertActions({
  alert,
  onAcknowledge,
  onClose,
}: {
  alert: OperationalAlertView;
  onAcknowledge: () => void;
  onClose: (outcome: 'resolve' | 'dismiss') => void;
}): ReactElement | null {
  const { t } = useI18n();
  if (alert.status === 'RESOLVED' || alert.status === 'DISMISSED') {
    return null;
  }
  const subject = (
    <span className="visually-hidden">{t(`integrationAdmin.alertType.${alert.alertType}`)}</span>
  );
  return (
    <span className="actions-group">
      {alert.status === 'OPEN' && (
        <button type="button" className="button" onClick={onAcknowledge}>
          {t('integrationAdmin.actions.acknowledge')} {subject}
        </button>
      )}
      <button
        type="button"
        className="button"
        onClick={() => {
          onClose('resolve');
        }}
      >
        {t('integrationAdmin.actions.resolve')} {subject}
      </button>
      <button
        type="button"
        className="button"
        onClick={() => {
          onClose('dismiss');
        }}
      >
        {t('integrationAdmin.actions.dismiss')} {subject}
      </button>
    </span>
  );
}

function CloseAlertDialog({
  closing,
  onClose,
  onDone,
}: {
  closing: Closing;
  onClose: () => void;
  onDone: (outcome: 'resolve' | 'dismiss') => void;
}): ReactElement {
  const { t } = useI18n();
  const title =
    closing?.outcome === 'dismiss'
      ? t('integrationAdmin.alerts.dismissTitle')
      : t('integrationAdmin.alerts.resolveTitle');
  return (
    <Dialog open={closing !== null} title={title} onClose={onClose}>
      {closing !== null && <CloseAlertForm closing={closing} onClose={onClose} onDone={onDone} />}
    </Dialog>
  );
}

function CloseAlertForm({
  closing,
  onClose,
  onDone,
}: {
  closing: NonNullable<Closing>;
  onClose: () => void;
  onDone: (outcome: 'resolve' | 'dismiss') => void;
}): ReactElement {
  const { t, language } = useI18n();
  const save = useSaveAction(integrationProblemMessage);
  const [note, setNote] = useState('');
  const { alert, outcome } = closing;

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (
      !save.validate({ 'note.text': checkText(note, { required: true, maxLength: NOTE_LENGTH }) })
    ) {
      return;
    }
    const result = await save.run(() =>
      integrationAdminApi.closeAlert(alert.id, outcome, {
        text: note.trim(),
        language: language === 'ar' ? 'AR' : 'EN',
      }),
    );
    if (result.ok) {
      onDone(outcome);
    }
  };

  return (
    <form onSubmit={(event) => void submit(event)} noValidate>
      <p>
        {t(
          outcome === 'resolve'
            ? 'integrationAdmin.alerts.resolveBody'
            : 'integrationAdmin.alerts.dismissBody',
        )}
      </p>
      <FormAlert message={save.formError} />
      <TextAreaField
        label={t('integrationAdmin.fields.note')}
        name="note"
        value={note}
        onChange={setNote}
        required
        maxLength={NOTE_LENGTH}
        error={save.fieldErrors['note.text']}
      />
      <div className="form__actions">
        <button
          type="submit"
          className={outcome === 'dismiss' ? 'button button--danger' : 'button button--primary'}
          disabled={save.saving}
        >
          {save.saving
            ? t('common.states.saving')
            : t(
                outcome === 'resolve'
                  ? 'integrationAdmin.actions.resolve'
                  : 'integrationAdmin.actions.dismiss',
              )}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
