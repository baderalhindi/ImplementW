# Item 11 — Which map provider for the platform

Prepared 2026-11-07 for RPMO reviewers. Provider pages were read on 2026-11-07; links are in "Sources".

## Short answer

Run our own map data: **Protomaps (PMTiles) for Saudi Arabia, stored in the platform's own Saudi cloud region and drawn with MapLibre.** It costs only storage, has no usage limit, sends no project location to any third party, works without internet access and is allowed for production. Use the same setup for the demo. Of the hosted services, none can be used free for a government production platform, and none can be shown to keep data in Saudi Arabia.

## Where we are today

- The Home map (`features/dashboards/components/LocationsMap.tsx`) uses Leaflet with the public OpenStreetMap tile server (`https://tile.openstreetmap.org`).
- A project's Location tab draws no map. It shows the text "no map service is approved for the platform yet", and the code comment says why: a public tile server learns what area users look at (finding F-5).
- The Content-Security-Policy in `vite.config.ts` allows images only from the site itself, `data:` and `tile.openstreetmap.org`. Documentation `docs/architecture/endpoint-hardening.md` repeats it.
- The platform is planned for Google Cloud `me-central2` (Dammam). The region is provisional until AHDA confirms it in writing.

## Comparison

| | OpenStreetMap standard tiles | Self-hosted Protomaps / OpenMapTiles + MapLibre | MapTiler Cloud | Carto basemaps | Stadia Maps |
|---|---|---|---|---|---|
| **Cost and free limit** | Free. No numbers published | Software free. We pay only for storage and traffic (a Saudi extract is small; size not measured) | Free: 100k requests, 5k sessions a month. Flex: US$30 a month incl. 500k requests, then US$0.15 per 1k | Free: 1M requests a month commercial, 5M non-commercial. Paid: US$500 a month for 10M | Free: 200k credits a month, no overage. Paid from US$20 a month for 1M credits |
| **Government production use** | Allowed, but "best effort, no SLA" and access can be withdrawn at any point for commercial services. Heavy use is blocked without notice | Yes. Data and software licences (ODbL, BSD, CC-BY) permit it | Free plan page says "testing, prototyping, personal or non-commercial". The terms do not ban commercial use of the free plan. The two pages differ, so we need MapTiler to confirm in writing | Free commercial tier exists. The terms define commercial use broadly (any business or revenue use); whether a government authority counts is not stated. No SLA. Keys may be suspended at any time | Free plan: "commercial use not allowed". Whether a government body counts as commercial is not stated |
| **Arabic place names** | Shows the local name, which is Arabic in Saudi Arabia. No language switch | Yes. `name:ar` is supported. Arabic needs MapLibre's right-to-left text plugin and Arabic fonts that we host | Not verified | Not verified | Not verified |
| **Hosted in Saudi Arabia** | No. Servers outside the Kingdom | Yes. We choose the location (`me-central2`), no outside call | Could not find any Saudi region or residency commitment | Not stated | Not stated |
| **Attribution** | "© OpenStreetMap contributors", visible on the map | "© OpenStreetMap contributors" plus "© Protomaps" or "© OpenMapTiles" | "© MapTiler" with logo on the free plan, plus "© OpenStreetMap" | "© OpenStreetMap contributors, © CARTO" | Stadia Maps, OpenMapTiles and OpenStreetMap credits |
| **Works offline / on a closed network** | No. Bulk download for offline use is forbidden | Yes, fully | No | No. Caching more than 30 days and server-side proxying are forbidden | No |
| **Other limits** | Must send a Referer. Must cache 7 days or more | We must update the file ourselves (daily builds exist) | Fees and terms set by MapTiler; on-premise page exists but not read | Cannot proxy or store tiles | Commercial licence by arrangement |

Two further points on the hosted services: each one sees the visitor's IP address and the map area being viewed, which is the privacy concern already recorded as F-5; and each can change its prices or terms (Carto's page says commercial keys registered before 23 September 2026 keep working only until 30 November 2026).

## Reasons for the recommendation

1. **Data stays in the Kingdom.** The platform's own rule (ADR-001 C-1 and C-11, as quoted in the docs) keeps data in Saudi Arabia. A third-party tile server receives each user's IP address and the area they view. No hosted provider documents a Saudi region.
2. **No licence question.** The hosted free tiers either forbid commercial use or leave government use undefined, and the OpenStreetMap policy says commercial services can be cut off. Self-hosting needs no permission.
3. **Cost is flat and tiny.** A country file is served as a static file; no per-request fee, no quota that a busy day can exhaust.
4. **Works on a closed network** and in the demo without internet.
5. **Cost of choosing it:** we own updates, fonts and Arabic text rendering (see below).

