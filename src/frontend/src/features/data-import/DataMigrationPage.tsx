import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader, TableContainer } from '@/shared/ui/Layout.tsx';
import { SelectAllHeader, SelectRowCell } from '@/shared/ui/rowSelection.tsx';
import { useRowSelection } from '@/shared/ui/useRowSelection.ts';

import { ImportAction } from './ImportAction.tsx';
import { IMPORT_REGISTERS } from './registers.ts';

/** TASK-102: every register a migration workbook loads, in the order they are loaded, each with its import. R01 only. */
export function DataMigrationPage(): ReactElement {
  const { t } = useI18n();
  const selection = useRowSelection();
  return (
    <>
      <PageHeader
        title={t('dataImport.page.title')}
        description={t('dataImport.page.description')}
      />
      <TableContainer
        caption={t('dataImport.page.title')}
        selection={{ selection, exportName: 'data-migration-registers' }}
      >
        <thead>
          <tr>
            <SelectAllHeader
              ids={IMPORT_REGISTERS.map((register) => register.code)}
              selection={selection}
            />
            <th scope="col">{t('dataImport.page.order')}</th>
            <th scope="col">{t('dataImport.page.register')}</th>
            <th scope="col">{t('dataImport.page.holds')}</th>
            <th scope="col">
              <span className="visually-hidden">{t('common.table.actions')}</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {IMPORT_REGISTERS.map((register, index) => (
            <tr key={register.code}>
              <SelectRowCell id={register.code} name={t(register.name)} selection={selection} />
              <td>{index + 1}</td>
              <th scope="row">
                {register.page === undefined ? (
                  t(register.name)
                ) : (
                  <Link to={register.page}>{t(register.name)}</Link>
                )}
              </th>
              <td>{t(register.description)}</td>
              <td className="cell--actions">
                <ImportAction
                  collectionPath={register.collectionPath}
                  registerName={t(register.name)}
                  onImported={() => undefined}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </TableContainer>
    </>
  );
}
