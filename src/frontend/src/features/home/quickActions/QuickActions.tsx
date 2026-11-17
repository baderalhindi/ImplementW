import { type ReactElement, useRef, useState } from 'react';
import { Link } from 'react-router';

import { EXECUTIVE_ROLE, landingRole } from '@/features/dashboards/landing.ts';
import { useDashboardCatalogue } from '@/features/dashboards/useDashboardCatalogue.ts';
import { usePermissions } from '@/features/identity-access/permissions/permissions.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { MoreMenu } from '@/shared/ui/MoreMenu.tsx';
import { useOneLineCount } from '@/shared/ui/useOneLineCount.ts';

import { ProjectActionDialog } from './ProjectActionDialog.tsx';
import {
  type QuickAction,
  type QuickActionContext,
  quickActionLabel,
  shownQuickActions,
} from './quickActions.ts';
import { QuickActionsPicker } from './QuickActionsPicker.tsx';
import { useQuickActions } from './useQuickActions.ts';

function PencilIcon(): ReactElement {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true" focusable="false">
      <path
        d="M4 20h4L19 9l-4-4L4 16v4zM13.5 6.5l4 4"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

/**
 * The welcome banner's quick actions (review round 8, point 08): the person's own choice of up to six, or their role's set until they choose, each
 * shown only while they may do it. The pencil opens "تخصيص الإجراءات السريعة". An action done inside a project first asks which project.
 */
export function QuickActions({ user }: { user: SessionUser }): ReactElement | null {
  const { t } = useI18n();
  const { can } = usePermissions();
  const dashboards = useDashboardCatalogue();
  const quick = useQuickActions();
  const [picking, setPicking] = useState(false);
  const [inProject, setInProject] = useState<QuickAction | null>(null);
  const barRef = useRef<HTMLElement>(null);
  const measureRef = useRef<HTMLUListElement>(null);
  const context: QuickActionContext | null =
    quick.loaded && !dashboards.loading
      ? {
          can,
          external: user.userType === 'EXTERNAL',
          portfolioDashboard: (dashboards.data ?? []).some((entry) => entry.code === 'PORTFOLIO'),
          executive: landingRole(user) === EXECUTIVE_ROLE,
        }
      : null;
  const shown = context === null ? [] : shownQuickActions(quick.saved, context);
  const labelOf = (entry: QuickAction): string =>
    context === null ? '' : t(quickActionLabel(entry, context));
  // The actions stay on one line; those that do not fit go into "More", before the pencil.
  const fitting = useOneLineCount(
    barRef,
    measureRef,
    shown.length,
    shown.map(labelOf).join('\u0000'),
    1,
  );
  // The catalogue's checks wait for what they read, so a role's set never shows in place of the person's choice.
  if (context === null) {
    return null;
  }
  const overflow = shown.slice(fitting);

  return (
    <nav ref={barRef} className="home__actions-bar" aria-label={t('home.actions.title')}>
      <ul className="home__actions">
        {shown.slice(0, fitting).map((entry) => (
          <li key={entry.code}>
            {'to' in entry.target ? (
              <Link to={entry.target.to}>{t(quickActionLabel(entry, context))}</Link>
            ) : (
              <button
                type="button"
                onClick={() => {
                  setInProject(entry);
                }}
              >
                {t(quickActionLabel(entry, context))}
              </button>
            )}
          </li>
        ))}
        {overflow.length > 0 && (
          <li>
            <MoreMenu
              floating
              className="home__actions-more"
              items={overflow.map((entry) =>
                'to' in entry.target
                  ? { key: entry.code, label: labelOf(entry), to: entry.target.to }
                  : {
                      key: entry.code,
                      label: labelOf(entry),
                      onSelect: () => {
                        setInProject(entry);
                      },
                    },
              )}
            />
          </li>
        )}
        <li>
          <button
            type="button"
            className="home__actions-edit"
            aria-label={t('home.quickActions.customize')}
            title={t('home.quickActions.customize')}
            onClick={() => {
              setPicking(true);
            }}
          >
            <PencilIcon />
          </button>
        </li>
      </ul>
      {/* Every action, "More" and the pencil at their natural width, out of sight, to decide how many fit. */}
      <ul
        ref={measureRef}
        className="home__actions home__actions--measure"
        aria-hidden="true"
        inert
      >
        {shown.map((entry) => (
          <li key={entry.code}>
            <a data-label={labelOf(entry)} />
          </li>
        ))}
        <li>
          <a data-label={`⋯ ${t('common.actions.more')}`} />
        </li>
        <li>
          <a className="home__actions-edit" />
        </li>
      </ul>
      <QuickActionsPicker
        open={picking}
        context={context}
        shown={shown}
        usingDefault={quick.saved === null}
        onSave={quick.save}
        onClose={() => {
          setPicking(false);
        }}
      />
      {inProject !== null && 'projectTab' in inProject.target && (
        <ProjectActionDialog
          action={{ ...inProject, target: inProject.target }}
          user={user}
          onClose={() => {
            setInProject(null);
          }}
        />
      )}
    </nav>
  );
}
