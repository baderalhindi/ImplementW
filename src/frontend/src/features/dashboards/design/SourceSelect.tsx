import { type ReactElement, useId, useState } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type FieldDescriptor, type RecordSource, type Visual } from './designApi.ts';
import { RECORD_SOURCES, sourceOf, withSource } from './recordSources.ts';

/**
 * The Data panel's Source (20 Nov 2026, docs/architecture/dashboard-designer-record-sources.md): what the visual counts, Projects or Risks.
 * Another source replaces the visual's figures with that source's first ones, and the panel says so in one line. Opening it asks for the risk
 * fields (`onOpen`), so they are in hand when Risks is chosen.
 */
export function SourceSelect({
  visual,
  projectFields,
  onOpen,
  onChange,
}: {
  visual: Visual;
  projectFields: readonly FieldDescriptor[];
  onOpen: () => void;
  onChange: (next: Visual) => void;
}): ReactElement {
  const { t } = useI18n();
  const id = useId();
  const [switched, setSwitched] = useState(false);
  return (
    <div className="field designer-source">
      <label className="field__label" htmlFor={`${id}-source`}>
        {t('dashboards.designer.source.label')}
      </label>
      <select
        id={`${id}-source`}
        className="field__input"
        aria-describedby={`${id}-hint`}
        value={sourceOf(visual)}
        onFocus={onOpen}
        onPointerDown={onOpen}
        onChange={(event) => {
          setSwitched(true);
          onChange(withSource(visual, event.target.value as RecordSource, projectFields));
        }}
      >
        {RECORD_SOURCES.map((source) => (
          <option key={source} value={source}>
            {t(`dashboards.designer.source.${source}`)}
          </option>
        ))}
      </select>
      <p id={`${id}-hint`} className="field__hint">
        {t('dashboards.designer.source.hint')}
      </p>
      {switched && (
        <p className="designer-data__note" role="status">
          {t('dashboards.designer.replacedBySource')}
        </p>
      )}
    </div>
  );
}
