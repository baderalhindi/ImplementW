import { environment } from './environment.ts';

// QA's controlled fixtures (Step 18 TD-05: failed apply, timeout, duplicate request, recovery) by name. On the e2e stack they
// are e2e/stack/standins/fixtures/<name>.sql, run by the stand-ins service; on SIT, E2E_FIXTURE_CONTROL_URL names QA's service
// for the same names (docs/testing/e2e-suite.md §5). A journey that needs one fails loudly when it is missing.

async function call<T>(
  method: 'GET' | 'POST' | 'DELETE',
  path: string,
  body?: unknown,
): Promise<T> {
  const response = await fetch(`${environment.fixtureControlUrl()}${path}`, {
    method,
    headers: body === undefined ? {} : { 'Content-Type': 'application/json' },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
  const text = await response.text();
  if (!response.ok)
    throw new Error(
      `Fixture control ${method} ${path} answered ${String(response.status)}: ${text}`,
    );
  return (text === '' ? undefined : JSON.parse(text)) as T;
}

/** Runs a named fixture with string parameters; answers the rows it printed, one string per row. */
export async function runFixture(
  name: string,
  parameters: Record<string, string> = {},
): Promise<string[]> {
  return (await call<{ rows: string[] }>('POST', `/fixtures/${name}`, parameters)).rows;
}

export interface CapturedMail {
  from: string;
  to: string[];
  data: string;
  receivedAt: string;
}

/** The mail relay stand-in: what it accepted, and a switch that makes it refuse every recipient. */
export const mailRelay = {
  messages: (): Promise<CapturedMail[]> => call<CapturedMail[]>('GET', '/mail'),
  clear: (): Promise<void> => call<undefined>('DELETE', '/mail'),
  refuse: (): Promise<unknown> => call('POST', '/mail/mode', { mode: 'reject' }),
  accept: (): Promise<unknown> => call('POST', '/mail/mode', { mode: 'accept' }),
};
