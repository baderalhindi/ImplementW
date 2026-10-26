import { type ReactElement, type SyntheticEvent, useState } from 'react';
import { useSearchParams } from 'react-router';

import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField, TextField } from '@/shared/ui/FormFields.tsx';

import { type AuditCategory } from '../api/types.ts';
import {
  AUDIT_EVENT_CLASSES,
  AUDIT_OUTCOMES,
  type AuditFilterName,
  type AuditFilterValues,
  type FilterProblem,
  filterProblems,
} from '../filters.ts';

const PROBLEM_MESSAGES: Record<FilterProblem, TranslationKey> = {
  malformed: 'auditActivity.filters.malformed',
  dateBeforeStart: 'auditActivity.filters.dateBeforeStart',
};

interface AuditFiltersProps {
  /** The filters this screen offers, in order. */
  fields: readonly AuditFilterName[];
  /** The categories the category filter offers: the ones the screen reads and the person holds. */
  categories: readonly AuditCategory[];
  values: AuditFilterValues;
}

/**
 * The server-side audit filters of FG-06 §8.2 that the API supports, applied together into the URL. Ids are typed,
 * not picked: the authorisation-aware value lists (AUD-API-015–017) are not built, and a list of every user or project
 * would show what the reader may not see. Keyed on the URL by the caller, so Clear or Back resets the inputs.
 */
export function AuditFilters({ fields, categories, values }: AuditFiltersProps): ReactElement {
  const { t } = useI18n();
  const [params, setParams] = useSearchParams();
  const [draft, setDraft] = useState(values);
  const [problems, setProblems] = useState<Partial<Record<AuditFilterName, FilterProblem>>>({});

  const offered = (name: AuditFilterName) => fields.includes(name);
  const set = (name: AuditFilterName) => (value: string) => {
    setDraft({ ...draft, [name]: value });
  };
  const error = (name: AuditFilterName) => {
    const problem = problems[name];
    return problem === undefined ? undefined : t(PROBLEM_MESSAGES[problem]);
  };

  const apply = (event: SyntheticEvent) => {
    event.preventDefault();
    const found = filterProblems(draft);
    setProblems(found);
    if (Object.keys(found).length > 0) {
      return;
    }
    const next = new URLSearchParams(params);
    next.delete('page');
    for (const name of fields) {
      const value = draft[name].trim();
      if (value === '') {
        next.delete(name);
      } else {
        next.set(name, value);
      }
    }
    setParams(next);
  };

  const clear = () => {
    const next = new URLSearchParams(params);
    next.delete('page');
    for (const name of fields) {
      next.delete(name);
    }
    setParams(next);
  };

  return (
    <form className="filters" onSubmit={apply} aria-label={t('common.filters.label')} noValidate>
      {offered('category') && categories.length > 1 && (
        <SelectField
          label={t('auditActivity.fields.category')}
          name="category"
          value={draft.category}
          placeholder={t('auditActivity.filters.anyHeldCategory')}
          options={categories.map((value) => ({
            value,
            label: t(`auditActivity.category.${value}`),
          }))}
          onChange={set('category')}
        />
      )}
      {offered('eventClass') && (
        <SelectField
          label={t('auditActivity.fields.eventClass')}
          name="eventClass"
          value={draft.eventClass}
          placeholder={t('common.filters.any')}
          options={AUDIT_EVENT_CLASSES.map((value) => ({
            value,
            label: t(`auditActivity.eventClass.${value}`),
          }))}
          onChange={set('eventClass')}
        />
      )}
      {offered('outcome') && (
        <SelectField
          label={t('auditActivity.fields.outcome')}
          name="outcome"
          value={draft.outcome}
          placeholder={t('common.filters.any')}
          options={AUDIT_OUTCOMES.map((value) => ({
            value,
            label: t(`auditActivity.outcome.${value}`),
          }))}
          onChange={set('outcome')}
        />
      )}
      {offered('from') && (
        <TextField
          label={t('auditActivity.filters.from')}
          name="from"
          type="date"
          value={draft.from}
          error={error('from')}
          onChange={set('from')}
        />
      )}
      {offered('to') && (
        <TextField
          label={t('auditActivity.filters.to')}
          name="to"
          type="date"
          value={draft.to}
          error={error('to')}
          onChange={set('to')}
        />
      )}
      {offered('eventType') && (
        <TextField
          label={t('auditActivity.fields.eventType')}
          name="eventType"
          value={draft.eventType}
          hint={t('auditActivity.filters.eventTypeHint')}
          dir="ltr"
          onChange={set('eventType')}
        />
      )}
      {offered('projectId') && (
        <TextField
          label={t('auditActivity.fields.projectId')}
          name="projectId"
          value={draft.projectId}
          error={error('projectId')}
          dir="ltr"
          onChange={set('projectId')}
        />
      )}
      {offered('actorUserId') && (
        <TextField
          label={t('auditActivity.fields.actorUserId')}
          name="actorUserId"
          value={draft.actorUserId}
          error={error('actorUserId')}
          dir="ltr"
          onChange={set('actorUserId')}
        />
      )}
      {offered('correlationId') && (
        <TextField
          label={t('auditActivity.fields.correlationId')}
          name="correlationId"
          value={draft.correlationId}
          error={error('correlationId')}
          dir="ltr"
          onChange={set('correlationId')}
        />
      )}
      <div className="filters__actions">
        <button type="submit" className="button button--primary">
          {t('common.filters.apply')}
        </button>
        <button type="button" className="button" onClick={clear}>
          {t('common.filters.clear')}
        </button>
      </div>
    </form>
  );
}
