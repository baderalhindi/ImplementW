import { type ReactElement, type ReactNode } from 'react';

import { Detail } from '@/features/projects/components/Detail.tsx';
import { languageTag } from '@/features/projects/presentation.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { type ContributionFieldDefinition, type ContributionFieldValue } from '../api/types.ts';
import { useFieldLabel } from '../labels.ts';

/** One stored value: a number or date left-to-right, a narrative in the language it was written in. */
export function FieldValue({ value }: { value: ContributionFieldValue | undefined }): ReactElement {
  const { t } = useI18n();
  if (value === undefined) {
    return <span className="cell__aside">{t('externalParticipation.values.notAnswered')}</span>;
  }
  return value.language === null ? (
    <span dir="ltr">{value.value}</span>
  ) : (
    <span dir="auto" lang={languageTag(value.language)} className="pre-line">
      {value.value}
    </span>
  );
}

/**
 * A revision's values in the schema's order, read-only: nothing on any screen edits a submitted value (TASK-066 D-5).
 * `aside` adds a note per field (SCR-165's current source value).
 */
export function FieldValues({
  definitions,
  values,
  aside,
}: {
  definitions: readonly ContributionFieldDefinition[];
  values: readonly ContributionFieldValue[];
  aside?: (fieldCode: string) => ReactNode;
}): ReactElement {
  const label = useFieldLabel();
  return (
    <dl className="details" data-field="values">
      {definitions.map((definition) => (
        <Detail key={definition.fieldCode} term={label(definition.fieldCode)}>
          <FieldValue value={values.find((value) => value.fieldCode === definition.fieldCode)} />
          {aside?.(definition.fieldCode)}
        </Detail>
      ))}
    </dl>
  );
}
