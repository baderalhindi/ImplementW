import { type ReactElement } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { type SavedViewDetail } from '../api/types.ts';
import { entryLanguage, issueMessage } from '../presentation.ts';

/**
 * Which saved view the page was opened with, whose it is, and — when it no longer fits the report version or the
 * allowlist in force — what does not, named and never silently reinterpreted (FG-02 §13.4, SAV-012).
 */
export function SavedViewNotice({
  view,
  isOwn,
}: {
  view: SavedViewDetail;
  isOwn: boolean;
}): ReactElement {
  const { t } = useI18n();
  const nameOf = usePersonNames([view.ownerUserId]);
  const name = (
    <span lang={entryLanguage(view.name.language)} dir="auto">
      {view.name.text}
    </span>
  );
  return (
    <div
      className={view.compatibility === 'VALID' ? 'notice' : 'notice notice--warning'}
      role="status"
      data-saved-view={view.id}
    >
      <p>
        {t('reports.savedView.opened')} {name}{' '}
        {isOwn
          ? t('reports.savedView.yours')
          : t('reports.savedView.sharedBy', { owner: nameOf(view.ownerUserId) })}
      </p>
      {!isOwn && <p>{t('reports.savedView.ownAccess')}</p>}
      {view.compatibility === 'INCOMPATIBLE' && (
        <>
          <p>{t('reports.savedView.incompatible')}</p>
          <ul>
            {view.issues.map((issue) => (
              <li key={`${issue.field}:${issue.code}`}>
                <code>{issue.field}</code> · {issueMessage(issue.code, t)}
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
}
