# Named QA fixtures (Step 18 TD-05)

Each `<name>.sql` here is a controlled fixture a journey asks for by name through the fixture control service
(`POST /fixtures/<name>`, `e2e/support/fixtures.ts`). The JSON body's string fields arrive as psql variables, so a file
reads them as `:'projectId'`. Whatever the file prints with `SELECT` comes back as rows, one string per row. A file runs in
one transaction against the e2e stack's database.

On SIT, `E2E_FIXTURE_CONTROL_URL` names QA's service, which must offer the same names with the same effect
(docs/testing/e2e-suite.md §5). Keep a fixture to the one condition its journey needs, and say in its header which
journey uses it and what it stands in for.
