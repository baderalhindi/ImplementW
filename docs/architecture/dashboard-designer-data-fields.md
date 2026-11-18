# Portfolio Dashboard Designer — data fields (contract)

| Field | Value |
| --- | --- |
| Task | TASK-112 (client review follow-up, 18 Nov 2026) |
| Branch | `feat/task-112-dashboard-builder` (worktree `artifacts/wt-dashboard-builder`) |
| Builds on | `dashboard-designer.md` (D-1 … D-12) |
| Status | Contract for the build: the backend, the frontend and the tests are written against it in parallel |

## Why

The Portfolio Manager asked for "anything from the database he owns", not the eight fixed values. The designer now offers the platform's
governed data catalogue — the registered projections and their fields, the same one the Custom report builder reads (FG-01's register,
`IProjectionRowReader`) — with a calculation per value, a grouping by any status-type field and filters per visual. No free table, column or
SQL: every value is a registered field read under its own source's permission and masking (BR-DSH-030, FG-02 §8.2). A field the catalogue
lacks is a new projection (a developer task); it then appears in the designer by itself.

## Terms

- **Field key**: `<PROJECTION_CODE>.<FIELD_CODE>`, for example `FINANCIAL_KPI.FINANCIAL_POSITION.ACTUAL_EXPENDITURE_TO_DATE`. The projection
  code is the register's (it has one dot); the field code is the projection's field. Shape: `^[A-Z][A-Z_]*\.[A-Z][A-Z_]*\.[A-Z][A-Z0-9_]*$`.
- **Dimension key**: `PROJECT.<GROUP_BY>` for a project attribute already in `PortfolioGroupBy`, for example `PROJECT.STATUS`,
  `PROJECT.PORTFOLIO`, `PROJECT.SECTOR`. Used only in filters (a visual groups by a project attribute through its existing `groupBy`).
