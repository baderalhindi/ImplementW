import { type ReactElement, useCallback } from 'react';

import { documentsApi } from '@/features/documents/api/documentsApi.ts';
import {
  type EvidenceReferenceDetail,
  type NarrativeText,
} from '@/features/documents/api/types.ts';
import { ScanStateBadge } from '@/features/documents/components/ScanState.tsx';
import { languageTag, shortId } from '@/features/projects/presentation.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { type EvidenceRule } from '../milestoneRules.ts';
import { type MilestoneLookups } from '../useMilestoneData.ts';

/**
 * The evidence rule in force, stated in words from what the API answered (acceptance criterion 1; PTBC-006). While no
 * EVIDENCE_POLICY is published the claim is labelled "optional pending policy", never left to look validated.
 */
export function EvidencePolicyLabel({
  rule,
  lookups,
}: {
  rule: EvidenceRule;
  lookups: MilestoneLookups;
}): ReactElement {
  const { t } = useI18n();
  switch (rule.kind) {
    case 'pending':
      return (
        <div className="evidence-policy evidence-policy--pending" data-policy="pending">
          <p className="evidence-policy__title">{t('milestones.evidence.pending.title')}</p>
          <p>{t('milestones.evidence.pending.body')}</p>
        </div>
      );
    case 'notRequired':
      return (
        <div className="evidence-policy" data-policy="notRequired">
          <p className="evidence-policy__title">{t('milestones.evidence.notRequired.title')}</p>
          <p>{t('milestones.evidence.notRequired.body')}</p>
        </div>
      );
    case 'required':
      return (
        <div className="evidence-policy" data-policy="required">
          <p className="evidence-policy__title">{t('milestones.evidence.required.title')}</p>
          <ul className="evidence-policy__types">
            {rule.mandatory.map((id) => {
              const missing = rule.missing.includes(id);
              return (
                <li key={id} data-missing={missing || undefined}>
                  <span>{lookups.itemLabel(id)}</span>
                  {' — '}
                  <strong>
                    {missing
                      ? t('milestones.evidence.required.missing')
                      : t('milestones.evidence.required.held')}
                  </strong>
                </li>
              );
            })}
          </ul>
        </div>
      );
    case 'undetermined':
      return (
        <div className="evidence-policy evidence-policy--blocked" data-policy="undetermined">
          <p className="evidence-policy__title">{t('milestones.evidence.undetermined.title')}</p>
          <p>{t('milestones.evidence.undetermined.body')}</p>
        </div>
      );
  }
}

async function readTitles(ids: string[], signal: AbortSignal): Promise<Map<string, NarrativeText>> {
  const titles = await Promise.all(
    ids.map(async (id): Promise<[string, NarrativeText] | null> => {
      try {
        const { data } = await documentsApi.get(id, signal);
        return [id, data.title];
      } catch {
        // A document the caller may no longer read (or gone) is named by its id; the evidence row still shows.
        return null;
      }
    }),
  );
  return new Map(titles.filter((entry) => entry !== null));
}

/**
 * The revision's evidence in force (VALID, on an active link): the document, what it is evidence of, the version pinned,
 * its scan state, and whether it counts. `onWithdraw` is given while the revision is a DRAFT the person may change.
 */
export function EvidenceList({
  pieces,
  lookups,
  saving,
  onWithdraw,
}: {
  pieces: EvidenceReferenceDetail[];
  lookups: MilestoneLookups;
  saving: boolean;
  onWithdraw?: ((piece: EvidenceReferenceDetail, documentName: string) => void) | undefined;
}): ReactElement {
  const { t } = useI18n();
  const key = [...new Set(pieces.map((piece) => piece.documentId))].sort().join(',');
  const load = useCallback(
    (signal: AbortSignal) => readTitles(key === '' ? [] : key.split(','), signal),
    [key],
  );
  const titles = useApiResource(load);
  if (pieces.length === 0) {
    return <p className="form__note">{t('milestones.evidence.none')}</p>;
  }
  return (
    <ul className="evidence-list">
      {pieces.map((piece) => {
        const title = titles.data?.get(piece.documentId);
        const name =
          title?.text ??
          t('milestones.evidence.unknownDocument', { id: shortId(piece.documentId) });
        return (
          <li key={piece.id}>
            <span
              dir="auto"
              lang={title === undefined ? undefined : languageTag(title.language)}
              className="evidence-list__document"
            >
              {name}
            </span>
            <span className="cell__aside">
              {t('milestones.evidence.of', { type: lookups.itemLabel(piece.evidenceTypeItemId) })}
              {' · '}
              {t('milestones.evidence.version', { number: piece.versionNo })}
            </span>
            <span className="figure-group">
              <ScanStateBadge state={piece.scanState} />
              <span>
                {piece.satisfies
                  ? t('milestones.evidence.counts')
                  : t('milestones.evidence.doesNotCount')}
              </span>
            </span>
            {onWithdraw !== undefined && (
              <button
                type="button"
                className="button button--link"
                disabled={saving}
                aria-label={t('milestones.evidence.withdrawNamed', { document: name })}
                onClick={() => {
                  onWithdraw(piece, name);
                }}
              >
                {t('milestones.evidence.withdraw')}
              </button>
            )}
          </li>
        );
      })}
    </ul>
  );
}
