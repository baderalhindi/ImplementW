// The platform's load test (TASK-086, docs/testing/load-test.md). k6 1.3.
//
// Each virtual user is one of the load people (volume/10-organisation.sql), signed in as themselves, opening the screens their role
// opens — each screen sends the requests the SPA sends for it (§4) — with a person's pause between screens. Profiles:
//
//   acceptance  25 people (the brief's "normally active" ceiling) for 4 min, then 50 (the peak) for 8 min: the measured run
//   saturation  50 people with no pause between screens for 5 min: headroom, not acceptance (§5.4)
//   smoke       one person per role, every screen once, each request checked for 2xx: proves the harness, measures nothing
//
// Every request is tagged `endpoint` (method and route template, never containing `&` or `=`, which k6's CSV output puts
// between tags), `budget` (its group in budget.json), `scope`, `role` and `phase` (normal, peak, ramp). run.sh has k6 write
// each request to requests.csv.gz, and load-test-report.py judges them against budget.json. k6 itself fails nothing on
// latency except in smoke, so one script serves SIT and the e2e stack alike.
import http from 'k6/http';
import exec from 'k6/execution';
import { check, fail, sleep } from 'k6';
import { Trend } from 'k6/metrics';
import encoding from 'k6/encoding';

const BASE = `${__ENV.LOAD_BASE_URL || 'http://e2e-api:8080'}/api/v1`;
const PASSWORD = __ENV.LOAD_PASSWORD;
const PROFILE = __ENV.LOAD_PROFILE || 'acceptance';

// The role groups of volume/10-organisation.sql: their numbers, and how many of each are active at the peak (§4.1).
const GROUPS = [
  { role: 'portfolio', first: 1, count: 10, active: 5 },
  { role: 'department', first: 11, count: 26, active: 10 },
  { role: 'projectManager', first: 37, count: 75, active: 20 },
  { role: 'entityProjectManager', first: 112, count: 20, active: 7 },
  { role: 'executive', first: 132, count: 5, active: 4 },
  { role: 'viewer', first: 137, count: 4, active: 4 },
];

// The 50 people active at the peak, ordered so that any prefix keeps the role mix: the first 25 are the normal load.
function activePeople() {
  const people = [];
  for (const g of GROUPS) {
    for (let i = 0; i < g.active; i += 1) {
      people.push({ role: g.role, number: g.first + i, rank: (i + 0.5) / g.active });
    }
  }
  people.sort((a, b) => a.rank - b.rank || a.number - b.number);
  return people.map(({ role, number }) => ({ role, username: `load.u${String(number).padStart(3, '0')}` }));
}

const PEOPLE = activePeople();
const SMOKE_PEOPLE = GROUPS.map((g) => PEOPLE.find((p) => p.role === g.role));

const PROFILES = {
  acceptance: {
    executor: 'ramping-vus',
    startVUs: 0,
    stages: [
      { duration: '30s', target: 25 },
      { duration: '4m', target: 25 },
      { duration: '30s', target: 50 },
      { duration: '8m', target: 50 },
      { duration: '15s', target: 0 },
    ],
    gracefulRampDown: '30s',
  },
  saturation: {
    executor: 'ramping-vus',
    startVUs: 0,
    stages: [
      { duration: '30s', target: 50 },
      { duration: '5m', target: 50 },
      { duration: '15s', target: 0 },
    ],
    gracefulRampDown: '30s',
  },
  smoke: { executor: 'per-vu-iterations', vus: SMOKE_PEOPLE.length, iterations: 1, maxDuration: '5m' },
};

// The phase a request belongs to, from the time since the scenario started: only `normal` and `peak` are judged.
function phaseAt(elapsedMs) {
  const s = elapsedMs / 1000;
  if (PROFILE === 'acceptance') {
    if (s >= 30 && s < 270) return 'normal';
    if (s >= 300 && s < 780) return 'peak';
    return 'ramp';
  }
  if (PROFILE === 'saturation') return s >= 30 && s < 330 ? 'peak' : 'ramp';
  return 'smoke';
}

