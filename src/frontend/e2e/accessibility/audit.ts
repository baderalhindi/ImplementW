import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';

import { type Page } from '@playwright/test';
import type axe from 'axe-core';

// The two automated halves of the platform accessibility audit (TASK-087, docs/testing/accessibility-audit.md §3): axe-core in a
// real browser at the working baseline, WCAG 2.1 Level AA (PTBC-040, OQ-010), and a keyboard walk that presses Tab through the
// screen as a keyboard user would. The component tests run axe in jsdom, which cannot measure colour contrast; this run can.

/** WCAG 2.0 and 2.1, Levels A and AA: the baseline's rules. axe's best-practice rules run too and are reported apart. */
export const BASELINE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] as const;

/** The impacts the acceptance criterion counts: zero of these on the audited screens. */
export const BLOCKING_IMPACTS = ['critical', 'serious'] as const;

// Evaluated through the DevTools protocol, which the page's Content Security Policy (script-src 'self') does not govern: an
// injected <script> tag would be refused, and the audit must run against the production headers unchanged.
const axeSource = readFileSync(
  createRequire(import.meta.url).resolve('axe-core/axe.min.js'),
  'utf8',
);

export interface Violation {
  rule: string;
  impact: string;
  help: string;
  helpUrl: string;
  /** WCAG success criteria from axe's tags, e.g. "1.4.3"; empty for a best-practice rule. */
  criteria: string[];
  baseline: boolean;
  targets: { selector: string; html: string; summary: string }[];
}

export interface AxeOutcome {
  axeVersion: string;
  violations: Violation[];
  /** Rules axe could not decide (e.g. contrast over an image): reviewed by hand, §5. */
  incomplete: {
    rule: string;
    impact: string;
    nodes: number;
    /** `related`: the nodes axe's check points at, e.g. the header cell it found no data cells for. */
    targets: { selector: string; summary: string; related: string[] }[];
  }[];
  passes: number;
}

function criteriaOf(tags: string[]): string[] {
  return tags
    .map((tag) => /^wcag(\d)(\d)(\d+)$/.exec(tag))
    .filter((match): match is RegExpExecArray => match !== null)
    .map((match) => `${match[1] ?? ''}.${match[2] ?? ''}.${match[3] ?? ''}`);
}

export async function runAxe(page: Page): Promise<AxeOutcome> {
  if (!(await page.evaluate(() => 'axe' in window))) await page.evaluate(axeSource);
  const results = await page.evaluate(() =>
    (window as unknown as { axe: typeof axe }).axe.run(document, {
      resultTypes: ['violations', 'incomplete'],
    }),
  );
  return {
    axeVersion: results.testEngine.version,
    violations: results.violations.map((v) => ({
      rule: v.id,
      impact: v.impact ?? 'unknown',
      help: v.help,
      helpUrl: v.helpUrl,
      criteria: criteriaOf(v.tags),
      baseline: v.tags.some((tag) => (BASELINE_TAGS as readonly string[]).includes(tag)),
      targets: v.nodes.map((n) => ({
        selector: n.target.join(' '),
        html: n.html.slice(0, 300),
        summary: (n.failureSummary ?? '').slice(0, 400),
      })),
    })),
    incomplete: results.incomplete.map((v) => ({
      rule: v.id,
      impact: v.impact ?? 'unknown',
      nodes: v.nodes.length,
      targets: v.nodes.slice(0, 10).map((n) => ({
        selector: n.target.join(' '),
        summary: (n.failureSummary ?? '').slice(0, 400),
        related: [...n.any, ...n.all, ...n.none]
          .flatMap((check) => check.relatedNodes ?? [])
          .slice(0, 5)
          .map((related) => `${related.target.join(' ')} ${related.html.slice(0, 120)}`),
      })),
    })),
    passes: results.passes.length,
  };
}

export function blocking(outcome: AxeOutcome): Violation[] {
  return outcome.violations.filter(
    (v) => v.baseline && (BLOCKING_IMPACTS as readonly string[]).includes(v.impact),
  );
}

