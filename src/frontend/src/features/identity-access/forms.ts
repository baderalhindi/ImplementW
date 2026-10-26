import { type RefObject, useCallback, useEffect, useState } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import {
  type FieldErrors,
  fieldErrorsOf,
  fieldMessage,
  type ProblemDescriber,
  problemMessage,
} from './problems.ts';

// Client-side checks mirror the API's shape validation (RequestValidation.cs), so most mistakes are caught before a
// round trip; the API stays the authority and its field errors are shown the same way.

export const E164_PATTERN = /^\+[1-9][0-9]{1,14}$/;
export const EMAIL_PATTERN = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;
export const CODE_PATTERN = /^[A-Z0-9][A-Z0-9_-]*$/;
export const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export const NAME_LENGTH = 200;
export const CODE_LENGTH = 50;

export type Validator = (value: string) => string | null;

/** The API code of the first failed check, or null: REQUIRED, MAX_LENGTH or MALFORMED. */
export function checkText(
  value: string,
  {
    required = false,
    maxLength = NAME_LENGTH,
    pattern,
  }: { required?: boolean; maxLength?: number; pattern?: RegExp },
): string | null {
  const trimmed = value.trim();
  if (trimmed === '') {
    return required ? 'REQUIRED' : null;
  }
  if (value.length > maxLength) {
    return 'MAX_LENGTH';
  }
  if (pattern !== undefined && !pattern.test(value)) {
    return 'MALFORMED';
  }
  return null;
}

/** An optional text input as the API expects it: absent (null) when blank, never an empty string. */
export function optionalText(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}

export type SaveResult<T> = { ok: true; value: T } | { ok: false; error: unknown };

export interface SaveAction {
  saving: boolean;
  formError: string | null;
  fieldErrors: FieldErrors;
  /** Shows client-side errors (field path → API code) without calling the API; false when there are any. */
  validate: (codes: Partial<Record<string, string | null>>) => boolean;
  run: <T>(action: () => Promise<T>) => Promise<SaveResult<T>>;
}

/** `describe` names a feature's own refusal codes; it defaults to the platform and FG-03 codes. */
export function useSaveAction(describe: ProblemDescriber = problemMessage): SaveAction {
  const { t } = useI18n();
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  const validate = useCallback(
    (codes: Partial<Record<string, string | null>>) => {
      const errors: FieldErrors = {};
      for (const [field, code] of Object.entries(codes)) {
        if (code !== null && code !== undefined) {
          errors[field] = fieldMessage(code, t);
        }
      }
      const count = Object.keys(errors).length;
      setFieldErrors(errors);
      setFormError(count === 0 ? null : t('common.form.fixErrors', { count }));
      return count === 0;
    },
    [t],
  );

  const run = useCallback(
    async <T>(action: () => Promise<T>): Promise<SaveResult<T>> => {
      setSaving(true);
      setFormError(null);
      setFieldErrors({});
      try {
        return { ok: true, value: await action() };
      } catch (error) {
        setFieldErrors(fieldErrorsOf(error, t));
        setFormError(describe(error, t));
        return { ok: false, error };
      } finally {
        setSaving(false);
      }
    },
    [describe, t],
  );

  return { saving, formError, fieldErrors, validate, run };
}

/** Moves focus to the first invalid input after a failed submit, so a keyboard or screen-reader user lands on it. */
export function useFocusFirstError(
  formRef: RefObject<HTMLElement | null>,
  fieldErrors: FieldErrors,
): void {
  useEffect(() => {
    if (Object.keys(fieldErrors).length === 0) {
      return;
    }
    const invalid = formRef.current?.querySelector<HTMLElement>(
      '[aria-invalid="true"] input, input[aria-invalid="true"], select[aria-invalid="true"], textarea[aria-invalid="true"]',
    );
    invalid?.focus();
  }, [formRef, fieldErrors]);
}