export const options = {
  scenarios: { load: PROFILES[PROFILE] },
  // Smoke proves the harness: every request answers 2xx and every export completes, or k6 exits 99.
  thresholds: PROFILE === 'smoke' ? { checks: ['rate==1'], http_reqs: ['count>0'] } : {},
  // Sign-in is throttled at 30 a minute per client address (endpoint-hardening.md D-6), and every person signs in from this
  // one runner: setup paces the sign-ins under that limit.
  setupTimeout: '5m',
  summaryTrendStats: ['med', 'p(90)', 'p(95)', 'p(99)', 'max', 'count'],
  discardResponseBodies: false,
  insecureSkipTLSVerify: __ENV.LOAD_INSECURE_TLS === '1',
};

// Whole screens, from the first request to the last: what the person waits for (§4.2). Judged like endpoints, by group.
const screenDuration = new Trend('screen_duration', true);
const reportJobCompletion = new Trend('report_job_completion', true);

const SIGN_IN_INTERVAL_S = 2.2;
const REFRESH_AFTER_MS = 12 * 60 * 1000;
const UNREAD_POLL_MS = 30 * 1000; // UNREAD_POLL_INTERVAL_MS, notifications/unreadCount.ts
const JOB_POLL_S = 2; // JOB_POLL_MS, reports/dialogs/ExportDialog.tsx
const PAGE_SIZE = 25; // shared/api/paging.ts
const MAX_PAGE_SIZE = 200;
const OPEN_STATES = 'APPROVED_PLANNED,ACTIVE,SUSPENDED,COMPLETED,CLOSED'; // the cross-project registers' project read

// The data scope each role reaches a register with (volume/10-organisation.sql): it selects the TASK-026 baseline row (§6).
const SCOPE = {
  portfolio: 'ALL',
  department: 'DEPT',
  projectManager: 'OWN',
  entityProjectManager: 'ENTITY',
  executive: 'ALL',
  viewer: 'ALL',
};

// How often each role opens each screen, out of 100 (assumption A-2, load-test.md §4.2). A role never opens a screen its
// navigation does not offer it.
const MIX = {
  portfolio: { home: 25, projectRegister: 15, workspace: 15, approvals: 10, riskRegister: 8, issueRegister: 5, changeRequests: 4, documents: 5, notifications: 5, reports: 8 },
  department: { home: 25, projectRegister: 15, workspace: 15, approvals: 10, riskRegister: 8, issueRegister: 5, changeRequests: 4, documents: 5, notifications: 5, reports: 8 },
  projectManager: { home: 15, myProjects: 20, workspace: 25, riskRegister: 10, issueRegister: 8, changeRequests: 4, documents: 8, notifications: 5, reports: 5 },
  entityProjectManager: { home: 15, projectRegister: 15, workspace: 30, riskRegister: 10, issueRegister: 10, documents: 10, notifications: 10 },
  // The shipped R07 and R06 profiles read no project (only R04 and R08 hold PROJECT_VIEW; R06 has no DOCUMENT_VIEW either), so
  // every project, register and document screen refuses them: they use what they can (load-test.md F-4).
  executive: { home: 60, reports: 30, notifications: 10 },
  viewer: { home: 85, notifications: 15 },
};

const MASTER_DATA = {
  project: ['PROJECT_CLASSIFICATION', 'GOVERNANCE_PROFILE', 'REGION', 'CITY'],
  risk: ['RISK_CATEGORY', 'IMPACT_DIMENSION'],
  concern: ['CONCERN_CATEGORY', 'PRIORITY', 'CONCERN_SEVERITY', 'IMPACT_DIMENSION'],
  document: ['DOCUMENT_TYPE', 'DATA_CLASSIFICATION', 'EVIDENCE_TYPE'],
};