export interface KeyboardOutcome {
  /** Visible, enabled controls a keyboard user must be able to reach (FE-AX-001). */
  controls: number;
  /** Tab presses until focus came back round (or the limit). */
  presses: number;
  /** Distinct controls focus landed on, in order, as "role name". */
  order: string[];
  /** Controls Tab never reached, neither directly nor through their composite widget's one tab stop. */
  unreached: string[];
  /** Controls that took focus without any visible change: no outline, ring, border, background or underline. */
  noVisibleFocus: string[];
  /** Focus stuck on one element for TRAP_PRESSES consecutive presses. */
  trapped: string[];
  /** Within a modal: elements behind it that focus reached, which a modal must never allow. */
  escaped: string[];
  /** Whether focus went round the whole page and back to where it started. */
  cycled: boolean;
}

const MAX_PRESSES = 600;
/** Presses on one element before it counts as a trap: more than a date-time input's segments. */
const TRAP_PRESSES = 6;

/**
 * Presses Tab from the top of the page (or from where focus is, within a modal) until focus comes back round, and compares
 * what it reached with every visible, enabled control in `within`. A composite widget (tab list, radio group, grid, menu,
 * toolbar) using a roving tabindex is one tab stop: its members are reached when any of them is. Native radios of one name
 * are one tab stop too.
 */
