import { readFile } from 'node:fs/promises';
import { inflateRawSync, inflateSync } from 'node:zlib';

import { type Locator, type Page } from '@playwright/test';

import { eventually, type PlatformApi } from './api.ts';
import { expect } from './journey.ts';
import { approveTaskOn } from './projects.ts';
import { itemId } from './reference.ts';

// What the read-projection journeys (UJ-008, UJ-009, UJ-010, UJ-015) share: WF-14's KPI path through the API, as the people
// who own each step take it (financial-kpi.md D-5, D-13); a record of the writes a page sends; an export followed to its
// file in the browser; and readers for the three committed formats (ADR-005) that need no package beyond Node's own.

/** The e2e data set's KPI (stack/provision/47-kpi-definitions.sql): HIGHER_IS_BETTER, in percent. */
export const KPI_CODE = 'E2E_SAFE_HOURS';

export type ValueStatus = 'MEASURED' | 'MISSING' | 'STALE' | 'NOT_APPLICABLE';

export interface Measurement {
  id: string;
  valueStatus: ValueStatus;
  measuredValue: number | null;
  ragStatus: string | null;
  status: string;
  asOfDate: string;
  periodStart: string;
  periodEnd: string;
}

interface Coded {
  id: string;
  code: string;
}

async function kpiDefinitionId(api: PlatformApi): Promise<string> {
  const definitions = await api.get<{ items: Coded[] }>('/kpi-definitions?pageSize=100');
  const found = definitions.items.find((d) => d.code === KPI_CODE)?.id;
  if (found === undefined) throw new Error(`The e2e data set has no KPI ${KPI_CODE}.`);
  return found;
}

/**
 * The KPI assigned to the project with an ACTIVE target version (WF-11 KPI_TARGET, decided by AHDA): target 95%, GREEN from
 * 95%, AMBER from 90%. Returns the assignment's id.
 */
export async function assignedKpi(
  manager: PlatformApi,
  reviewer: PlatformApi,
  projectId: string,
): Promise<string> {
  const assignment = await manager.post<{ id: string }>('/kpi-assignments', {
    projectId,
    kpiDefinitionId: await kpiDefinitionId(manager),
    ownerUserId: manager.userId,
    measurementFrequencyItemId: await itemId(manager, 'MEASUREMENT_FREQUENCY', 'E2E_MONTHLY'),
  });
  const target = await manager.post<{ id: string }>('/kpi-target-versions', {
    kpiAssignmentId: assignment.id,
    targetValue: '95',
    greenThreshold: '95',
    amberThreshold: '90',
    effectiveFrom: null,
  });
  await manager.post(`/kpi-target-versions/${target.id}/submit`);
  await approveTaskOn(reviewer, target.id, 'KPI_TARGET');
  await eventually(
    () => manager.get<{ status: string }>(`/kpi-target-versions/${target.id}`),
    (t) => t.status === 'ACTIVE',
    `KPI target ${target.id} to be ACTIVE`,
  );
  return assignment.id;
}

export interface MeasurementInput {
  /** The month measured: its first day is this many days before today. */
  periodStartsDaysAgo: number;
  valueStatus: ValueStatus;
  value?: string;
  /** The source's as-of date, yyyy-mm-dd. */
  asOfDate: string;
}

/** A measurement recorded by the Project Manager, still DRAFT; the ETag is its version. */
export async function recordedMeasurement(
  manager: PlatformApi,
  assignmentId: string,
  input: MeasurementInput,
): Promise<{ id: string; etag: string }> {
  const start = new Date();
  start.setUTCDate(start.getUTCDate() - input.periodStartsDaysAgo);
  const end = new Date(start);
  end.setUTCDate(end.getUTCDate() + 29);
  const created = await manager.ok<{ id: string }>('POST', '/kpi-measurements', {
    kpiAssignmentId: assignmentId,
    periodStart: start.toISOString().slice(0, 10),
    periodEnd: end.toISOString().slice(0, 10),
    measuredValue: input.value ?? null,
    valueStatus: input.valueStatus,
    asOfDate: input.asOfDate,
    narrative: null,
  });
  return { id: created.body.id, etag: created.etag ?? '' };
}

/** A measurement recorded and submitted by the Project Manager and published by AHDA (KPI_REVIEW, not the recorder). */
export async function publishedMeasurement(
  manager: PlatformApi,
  reviewer: PlatformApi,
  assignmentId: string,
  input: MeasurementInput,
): Promise<Measurement> {
  const { id } = await recordedMeasurement(manager, assignmentId, input);
  await manager.post(`/kpi-measurements/${id}/submit`);
  return reviewer.post<Measurement>(`/kpi-measurements/${id}/publish`);
}

