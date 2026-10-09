# Nafath Data-Minimisation Decision Record

| Field | Value |
| --- | --- |
| Task | TASK-068 — Integrate Nafath Identity Verification (P11 - Collaboration & External Participation (WF-13)). Deliverable 2 of 2: "data-minimization decision record" |
| Control | CTL-21, Data minimisation for external identity (`cybersecurity-control-matrix.md`) |
| Record date | 2026-10-09 |
| Status | **BUILT AT THE NARROWEST BOUNDARY; AWAITING AHDA CYBERSECURITY.** OQ-007 is still open: "the data-minimization boundary — which attributes are requested and retained — with AHDA Cybersecurity" (Open Questions sheet, resolution of 19 Sep 2026). This record states what the platform requests, reads, keeps and logs today, what that leaves unanswerable, and what each wider option would take. A wider boundary is AHDA's decision and changes this record first |
| Decision owner | AHDA Cybersecurity (OQ-007), with AHDA IT (Identity) and the Business Sponsor (PTBC-031) |
| Companion record | `nafath-identity-verification.md` (the integration) |

## 1. The question

PTBC-031's resolution (ADR-007, ADR-013 note of 19 Sep 2026) fixes the use: Nafath verifies the identity of **external entity users, at onboarding, once**; it is never an internal sign-in, and a persistent sign-in session follows. The workbook asks that "only the minimum required identity attributes are stored, and this minimization decision is documented". The minimum depends on what the verification is for. Here it proves that the person holding an external account is a person Nafath identifies. Nothing in the platform reads who that person is: roles come from FG-03 assignments (ADR-007), and the account's name, e-mail and entity are entered by AHDA at onboarding (TASK-031).

## 2. Decision

| # | Data | Decision | Where it is enforced |
| --- | --- | --- | --- |
| MD-1 | **What is requested** | The OpenID Connect scope `openid` and nothing else. No `profile`, national-id, nationality, date-of-birth or other attribute scope. | `NafathOptions.Scopes`; the test asserts `scope=openid` on the authorization URL |
| MD-2 | **What is read** | The ID token is validated (signature, issuer, audience = the platform's client id, lifetime, nonce, and the transaction's binding to the platform user). Its `sub` is checked to be present, because a token that identifies no one verifies no one. Nothing else in it is read, including the national id the subject may be. | `OpenIdConnectRelyingParty.CompleteAsync`, `NafathIdentityVerifier.CompleteAsync` |
| MD-3 | **What is kept** | Two columns of `identity_access.user`: `nafath_verification_reference`, a UUID the platform generates for the verification event, which names the event and not the person; and `nafath_verified_at`. Both existed since TASK-025 (ERD F-086). The ID token, its subject and every other claim are discarded once the adapter returns; no column, table or cache holds them. | `UserAccessRepository.RecordIdentityVerificationAsync` |
| MD-4 | **What is audited** | `IdentityAccess.IdentityVerificationRequired`, `.IdentityVerified` (with `verification_reference`) and `.IdentityVerificationFailed` (with `failure_reason`), each naming the platform user. No identity attribute. | `AuthenticationService`; `IdentityAccessAuditAttributes.VerificationReference` |
| MD-5 | **What is logged** | The platform user id, the reference, and the kind of failure. Never a token, a code, a transaction, the client secret, a national id or a name. | `NafathIdentityVerifier`, `OpenIdConnectRelyingParty` log messages |
| MD-6 | **What passes through** | The person signs in to Nafath on Nafath's page: they never type a national id into the platform. The ID token is in the API's memory for the length of one request. | The authorization code flow (`nafath-identity-verification.md` D-4) |
| MD-7 | **Who sees it** | An administrator's user record shows `nafathVerifiedAt`, so whether and when the user was verified. The reference is never returned (TASK-031, `UserDetail`). | `UserDetail` |
| MD-8 | **How long** | As long as the user row, whose delete policy is RETAIN (ERD). The two values hold no personal attribute, so no retention rule beyond the row's is proposed. | ERD §5 IdentityAccess |

## 3. What this boundary cannot answer

Keeping nothing of the identity has a cost, and AHDA should accept it knowingly:

1. **Which person verified an account.** The platform cannot say. If an incident needs it, the question goes to Nafath with the platform's client id, the verification time and the correlation id. Whether Nafath can answer it is part of the registration with Nafath (`nafath-identity-verification.md` F-2).
2. **One person on two accounts.** Two external accounts verified by the same person look like two people.
3. **An account handed to someone else.** A verification is not repeated (once, at onboarding), and the platform has no way to re-verify an account or reset its verification (`nafath-identity-verification.md` F-7). If an entity reassigns an account to a different person, that person inherits the first person's verification.

## 4. Options for AHDA Cybersecurity

| Option | Kept | Answers §3 | Cost | Position |
| --- | --- | --- | --- | --- |
| **A — reference and time (built)** | MD-3 | None of 1–3 | — | Narrowest. Fits "only the verification reference is stored" (ERD `User.NafathVerificationReference`) |
| **B — A plus a keyed one-way hash of the Nafath subject** | MD-3 + `HMAC-SHA256(key, sub)` | 2 and 3, and 1 only by recomputing with a candidate national id | A new secret-store key and its rotation rule (a rotated key breaks every comparison); a column; the comparison rule (refuse, or alert); the hash is still personal data (pseudonymised, not anonymous) | Proportionate if AHDA needs §3.2 or §3.3 |
| **C — A plus the national id and name in clear** | MD-3 + national id, names | 1, 2 and 3 directly | National identifiers at rest in the platform: a classification, encryption and access decision of their own, and a breach impact the other options do not have | Not recommended: the platform has no function that reads them |

Moving to B or C changes MD-1 to MD-3 here, the ERD row F-086, a migration, and `NafathDataMinimisationTests`, which assert today that no identity attribute reaches any table, log or response. The lawful basis and the retention period for B or C under the PDPL are for AHDA Cybersecurity and AHDA Legal.

## 5. How the decision is checked

| Check | Evidence |
| --- | --- |
| Stored fields reviewed against this record (workbook validation check) | `NafathDataMinimisationTests.OnlyAReferenceAndATimeAreKeptOfAVerification`. The test Nafath asserts a synthetic national id as the subject and adds a national id, two names and a birth date as claims. After a verification, the user row differs only in MD-3's two columns and its own `updated_at`/`updated_by`. None of the four values appears in any row of any table (every base table, read as text), in any log line, or in any response |
| The reference is not exposed | `NafathDataMinimisationTests.TheUserRecordShowsWhenTheIdentityWasVerifiedAndNotTheReference` |
| Only `openid` is requested | `NafathVerificationTests.AnExternalUserGetsNoSessionUntilNafathVerifiesThem…` |
| The checks fail when the boundary is breached | Mutation M-10 (`nafath-identity-verification.md` §6.1): keeping the subject as the reference fails the database search |

## 6. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-10-09 | Initial record: Option A built and checked; options B and C laid out for OQ-007 | Identity (TASK-068) |
