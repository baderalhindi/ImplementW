import { type ReactElement, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';
import { StatusBadge, type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import { integrationAdminApi } from '../api/integrationAdminApi.ts';
import {
  type ConnectionTestOutcome,
  type ConnectionTestResult,
  type IdentityIntegrationTestResult,
} from '../api/types.ts';
import { plainCode } from '../catalogue.ts';
import { integrationProblemMessage } from '../problems.ts';

import { Code, Moment } from './Display.tsx';

// MOD-100 Test Integration Connection. The API binds to the directory with the service account and resolves the SSO
// and Nafath discovery documents with the configured values, stores nothing and answers each target's outcome, the
// time and a fixed failure code (TASK-028 D-10, TASK-068 D-11). Acceptance criterion 1: no secret value is shown or
// logged. The dialog sends no value of its own, renders only those three fields of each result — never the response as
// a whole — shows a failure code only when it is a plain code, translates refusals from their code alone, and writes
// nothing to the console.

const TARGETS = [
  { key: 'directory', label: 'integrationAdmin.connectionTest.targets.directory' },
  { key: 'singleSignOn', label: 'integrationAdmin.connectionTest.targets.singleSignOn' },
  {
    key: 'identityVerification',
    label: 'integrationAdmin.connectionTest.targets.identityVerification',
  },
] as const satisfies readonly {
  key: keyof IdentityIntegrationTestResult;
  label: TranslationKey;
}[];

const KNOWN_FAILURES: Record<string, TranslationKey> = {
  UNREACHABLE: 'integrationAdmin.connectionTest.failures.UNREACHABLE',
  BIND_REJECTED: 'integrationAdmin.connectionTest.failures.BIND_REJECTED',
  ISSUER_MISMATCH: 'integrationAdmin.connectionTest.failures.ISSUER_MISMATCH',
};

function outcomeTone(outcome: ConnectionTestOutcome): StatusTone {
  switch (outcome) {
    case 'SUCCEEDED':
      return 'positive';
    case 'FAILED':
      return 'negative';
    case 'NOT_CONFIGURED':
      return 'neutral';
  }
}

interface TestConnectionDialogProps {
  open: boolean;
  onClose: () => void;
}

export function TestConnectionDialog({ open, onClose }: TestConnectionDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={open} title={t('integrationAdmin.connectionTest.title')} onClose={onClose}>
      <TestConnectionBody onClose={onClose} />
    </Dialog>
  );
}

function TestConnectionBody({ onClose }: { onClose: () => void }): ReactElement {
  const { t } = useI18n();
  const save = useSaveAction(integrationProblemMessage);
  const [result, setResult] = useState<IdentityIntegrationTestResult | null>(null);

  const run = async () => {
    setResult(null);
    const outcome = await save.run(() => integrationAdminApi.testIdentityConnections());
    if (outcome.ok) {
      setResult(outcome.value);
    }
  };

  return (
    <>
      <p>{t('integrationAdmin.connectionTest.intro')}</p>
      <p className="integration-note">{t('integrationAdmin.connectionTest.noSecret')}</p>
      <FormAlert message={save.formError} />
      {result !== null && (
        <ul
          className="connection-results"
          aria-label={t('integrationAdmin.connectionTest.results')}
        >
          {TARGETS.map((target) => (
            <ConnectionResult
              key={target.key}
              target={target.key}
              label={t(target.label)}
              result={result[target.key]}
            />
          ))}
        </ul>
      )}
      <div className="form__actions">
        <button
          type="button"
          className="button button--primary"
          disabled={save.saving}
          onClick={() => void run()}
        >
          {save.saving
            ? t('integrationAdmin.connectionTest.running')
            : result === null
              ? t('integrationAdmin.connectionTest.run')
              : t('integrationAdmin.connectionTest.runAgain')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('integrationAdmin.actions.close')}
        </button>
      </div>
    </>
  );
}

function ConnectionResult({
  target,
  label,
  result,
}: {
  target: string;
  label: string;
  result: ConnectionTestResult;
}): ReactElement {
  const { t } = useI18n();
  const code = plainCode(result.failureCode);
  const known = code === null ? undefined : KNOWN_FAILURES[code];
  return (
    <li className="connection-results__item" data-connection-target={target}>
      <span className="connection-results__target">{label}</span>
      <StatusBadge
        label={t(`integrationAdmin.connectionTest.outcome.${result.outcome}`)}
        tone={outcomeTone(result.outcome)}
      />
      <Moment value={result.testedAt} />
      {result.outcome === 'FAILED' && (
        <p className="connection-results__failure">
          {known !== undefined ? t(known) : t('integrationAdmin.connectionTest.failures.other')}
          {code !== null && (
            <>
              {' '}
              <Code>{code}</Code>
            </>
          )}
        </p>
      )}
    </li>
  );
}