/**
 * A period's financial figures entered by the Project Manager and published by AHDA (WF-14, financial-kpi.md D-7, D-15): the
 * earliest begun period without published figures, MEASURED, manual provenance. WF-14 reports on WF-02's periods, so the
 * project needs one first. Returns the published snapshot.
 */
export async function publishedFinancials(
  manager: PlatformApi,
  reviewer: PlatformApi,
  projectId: string,
  figures: { actualSar: string; forecastSar: string; asOfDate: string },
): Promise<Record<string, unknown>> {
  const started = await manager.ok<{ id: string }>('POST', '/financial-progress-updates', {
    projectId,
  });
  const update = started.body;
  await manager.ok(
    'PUT',
    `/financial-progress-updates/${update.id}`,
    {
      actualExpenditureToDateSar: figures.actualSar,
      forecastAtCompletionSar: figures.forecastSar,
      valueStatus: 'MEASURED',
      narrative: null,
      sourceReference: 'Invoice register (e2e)',
      asOfDate: figures.asOfDate,
    },
    { ifMatch: started.etag },
  );
  await manager.post(`/financial-progress-updates/${update.id}/submit`);
  await reviewer.post(`/financial-progress-updates/${update.id}/start-review`);
  await reviewer.post(`/financial-progress-updates/${update.id}/publish`);
  const snapshots = await reviewer.get<{ items: Record<string, unknown>[] }>(
    `/published-financial-snapshots?projectId=${projectId}`,
  );
  const latest = snapshots.items[0];
  if (latest === undefined) throw new Error(`No financial snapshot was published on ${projectId}.`);
  return latest;
}

/** The writes (anything but GET) a page sends to the API while the journey watches, as `METHOD /path`. */
export function recordWrites(page: Page): string[] {
  const writes: string[] = [];
  page.on('request', (request) => {
    const url = new URL(request.url());
    if (url.pathname.startsWith('/api/v1/') && request.method() !== 'GET') {
      writes.push(`${request.method()} ${url.pathname.slice('/api/v1'.length)}`);
    }
  });
  return writes;
}

/**
 * The writes FG-01 and FG-02 may send for a viewer: a report run (a POST that stores nothing, api-conventions R-35), an
 * export job, the controlled explorer's run and export, the viewer's own saved views (MOD-062: configuration, never data,
 * reports.md D-10) and ADR-019's layout of the viewer's own dashboard. Anything else would be a projection screen writing a
 * source.
 */
export const PROJECTION_WRITES =
  /^(POST \/(reports\/[A-Z_]+\/(run|export)|report-explorer\/(run|export)|saved-views|dashboards\/PORTFOLIO\/(personalize|reset-personalization))|(PUT|DELETE) \/saved-views\/[0-9a-f-]{36})$/;

export type ExportFormat = 'PDF' | 'XLSX' | 'CSV';

const FORMAT_LABELS: Record<ExportFormat, string> = {
  PDF: 'PDF',
  XLSX: 'Excel (XLSX)',
  CSV: 'CSV',
};

/**
 * MOD-060 on the report shown: the format chosen, the file in English, the job followed until the dialog offers the file,
 * and the file downloaded through the browser. Answers the file's name and bytes.
 */
