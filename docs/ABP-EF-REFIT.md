# ABP, EF Core, and Refit analysis

These are three independent static-analysis modules. Passing one does not validate
another. Fixtures under samples/Phase9 are synthetic projects compiled against real
packages, not evidence from an inaccessible user application. Analysis starts no
host, DI container, DbContext, migration, database, or HTTP client.
[STATUS](STATUS.md) contains actual execution results.

## Pinned versions and API evidence

| Module | Package/asset | Verified API | License/scope |
| --- | --- | --- | --- |
| ABP | Volo.Abp.AspNetCore.Mvc and closure **10.1.1**, net10.0 | ApplicationService/IApplicationService/IRemoteService, AbpModule/DependsOn, Dependency/ExposeServices, repository interfaces, AbpConventionalControllerOptions.Create | LGPL-3.0-only; isolated non-packable fixture, not product/test/runner dependency |
| EF Core | Relational/Core **10.0.5**, net10.0 | DbContext/DbSet, ModelBuilder.Entity, IEntityTypeConfiguration, ToTable/ToView, Property/HasColumnName | MIT fixture |
| Refit | **8.0.0**, net8.0 asset in net10.0 fixture | HttpMethodAttribute and Get/Post/Put/Delete/Patch/Head/Options; RestService.For | MIT fixture |
| Recorded local platform | SDK **10.0.201**, runtime/reference pack **10.0.5**, Roslyn **5.0.0.0** | SemanticModel, IOperation, real assembly/symbol binding | macOS ARM64; later platform results in STATUS |

These exact versions are the tested baseline, not a promise about same-named APIs
in other versions. Recognition checks namespace, metadata name, and metadata
assembly; source lookalikes are not frameworks. Snapshot project context and
Framework/HttpContract metadata retain assembly identity; locks retain complete
dependency/content hashes.

Primary references:

