import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type DepartmentSummary } from '../api/types.ts';
import { labelOf, useOrganizationLookups } from '../lookups.ts';
import { problemMessage } from '../problems.ts';

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

function DepartmentBranch({
  parentId,
  byParent,
}: {
  parentId: string | null;
  byParent: Map<string | null, DepartmentSummary[]>;
}): ReactElement | null {
  const { t, language } = useI18n();
  const children = byParent.get(parentId) ?? [];
  if (children.length === 0) {
    return null;
  }
  return (
    <ul className="tree">
      {children.map((department) => (
        <li key={department.id} className="tree__node">
          <span className="tree__label">
            {labelOf(department.name, language)} <span dir="ltr">({department.code})</span>
            {!department.isActive && (
              <StatusBadge label={t('identityAccess.departments.inactive')} tone="neutral" />
            )}
          </span>
          <DepartmentBranch parentId={department.id} byParent={byParent} />
        </li>
      ))}
    </ul>
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
        <div className="columns">
          <section className="section" aria-labelledby="departments-heading">
            <div className="section__header">
              <h2 id="departments-heading">{t('identityAccess.departments.title')}</h2>
              <Link to="/admin/organization/departments">
                {t('identityAccess.organization.manage')}
              </Link>
            </div>
            {lookups.departments.length === 0 ? (
              <EmptyState title={t('identityAccess.departments.empty')} />
            ) : (
              <DepartmentBranch parentId={null} byParent={childrenOf(lookups.departments)} />
            )}
          </section>
          <section className="section" aria-labelledby="entities-heading">
            <div className="section__header">
              <h2 id="entities-heading">{t('identityAccess.entities.title')}</h2>
              <Link to="/admin/organization/entities">
                {t('identityAccess.organization.manage')}
              </Link>
            </div>
            {lookups.entities.length === 0 ? (
              <EmptyState title={t('identityAccess.entities.empty')} />
            ) : (
              <ul className="link-list">
                {lookups.entities.map((entity) => (
                  <li key={entity.id}>
                    {labelOf(entity.name, language)} <span dir="ltr">({entity.code})</span>{' '}
                    <StatusBadge
                      label={t(`identityAccess.entityStatus.${entity.status}`)}
                      tone={entity.status === 'ACTIVE' ? 'positive' : 'neutral'}
                    />
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>
      )}
    </>
  );
}