export async function walkWithTab(page: Page, within = 'body'): Promise<KeyboardOutcome> {
  await page.evaluate((scope) => {
    const CONTROL =
      'a[href], area[href], button, input:not([type="hidden"]), select, textarea, summary, iframe, [contenteditable=""], [contenteditable="true"], [tabindex], [role="button"], [role="link"], [role="checkbox"], [role="radio"], [role="switch"], [role="tab"], [role="menuitem"], [role="option"], [role="combobox"], [role="slider"], [role="spinbutton"], [role="textbox"], [role="gridcell"][tabindex]';
    const COMPOSITE =
      '[role="tablist"], [role="radiogroup"], [role="grid"], [role="treegrid"], [role="menu"], [role="menubar"], [role="toolbar"], [role="listbox"], [role="tree"]';
    const visible = (el: Element): boolean => {
      const box = el.getBoundingClientRect();
      const style = getComputedStyle(el);
      return (
        box.width > 0 && box.height > 0 && style.visibility !== 'hidden' && style.display !== 'none'
      );
    };
    const usable = (el: Element): boolean =>
      !el.closest('[inert], [aria-hidden="true"]') &&
      !(el as HTMLButtonElement).disabled &&
      el.getAttribute('aria-disabled') !== 'true' &&
      !(
        el.getAttribute('tabindex') === '-1' &&
        !el.closest(COMPOSITE) &&
        el.getAttribute('role') === null
      ) &&
      visible(el);
    const label = (el: Element): string => {
      const role = el.getAttribute('role') ?? el.tagName.toLowerCase();
      const input = el as Partial<HTMLInputElement>;
      const name = (
        el.getAttribute('aria-label') ??
        input.labels?.[0]?.textContent ??
        (el.textContent || (input.value ?? ''))
      )
        .replace(/[⁨⁩]/g, '')
        .replace(/\s+/g, ' ')
        .trim()
        .slice(0, 60);
      return `${role} "${name}"`;
    };
    const root = document.querySelector(scope) ?? document.body;
    const controls = Array.from(root.querySelectorAll(CONTROL)).filter(usable);
    controls.forEach((el, i) => {
      el.setAttribute('data-a11y-control', String(i));
    });
    const styleOf = (el: Element): string => {
      const s = getComputedStyle(el);
      return [
        s.outlineStyle,
        s.outlineWidth,
        s.outlineColor,
        s.boxShadow,
        s.borderColor,
        s.backgroundColor,
        s.textDecorationLine,
        s.color,
      ].join('|');
    };
    const unfocused = new Map(controls.map((el) => [el, styleOf(el)]));
    (window as unknown as { a11yWalk: unknown }).a11yWalk = {
      root,
      controls,
      label,
      styleOf,
      unfocused,
      COMPOSITE,
    };
    if (root === document.body) {
      // Start from the top of the page. blur() would leave the browser's sequential focus starting point where focus was;
      // focusing an element at the very start of <body> (out of the tab order) moves it there.
      const start = document.createElement('span');
      start.tabIndex = -1;
      start.setAttribute('data-a11y-start', '');
      document.body.prepend(start);
      start.focus();
    }
  }, within);

  const visits: {
    index: string | null;
    label: string;
    visibleChange: boolean;
    outside: boolean;
  }[] = [];
  let presses = 0;
  let cycled = false;
  const trapped: string[] = [];
  const escaped: string[] = [];
  let streak = 0;
  while (presses < MAX_PRESSES) {
    await page.keyboard.press('Tab');
    presses += 1;
    const visit = await page.evaluate(() => {
      const walk = (
        window as unknown as {
          a11yWalk: {
            root: Element;
            label: (el: Element) => string;
            styleOf: (el: Element) => string;
            unfocused: Map<Element, string>;
          };
        }
      ).a11yWalk;
      const el = document.activeElement;
      if (el === null || el === document.body)
        return { index: null, label: 'body', visibleChange: true, outside: false };
      if (!walk.root.contains(el))
        return { index: null, label: walk.label(el), visibleChange: true, outside: true };
      const before = walk.unfocused.get(el);
      const ring = getComputedStyle(el);
      const outline =
        ring.outlineStyle !== 'none' &&
        parseFloat(ring.outlineWidth) > 0 &&
        ring.outlineColor !== 'rgba(0, 0, 0, 0)';
      return {
        index: el.getAttribute('data-a11y-control'),
        label: walk.label(el),
        visibleChange:
          outline ||
          ring.boxShadow !== 'none' ||
          (before !== undefined && before !== walk.styleOf(el)),
        outside: false,
      };
    });
    if (visit.outside) {
      // Focus left the scope (a modal) for the page behind it.
      escaped.push(visit.label);
      if (escaped.length > 3) break;
      continue;
    }
    const previous = visits[visits.length - 1];
    if (previous !== undefined && previous.index !== null && previous.index === visit.index) {
      // Tab moves through a native date or time input's segments (day, month, year) without leaving the element, so a few
      // presses on one element are normal; a trap is focus that does not move on at all.
      streak += 1;
      if (streak >= TRAP_PRESSES) {
        trapped.push(visit.label);
        break;
      }
      continue;
    }
    if (visits.length > 0 && visit.index !== null && visit.index === visits[0]?.index) {
      cycled = true;
      break;
    }
    if (visit.index === null && visits.length > 0 && previous?.index !== null) {
      // Focus left the document (the browser chrome) after the last control: the next press starts the page again.
      continue;
    }
    streak = 1;
    visits.push(visit);
  }

  return page.evaluate(
    ({
      reachedIndexes,
      order,
      presses: count,
      cycled: wentRound,
      trapped: stuck,
      invisible,
      left,
    }) => {
      const walk = (
        window as unknown as {
          a11yWalk: { controls: Element[]; label: (el: Element) => string; COMPOSITE: string };
        }
      ).a11yWalk;
      document.querySelector('[data-a11y-start]')?.remove();
      const reached = new Set(reachedIndexes);
      const reachedEls = walk.controls.filter((el) =>
        reached.has(el.getAttribute('data-a11y-control') ?? ''),
      );
      const reachedGroups = new Set(
        reachedEls.map((el) => el.closest(walk.COMPOSITE)).filter((g) => g !== null),
      );
      const reachedRadioNames = new Set(
        reachedEls
          .filter((el) => el instanceof HTMLInputElement && el.type === 'radio')
          .map(
            (el) => `${(el as HTMLInputElement).form?.id ?? ''}/${(el as HTMLInputElement).name}`,
          ),
      );
      const unreached = walk.controls
        .filter((el) => !reached.has(el.getAttribute('data-a11y-control') ?? ''))
        .filter((el) => {
          const group = el.closest(walk.COMPOSITE);
          if (group !== null && reachedGroups.has(group)) return false;
          if (el instanceof HTMLInputElement && el.type === 'radio')
            return !reachedRadioNames.has(`${el.form?.id ?? ''}/${el.name}`);
          // A control inside a reached control (a link wrapping a button) shares its tab stop.
          return !reachedEls.some((r) => r.contains(el) || el.contains(r));
        })
        .map((el) => walk.label(el));
      return {
        controls: walk.controls.length,
        presses: count,
        order,
        unreached,
        noVisibleFocus: invisible,
        trapped: stuck,
        escaped: left,
        cycled: wentRound,
      };
    },
    {
      reachedIndexes: [
        ...new Set(visits.map((v) => v.index).filter((i): i is string => i !== null)),
      ],
      order: visits.filter((v) => v.index !== null).map((v) => v.label),
      presses,
      cycled,
      trapped,
      left: [...new Set(escaped)],
      invisible: [
        ...new Set(visits.filter((v) => v.index !== null && !v.visibleChange).map((v) => v.label)),
      ],
    },
  );
}

