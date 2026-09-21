# PMPlatform

Project-management platform for AHDA: a React SPA over an ASP.NET Core modular monolith on PostgreSQL (ADR-002), organised as 21 bounded modules (ADR-003).

- [CONTRIBUTING.md](CONTRIBUTING.md) — repository layout, toolchain, build and quality gates
- [docs/architecture/adrs/ADR-002-technology-stack.md](docs/architecture/adrs/ADR-002-technology-stack.md) — stack and directory scheme
- [docs/architecture/solution-architecture.md](docs/architecture/solution-architecture.md) — ADR-003: tiers, modules, call rules, enforcement

```sh
dotnet build src/backend -warnaserror && dotnet test src/backend
cd src/frontend && npm ci && npm run lint && npm run build
```
