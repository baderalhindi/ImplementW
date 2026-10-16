# Risk-Acceptance Register

| Field | Value |
| --- | --- |
| Task | TASK-081 |
| Purpose | The risk acceptances AHDA is asked to decide, for High findings that will not be fixed before UAT |
| Who signs | AHDA Cybersecurity, as risk owner (CS-003: "risk register with likelihood/impact, treatment, owner and acceptance"). The delivery team prepares a request; it cannot accept a risk on AHDA's behalf |
| How a record becomes Signed | The signer's name, role, date and a reference to the signed copy (e-mail, memo or ticket) are entered here, in a PR the Security Lead reviews, and `Status` is set to `Signed`. `findings-log-check.py` refuses a `Signed` record with any of the three missing |

An acceptance covers the finding as written on the date in "Prepared". If the finding changes (its threats, ratings or compensating controls), the acceptance lapses and is requested again. AHDA may decline a request, in which case the finding stays Open until fixed.

### RA-01

| Field | Value |
| --- | --- |
| Status | Pending AHDA signature |
| Findings | SR-10 |
| Prepared | 2026-10-16, TASK-081 |
| Risk | A stolen refresh token keeps a user's session alive until its 8-hour absolute limit. Refresh tokens are not rotated, reuse is not detected, and sign-out clears only the browser tab (AUTH-T13). Inherent High, residual Medium |
| Compensating controls in place | Access tokens live 15 minutes; refresh requires activity within 30 minutes; 8 hours absolute. Every refresh re-reads the user: disabling a user, or ending their assignments, stops the next refresh and the next request. Tokens are never in a cookie or URL; the API's CSP and the SPA's sink lint limit script injection |
| What accepting means | UAT runs with stateless sessions as built. An administrator cannot end a specific session except by disabling the user |
| Conditions | Disabling the user is the documented response to a suspected stolen session, in the on-call runbook. AHDA confirms the 15 min / 30 min / 8 h values (`sso-directory-integration.md` F-1) |
| Review by | Before production go-live; the acceptance does not extend to PROD |
| Accepted by | — |
| Date signed | — |
| Signed copy | — |

### RA-02

| Field | Value |
| --- | --- |
| Status | Pending AHDA signature |
| Findings | SR-13 |
| Prepared | 2026-10-16, TASK-081 |
| Risk | An R01 who has not yet enrolled a second factor enrols one at their first sign-in, on the strength of the password alone. Whoever holds that password first owns the factor and the administrator account (AUTH-T17). Inherent High, residual High |
| Compensating controls in place | The enrolment is an audited event (`MULTI_FACTOR_ENROLLED`) forwarded with the AUTHENTICATION class. R01 cannot assign themselves roles. The switch (`Identity:Mfa:AllowEnrolmentAtSignIn`) exists and is tested |
| What accepting means | R01 accounts in UAT enrol at first sign-in |
| Conditions | Each R01's first sign-in in UAT happens in a supervised session on the day the account is created, and the enrolment event is checked against that session. The decision for PROD is taken separately (PTBC-027) |
| Review by | Before production go-live |
| Accepted by | — |
| Date signed | — |
| Signed copy | — |

### RA-03

| Field | Value |
| --- | --- |
| Status | Pending AHDA signature |
| Findings | SR-53 |
| Prepared | 2026-10-16, TASK-081 |
| Risk | Nafath verification proves that some person with a Nafath account approved the login, not that it was the account's intended holder. Someone holding an unverified external user's password completes the verification with their own Nafath identity and gets a session (NAF-T01). Inherent High, residual High |
| Compensating controls in place | Nafath is off (`Identity:Nafath:Enabled=false`) in every environment, and the SPA cannot complete a verification yet (SR-56). Verification comes only after every platform factor; the transaction is bound to the user; outages fail closed. External users reach only their own entity's records (SR-24, SR-62) |
| What accepting means | If Nafath is switched on for UAT, the verification is accepted as a check that the account holder has a Nafath identity, not as proof of which person they are |
| Conditions | OQ-007 is answered before production. If AHDA chooses to bind identities, SR-53 is fixed instead (an expected identity captured at onboarding and compared with `sub`) |
| Review by | When OQ-007 is answered, or before production go-live, whichever is first |
| Accepted by | — |
| Date signed | — |
| Signed copy | — |
