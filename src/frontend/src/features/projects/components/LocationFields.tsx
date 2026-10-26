import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField, TextField } from '@/shared/ui/FormFields.tsx';

import { type RegistrationField, type RegistrationValues } from '../registration.ts';
import { type ProjectLookups } from '../useProjectLookups.ts';

interface LocationFieldsProps {
  values: RegistrationValues;
  onChange: (field: RegistrationField, value: string) => void;
  lookups: ProjectLookups;
  errorOf: (field: RegistrationField) => string | undefined;
  /** Fields asked for before submission (the profile's), marked as such. */
  neededToSubmit: readonly RegistrationField[];
}

/**
 * SCR-035's fields, also on SCR-033/034: region, city and coordinates. A city is offered only within the chosen
 * region, and changing the region clears a city outside it (core-platform-schema N-1 (b); the API refuses the pair
 * with 422 PROJECT_REFERENCE_INVALID).
 */
export function LocationFields({
  values,
  onChange,
  lookups,
  errorOf,
  neededToSubmit,
}: LocationFieldsProps): ReactElement {
  const { t } = useI18n();
  const needed = (field: RegistrationField) =>
    neededToSubmit.includes(field) ? t('projects.form.neededToSubmit') : undefined;
  const cities = lookups.options('CITY', values.regionItemId);
  return (
    <>
      <SelectField
        label={t('projects.fields.region')}
        name="regionItemId"
        value={values.regionItemId}
        placeholder={t('common.values.none')}
        options={lookups.options('REGION')}
        hint={needed('regionItemId')}
        error={errorOf('regionItemId')}
        onChange={(value) => {
          onChange('regionItemId', value);
          if (!lookups.options('CITY', value).some((city) => city.value === values.cityItemId)) {
            onChange('cityItemId', '');
          }
        }}
      />
      <SelectField
        label={t('projects.fields.city')}
        name="cityItemId"
        value={values.cityItemId}
        placeholder={t('common.values.none')}
        options={cities}
        hint={needed('cityItemId')}
        error={errorOf('cityItemId')}
        onChange={(value) => {
          onChange('cityItemId', value);
        }}
      />
      <TextField
        label={t('projects.fields.latitude')}
        name="latitude"
        dir="ltr"
        inputMode="decimal"
        value={values.latitude}
        hint={[t('projects.form.latitudeHint'), needed('latitude')].filter(Boolean).join(' ')}
        error={errorOf('latitude')}
        onChange={(value) => {
          onChange('latitude', value);
        }}
      />
      <TextField
        label={t('projects.fields.longitude')}
        name="longitude"
        dir="ltr"
        inputMode="decimal"
        value={values.longitude}
        hint={[t('projects.form.longitudeHint'), needed('longitude')].filter(Boolean).join(' ')}
        error={errorOf('longitude')}
        onChange={(value) => {
          onChange('longitude', value);
        }}
      />
    </>
  );
}
