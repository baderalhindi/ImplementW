import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';

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
