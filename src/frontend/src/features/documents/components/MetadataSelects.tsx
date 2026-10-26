import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { type SelectOption, SelectField } from '@/shared/ui/FormFields.tsx';

interface MetadataSelectsProps {
  documentTypeItemId: string;
  onDocumentTypeChange: (value: string) => void;
  dataClassificationItemId: string;
  onClassificationChange: (value: string) => void;
  typeOptions: SelectOption[];
  classificationOptions: SelectOption[];
  loading: boolean;
  fieldErrors: Partial<Record<string, string>>;
}

/**
 * The document type and classification, from their PUBLISHED items. An empty catalogue says so: nothing can be
 * uploaded until AHDA publishes one (TASK-037 F-4).
 */
export function MetadataSelects({
  documentTypeItemId,
  onDocumentTypeChange,
  dataClassificationItemId,
  onClassificationChange,
  typeOptions,
  classificationOptions,
  loading,
  fieldErrors,
}: MetadataSelectsProps): ReactElement {
  const { t } = useI18n();
  const emptyHint = (count: number) =>
    !loading && count === 0 ? t('documents.lookups.nonePublished') : undefined;
  return (
    <>
      <SelectField
        label={t('documents.fields.documentType')}
        name="documentTypeItemId"
        value={documentTypeItemId}
        onChange={onDocumentTypeChange}
        options={typeOptions}
        placeholder={loading ? t('common.states.loading') : t('common.form.choose')}
        required
        hint={emptyHint(typeOptions.length)}
        error={fieldErrors.documentTypeItemId}
      />
      <SelectField
        label={t('documents.fields.classification')}
        name="dataClassificationItemId"
        value={dataClassificationItemId}
        onChange={onClassificationChange}
        options={classificationOptions}
        placeholder={loading ? t('common.states.loading') : t('common.form.choose')}
        required
        hint={emptyHint(classificationOptions.length) ?? t('documents.upload.classificationHint')}
        error={fieldErrors.dataClassificationItemId}
      />
    </>
  );
}
