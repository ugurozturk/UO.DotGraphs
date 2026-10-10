# Microsoft DI and ASP.NET Core analysis

These extractors record **static declarations**, not execution, container membership,
resolution, or host activation. UO.Graphs does not load the analyzed assembly through
reflection or start its application, container, HTTP host, or database. Current
source 0.10.0 uses query/status JSON v5 / SQLite 3 / delivery 1; these extractors began in Phase 7.

## Extractor contract

Internal `IFrameworkExtractor` exposes Id and Extract(FrameworkExtractionContext).
The statically registered microsoft-di-v1 runs before aspnet-core-v1. DI shares
service types seen in the same compilation for implicit HTTP parameter candidates;
registrations from other projects are not silently imported into that set.

The context supplies compilation/documents, identity resolution, evidence, and
graph building, without storage/host access. Tests use real framework metadata
references; MSBuild/CLI/SQLite integration is checked separately. No plugin loader,
marketplace, DI infrastructure, or extra product project is introduced.

## Relationship meanings

| Kind | Direction and confidence |
| --- | --- |
| REGISTERS | Registration → declared service type; exact static argument, not executed registration |
| IMPLEMENTED_BY | Registration → declared implementation; unresolved/null for factory or computed type |
| USES_FACTORY | Registration → bound lambda/method; unresolved delegate-variable value stays null |
| INJECTS | Public constructor/handler → service parameter type; exact for declared constructor/FromServices, inferred for implicit HTTP candidates |
| HANDLED_BY | Endpoint declaration → action/handler; may be exact while activation remains notProven |
| ACTIVATES | Controller action → public constructor candidate; always inferred |

CALLS stays at its compile-time target: an IMessage.Read call is not expanded to
registered implementations. REGISTERS and IMPLEMENTED_BY meet at the registration
node; no single selected service→implementation edge is invented.

Evidence retains ranges, decoded-source SHA-256, analysis/context IDs, reasons,
and constructed boundTarget. Erroneous declarations do not acquire exact targets;
candidates remain related symbols. Generic type endpoints normalize to definitions;
constructed uses such as IRepository<string> remain in occurrence signatures.

## Microsoft DI matrix

Supported recognition requires the real ServiceCollectionServiceExtensions symbol
and metadata assembly, not an AddScoped-like name:

- AddSingleton/AddScoped/AddTransient in extension or static-call form; generic
  self/interface→implementation, typeof overloads, and open-generic declarations.
- Factory method/lambda and instance registrations. Only explicit new expressions
  establish concrete instance type; factory constraints do not prove runtime type.
- Separate registration entities for multiple declarations/collections, preserving
  lifetime, form, conditional, service/implementation type, openGeneric, and limitations.
  Collection alias/order analysis is not performed.
- Normal/primary public constructor parameters as eligibility evidence, keeping
  every constructor candidate. Optional parameters, overload selection, and actual
  DI construction are not resolved.
- if/switch/loops/conditional access/short-circuit contexts marked conditional,
  without evaluating branches or reachability.
- Unsupported calls receiving an IServiceCollection, including scanning, wrappers,
  descriptors, and mutation, as opaque null-target REGISTERS candidates. This does
  not claim every such call registers a service.

Dynamic invocation produces a diagnostic without invented registration/targets.
Factory return-flow, scanning expansion, keyed registration, conditional outcomes,
and actual service resolution remain unsupported.

## ASP.NET Core matrix

Minimal APIs recognize EndpointRouteBuilderExtensions.MapGet, MapPost, and
MapMethods. Constant inline arrays and C# collection expressions produce one
endpoint per method; dynamic/null/empty method collections remain unresolved.
Lambda, method-group, local, and top-level handlers are supported; delegate-variable
data flow is not.

MapGroup chains and single-initializer local aliases are followed for up to 16
steps if not reassigned or escaped by ref. Prefixes/routes must be Roslyn constants.
WebApplication roots are recognized. Arbitrary IEndpointRouteBuilder parameters,
mutated groups, fields/properties/helper returns, and computed prefixes leave the
route null. Group evidence is included in endpoint Sources. Conventions and
endpoint-filter behavior are not executed.

