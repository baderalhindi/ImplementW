// The e2e stack's stand-ins (TASK-085): what SIT gets from real providers and from QA, and the compose stack has none of.
// Test infrastructure only — this service runs on the e2e stack's private network and is never deployed.
//
//   MFA provider       :5397  mfa-privileged-access.md §4 over HTTP. Every challenge accepts the code in MFA_ACCEPTED_CODE.
//   Mail relay         :2525  a plain SMTP sink (Notifications:Email:UseTls=false). It accepts every message, or refuses
//                             every RCPT with 451 while it is switched to "reject" — the controlled delivery failure of UJ-007.
//   Malware scanner    (db)   no scanner adapter exists to stand in for over the network (document-management.md F-2), so every
//                             2 s the pending versions get the verdict their file name asks for: "infected" → QUARANTINED,
//                             "unscannable" → SCAN_FAILED, "held" → left pending, anything else → CLEAN.
//   Fixture control    :8025  QA's controlled fixtures (Step 18 TD-05), by name: POST /fixtures/<name> runs fixtures/<name>.sql
//                             with the JSON body's string fields as psql variables. Also GET/DELETE /mail and POST /mail/mode.
//
// On SIT the first three are AHDA's providers; the fourth is E2E_FIXTURE_CONTROL_URL, which QA points at the same named
// fixtures (docs/testing/e2e-suite.md §5).
import { spawn } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { existsSync, readdirSync } from 'node:fs';
import { createServer as createHttpServer } from 'node:http';
import { createServer as createTcpServer } from 'node:net';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const MFA_KEY = `Bearer ${process.env.MFA_PROVIDER_API_KEY ?? 'e2e-mfa-key'}`;
const MFA_ACCEPTED_CODE = process.env.MFA_ACCEPTED_CODE ?? '246810';
const FIXTURE_DIR = join(dirname(fileURLToPath(import.meta.url)), 'fixtures');
const FIXTURES = new Set(
  existsSync(FIXTURE_DIR)
    ? readdirSync(FIXTURE_DIR)
        .filter((f) => f.endsWith('.sql'))
        .map((f) => f.slice(0, -4))
    : [],
);

function readBody(request) {
  return new Promise((resolve) => {
    const chunks = [];
    request.on('data', (chunk) => chunks.push(chunk));
    request.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')));
  });
}

function reply(response, status, body) {
  const data = JSON.stringify(body);
  response.writeHead(status, {
    'Content-Type': 'application/json',
    'Content-Length': Buffer.byteLength(data),
  });
  response.end(data);
}

function psql(args, input) {
  return new Promise((resolve) => {
    const child = spawn('psql', [
      '--no-psqlrc',
      '--quiet',
      '--tuples-only',
      '--no-align',
      '--set=ON_ERROR_STOP=1',
      ...args,
    ]);
    let out = '';
    let err = '';
    child.stdout.on('data', (d) => (out += d));
    child.stderr.on('data', (d) => (err += d));
    child.on('close', (code) => resolve({ code, out: out.trim(), err: err.trim() }));
    if (input !== undefined) child.stdin.end(input);
  });
}

// ── MFA provider ─────────────────────────────────────────────────────────────────────────────────────────────────────
createHttpServer(async (request, response) => {
  const body = JSON.parse((await readBody(request)) || '{}');
  if (request.headers.authorization !== MFA_KEY) return reply(response, 401, {});
  const parts = request.url.split('?')[0].split('/').filter(Boolean);
  if (
    request.method === 'POST' &&
    (parts[0] === 'enrolments' || parts[0] === 'challenges') &&
    parts.length === 1
  ) {
    const challenge = { challengeId: randomUUID(), expiresAt: '2099-01-01T00:00:00Z' };
    if (parts[0] === 'enrolments')
      challenge.provisioningUri = 'otpauth://totp/AHDA:e2e?secret=E2ESTANDIN';
    return reply(response, 200, challenge);
  }
  if (request.method === 'POST' && parts.length === 3 && parts[2] === 'verify') {
    return reply(response, 200, { verified: body.code === MFA_ACCEPTED_CODE });
  }
  reply(response, 404, {});
}).listen(5397, '0.0.0.0');

// ── Mail relay ───────────────────────────────────────────────────────────────────────────────────────────────────────
const mailbox = [];
let mailMode = 'accept';