export interface ContrastReview {
  selector: string;
  text: string;
  foreground: string;
  background: string;
  ratio: number;
  /** 4.5:1, or 3:1 for large text (24 px, or 18.66 px bold) — WCAG 1.4.3. */
  required: number;
  passes: boolean;
}

/**
 * Decides the contrast axe left undecided (§3.1): it gives up when a cell is clipped by a scrolling container or overlapped,
 * though the text's own colours are plain. The background is the first opaque one up the element's ancestors; a translucent
 * layer on the way is composited over it. Answers one review per undecided node.
 */
export async function reviewContrast(page: Page, outcome: AxeOutcome): Promise<ContrastReview[]> {
  const selectors = outcome.incomplete
    .filter((v) => v.rule === 'color-contrast')
    .flatMap((v) => v.targets.map((t) => t.selector));
  if (selectors.length === 0) return [];
  return page.evaluate((targets) => {
    const parse = (value: string): number[] => (value.match(/[\d.]+/g) ?? []).map(Number);
    const luminance = ([r = 0, g = 0, b = 0]: number[]): number => {
      const channel = (c: number) => {
        const s = c / 255;
        return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
      };
      return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
    };
    const over = (top: number[], bottom: number[]): number[] => {
      const alpha = top[3] ?? 1;
      return [0, 1, 2].map((i) => (top[i] ?? 0) * alpha + (bottom[i] ?? 0) * (1 - alpha));
    };
    const backgroundOf = (el: Element): number[] => {
      const layers: number[][] = [];
      for (let node: Element | null = el; node !== null; node = node.parentElement) {
        const colour = parse(getComputedStyle(node).backgroundColor);
        const alpha = colour[3] ?? 1;
        if (alpha === 0) continue;
        layers.push(colour);
        if (alpha === 1) break;
      }
      return layers.reduceRight((base, layer) => over(layer, base), [255, 255, 255]);
    };
    return targets.flatMap((selector) => {
      const el = document.querySelector(selector);
      if (el === null) return [];
      const style = getComputedStyle(el);
      const background = backgroundOf(el);
      const foreground = over(parse(style.color), background);
      const [light, dark] = [luminance(foreground), luminance(background)].sort((a, b) => b - a);
      const ratio = Math.round((((light ?? 0) + 0.05) / ((dark ?? 0) + 0.05)) * 100) / 100;
      const size = parseFloat(style.fontSize);
      const large = size >= 24 || (size >= 18.66 && Number(style.fontWeight) >= 700);
      const required = large ? 3 : 4.5;
      return [
        {
          selector,
          text: el.textContent.trim().slice(0, 40),
          foreground: `rgb(${foreground.map(Math.round).join(', ')})`,
          background: `rgb(${background.map(Math.round).join(', ')})`,
          ratio,
          required,
          passes: ratio >= required,
        },
      ];
    });
  }, selectors);
}

