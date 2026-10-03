import { type RefObject, useEffect, useState } from 'react';

import { ApiError } from '@/shared/api/httpClient.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { scheduleFieldMessage } from './problems.ts';

/** Field path → API code (or a code of this module's own), null when the field is valid. */
export type FieldCodes = Partial<Record<string, string | null>>;

export function hasErrors(codes: FieldCodes): boolean {
  return Object.values(codes).some((code) => typeof code === 'string');
}

/** A field path as the API names it: a narrative's `.text` or `.language` belongs to the narrative's input. */
function inputOf(field: string): string {
  return field.replace(/\.(text|language)$/, '');
}

/** The API's field codes of a refusal, by input. */
export function serverCodesOf(error: unknown): FieldCodes {
  const codes: FieldCodes = {};
  if (error instanceof ApiError) {
    for (const issue of error.fieldIssues) {
      codes[inputOf(issue.field)] ??= issue.code;
    }
  }
  return codes;
}

export interface FieldErrors {
  /** The message to show on a field, or undefined. */
  errorOf: (field: string) => string | undefined;
  /** "Correct the highlighted fields (n)" once saving was tried with client-side errors; null otherwise. */
  summary: string | null;
  /** Call on submit: false (and the first invalid input focused) when a client-side check fails. */
  attempt: () => boolean;
  /** Puts the API's field codes of a refusal on the inputs they name. */
  showServer: (error: unknown) => void;
  /** Call on every edit: a server refusal no longer describes what is typed. */
  clearServer: () => void;
}

/**
 * A dialog's field errors, as the earlier forms show them (progress-ui.md D-6): a value of the wrong shape is flagged
 * as it is typed, a missing one once saving is tried, and the API's refusals land on the input they name. `messages`
 * overrides the message of a field (MOD-015's cycle, which names the chain).
 */
export function useFieldErrors(
  codes: FieldCodes,
  formRef: RefObject<HTMLFormElement | null>,
  messages: Partial<Record<string, string>> = {},
): FieldErrors {
  const { t } = useI18n();
  const [attempted, setAttempted] = useState(false);
  const [serverCodes, setServerCodes] = useState<FieldCodes>({});
  const [focusRequest, setFocusRequest] = useState(0);

  useEffect(() => {
    if (focusRequest > 0) {
      formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus();
    }
  }, [formRef, focusRequest]);

  const codeOf = (field: string): string | null => {
    const code = codes[field] ?? null;
    if (code !== null && (attempted || code !== 'REQUIRED')) {
      return code;
    }
    return serverCodes[field] ?? null;
  };
  const invalidCount = attempted
    ? Object.values(codes).filter((code) => typeof code === 'string').length
    : 0;

  return {
    errorOf: (field) => {
      const code = codeOf(field);
      return code === null ? undefined : (messages[field] ?? scheduleFieldMessage(field, code, t));
    },
    summary: invalidCount === 0 ? null : t('common.form.fixErrors', { count: invalidCount }),
    attempt: () => {
      if (!hasErrors(codes)) {
        return true;
      }
      setAttempted(true);
      setFocusRequest((current) => current + 1);
      return false;
    },
    showServer: (error) => {
      const server = serverCodesOf(error);
      setServerCodes(server);
      if (hasErrors(server)) {
        setFocusRequest((current) => current + 1);
      }
    },
    clearServer: () => {
      setServerCodes({});
    },
  };
}
