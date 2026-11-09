import { type ReactElement, type ReactNode } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge, type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import { type DepartmentSummary, type ExternalEntityStatus } from '../api/types.ts';
import { labelOf, useOrganizationLookups } from '../lookups.ts';
import { problemMessage } from '../problems.ts';

const ENTITY_TONE: Record<ExternalEntityStatus, StatusTone> = {
  ACTIVE: 'positive',
  SUSPENDED: 'warning',
  RETIRED: 'neutral',
};

/** Active records first; the API's order is kept within each group. */
function activeFirst<T>(items: T[], isActive: (item: T) => boolean): T[] {
  return [...items.filter(isActive), ...items.filter((item) => !isActive(item))];
}

function childrenOf(departments: DepartmentSummary[]): Map<string | null, DepartmentSummary[]> {
  const ids = new Set(departments.map((d) => d.id));
  const byParent = new Map<string | null, DepartmentSummary[]>();
  for (const department of departments) {
    // A parent outside the list (not visible to the caller) makes the department a root rather than hiding it.
    const parent =
      department.parentDepartmentId !== null && ids.has(department.parentDepartmentId)
        ? department.parentDepartmentId
        : null;
    byParent.set(parent, [...(byParent.get(parent) ?? []), department]);
  }
  return byParent;
}

/** One line of either list: the name, its code, its status; an inactive record reads quieter. */
function OrganizationRow({
  name,
  code,
  status,
  active,
}: {
  name: string;
  code: string;
  status: ReactNode;
  active: boolean;
}): ReactElement {
  return (
    <div className={`org-row${active ? '' : ' org-row--inactive'}`}>
      <span className="org-row__name">{name}</span>
      <span className="org-row__code" dir="ltr">
        {code}
      </span>
      <span className="org-row__status">{status}</span>
    </div>
  );
}

function DepartmentBranch({
  parentId,
  byParent,
}: {
  parentId: string | null;
  byParent: Map<string | null, DepartmentSummary[]>;
}): ReactElement | null {
  const { t, language } = useI18n();
  const children = activeFirst(byParent.get(parentId) ?? [], (d) => d.isActive);
  if (children.length === 0) {
    return null;
  }
  return (
    <ul className={parentId === null ? 'org-list' : 'org-list org-list--nested'}>
      {children.map((department) => (
        <li key={department.id}>
          <OrganizationRow
            name={labelOf(department.name, language)}
            code={department.code}
            active={department.isActive}
            status={
              <StatusBadge
                label={t(
                  department.isActive
                    ? 'identityAccess.departments.active'
                    : 'identityAccess.departments.inactive',
                )}
                tone={department.isActive ? 'positive' : 'neutral'}
              />
            }
          />
          <DepartmentBranch parentId={department.id} byParent={byParent} />
        </li>
      ))}
    </ul>
  );
}

function OrganizationPanel({
  id,
  title,
  active,
  total,
  manageTo,
  manageLabel,
  children,
}: {
  id: string;
  title: string;
  active: number;
  total: number;
  manageTo: string;
  manageLabel: string;
  children: ReactNode;
}): ReactElement {
  const { t } = useI18n();
  return (
    <section className="org-panel" aria-labelledby={id}>
      <div className="org-panel__header">
        <div>
          <h2 id={id}>{title}</h2>
          <p className="org-panel__summary">
            {t('identityAccess.organization.activeOfTotal', { active, total })}
          </p>
        </div>
        <Link className="button" to={manageTo}>
          {manageLabel}
        </Link>
      </div>
      {children}
    </section>
  );
}

/** ADM-011 Organization Structure: the department hierarchy and the external entities beside it. */
export function OrganizationStructurePage(): ReactElement {
  const { t, language } = useI18n();
  const { lookups, error, loading } = useOrganizationLookups(language);

  return (
    <>
      <PageHeader
        title={t('identityAccess.organization.title')}
        description={t('identityAccess.organization.description')}
      />
      {loading && <LoadingState />}
      {error !== null && <ErrorState message={problemMessage(error, t)} />}
      {lookups !== undefined && (
        <div className="org-structure">
          <OrganizationPanel
            id="departments-heading"
            title={t('identityAccess.departments.title')}
            active={lookups.departments.filter((d) => d.isActive).length}
            total={lookups.departments.length}
            manageTo="/admin/organization/departments"
            manageLabel={t('identityAccess.organization.manageDepartments')}
          >
            {lookups.departments.length === 0 ? (
              <EmptyState title={t('identityAccess.departments.empty')} />
            ) : (
              <DepartmentBranch parentId={null} byParent={childrenOf(lookups.departments)} />
            )}
          </OrganizationPanel>
          <OrganizationPanel
            id="entities-heading"
            title={t('identityAccess.entities.title')}
            active={lookups.entities.filter((e) => e.status === 'ACTIVE').length}
            total={lookups.entities.length}
            manageTo="/admin/organization/entities"
            manageLabel={t('identityAccess.organization.manageEntities')}
          >
            {lookups.entities.length === 0 ? (
              <EmptyState title={t('identityAccess.entities.empty')} />
            ) : (
              <ul className="org-list">
                {activeFirst(lookups.entities, (e) => e.status === 'ACTIVE').map((entity) => (
                  <li key={entity.id}>
                    <OrganizationRow
                      name={labelOf(entity.name, language)}
                      code={entity.code}
                      active={entity.status === 'ACTIVE'}
                      status={
                        <StatusBadge
                          label={t(`identityAccess.entityStatus.${entity.status}`)}
                          tone={ENTITY_TONE[entity.status]}
                        />
                      }
                    />
                  </li>
                ))}
              </ul>
            )}
          </OrganizationPanel>
        </div>
      )}
    </>
  );
}