function signIn(username) {
  for (let attempt = 0; attempt < 5; attempt += 1) {
    const res = http.post(`${BASE}/sessions`, JSON.stringify({ username, password: PASSWORD }), {
      headers: { 'Content-Type': 'application/json' },
      tags: { endpoint: 'POST /sessions', budget: 'sign-in', phase: 'setup' },
    });
    if (res.status === 200 || res.status === 201) {
      const body = res.json();
      return { accessToken: body.accessToken, refreshToken: body.refreshToken, issuedAt: Date.now() };
    }
    if (res.status !== 429) fail(`sign-in of ${username} answered ${res.status}: ${res.body}`);
    sleep(Number(res.headers['Retry-After'] || 10));
  }
  return fail(`sign-in of ${username} stayed throttled`);
}

// Every person signs in once, paced under the sign-in limit; each virtual user then refreshes its own session.
export function setup() {
  if (!PASSWORD) fail('LOAD_PASSWORD is not set');
  const people = PROFILE === 'smoke' ? SMOKE_PEOPLE : PEOPLE;
  return people.map((person, i) => {
    if (i > 0) sleep(SIGN_IN_INTERVAL_S);
    return { ...person, session: signIn(person.username) };
  });
}

// This virtual user's state: who they are, their session, and what their screens have shown them so far.
const vu = { person: null, session: null, userId: null, lastPoll: 0, shellMounted: false, projectIds: [] };

function phase() {
  return phaseAt(Date.now() - exec.scenario.startTime);
}

function tags(endpoint, budget) {
  return { endpoint, budget, phase: phase(), scope: SCOPE[vu.person.role], role: vu.person.role };
}

function userIdOf(accessToken) {
  return JSON.parse(encoding.b64decode(accessToken.split('.')[1], 'rawurl', 's')).sub;
}

function refresh() {
  const res = http.post(`${BASE}/sessions/current/refresh`, JSON.stringify({ refreshToken: vu.session.refreshToken }), {
    headers: { 'Content-Type': 'application/json' },
    tags: { endpoint: 'POST /sessions/current/refresh', budget: 'sign-in', phase: phase() },
  });
  if (res.status !== 200) fail(`refresh for ${vu.person.username} answered ${res.status}`);
  const body = res.json();
  vu.session = { accessToken: body.accessToken, refreshToken: body.refreshToken, issuedAt: Date.now() };
}

function params(endpoint, budget, extra = {}) {
  return { headers: { Authorization: `Bearer ${vu.session.accessToken}`, ...extra }, tags: tags(endpoint, budget) };
}

// Reads the SPA sends beside a screen and survives a 403 from (names fall back to short ids, lists to empty): the shipped
// matrix refuses several of them to some roles (load-test.md F-4). The report counts their 403s as denied, not as errors.
const DENIABLE = new Set([
  'GET /users/{id}',
  'GET /roles',
  'GET /master-data-catalogues',
  'GET /master-data-items?catalogueId',
  'GET /departments',
  'GET /external-entities',
  'GET /configuration-resolutions/{family}',
]);

function checked(res, endpoint) {
  const deniable = DENIABLE.has(endpoint);
  check(res, { [`${endpoint} ${deniable ? '2xx or 403' : '2xx'}`]: (r) => (r.status >= 200 && r.status < 300) || (deniable && r.status === 403) });
  return res;
}

// One request, sent again once after a refresh if the access token has expired (httpClient.ts does the same on a 401).
function get(path, endpoint, budget = 'register') {
  let res = http.get(`${BASE}${path}`, params(endpoint, budget));
  if (res.status === 401) {
    refresh();
    res = http.get(`${BASE}${path}`, params(endpoint, budget));
  }
  return checked(res, endpoint);
}

function post(path, body, endpoint, budget) {
  const send = () =>
    http.post(`${BASE}${path}`, JSON.stringify(body), params(endpoint, budget, { 'Content-Type': 'application/json', 'Idempotency-Key': uuid() }));
  let res = send();
  if (res.status === 401) {
    refresh();
    res = send();
  }
  return checked(res, endpoint);
}