createTcpServer((socket) => {
  let message = { from: null, to: [], data: '' };
  let inData = false;
  let buffer = '';
  socket.setEncoding('utf8');
  socket.write('220 e2e-standins ESMTP\r\n');
  socket.on('data', (chunk) => {
    buffer += chunk;
    let index;
    while ((index = buffer.indexOf('\r\n')) >= 0) {
      const line = buffer.slice(0, index);
      buffer = buffer.slice(index + 2);
      if (inData) {
        if (line === '.') {
          inData = false;
          mailbox.push({ ...message, receivedAt: new Date().toISOString() });
          message = { from: null, to: [], data: '' };
          socket.write('250 OK queued\r\n');
        } else {
          message.data += (line.startsWith('..') ? line.slice(1) : line) + '\n';
        }
        continue;
      }
      const verb = line.slice(0, 4).toUpperCase();
      if (verb === 'EHLO' || verb === 'HELO') socket.write('250-e2e-standins\r\n250 8BITMIME\r\n');
      else if (verb === 'MAIL') {
        message.from = line.slice(10).trim();
        socket.write('250 OK\r\n');
      } else if (verb === 'RCPT') {
        if (mailMode === 'reject') socket.write('451 4.3.0 Relay unavailable (e2e fixture)\r\n');
        else {
          message.to.push(line.slice(8).trim());
          socket.write('250 OK\r\n');
        }
      } else if (verb === 'DATA') {
        inData = true;
        socket.write('354 End data with <CR><LF>.<CR><LF>\r\n');
      } else if (verb === 'RSET') {
        message = { from: null, to: [], data: '' };
        socket.write('250 OK\r\n');
      } else if (verb === 'QUIT') {
        socket.end('221 Bye\r\n');
      } else socket.write('250 OK\r\n');
    }
  });
  socket.on('error', () => {});
}).listen(2525, '0.0.0.0');

// ── Malware scanner ──────────────────────────────────────────────────────────────────────────────────────────────────
const SCAN_PASS = `
UPDATE document_management.document_version
SET scan_state = CASE WHEN lower(file_name) LIKE '%infected%' THEN 'QUARANTINED'
                      WHEN lower(file_name) LIKE '%unscannable%' THEN 'SCAN_FAILED'
                      ELSE 'CLEAN' END,
    scan_completed_at = now(), scan_reference = 'e2e-standin:' || id, updated_at = now()
WHERE scan_state = 'SCAN_PENDING' AND lower(file_name) NOT LIKE '%held%';`;

async function scanForever() {
  for (;;) {
    const result = await psql(['--command', SCAN_PASS]);
    if (result.code !== 0 && !result.err.includes('does not exist'))
      console.error(`scanner: ${result.err}`);
    await new Promise((resolve) => setTimeout(resolve, 2000));
  }
}
void scanForever();

// ── Fixture control ──────────────────────────────────────────────────────────────────────────────────────────────────
createHttpServer(async (request, response) => {
  const path = request.url.split('?')[0];
  const raw = await readBody(request);
  if (path === '/health') return reply(response, 200, { status: 'ok', fixtures: [...FIXTURES] });
  if (path === '/mail' && request.method === 'GET') return reply(response, 200, mailbox);
  if (path === '/mail' && request.method === 'DELETE') {
    mailbox.length = 0;
    return reply(response, 204, {});
  }
  if (path === '/mail/mode' && request.method === 'POST') {
    const { mode } = JSON.parse(raw || '{}');
    if (mode !== 'accept' && mode !== 'reject')
      return reply(response, 400, { error: 'mode must be accept or reject' });
    mailMode = mode;
    return reply(response, 200, { mode });
  }
  const match = /^\/fixtures\/([a-z0-9-]+)$/.exec(path);
  if (match && request.method === 'POST') {
    if (!FIXTURES.has(match[1])) return reply(response, 404, { error: `no fixture ${match[1]}` });
    const variables = Object.entries(JSON.parse(raw || '{}'))
      .filter(([, value]) => typeof value === 'string')
      .map(([name, value]) => `--set=${name}=${value}`);
    const result = await psql([
      ...variables,
      '--single-transaction',
      '--file',
      join(FIXTURE_DIR, `${match[1]}.sql`),
    ]);
    if (result.code !== 0) return reply(response, 500, { error: result.err });
    return reply(response, 200, { rows: result.out === '' ? [] : result.out.split('\n') });
  }
  reply(response, 404, {});
}).listen(8025, '0.0.0.0');

console.warn(
  'e2e stand-ins listening: MFA :5397, SMTP :2525, fixture control :8025, scanner on the database',
);