- **Only project-grain projections** are offered (the register's `Grain == Project`); `PROGRESS.PUBLISHED_PROGRESS_HISTORY` (snapshot grain)
  and `DASHBOARDS.DEFINITION_BACKLOG` (no grain) are not.
- **Field types** are the register's `ProjectionValueType`: `CODE`, `COUNT`, `PERCENT`, `DAYS`, `SAR`, `BOOLEAN`, `DATE`, `DATE_TIME`.
  Fields of type `BOOLEAN`, `DATE` and `DATE_TIME` are not offered in this version.

## Aggregations (`PortfolioFieldAggregation`)

`COUNT`, `SUM`, `AVERAGE`, `MIN`, `MAX`.

| Field type | Allowed | Meaning |
| --- | --- | --- |
| `CODE` | `COUNT` | the projects in the group whose value is revealed (use a filter to count one value, e.g. RED) |
| `COUNT`, `DAYS`, `SAR` | `COUNT`, `SUM`, `AVERAGE`, `MIN`, `MAX` | over the projects in the group whose value is revealed |
| `PERCENT` | `COUNT`, `AVERAGE`, `MIN`, `MAX` | a sum of percentages means nothing |

A value is calculated over the revealed cells only (`ProjectionCell.IsRevealed`); a group with no revealed cell has value `null` ("not
available", never 0), except `COUNT`, which is then `0`. `COUNT` is always formatted as a count, whatever the field's type.

## Visual (API, camelCase JSON) — additions to `PortfolioVisualInput` / `PortfolioVisualRequest` / `Visual`

```jsonc
{
  // … every existing property unchanged (title, visualType, measures, groupBy, layout*, section) …
  "values": [                                  // optional; null or absent = none
    { "field": "FINANCIAL_KPI.FINANCIAL_POSITION.ACTUAL_EXPENDITURE_TO_DATE", "aggregation": "SUM" }
  ],
  "groupField": "PROGRESS.PROJECT_HEALTH_STATUS.OVERALL_HEALTH",   // optional; a CODE-type field key, or null
  "filters": [                                 // optional; null or absent = none
    { "field": "PROJECT.STATUS", "values": ["ACTIVE"] },
    { "field": "FINANCIAL_KPI.KPI_CONDITION.RAG_RED", "values": [] }   // see rules: CODE fields and dimensions only
  ]
}
```

C# (Application, `Features/Project/Insights/PortfolioDesign.cs`):

```csharp
public enum PortfolioFieldAggregation { Count = 1, Sum = 2, Average = 3, Min = 4, Max = 5 }
public sealed record PortfolioFieldValue(string Field, PortfolioFieldAggregation Aggregation);
public sealed record PortfolioFieldFilter(string Field, IReadOnlyList<string> Values);
// PortfolioVisualInput and PortfolioVisualResult gain, as optional trailing parameters (default null):
//   IReadOnlyList<PortfolioFieldValue>? Values = null, string? GroupField = null, IReadOnlyList<PortfolioFieldFilter>? Filters = null
```

### Rules (checked by `PortfolioDesignService.Issue`, 422 `PROJECT_PORTFOLIO_DESIGN_INVALID`, field path `visuals[i].values|groupField|filters`)

1. A visual draws **either** the fixed `measures` **or** field `values`, never both. A `SECTION` draws neither.
2. `values`: the same counts per type as `measures` today (card 2, bar/pie/donut/gauge 1, column 3) except **table: up to 6**; each `field` is a
   key in the catalogue, each `aggregation` allowed for its type (table above); no two equal entries.
3. Gauge: one value, a `PERCENT` field with `AVERAGE`, `MIN` or `MAX`. Pie and donut: one value with `COUNT` or `SUM`.
4. `groupField`: a `CODE`-type field key in the catalogue. When set, `groupBy` must be `NONE`, and the type must be one that groups (bar,
   column, pie, donut, table). A card or gauge takes neither `groupBy` (other than `NONE`) nor `groupField`. The existing rule "a chart is
   grouped" is met by `groupBy != NONE` **or** `groupField != null`.
5. `filters`: at most 5, each a `CODE`-type field key or a dimension key, with 1 to 20 `values`, each a code shape `^[A-Z0-9_-]{1,64}$` for
   CODE fields and `STATUS`/`STAGE`/`ON_PLAN`, or a GUID for item, department and entity dimensions. A project passes when, for every filter,
   its value is one of the filter's values (a dimension's `null` key never passes).
6. `groupField`, `values[].field` and `filters[].field` that are field keys must be offered to the designer (`IPortfolioFieldReader.Fields`).

### Calculation (`PortfolioDesignService.ViewAsync`)

- Population: as today, `PortfolioProjectRows` under `PermissionOf(groupBy)` (`NONE` when the visual groups by a field), narrowed by the
  dashboard's filter bar.
- Then the visual's `filters`, then the groups: by `groupBy` (existing `KeyOf`) or by the `groupField` cell's value (`null` key = projects
  whose value is not revealed, labelled "not set" by the frontend), ordered by the group's project count, largest first, `null` last.
- Field cells come from **one** `IPortfolioFieldReader.ReadAsync` per view, for the distinct field keys of all visuals and the population's
  project ids.
- Result: the existing `PortfolioVisualGroup` (`Values` one per entry of `values`, in order; `Projects` = the group's projects; `Measured`
  = `ValueEntered` = the number of projects whose first value's cell is revealed; `OnPlan` = null) and the visual echoes `values`,
  `groupField`, `filters`.

## Field reader (dependency inversion: Project declares, Dashboards implements)

Project may not reference Dashboards (edge 47 runs Dashboards → Project). The design service in Project therefore depends on an interface it
declares; Dashboards implements it over `IProjectionRowReader`, and the Application `DependencyInjection` registers it.

```csharp
// Application/Features/Project/Contracts/PortfolioFields.cs (Project module; Contracts, because Dashboards may reach Project only there)
public enum PortfolioFieldType { Code = 1, Count = 2, Percent = 3, Days = 4, Sar = 5 }

/// One field the designer offers: its key, the projection it belongs to (code and domain), its field code, its type, whether it is
/// sensitive, the codes a CODE field takes (the register's Values), and the aggregations its type allows.
public sealed record PortfolioFieldDescriptor(
    string Key, string ProjectionCode, string SourceDomain, string FieldCode, PortfolioFieldType Type, bool IsSensitive,
    IReadOnlyList<string> Values, IReadOnlyList<PortfolioFieldAggregation> Aggregations);

/// A project's value of one field: the source's exact string, or null when not revealed to the caller (unknown, restricted or masked).
public sealed record PortfolioFieldCell(string? Value);

public interface IPortfolioFieldReader
{
    /// The project-grain fields of the register of types CODE, COUNT, PERCENT, DAYS, SAR, in the register's order.
    public IReadOnlyList<PortfolioFieldDescriptor> Fields { get; }

    /// For each project id given (and reached by the caller), each field key's cell, authorised and masked exactly as a report cell
    /// (base projection PROJECT.LIFECYCLE_STATE, MaskSensitiveFields false, IncludeIneligible false). A project the reader does not return
    /// has every cell null.
    public Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, PortfolioFieldCell>>> ReadAsync(
        Guid callerId, IReadOnlyCollection<Guid> projectIds, IReadOnlyCollection<string> fieldKeys, DateTimeOffset now,
        CancellationToken cancellationToken);
}

// Application/Features/Dashboards/PortfolioFieldReader.cs (Dashboards module): internal sealed class PortfolioFieldReader(IProjectionRowReader rows)
//   : IPortfolioFieldReader; registered services.AddScoped<IPortfolioFieldReader, PortfolioFieldReader>().
```

## Endpoint

`GET /api/v1/portfolio-insights/design/fields` — `DASHBOARD_DESIGN`, endpoint name `Project_GetPortfolioDesignFields`, 200:

```json
{
  "fields": [
    {
      "key": "FINANCIAL_KPI.FINANCIAL_POSITION.ACTUAL_EXPENDITURE_TO_DATE",
      "projectionCode": "FINANCIAL_KPI.FINANCIAL_POSITION",
      "sourceDomain": "WF-09",
      "fieldCode": "ACTUAL_EXPENDITURE_TO_DATE",
      "type": "SAR",
      "isSensitive": true,
      "values": [],
      "aggregations": ["COUNT", "SUM", "AVERAGE", "MIN", "MAX"]
    }
  ]
}
```

The existing design endpoints (`GET/PUT /portfolio-insights/design`, `POST …/preview`, `POST …/reset`) carry the three new properties on
each visual, in and out.

## Persistence

`project.portfolio_visual` gains one nullable `jsonb` column `data_fields`, holding `{"values":[…],"groupField":…,"filters":[…]}` exactly as
the API shape (camelCase, enum names UPPER_SNAKE); null when the visual draws fixed measures or is a section. The CHECK on `measures` becomes:
a non-section visual has 1–3 measures **or** a non-null `data_fields` with zero measures. Migration `TASK-112_AddPortfolioVisualDataFields`.
The service validates the JSON's content (rules above); the database holds only what the service wrote.

## Frontend

- `design/designApi.ts`: `Visual` gains `values?: FieldValue[] | null`, `groupField?: string | null`, `filters?: FieldFilter[] | null`;
  `designApi.fields()` reads the endpoint above.
- The settings pane's **Values** become a **Data** panel, the Power BI field list: a search box, then the fields grouped by source (the eight
  fixed figures first as "Portfolio figures", then each projection), each with its Arabic and English name. Ticking a field adds it with its
  type's first aggregation; each chosen value has an aggregation select limited to its allowed ones. Choosing a field when fixed measures are
  ticked (or the reverse) replaces them, with a one-line note. **Group by** lists the project attributes then the CODE fields. **Filters**:
  add a filter, pick a field (CODE fields, Status, Stage, On-plan, Portfolio, Sector, Entity …), tick its values.
- Names: `dashboards.designer.fieldSource.<PROJECTION_CODE with "." → "_">` and `dashboards.designer.field.<PROJECTION_CODE with "." → "_">.<FIELD_CODE>`
  in `i18n/en.json` and `i18n/ar.json`, for every field the endpoint lists; a CODE value's words come from `labelOfValue(projectionCode, value,
  null, language, t)` (dashboards/presentation.ts).
- A value is formatted by its field type and aggregation: `COUNT` as a count; `SAR` with `sarCompact`; `PERCENT` as `n%`; `DAYS` as a number
  of days; `COUNT` type as a number.
- Automatic titles: "<aggregation> of <field>" ("Sum of Actual expenditure to date"), "<…> by <group>" when grouped.

## Who builds what (parallel; do not edit another's files; do not commit — the lead integrates)

| Agent | Owns |
| --- | --- |
| Backend | everything under `src/backend/` (domain, configuration, migration, service, reader, controller, request model, unit tests in `PMPlatform.Tests.Unit/Application/Project/`), `docs/architecture/erd.dbml` |
| Frontend | `src/frontend/src/features/dashboards/design/*.ts(x)` except test files, `src/frontend/src/features/dashboards/i18n/*.json`, the designer block at the end of `src/frontend/src/app/styles.css` |
| Tests | `src/frontend/src/features/dashboards/design/*.test.ts(x)`, `src/frontend/src/test/portfolioInsightFixtures.ts` (design fixtures only), `artifacts/designer/fieldswalk.mjs` (browser walk) |

## As built (18 Nov 2026)

- `PortfolioVisualResult` also carries `valueTypes` (one `PortfolioFieldType` per entry of `values`, `null` otherwise): a dashboard viewer
  does not hold `DASHBOARD_DESIGN` and cannot read the field list, so the dashboard formats each value from the visual itself. A field later
  removed from the register reads as unrevealed cells typed `COUNT`.
- The API accepts `groupField` and `filters` on a visual drawing fixed measures too; the designer offers them only with field values, and
  going back to fixed figures clears them.
- `groupField` groups are ordered by project count, largest first, `null` last; `groupBy` groups keep their scale order. Averages are rounded
  to 2 places. Filter values compare ignoring case (GUIDs in `D` format).
- A newly ticked field starts with the first aggregation its type allows **on that visual type** (gauge: `AVERAGE`; pie and donut: `COUNT`).
- The filter dimensions offered are Status, On-plan, Portfolio, Initiative, Sector, Region, City, Priority, Department and Entity; Stage is
  left out (the frontend has no list of stage codes). Field values open no record list (no list filters by a field).
