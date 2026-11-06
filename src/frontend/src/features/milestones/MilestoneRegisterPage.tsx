import { type ReactElement, useState } from 'react';

import { ImportAction } from '@/features/data-import/ImportAction.tsx';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { FilterBar } from '@/shared/ui/FilterBar.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { MilestoneTable } from './components/MilestoneTable.tsx';
import { type MilestoneDialog } from './dialogs/milestoneDialog.ts';
import { MilestoneDialogs } from './dialogs/MilestoneDialogs.tsx';
import {
  byForecast,
  matchesFilter,
  REGISTER_FILTERS,
  type RegisterFilter,
} from './milestoneRules.ts';
import { isForbidden, milestoneProblemMessage } from './problems.ts';
import { useMilestoneLookups, useMilestonePortfolio } from './useMilestoneData.ts';

const DEFAULT_FILTER: RegisterFilter = 'all';

/**
 * SCR-062 Milestone Register: every milestone of every project the person may see, earliest forecast first, with its
 * project, category, forecast against the baseline, WF-03's status and where its claim stands; a returned claim's reason
 * on its row. A filter narrows it to the planned, overdue, draft, in-review, returned, achieved or cancelled ones. A
 * title opens MOD-019 for that project.
 */
export function MilestoneRegisterPage(): ReactElement | null {
  const { t } = useI18n();
  const { session } = useSession();
  const portfolio = useMilestonePortfolio();
  const lookups = useMilestoneLookups();
  const [filter, setFilter] = useState<RegisterFilter>(DEFAULT_FILTER);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [open, setOpen] = useState<{ projectId: string; dialog: MilestoneDialog } | null>(null);

  if (session === null) {
    return null;
  }
  const header = (
    <PageHeader
      title={t('milestones.register.title')}
      description={t('milestones.register.description')}
      actions={
        <ImportAction
          collectionPath="/project-milestones"
          registerName={t('dataImport.registers.PROJECT_MILESTONE.name')}
          permission="SCHEDULE_EDIT"
          onImported={portfolio.reload}
        />
      }
    />
  );
  if (portfolio.data === undefined) {
    return (
      <>
        {header}
        {portfolio.loading ? (
          <LoadingState />
        ) : isForbidden(portfolio.error) ? (
          <p className="state">{t('milestones.forbidden')}</p>
        ) : (
          <ErrorState
            error={portfolio.error}
            message={milestoneProblemMessage(portfolio.error, t)}
            onRetry={portfolio.reload}
          />
        )}
      </>
    );
  }

  const today = todayUtc();
  const { entries, projects } = portfolio.data;
  const shown = entries.filter((entry) => matchesFilter(entry, filter, today)).sort(byForecast);
  const openProject =
    open === null ? undefined : projects.find((project) => project.id === open.projectId);
  const changed = (message: TranslationKey | null, tone: Notice['tone'] = 'success') => {
    if (message !== null) {
      setNotice({ tone, message: t(message) });
    }
    portfolio.reload();
  };

  return (
    <>
      {header}
      <PageNotice notice={notice} />
      {entries.some((entry) => entry.revisions === null) && (
        <p className="form__note">{t('milestones.claimsForbidden')}</p>
      )}
      {entries.length === 0 ? (
        <EmptyState title={t('milestones.empty.register.title')}>
          <p>{t('milestones.empty.register.body')}</p>
        </EmptyState>
      ) : (
        <>
          <FilterBar
            fields={[
              {
                name: 'filter',
                label: t('milestones.filter.label'),
                all: t(`milestones.filter.${DEFAULT_FILTER}`),
                options: REGISTER_FILTERS.filter((value) => value !== DEFAULT_FILTER).map(
                  (value) => ({
                    value,
                    label: t(`milestones.filter.${value}`),
                  }),
                ),
              },
            ]}
            values={{ filter: filter === DEFAULT_FILTER ? undefined : filter }}
            onChange={(_name, value) => {
              setFilter(
                REGISTER_FILTERS.find((candidate) => candidate === value) ?? DEFAULT_FILTER,
              );
            }}
            onClearAll={() => {
              setFilter(DEFAULT_FILTER);
            }}
          />
          {shown.length === 0 ? (
            <EmptyState title={t('milestones.empty.filtered.title')}>
              <p>{t('milestones.empty.filtered.body')}</p>
            </EmptyState>
          ) : (
            <MilestoneTable
              caption={t('milestones.register.caption')}
              entries={shown}
              today={today}
              showProject
              categoryLabel={lookups.itemLabel}
              onOpen={({ milestone, project }) => {
                setOpen({
                  projectId: project.id,
                  dialog: { kind: 'achievement', milestoneId: milestone.id },
                });
              }}
            />
          )}
        </>
      )}
      {open !== null && openProject !== undefined && (
        <MilestoneDialogs
          dialog={open.dialog}
          onDialogChange={(dialog) => {
            setOpen(dialog === null ? null : { projectId: open.projectId, dialog });
          }}
          project={openProject}
          user={session.user}
          lookups={lookups}
          onChanged={changed}
        />
      )}
    </>
  );
}