- [ABP 10.1.1](https://www.nuget.org/packages/Volo.Abp.AspNetCore.Mvc/10.1.1), repository
  commit `1ecba3756736acb90fc9c52340716530e001c7cd`:
  [controller selection](https://github.com/abpframework/abp/blob/1ecba3756736acb90fc9c52340716530e001c7cd/framework/src/Volo.Abp.AspNetCore.Mvc/Volo/Abp/AspNetCore/Mvc/Conventions/ConventionalControllerSetting.cs),
  [selector conventions](https://github.com/abpframework/abp/blob/1ecba3756736acb90fc9c52340716530e001c7cd/framework/src/Volo.Abp.AspNetCore.Mvc/Volo/Abp/AspNetCore/Mvc/Conventions/AbpServiceConvention.cs),
  [DI registrar](https://github.com/abpframework/abp/blob/1ecba3756736acb90fc9c52340716530e001c7cd/framework/src/Volo.Abp.Core/Volo/Abp/DependencyInjection/ConventionalRegistrarBase.cs),
  [exposure](https://github.com/abpframework/abp/blob/1ecba3756736acb90fc9c52340716530e001c7cd/framework/src/Volo.Abp.Core/Volo/Abp/DependencyInjection/ExposedServiceExplorer.cs).
- EF Core 10.0.5 [ModelBuilder](https://github.com/dotnet/efcore/blob/v10.0.5/src/EFCore/ModelBuilder.cs),
  [relational mapping](https://github.com/dotnet/efcore/blob/v10.0.5/src/EFCore.Relational/Extensions/RelationalEntityTypeBuilderExtensions.ToTable.cs),
  [table annotations](https://github.com/dotnet/efcore/blob/v10.0.5/src/EFCore.Relational/Metadata/Conventions/RelationalTableAttributeConvention.cs).
- Refit 8 [attributes](https://github.com/reactiveui/refit/blob/8.0.0/Refit/Attributes.cs),
  [abstract interface selection](https://github.com/reactiveui/refit/blob/8.0.0/Refit/RequestBuilderImplementation.cs),
  [HTTP attribute selection](https://github.com/reactiveui/refit/blob/8.0.0/Refit/RestMethodInfo.cs).

ABP's LGPL packages are not relicensed MIT. The audit permits ABP 10.1.1 only in
samples/Phase9/Abp/packages.lock.json and fails leakage into another closure.
NuGet binaries are not checked in; fixture redistribution has separate LGPL
obligations. NUglify BSD-2-Clause/Apache-2.0 and bundled MyCSharp MIT were separately
verified. See [DEPENDENCIES](DEPENDENCIES.md).

## ABP support matrix

| Feature | Status | Output and limit |
| --- | --- | --- |
| ApplicationService / IApplicationService-derived contract | Supported | Framework type role; exact APP_SERVICE_CONTRACT interface binding, no inherited endpoint claim |
| AbpModule / direct DependsOn(typeof(...)) | Supported | MODULE_DEPENDS_ON attribute evidence, not module execution |
| IRepository<TEntity,TKey>/IRepository<TEntity>, inherited IBasic/IReadOnly contracts | Supported | REPOSITORY_ENTITY from parameters/fields/properties/locals/interfaces/calls; no table/schema/context guess |
| Open generic repository | Partial | Null unresolved target; no substitution; closed entity contracts stay distinct |
| DI markers, Dependency lifetime constructor, DisableConventionalRegistration, ExposeServices | Partial | Registration and inferred REGISTERS/IMPLEMENTED_BY; default interface-name convention, not every interface |
| ConventionalControllers.Create(typeof(T).Assembly) | Partial | Direct public instance methods on eligible public nongeneric nonabstract IRemoteService classes in that assembly become endpointCandidate; inferred exposure/HANDLED_BY with separate config/type/method evidence |
| RemoteService false, inherited false, method false, NonAction/NonController | Supported filters | Method true cannot override disabled type; IsMetadataEnabled=false is not exposure disable; interface attributes are not copied to classes |
| No static assembly registration / runtime assembly value | Unresolved | No conventional endpoint candidate; external configuration remains possible |
| URL conventions, RootPath, HTTP/custom route overrides, TypePredicate | Unresolved | No route/verb computation; RoutePattern/HttpMethod null; HTTP attribute alone does not prove exposure |
| Custom/keyed exposure, scanning/callback/ReplaceServices, inherited actions, external assembly config | Unsupported | No runtime DI selection/route result; partial limitations retained |

False-positive boundary: TypePredicate, controller removal, or an unexecuted module
can disable a source candidate. It remains inferred with `Activation=candidateOnly`.
Independent explicit MVC selectors are analyzed by the ASP.NET extractor; ABP
RemoteService conventions do not erase all MVC routes.

Acceptance tests include AbpContractsModulesAndGenericRepositoryUseRealPackageSymbols,
AbpExposureOverridesMetadataAndConfigurationStaySeparate,
AbpWithoutStaticAssemblyRegistrationNeverInventsEndpoint,
AbpDiConventionsRespectLifetimeExposureAndDisableOverrides, compilation-error cases,
and Phase9AbpRealPackagePersistsContractsRepositoriesAndInferredCandidates.

## EF Core support matrix

| Feature | Status | Output and limit |
| --- | --- | --- |
| DbContext inheritance and direct DbSet<TEntity> property | Supported | Context role and CONTEXT_ENTITY; set name is not table name |
| ModelBuilder.Entity<T>() / Entity(typeof(T)) | Supported | Bound entity declaration, not effective model membership |
| IEntityTypeConfiguration<TEntity> | Supported | CONFIGURES_ENTITY; unused configuration is not attached to a context |
| Table/Column attributes, generic ToTable(name,schema)/ToView | Supported declarations | Separate modelMapping nodes and MAPS_ENTITY; table/view distinct; annotation/fluent declarations retained without resolving precedence |
| Property(b => b.Property).HasColumnName(literal) | Supported | Direct lambda-parameter property only; captured/computed association unresolved |
| Runtime names/schema/columns or conditional mapping | Partial | Known literals may survive; dynamic fields null and mapping unresolved; independently known entity link may stay exact |
| ApplyConfiguration/assembly scanning, Ignore, owned/split/non-generic/string builders, custom extensions | Unsupported | Observed real builder APIs and custom calls with builder parameters produce unresolved mapping evidence/diagnostics; no execution/order/scanning. Invisible wrappers outside this surface remain out of scope |
| Context creation, migrations, provider conventions, effective model, SQL/plans, physical tables | Unsupported | Never executed or derived; LINQ only has ordinary semantic evidence |

False-positive boundary: later overrides/Ignore or unused configuration can make a
source mapping ineffective. Exact means the declaration's constants/entity
association only. No default dbo/public schema is assumed. Table attributes do not
prove context membership; repository evidence does not invent a particular DbContext.

Acceptance includes EfContextDbSetsAndConfigurationContractsAreNotEffectiveModelClaims,
EfExplicitMappingsPreserveAnnotationAndFluentOverrideDeclarations,
EfRuntimeConfigurationScanningAndUnsupportedBuilderCannotBecomeExactMapping,
EfComputedAndCapturedPropertyExpressionsAndRuntimeColumnAreUnresolved,
compilation-error cases, and
Phase9EfRealPackagePersistsMappingsAndUnresolvedRuntimeConfiguration.

## Refit support matrix

| Feature | Status | Output and limit |
| --- | --- | --- |
| Built-in Get/Post/Put/Delete/Patch/Head/Options | Supported | Abstract instance interface method → HTTP_CONTRACT with static method/template; not server endpoint |
| Call to attributed interface method | Supported | INVOKES_HTTP to client contract; CALLS remains on interface method |
| Interface inheritance/new shadowing | Supported | Bound method identity; parent route not copied to child shadow |
| Same route in distinct services/contracts | Supported distinction | Context + actual method + attribute identity; path alone never merges services |
| Custom HTTP attribute / multiple HTTP attributes | Unresolved | No custom code or runtime first-attribute selection; method/route null |
| Default interface body/static interface method | Unsupported HTTP | No contract under abstract-instance selection; static negative compiled against actual DLL with generators off because Refit 8 generator emits CS0539; normal fixture builds with generator |
| Base address, HttpClient mutation/settings, URI substitution | Unresolved | BaseAddress null; RestService.For produces configuration diagnostic; even constant URL data flow is outside scope |
| Remote/local endpoint matching | Unresolved | Null-target POSSIBLE_ENDPOINT per contract; matching route text never creates a service link |
| Generated concrete client dispatch, closed generic-client substitution, serializers/auth handlers | Unsupported | No expansion beyond interface contract |

False-positive boundary: an interface can receive another implementation, and a
request may never execute. Templates are not final request addresses. Fixtures
include matching MVC routes and two Refit interfaces while remote targets remain
unresolved. Default generator exclusion is visible in snapshot coverage.

Acceptance includes RefitSamePathDifferentServicesStayDistinctAndCallsBindToContract,
RefitRuntimeBaseAddressCustomAttributeAndNonAbstractMethodsRemainUnknown,
RefitMultipleAndStaticHttpAttributesAreNotAssertedAsExecutableContracts,
compilation-error cases, and
Phase9RefitRealPackageKeepsSamePathServicesSeparateInContextAndExport.

## Persistence and traversal

Product **0.9.0** retains JSON **4**, SQLite **3**, delivery **1**. Optional Framework,
ModelMapping, and HttpContract metadata is atomically stored in existing JSON
payloads. Nine additional relationship directions are fixed in RelationshipContract
and its test. Dependencies/path/impact/context/export retain existing budgets and
confidence rules. Query traversal through inferred edges needs opt-in; unresolved
null targets never form invented paths. Framework uncertainty marks analysis partial.
The initial Phase 9 integration failures are historical; later standard test passes
are recorded in [STATUS](STATUS.md), not retroactively attributed to that phase run.