## What to run

- **Production:** Protomaps PMTiles extract of Saudi Arabia in a Cloud Storage bucket in `me-central2`, served through the platform's existing load balancer under the site's own address (same origin).
- **Demo:** the same PMTiles file from the demo stack. No internet needed and no second code path to test. If a demo must be shown before the work below is done, the current OpenStreetMap tiles are acceptable for a low-traffic demo only; they must not go to production.

## Work needed

1. **Swap Leaflet for MapLibre GL** in `LocationsMap.tsx` (replace the tile layer; keep the circle markers as MapLibre markers) and add the `pmtiles` library. Estimate: 2–3 days including the keyboard and screen-reader behaviour that exists now.
2. **Build the Saudi extract** with the `pmtiles extract` command (supports a bounding box and `--maxzoom`; each extra zoom level roughly doubles size). Publish it to the bucket; set a refresh schedule (for example quarterly). The final file size was not measured.
3. **Arabic labels:** use a style with `name:ar` as the first choice, load the right-to-left text plugin from our own server, and host the font files (glyphs) ourselves. Whether Protomaps' font set covers Arabic was not verifiable from its documentation, so test this first; it decides how much work this step is.
4. **Location tab:** add a map on the project's Location tab using the same component (one pin from the project's coordinates), and remove the "no map service is approved" text and its translation key.
5. **Security headers** (`vite.config.ts`, the deployed host's headers, and `endpoint-hardening.md`): remove `https://tile.openstreetmap.org` from `img-src`; add `worker-src blob:` and `img-src blob:` because MapLibre draws in a worker; keep `connect-src 'self'` if the tile file and fonts are served from the site's own address. The remote-image and `Referrer-Policy` settings added for OpenStreetMap in the code can go. If the bucket is on another address instead, add that address to `connect-src`. No nginx change is needed because the repo has no nginx config; the headers live in `vite.config.ts` and whatever serves the built site.
6. **Bucket and load balancer:** the file must support HTTP range requests and caching. Terraform change, held until AHDA confirms the region.
7. **Attribution** line on every map: "© OpenStreetMap contributors, © Protomaps".
8. **Tests and rescan:** update the end-to-end journeys and the security-header test, and repeat the ZAP scan on the SPA.

## What could not be verified

- Arabic label support and any Saudi hosting for MapTiler, Carto and Stadia: their pages do not say. Hosted-provider Arabic support is therefore marked "not verified", not "no".
- Whether a government body counts as "commercial" under Carto, Stadia or MapTiler free tiers; written confirmation from each would be needed.
- MapTiler's pricing page and terms differ on commercial free use.
- The size of a Saudi PMTiles file, Arabic font coverage of the Protomaps assets, and the OpenMapTiles regional-extract steps (the page read covers only the default sample).
- Whether `me-central2` Cloud Storage and load balancer features match the plan: assumed from the existing docs, not re-checked here.
- ADR-001 itself was not opened; its clause numbers are as quoted in other docs.

## Sources (read 2026-11-07)

- OSM tile usage policy: https://operations.osmfoundation.org/policies/tiles/
- MapTiler pricing: https://www.maptiler.com/cloud/pricing/
- MapTiler terms: https://www.maptiler.com/terms/
- Carto basemaps: https://carto.com/basemaps
- Carto basemap terms: https://carto.com/legal/basemap-terms/
- Stadia Maps pricing: https://stadiamaps.com/pricing/
- Stadia Maps documentation (attribution and licensing note): https://docs.stadiamaps.com/guides/migrating-from-stamen-map-tiles/
- Protomaps: https://docs.protomaps.com/
- Protomaps downloads: https://docs.protomaps.com/basemaps/downloads
- Protomaps localization: https://docs.protomaps.com/basemaps/localization
- OpenMapTiles: https://openmaptiles.org/docs/
- Repo: `src/frontend/src/features/dashboards/components/LocationsMap.tsx`, `src/frontend/src/features/projects/workspace/LocationTab.tsx`, `src/frontend/vite.config.ts`, `docs/architecture/endpoint-hardening.md` (in `artifacts/wt-round5`)
