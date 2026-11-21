/** The dashboards' page (FG-01), and the Portfolio Dashboard's rendering of it: the Portfolio menu's analysis (review H2). */
export const DASHBOARDS_PATH = '/dashboards';
export const PORTFOLIO_DASHBOARD_PATH = `${DASHBOARDS_PATH}?dashboard=PORTFOLIO`;
/** The Portfolio dashboard's designer (17 Nov 2026), under Dashboards and Reports. */
export const DASHBOARD_DESIGNER_PATH = `${DASHBOARDS_PATH}/design`;

/** A printable portfolio, initiative or sector card (the 5 Nov 2026 review §3). */
export function portfolioCardPath(
  dimension: 'portfolio' | 'initiative' | 'sector',
  itemId: string,
): string {
  return `/reports/cards/${dimension}/${itemId}`;
}