export async function exportThroughTheDialog(
  page: Page,
  format: ExportFormat,
): Promise<{ name: string; bytes: Buffer }> {
  await page.getByRole('button', { name: 'Export', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Export report' });
  await dialog.getByRole('radio', { name: FORMAT_LABELS[format], exact: true }).check();
  await dialog.getByRole('radio', { name: 'English' }).check();
  await dialog.getByRole('button', { name: 'Start export' }).click();
  // The worker takes the job through its steps every few seconds; the dialog reads it every 2 s.
  const download = dialog.getByRole('button', { name: /^Download/ });
  await expect(download).toBeVisible({ timeout: 90_000 });
  const [file] = await Promise.all([page.waitForEvent('download'), download.click()]);
  const path = await file.path();
  const bytes = await readFile(path);
  await dialog.getByRole('button', { name: 'Close' }).click();
  return { name: file.suggestedFilename(), bytes };
}

/** A CSV as FG-02 writes it (RFC 4180, UTF-8 with a BOM): its rows of fields. */
export function csvRows(bytes: Buffer): string[][] {
  const text = bytes.toString('utf8').replace(/^\uFEFF/, '');
  const rows: string[][] = [];
  let row: string[] = [];
  let field = '';
  let quoted = false;
  for (let i = 0; i < text.length; i++) {
    const char = text.charAt(i);
    if (quoted) {
      if (char === '"' && text[i + 1] === '"') {
        field += '"';
        i++;
      } else if (char === '"') quoted = false;
      else field += char;
    } else if (char === '"') quoted = true;
    else if (char === ',') {
      row.push(field);
      field = '';
    } else if (char === '\r' && text[i + 1] === '\n') {
      row.push(field);
      rows.push(row);
      row = [];
      field = '';
      i++;
    } else field += char;
  }
  if (field !== '' || row.length > 0) rows.push([...row, field]);
  return rows;
}

/** The entries of a zip archive (an XLSX), by name, inflated: a minimal reader of the central directory (APPNOTE 4.3). */
export function zipEntries(bytes: Buffer): Map<string, string> {
  let end = bytes.length - 22;
  while (end >= 0 && bytes.readUInt32LE(end) !== 0x06054b50) end--;
  if (end < 0) throw new Error('Not a zip archive: no end of central directory.');
  const count = bytes.readUInt16LE(end + 10);
  let at = bytes.readUInt32LE(end + 16);
  const entries = new Map<string, string>();
  for (let n = 0; n < count; n++) {
    if (bytes.readUInt32LE(at) !== 0x02014b50) throw new Error('A broken central directory.');
    const method = bytes.readUInt16LE(at + 10);
    const size = bytes.readUInt32LE(at + 20);
    const nameLength = bytes.readUInt16LE(at + 28);
    const extraLength = bytes.readUInt16LE(at + 30);
    const commentLength = bytes.readUInt16LE(at + 32);
    const local = bytes.readUInt32LE(at + 42);
    const name = bytes.toString('utf8', at + 46, at + 46 + nameLength);
    const dataAt = local + 30 + bytes.readUInt16LE(local + 26) + bytes.readUInt16LE(local + 28);
    const data = bytes.subarray(dataAt, dataAt + size);
    entries.set(name, (method === 8 ? inflateRawSync(data) : data).toString('utf8'));
    at += 46 + nameLength + extraLength + commentLength;
  }
  return entries;
}

function xmlText(value: string): string {
  return value
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, '&');
}

/** A worksheet's rows of cell texts: inline strings and typed numbers, in column order. */
export function sheetRows(xml: string): string[][] {
  return [...xml.matchAll(/<row\b[^>]*>(.*?)<\/row>/gs)].map((row) =>
    [...(row[1] ?? '').matchAll(/<c\b[^>]*?(?:\/>|>(.*?)<\/c>)/gs)].map((cell) => {
      const body = cell[1] ?? '';
      const inline = /<t\b[^>]*>(.*?)<\/t>/s.exec(body);
      if (inline !== null) return xmlText(inline[1] ?? '');
      return xmlText(/<v>(.*?)<\/v>/s.exec(body)?.[1] ?? '');
    }),
  );
}

/** An XLSX as FG-02 writes it: the report sheet and the Context sheet, as rows of texts. */
export function xlsxSheets(bytes: Buffer): { report: string[][]; context: string[][] } {
  const entries = zipEntries(bytes);
  const sheet = (name: string): string => {
    const xml = entries.get(name);
    if (xml === undefined) throw new Error(`The workbook has no ${name}.`);
    return xml;
  };
  return {
    report: sheetRows(sheet('xl/worksheets/sheet1.xml')),
    context: sheetRows(sheet('xl/worksheets/sheet2.xml')),
  };
}

/**
 * The text a PDF of FG-02 draws, one string per text operation, in page order. Its streams are Flate-compressed and its one
 * font is Identity-H with a ToUnicode map (reports.md D-9), so each `<glyph ids> Tj` is read back through that map.
 */
export function pdfTexts(bytes: Buffer): string[] {
  const streams: string[] = [];
  const raw = bytes.toString('latin1');
  for (const match of raw.matchAll(/(?<!end)stream\r?\n/g)) {
    const from = match.index + match[0].length;
    const to = raw.lastIndexOf('\n', raw.indexOf('endstream', from));
    const data = bytes.subarray(from, to);
    try {
      streams.push(inflateSync(data).toString('latin1'));
    } catch {
      streams.push(data.toString('latin1'));
    }
  }
  const content = streams.join('\n');
  const map = new Map<string, string>();
  for (const [, glyph, unicode] of content.matchAll(/<([0-9A-F]{4})> <([0-9A-F]+)>/g)) {
    map.set(
      glyph ?? '',
      Buffer.from(unicode ?? '', 'hex')
        .swap16()
        .toString('utf16le'),
    );
  }
  return [...content.matchAll(/<([0-9A-F]*)> Tj/g)].map(([, hex]) =>
    (hex?.match(/.{4}/g) ?? []).map((glyph) => map.get(glyph) ?? '?').join(''),
  );
}

/** A parameter of the report shown (SCR-131–137's Parameter Panel), by its label. */
export function reportParameter(page: Page, label: string): Locator {
  return page
    .getByRole('form', { name: 'Report parameters' })
    .getByRole('combobox', { name: new RegExp(`^${label}`) });
}