export interface LanguagePart {
  text: string;
  /** The effective lang of the text: its nearest ancestor's lang attribute. */
  lang: string;
  element: string;
}

/**
 * Text in the other script than the page's language whose effective lang is still the page's (WCAG 3.1.2, Language of
 * Parts): on an Arabic page, a run of two or more Latin words; on an English page, any Arabic. Proper names are exempt from
 * 3.1.2 and look the same, so the list is reviewed by hand (§5), not counted.
 */
export async function languageOfParts(page: Page, language: 'ar' | 'en'): Promise<LanguagePart[]> {
  return page.evaluate((pageLanguage) => {
    const other = pageLanguage === 'ar' ? /[A-Za-z]{2,}[a-z]\s+[A-Za-z][a-z]+/ : /[؀-ۿ]/;
    const found = new Map<string, { text: string; lang: string; element: string }>();
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node !== null; node = walker.nextNode()) {
      const parent = node.parentElement;
      const text = (node.textContent ?? '').replace(/[⁨⁩]/g, '').trim();
      if (parent === null || text === '' || !other.test(text)) continue;
      if (parent.closest('[aria-hidden="true"], script, style') !== null) continue;
      const lang = (parent.closest('[lang]')?.getAttribute('lang') ?? '').toLowerCase();
      if (!lang.startsWith(pageLanguage)) continue;
      const cls = (parent.getAttribute('class') ?? '').split(' ')[0] ?? '';
      const element = `${parent.tagName.toLowerCase()}${cls === '' ? '' : `.${cls}`}`;
      found.set(`${element}|${text}`, { text: text.slice(0, 80), lang, element });
    }
    return [...found.values()].slice(0, 80);
  }, language);
}

export interface ReflowOutcome {
  width: number;
  /** The page's own scroll width at that viewport; above `width`, the page scrolls sideways (WCAG 1.4.10 fails). */
  scrollWidth: number;
  /** Elements reaching past the viewport outside any scroll container of their own, innermost first. */
  overflowing: string[];
}

/**
 * WCAG 1.4.10 Reflow: at 320 CSS px wide (1280 px at 400% zoom) the page must not scroll in two directions. Content that
 * needs two dimensions — a data table, the permission matrix, a Gantt chart — may scroll inside its own container, so only
 * overflow that escapes every scroll container counts. The viewport is restored afterwards.
 */
export async function reflowAt(page: Page, width = 320): Promise<ReflowOutcome> {
  const original = page.viewportSize();
  await page.setViewportSize({ width, height: 800 });
  await page.waitForFunction((w) => window.innerWidth === w, width);
  await page.waitForTimeout(250); // one layout pass after the resize; there is no event to wait for
  const outcome = await page.evaluate((w) => {
    const scrolls = (el: Element): boolean => {
      const overflow = getComputedStyle(el).overflowX;
      return overflow === 'auto' || overflow === 'scroll' || overflow === 'hidden';
    };
    const escapes = (el: Element): boolean => {
      const box = el.getBoundingClientRect();
      if (box.width === 0 || (box.right <= w + 1 && box.left >= -1)) return false;
      for (
        let node = el.parentElement;
        node !== null && node !== document.body;
        node = node.parentElement
      ) {
        if (scrolls(node)) return false;
      }
      return true;
    };
    const all = Array.from(document.body.querySelectorAll('*')).filter(escapes);
    const innermost = all.filter((el) => !all.some((other) => other !== el && el.contains(other)));
    return {
      width: w,
      scrollWidth: document.documentElement.scrollWidth,
      overflowing: [
        ...new Set(
          innermost.map((el) => {
            const cls = (el.getAttribute('class') ?? '').split(' ')[0] ?? '';
            return `${el.tagName.toLowerCase()}${cls === '' ? '' : `.${cls}`} "${el.textContent.trim().slice(0, 30)}"`;
          }),
        ),
      ].slice(0, 20),
    };
  }, width);
  if (original !== null) await page.setViewportSize(original);
  return outcome;
}