// Requests the SPA sends side by side, at most six at a time per host, as a browser does.
function parallel(requests) {
  const out = [];
  for (let i = 0; i < requests.length; i += 6) {
    const chunk = requests.slice(i, i + 6);
    const responses = http.batch(chunk.map(([path, endpoint, budget]) => ['GET', `${BASE}${path}`, null, params(endpoint, budget || 'register')]));
    responses.forEach((res, j) => out.push(res.status === 401 ? (refresh(), get(chunk[j][0], chunk[j][1], chunk[j][2])) : checked(res, chunk[j][1])));
  }
  return out;
}

function uuid() {
  const b = crypto.getRandomValues(new Uint8Array(16));
  b[6] = (b[6] & 0x0f) | 0x40;
  b[8] = (b[8] & 0x3f) | 0x80;
  const h = Array.from(b, (x) => x.toString(16).padStart(2, '0')).join('');
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`;
}

function json(res) {
  try {
    return res.json();
  } catch (e) {
    return null;
  }
}

// readAllPages (shared/api/paging.ts): page after page of 200 until totalCount.
function readAll(path, endpoint) {
  const items = [];
  for (let page = 1; ; page += 1) {
    const body = json(get(`${path}${path.includes('?') ? '&' : '?'}page=${page}&pageSize=${MAX_PAGE_SIZE}`, endpoint));
    if (!body || !body.items) return items;
    items.push(...body.items);
    if (items.length >= body.totalCount || body.items.length === 0) return items;
  }
}

// readCatalogueItems (shared/api/masterData.ts): the catalogues, then each needed catalogue's items, side by side.
function masterData(codes) {
  const body = json(get('/master-data-catalogues', 'GET /master-data-catalogues'));
  const catalogues = Array.isArray(body) ? body : [];
  const ids = codes.map((code) => (catalogues.find((c) => c.code === code) || {}).id).filter(Boolean);
  parallel(ids.map((id) => [`/master-data-items?catalogueId=${id}&pageSize=${MAX_PAGE_SIZE}`, 'GET /master-data-items?catalogueId']));
}

// usePersonNames: one read per distinct person shown, not the caller.
function personNames(ids) {
  const distinct = [...new Set(ids.filter((id) => id && id !== vu.userId))];
  parallel(distinct.map((id) => [`/users/${id}`, 'GET /users/{id}']));
}

function projectLookups() {
  masterData(MASTER_DATA.project);
  parallel([
    [`/departments?page=1&pageSize=${MAX_PAGE_SIZE}`, 'GET /departments'],
    [`/external-entities?page=1&pageSize=${MAX_PAGE_SIZE}`, 'GET /external-entities'],
    ['/configuration-resolutions/GOVERNANCE_PROFILE', 'GET /configuration-resolutions/{family}'],
  ]);
}

function remember(projects) {
  if (projects && projects.length) vu.projectIds = projects.map((p) => p.id);
}

// The cross-project registers: every open project the caller reaches, then that register's rows one project at a time
// (useRiskData.ts, useConcernData.ts, useChangeRequestData.ts).
function perProject(path, endpoint) {
  const projects = readAll(`/projects?status=${OPEN_STATES}`, 'GET /projects?status');
  remember(projects);
  const requests = projects.map((p) => [`${path}${path.includes('?') ? '&' : '?'}projectId=${p.id}&page=1&pageSize=${MAX_PAGE_SIZE}`, endpoint]);
  parallel(requests);
}

const SCREENS = {
  home: ['Home dashboard', 'dashboard', () => {
    const catalogue = json(get(`/dashboards?page=1&pageSize=${MAX_PAGE_SIZE}`, 'GET /dashboards')) || { items: [] };
    const entry = catalogue.items.find((d) => d.isDefaultLanding) || catalogue.items.find((d) => d.contextKind === 'PORTFOLIO') || catalogue.items[0];
    if (entry && entry.contextKind === 'PROJECT') {
      remember((json(get(`/projects?page=1&pageSize=${PAGE_SIZE}`, 'GET /projects')) || {}).items);
    } else if (entry) {
      get(`/dashboards/${entry.code}`, 'GET /dashboards/{code}', 'dashboard');
    }
    get(`/departments?page=1&pageSize=${MAX_PAGE_SIZE}`, 'GET /departments');
  }],
  projectRegister: ['Project Register', 'register', () => {
    const page = Math.random() < 0.2 ? 2 : 1;
    const filter = Math.random() < 0.25 ? '&status=ACTIVE' : '';
    const body = json(get(`/projects?page=${page}&pageSize=${PAGE_SIZE}${filter}`, filter ? 'GET /projects?status' : 'GET /projects')) || {};
    remember(body.items);
    projectLookups();
    personNames((body.items || []).map((p) => p.projectManagerUserId));
  }],
  myProjects: ['My Projects', 'register', () => {
    const body = json(get(`/projects?projectManagerUserId=${vu.userId}&page=1&pageSize=${PAGE_SIZE}`, 'GET /projects?projectManagerUserId')) || {};
    remember(body.items);
    projectLookups();
  }],
  workspace: ['Project workspace', 'dashboard', () => {
    if (!vu.projectIds.length) remember((json(get(`/projects?page=1&pageSize=${PAGE_SIZE}`, 'GET /projects')) || {}).items);
    if (!vu.projectIds.length) return;
    const id = vu.projectIds[Math.floor(Math.random() * vu.projectIds.length)];
    const project = json(get(`/projects/${id}`, 'GET /projects/{id}')) || {};
    get('/audit-access', 'GET /audit-access', 'shell');
    projectLookups();
    get(`/dashboards/PROJECT?projectId=${id}`, 'GET /dashboards/{code}?projectId', 'dashboard');
    personNames([project.projectManagerUserId]);
  }],
  riskRegister: ['Risk Register', 'register', () => {
    perProject('/risks', 'GET /risks?projectId');
    get('/configuration-resolutions/RISK_MATRIX', 'GET /configuration-resolutions/{family}');
    masterData(MASTER_DATA.risk);
  }],
  issueRegister: ['Issue Register', 'register', () => {
    perProject('/management-concerns?concernType=ISSUE', 'GET /management-concerns?projectId');
    masterData(MASTER_DATA.concern);
  }],
  changeRequests: ['Change Requests', 'register', () => {
    perProject('/change-requests', 'GET /change-requests?projectId');
  }],
  approvals: ['Approval Inbox', 'register', () => {
    const body = json(get(`/approval-tasks?page=1&pageSize=${PAGE_SIZE}`, 'GET /approval-tasks')) || {};
    get('/roles', 'GET /roles');
    personNames((body.items || []).flatMap((t) => [t.requestedByUserId, t.onBehalfOfUserId]));
  }],
  notifications: ['Notifications', 'register', () => {
    parallel([
      ['/notification-preferences', 'GET /notification-preferences'],
      [`/notifications?page=1&pageSize=${PAGE_SIZE}`, 'GET /notifications'],
    ]);
  }],
  documents: ['Document Library', 'register', () => {
    const body = json(get(`/documents?page=1&pageSize=${PAGE_SIZE}`, 'GET /documents')) || {};
    masterData(MASTER_DATA.document);
    personNames((body.items || []).map((d) => d.ownerUserId));
  }],
  reports: ['Report run', 'report-run', () => {
    const catalogue = json(get(`/reports?page=1&pageSize=${MAX_PAGE_SIZE}`, 'GET /reports')) || { items: [] };
    get('/report-jobs?page=1&pageSize=5', 'GET /report-jobs');
    if (!catalogue.items.length) return;
    const code = catalogue.items[Math.floor(Math.random() * catalogue.items.length)].code;
    const report = json(get(`/reports/${code}`, 'GET /reports/{code}')) || {};
    const columns = (report.columns || []).filter((c) => c.isDefaultVisible).map((c) => ({ sourceEntityCode: c.sourceEntityCode, fieldCode: c.fieldCode }));
    // A required parameter (the project of PROJECT_REPORT and PROGRESS_HISTORY) is chosen from the options the report offers,
    // as the SPA's parameter control does.
    const required = (report.parameters || []).filter((p) => p.isRequired);
    if (required.some((p) => !(p.options || []).length)) return null;
    const pick = (options) => options[Math.floor(Math.random() * options.length)].value;
    const body = { parameters: required.map((p) => ({ code: p.code, value: pick(p.options) })), columns, sort: [] };
    post(`/reports/${code}/run?page=1&pageSize=${PAGE_SIZE}`, body, 'POST /reports/{code}/run', 'report-run');
    // One report in four is exported (assumption A-3): a second action, after the run's screen, timed as a report job.
    if (report.mayExport && (report.exportFormats || []).length && (PROFILE === 'smoke' || Math.random() < 0.25)) {
      return () => exportReport(code, body, report.exportFormats[0]);
    }
    return null;
  }],
};

const TERMINAL = ['COMPLETED', 'FAILED', 'CANCELLED', 'EXPIRED', 'REJECTED'];

function exportReport(code, body, format) {
  const started = Date.now();
  const job = json(post(`/reports/${code}/export`, { ...body, format, language: 'en' }, 'POST /reports/{code}/export', 'report-export'));
  if (!job || !job.id) return;
  let status = job.status;
  while (!TERMINAL.includes(status) && Date.now() - started < 120000) {
    sleep(JOB_POLL_S);
    status = (json(get(`/report-jobs/${job.id}`, 'GET /report-jobs/{id}')) || {}).status;
  }
  check(status, { 'report job COMPLETED': (s) => s === 'COMPLETED' });
  if (status === 'COMPLETED') {
    get(`/report-jobs/${job.id}/content`, 'GET /report-jobs/{id}/content', 'register');
    reportJobCompletion.add(Date.now() - started, { phase: phase(), format });
  }
}

function pollUnread(force) {
  if (force || Date.now() - vu.lastPoll >= UNREAD_POLL_MS) {
    get('/notifications/unread-count', 'GET /notifications/unread-count', 'shell');
    vu.lastPoll = Date.now();
  }
}

function openScreen(key) {
  const [name, budget, run] = SCREENS[key];
  const started = Date.now();
  const next = run();
  screenDuration.add(Date.now() - started, { endpoint: `screen ${name}`, budget, phase: phase(), scope: SCOPE[vu.person.role], role: vu.person.role });
  if (typeof next === 'function') next();
}

function pickScreen(role) {
  const mix = MIX[role];
  let r = Math.random() * Object.values(mix).reduce((a, b) => a + b, 0);
  for (const [key, weight] of Object.entries(mix)) {
    r -= weight;
    if (r < 0) return key;
  }
  return 'home';
}

// A person reads the screen before the next one: 5–15 s (assumption A-4), polling the badge every 30 s meanwhile, as the
// shell does. Saturation has no pause.
function think() {
  if (PROFILE === 'saturation') return;
  const until = Date.now() + (5 + Math.random() * 10) * 1000;
  while (Date.now() < until) {
    sleep(Math.min(1, (until - Date.now()) / 1000));
    pollUnread(false);
  }
}

export default function (people) {
  if (!vu.person) {
    const index = PROFILE === 'smoke' ? exec.vu.idInTest - 1 : (exec.vu.idInTest - 1) % people.length;
    vu.person = people[index];
    vu.session = vu.person.session;
    vu.userId = userIdOf(vu.session.accessToken);
  }
  if (Date.now() - vu.session.issuedAt > REFRESH_AFTER_MS) refresh();
  // The shell mounts once per visit: its audit-access read and the first badge count (AppLayout.tsx, unreadCount.ts).
  if (!vu.shellMounted) {
    get('/audit-access', 'GET /audit-access', 'shell');
    pollUnread(true);
    vu.shellMounted = true;
  }
  if (PROFILE === 'smoke') {
    for (const key of Object.keys(MIX[vu.person.role])) openScreen(key);
    return;
  }
  openScreen(pickScreen(vu.person.role));
  pollUnread(false);
  think();
}
