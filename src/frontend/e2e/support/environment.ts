import { readFileSync } from 'node:fs';

// Where the suite runs and who it runs as (docs/testing/e2e-suite.md §4). Nothing here has a default that could point at a
// real environment: the base URL and the credentials are always supplied by whoever starts the run.

/** The people the journeys act as. Each is a synthetic account; on SIT, one of the SIT directory's e2e accounts. */
export const PERSONAS = [
  'administrator', // R01 — needs a second factor
  'portfolioManager', // AHDA's reviewer, approver and activator (E2E-R02, ALL)
  'departmentManager', // E2E-R03 on DEPT-E2E
  'otherDepartmentManager', // E2E-R03 on DEPT-E2E-OTHER: the out-of-scope department
  'projectManager', // an internal Project Manager (E2E-R04)
  'executive', // R07
  'viewer', // R06
  'entityProjectManager', // ADR-013: entity A's own Project Manager, shipped R04 + R08 on entity A
  'entityContributor', // entity A, shipped R08
  'otherEntityProjectManager', // entity B's Project Manager: must never reach entity A's projects
] as const;

export type Persona = (typeof PERSONAS)[number];

export interface Credential {
  username: string;
  password: string;
  /** A fixed code the MFA stand-in accepts (the e2e stack). */
  mfaCode?: string;
  /** The base32 TOTP secret of a real MFA enrolment (SIT). */
  totpSecret?: string;
}

function required(name: string): string {
  const value = process.env[name];
  if (value === undefined || value.trim() === '') {
    throw new Error(`${name} is not set; see docs/testing/e2e-suite.md §4.`);
  }
  return value.trim();
}

/**
 * E2E_TEST_USER_CREDENTIALS holds the JSON the secret E2E_TEST_USER_CREDENTIALS_SECRET_REF names (the workflow reads it from
 * the secret store); E2E_TEST_USER_CREDENTIALS_FILE names a file with the same JSON (the e2e stack's local defaults).
 */
function loadCredentials(): Record<Persona, Credential> {
  const inline = process.env.E2E_TEST_USER_CREDENTIALS;
  const json =
    inline !== undefined && inline.trim() !== ''
      ? inline
      : readFileSync(required('E2E_TEST_USER_CREDENTIALS_FILE'), 'utf8');
  const parsed = JSON.parse(json) as Partial<Record<Persona, Credential>>;
  const missing = PERSONAS.filter((persona) => {
    const credential = parsed[persona];
    return credential === undefined || credential.username === '' || credential.password === '';
  });
  if (missing.length > 0) {
    throw new Error(`The e2e credentials lack ${missing.join(', ')}.`);
  }
  return parsed as Record<Persona, Credential>;
}

let credentials: Record<Persona, Credential> | null = null;

export function credentialOf(persona: Persona): Credential {
  credentials ??= loadCredentials();
  return credentials[persona];
}

export const environment = {
  /** `compose` (the e2e stack) or `sit`. Recorded in the report; the journeys do not branch on it. */
  target: process.env.E2E_TARGET ?? 'sit',
  baseUrl: (): string => required('E2E_BASE_URL').replace(/\/$/, ''),
  /** QA's named fixtures (Step 18 TD-05): the e2e stand-ins, or SIT's fixture service. */
  fixtureControlUrl: (): string => required('E2E_FIXTURE_CONTROL_URL').replace(/\/$/, ''),
};
