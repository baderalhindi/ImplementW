# UAT Sign-off Record

| Field | Value |
| --- | --- |
| Task | TASK-088 — Prepare & Execute UAT Catalogue Scenarios |
| Release Checklist gate | UAT Catalogue Executed & Signed Off (owner: QA Lead / AHDA Business Sponsor) |
| Status | **NOT SIGNED.** UAT has not started: no UAT environment exists and nine Critical readiness defects are open (`uat-execution.md` §8) |
| Build under test | *to be recorded at UAT entry*: release commit (40 hex) and image digest, as the `deploy-uat` deployment record names them |
| Environment | *to be recorded at UAT entry*: the `uat` environment (`infra/environments/github/environments.json`) |
| Execution window | *to be recorded* |
| Evidence | `execution-log.csv` (every run), `defect-tracker.csv` (every defect), `scenario-register.csv` and `adr-013-cases.csv` (the cases in scope); `python3 docs/testing/uat/uat-catalogue.py --gate` at the date of signature |

## 1. What a signature accepts

AHDA accepts the release named above for production when:

1. every case in scope — the 4,079 Step 18 scenarios and the 16 ADR-013 cases — has a recorded UAT outcome by an AHDA tester
   on that build (G-1);
2. a case whose last outcome is FAIL fails only on Medium or Low defects AHDA has deferred, and a NOT APPLICABLE names the
   reviewer who agreed it (G-2);
3. no Critical or High defect is open: each is closed by a fix and a passed UAT retest, or rejected with a reason (G-3);
4. the exit criteria and severity policy in `uat-execution.md` §7 were approved by AHDA before execution (Step 18 18-G-05).

`uat-catalogue.py --gate` checks 1 to 3 and the signatures below (G-4). `--check`, which runs on every pull request, fails if a
row below records an acceptance while the gate is red.

## 2. Entry gates (Step 18 Readiness_Gates)

| Gate | Issue | Status | Evidence |
| --- | --- | --- | --- |
| 18-G-01 | Deployed integrated build and UAT environment | OPEN | No UAT environment: every apply is held on the hosting confirmation (`penetration-test.md` §2). The e2e stack stands in for rehearsal only (`uat-execution.md` §5) |
| 18-G-02 | Business acceptance and tester confirmation | OPEN | No named AHDA testers; the scenarios' observable oracles are Step 18's proposals, unconfirmed by domain owners |
| 18-G-03 | Exact acceptance/control binding | OPEN | 1,517 scenarios carry story intent only and 992 control links or generic references (Step 18 Control sheet) |
| 18-G-04 | Inherited implementation/contract conditions | OPEN | Readiness defects UAT-D-001 to UAT-D-009 |
| 18-G-05 | Acceptance completion criteria | OPEN | Severity policy and exit criteria proposed in `uat-execution.md` §7, awaiting AHDA |

## 3. Signatures

| Role | Name | Organisation | Decision | Date | Signed copy |
| --- | --- | --- | --- | --- | --- |
| AHDA Business Sponsor | | AHDA | Pending | | |
| QA Lead | | | Pending | | |

Decision is one of Pending, Accepted, Accepted with deferrals, Rejected. "Signed copy" is the reference of the signed
acceptance held in AHDA's records system; the signed document itself is not committed (the repository is public).
