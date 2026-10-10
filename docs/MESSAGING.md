# Mediator and messaging analysis

Current source 0.10.0 uses query/status JSON v5 / SQLite 3 / delivery 1. Messaging extraction
uses Roslyn symbols and IOperation; the product does not acquire a mediator/event
bus dependency or start the analyzed application, container, or broker.

## Verified contracts

| Module | Exact fixture baseline | Supported surface |
| --- | --- | --- |
| UO.Mediator | UO.Mediator.Shared **2.4.2**, MIT; repository RequestContracts.cs and service-registration sources plus actual DLL/XML/nuspec | IRequestDispatcher.DispatchAsync; IRequest<TResponse> and IRequest : IRequest<Unit>; response/no-response IRequestHandler.HandleAsync; IRequestBehavior and RequestHandlerNext.InvokeAsync |
| MediatR | **12.4.1**, MediatR.Contracts **2.0.1**, Apache-2.0; actual DLL and tag source | ISender.Send, response/no-response IRequestHandler, IPublisher.Publish, INotificationHandler, IPipelineBehavior |
| MassTransit | MassTransit.Abstractions **8.3.6**, Apache-2.0; actual DLL and tag source | IPublishEndpoint.Publish, ISendEndpoint.Send, IConsumer<T>.Consume(ConsumeContext<T>); generic/object/Type overloads |

Sources: [UO.Mediator](https://github.com/ugurozturk/UO.Mediator),
[MediatR 12.4.1](https://github.com/LuckyPennySoftware/MediatR/tree/v12.4.1/src/MediatR),
[MassTransit 8.3.6](https://github.com/MassTransit/MassTransit/tree/v8.3.6/src/MassTransit.Abstractions).
Their recorded nuspec repository commits are, respectively,
`3556291c4cbb8f6191983054298257ca94365cba`,
`fb309026775ef953a64fb5339d074426c1ad2c37`, and
`1ac5197c10ae9834b68c5721d28480e5c65ae876`.
Versions and transitive content hashes are locked; [DEPENDENCIES](DEPENDENCIES.md)
records licenses. These are fixture/test dependencies, not product runtime services.

The inspected UO.Mediator contract has no notification/Publish API. Cached MediatR
14.2.0 was not selected as this license/compatibility baseline. Recognition uses
metadata namespace, assembly, and bound interface/member, not Send/Handle-like
names. Assembly-major gates are UO 2, MediatR 12, and MassTransit 8; these gates do
not establish compatibility for every package in those majors. Acceptance applies
to the exact versions above. Framework APIs must be metadata references;
source-ProjectReference framework-contract compatibility is unverified.

## Direction and confidence

| Kind | Direction | Meaning |
| --- | --- | --- |
| HANDLES | Handler method → message | Exact bound request-handler contract |
| CONSUMES | Notification handler/consumer method → message | Exact contract, not subscription/delivery |
| DISPATCHES | Caller → message | Bound dispatch/send API and statically known message; unresolved type yields null |
| PUBLISHES | Caller → message | Bound publish API, not selection of one consumer |
| POSSIBLE_TARGET | Message → handler/consumer | Inferred local contract-compatible candidate; runtime target separately unresolved |
| BEHAVIOR_FOR | Behavior method → message | Exact closed pipeline contract; open generics unresolved |
| POSSIBLE_BEHAVIOR | Message → behavior | Inferred participant, not ordering/continuation proof |

CALLS remains the compile-time API target; dispatch evidence also records the bound
API. Exact API binding does not prove runtime recipient selection. Candidates are
contract-compatible declarations, not an active registration list. Direct generic
Microsoft DI AddSingleton/AddScoped/AddTransient<contract,implementation> adds a
separate registration occurrence, preserving conditional/source/hash evidence,
without changing inferred confidence.

Scanning, AddUOMediator/AddMediatR wrappers, container identity/order, factory
results, MassTransit receive endpoints/topology, and service boundaries are not
resolved. Every message retains null-target POSSIBLE_TARGET evidence for the open
runtime recipient set, even when local candidates exist. Coverage remains partial;
no local consumer is not proof of no consumer. Multiple handlers remain visible
without predicting selection or registration errors.

Pipeline behaviors are separate from handlers and are not forced into every path.
A continuation can run zero/one/many times. Order, conditions, and continuation
execution are unknown. Open-generic handler/behavior substitution and constraint
expansion are unsupported and retain null-target diagnostics.

## Message identity and scope

Canonical message identity includes framework, actual assembly identity, Roslyn
declaration ID, matching source project context where available, recursive closed
type arguments, and containing type. Request<int>/Request<string>, equal short or
qualified names from different assemblies/namespaces/frameworks remain distinct.
Display/source text is never identity evidence. Reference contexts retain the
normal identity rules; metadata contracts use assembly identity, not federation.

Optional Message metadata contains framework, constructed type, assembly, canonical
contract identity, scope, and limitations. Declaration/call evidence retains source
hashes. Multiple calls/framework observations may share a caller/kind/null-target
edge but remain distinct occurrences.

Mediator dispatch uses runtime type, so exact type evidence requires new Request()
or a sealed/value static type. Interface/object, open type parameters, and unsealed
variables leave runtime type unresolved; arbitrary data flow is not evaluated.
MassTransit generic T is the static publication contract, including Publish<IEvent>
interface initializers. Object + typeof(Event) supplies an explicit contract;
computed Type remains unresolved. No unlimited variance/hierarchy/topology expansion.

Stream APIs, custom extensions/wrappers, concrete dispatcher overloads, request
clients/sagas/filters, and runtime-generated messages are outside messaging
extraction; ordinary CALLS evidence can still exist.

## Query and fixture verification

Dependencies/path/impact can follow these kinds; inferred targets require
`--include-inferred`. Callers/callees default to CALLS. Context/export preserve all
confidence classes and evidence. An inferred path is not runtime delivery proof.

The real UO fixture exposes this six-edge static/candidate flow:

```text
GET /phase8/start
  --HANDLED_BY exact--> FlowController.Start
  --CALLS exact-------> Service.Run
  --DISPATCHES exact--> RequestA message
  --POSSIBLE_TARGET inferred--> HandlerA.HandleAsync
  --DISPATCHES exact--> RequestB message
  --POSSIBLE_TARGET inferred--> HandlerB.HandleAsync
```

```sh
dotnet run --project src/UO.Graphs.Cli --no-build --no-restore -- index samples/Phase8/Uo/Uo.csproj --full --trust-project --db .uographs/phase8-uo.db
dotnet run --project src/UO.Graphs.Cli --no-build --no-restore -- query path 'GET /phase8/start' 'UoFlows.HandlerB.HandleAsync(UoFlows.RequestB)' --depth 10 --edge-types HANDLED_BY,CALLS,DISPATCHES,POSSIBLE_TARGET --include-inferred --db .uographs/phase8-uo.db
dotnet run --project src/UO.Graphs.Cli --no-build --no-restore -- context 'GET /phase8/start' --depth 8 --max-nodes 300 --limit 800 --max-edges 10000 --max-chars 900000 --db .uographs/phase8-uo.db
```

Real-package fixtures are samples/Phase8/Uo and PackageCompatibility.
[ContractFixtures](../samples/Phase8/ContractFixtures/README.md) are separate
source-based test inputs; their success alone is not package compatibility.
Unit tests compile against actual DLLs, and integration tests exercise real package
projects through CLI/SQLite/query/context/export. No host or broker is started.
Actual counts and dated results are in [Phase 8](history/PHASE8.md) and
[STATUS](STATUS.md).