Controllers cover directly declared public, nonabstract, nongeneric classes and
public instance nongeneric actions with built-in Route, HttpGet/Post/Put/Delete/
Patch/Head/Options, AcceptVerbs, ActionName, and Area. Controller/action prefixes,
absolute `/` and `~/`, controller/action/area tokens, NonController/NonAction, and
default Async-suffix removal are interpreted as declarations, not final host routes.

Conventional routes are not invented. Custom/source/metadata base-controller
inheritance and custom route providers produce diagnostics/unresolved routes.
Inherited action enumeration, application parts, custom conventions/transformers,
and all MVC selector combinations are not fully supported. Explicit FromServices
creates an exact type link; implicit parameters matching static registrations in
the compilation remain inferred. Model-binding precedence and cross-project runtime
availability are not proven.

## Endpoint identity and uncertainty

Canonical/SHA-256 identity includes project context, HTTP method, combined pattern,
handler, and declaration discriminator (relative file + syntax-child ordinal path,
including group declaration identity). It is independent of absolute checkout and
line number; structural syntax edits can change it.

`collisionKey` is context + method + route, not identity. Duplicate GET declarations
remain separate entities with a duplicate limitation; POST remains distinct.
Unconstrained controller routes use `*`. Unresolved method/route yields null
collisionKey and unresolved endpoint confidence, while the handler may independently
be exact. An unknown-route display label is not a real literal route.

Duplicate detection is lexical, not runtime constraint/case/precedence analysis.
Source declaration alone never proves activation (`activation: notProven`).
Framework diagnostics use `framework:*` and mark coverage partial; prior generator
or compilation limitations remain. Completeness is limited to this support matrix.

## Real fixtures and CLI

`samples/Phase7/Frameworks.csproj` uses Microsoft.AspNetCore.App FrameworkReference
for library/negative fixtures. `MinimalWeb/MinimalWeb.csproj` uses the real Web SDK
for top-level handlers. Tests compile/analyze them without running their hosts.
The recorded SDK 10.0.201 targeting/runtime pack is **10.0.5**.

```sh
uographs index samples/Phase7/Frameworks.csproj --trust-project --full --db .uographs/phase7-frameworks.db
uographs query find AddScoped --db .uographs/phase7-frameworks.db
uographs index samples/Phase7/MinimalWeb/MinimalWeb.csproj --trust-project --full --db .uographs/phase7-web.db
uographs query dependencies 'GET /api/time' --depth 3 --db .uographs/phase7-web.db
uographs export --format json --db .uographs/phase7-web.db
```

Paths through constructor candidates need include-inferred. Dependencies/path/impact
include these six kinds; callers/callees default to CALLS. Optional registration/
httpEndpoint metadata uses existing entity JSON payloads and is omitted when absent.
Re-index old snapshots to obtain these records. Atomic replacement removes obsolete
framework evidence too. The Web SDK remains outside the conservative base-SDK reuse
model where dependencies are unproven; no fresh/no-op claim is inferred.

## Framework license evidence

Phase 7 added no NuGet dependency; its historical inventory contained 50 packages.
The current combined inventory is [DEPENDENCIES](DEPENDENCIES.md). FrameworkReference
is limited to fixture/unit-test projects, not an ASP.NET runtime dependency in the
product. UO.Graphs does not download or copy the installed framework into its package.

Recorded upstream sources are [.NET DI](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview),
[Minimal route handlers](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/route-handlers?view=aspnetcore-10.0),
[controller routing](https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/routing?view=aspnetcore-10.0),
[ASP.NET Core 10.0.5 MIT](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.5/LICENSE.txt),
and [runtime 10.0.5 MIT](https://raw.githubusercontent.com/dotnet/runtime/v10.0.5/LICENSE.TXT).
The license texts were inspected on 2026-10-09. Preserve copyright/permission and
upstream notices when distributing applicable components.

Recorded installed `/usr/local/share/dotnet/LICENSE.txt` SHA-256:
`cfc21f5e8bd655ae997eec916138b707b1d290b83272c02a95c9f821b8c87310`;
ThirdPartyNotices.txt:
`2dc8f8c5a39401e928b5784ab564eb8b3ceb99ead3df8f260e0cab7e0bbecc7a`.
These are dated local observations, not universal SDK-file hashes.
