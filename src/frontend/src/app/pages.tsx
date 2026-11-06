import { type ReactElement } from 'react';
import { Link, Navigate } from 'react-router';

import { usePermissions } from '@/features/identity-access/permissions/permissions.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';

import { administrationNavigation } from './menus.ts';

/** /admin leads to the first administration screen the person may open, which is the user list for R01. */
export function AdministrationIndex(): ReactElement {
  const { can } = usePermissions();
  const first = administrationNavigation.find((item) => can(...(item.permissions ?? [])));
  return <Navigate to={first?.to ?? '/'} replace />;
}

export function NotFoundPage(): ReactElement {
  const { t } = useI18n();
  return (
    <>
      <PageHeader title={t('common.notFound.title')} />
      <p>
        <Link to="/">{t('common.notFound.home')}</Link>
      </p>
    </>
  );
}
