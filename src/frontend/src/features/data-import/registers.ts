import { type TranslationKey } from '@/shared/i18n/i18n.ts';

/** A register a migration workbook loads (TASK-102), as the Data migration page lists it. */
export interface ImportRegister {
  code: string;
  /** Its collection: the template and the imports live under it. */
  collectionPath: string;
  name: TranslationKey;
  description: TranslationKey;
  /** The register's own page, where the same import is offered beside its records. */
  page?: string;
}

/**
 * Load order: each register names only records of the registers above it, so loading top to bottom never meets a reference that
 * is not there yet (data-import.md D-5).
 */
export const IMPORT_REGISTERS: ImportRegister[] = [
  {
    code: 'DEPARTMENT',
    collectionPath: '/departments',
    name: 'dataImport.registers.DEPARTMENT.name',
    description: 'dataImport.registers.DEPARTMENT.description',
    page: '/admin/organization/departments',
  },
  {
    code: 'MASTER_DATA_ITEM',
    collectionPath: '/master-data-items',
    name: 'dataImport.registers.MASTER_DATA_ITEM.name',
    description: 'dataImport.registers.MASTER_DATA_ITEM.description',
  },
  {
    code: 'EXTERNAL_ENTITY',
    collectionPath: '/external-entities',
    name: 'dataImport.registers.EXTERNAL_ENTITY.name',
    description: 'dataImport.registers.EXTERNAL_ENTITY.description',
    page: '/admin/organization/entities',
  },
  {
    code: 'USER',
    collectionPath: '/users',
    name: 'dataImport.registers.USER.name',
    description: 'dataImport.registers.USER.description',
    page: '/admin/users',
  },
  {
    code: 'KPI_DEFINITION',
    collectionPath: '/kpi-definitions',
    name: 'dataImport.registers.KPI_DEFINITION.name',
    description: 'dataImport.registers.KPI_DEFINITION.description',
  },
];
