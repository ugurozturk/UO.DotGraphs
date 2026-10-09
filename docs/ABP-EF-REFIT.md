# Faz 9 — ABP, EF Core ve Refit

Bu üç modül bağımsız statik analiz kapsamıdır. Bir modülün olumlu testi diğerinin
kabulü değildir. Gerçek komut/sonuçlar [STATUS](STATUS.md)'tadır. Kullanıcıya ait,
erişilmeyen bir uygulama analiz edilmedi; `samples/Phase9` sentetik ama gerçek NuGet
paketleriyle derlenen üç ayrı projedir. Host, DI container, DbContext, migration,
veritabanı veya HTTP istemcisi çalıştırılmaz.

## Sabit sürümler ve API kanıtı

| Modül | Derlenen paket/asset | Doğrulanan API | Lisans/kapsam |
| --- | --- | --- | --- |
| ABP | Volo.Abp.AspNetCore.Mvc ve ABP closure **10.1.1**, net10.0 | ApplicationService, IApplicationService, IRemoteService, AbpModule/DependsOn, Dependency/ExposeServices, IRepository ve temel interface'leri, AbpConventionalControllerOptions.Create | **LGPL-3.0-only**, yalnız paketlenmeyen fixture; ürün/test/runner bağımlılığı değil |
| EF Core | Microsoft.EntityFrameworkCore.Relational/Core **10.0.5**, net10.0 | DbContext, DbSet, ModelBuilder.Entity, IEntityTypeConfiguration, ToTable/ToView, Property/HasColumnName | MIT, fixture |
| Refit | **8.0.0**, net8.0 asset / net10.0 fixture | HttpMethodAttribute ve Get/Post/Put/Delete/Patch/Head/Options; RestService.For | MIT, fixture |
| Platform | SDK **10.0.201**, runtime/ref pack **10.0.5**, Roslyn **5.0.0.0** | SemanticModel, IOperation, gerçek assembly/symbol binding | macOS ARM64 kabul ortamı |

Bu sürümler test edilen destek tabanıdır; başka sürümlerde aynı isimli API bulmak
uyumluluk kabulü değildir. Her tanıma namespace + metadata name + metadata assembly
kontrolü uygulanır; aynı isimli kaynak taklitleri framework sayılmaz. Snapshot proje
bağlamını, Framework/HttpContract metadata'sı ilgili assembly identity'sini taşır;
lock dosyaları tam dependency/content hash envanteridir.

Birincil kaynaklar:

- [ABP 10.1.1 NuGet](https://www.nuget.org/packages/Volo.Abp.AspNetCore.Mvc/10.1.1),
  nuspec kaynak commit'i `1ecba3756736acb90fc9c52340716530e001c7cd`.
  [Controller seçimi](https://github.com/abpframework/abp/blob/1ecba3756736acb90fc9c52340716530e001c7cd/framework/src/Volo.Abp.AspNetCore.Mvc/Volo/Abp/AspNetCore/Mvc/Conventions/ConventionalControllerSetting.cs),
  [selector convention](https://github.com/abpframework/abp/blob/1ecba3756736acb90fc9c52340716530e001c7cd/framework/src/Volo.Abp.AspNetCore.Mvc/Volo/Abp/AspNetCore/Mvc/Conventions/AbpServiceConvention.cs),
  [DI registrar](https://github.com/abpframework/abp/blob/1ecba3756736acb90fc9c52340716530e001c7cd/framework/src/Volo.Abp.Core/Volo/Abp/DependencyInjection/ConventionalRegistrarBase.cs),
  [exposure](https://github.com/abpframework/abp/blob/1ecba3756736acb90fc9c52340716530e001c7cd/framework/src/Volo.Abp.Core/Volo/Abp/DependencyInjection/ExposedServiceExplorer.cs).
- [EF Core ModelBuilder 10.0.5](https://github.com/dotnet/efcore/blob/v10.0.5/src/EFCore/ModelBuilder.cs),
  [relational mapping API](https://github.com/dotnet/efcore/blob/v10.0.5/src/EFCore.Relational/Extensions/RelationalEntityTypeBuilderExtensions.ToTable.cs),
  [table annotation convention](https://github.com/dotnet/efcore/blob/v10.0.5/src/EFCore.Relational/Metadata/Conventions/RelationalTableAttributeConvention.cs).
- [Refit 8 HTTP attributes](https://github.com/reactiveui/refit/blob/8.0.0/Refit/Attributes.cs),
  [abstract interface method seçimi](https://github.com/reactiveui/refit/blob/8.0.0/Refit/RequestBuilderImplementation.cs),
  [HTTP attribute seçimi](https://github.com/reactiveui/refit/blob/8.0.0/Refit/RestMethodInfo.cs).

ABP LGPL paketleri ürünün MIT bağımlılık kümesine eklenmedi. Audit yalnız
`samples/Phase9/Abp/packages.lock.json` içinde ABP 10.1.1 istisnasına izin verir;
başka closure'a sızması hatadır. NuGet binary'leri repository'ye kopyalanmaz.
Fixture yeniden dağıtımı ayrıca LGPL koşullarına tabidir. NUglify BSD-2-Clause /
Apache-2.0 ve MyCSharp bundled MIT ayrı doğrulandı; [envanter](DEPENDENCIES.md).

## ABP destek matrisi

| Özellik | Durum | Çıktı ve sınır |
| --- | --- | --- |
| ApplicationService / IApplicationService-derived sözleşme | supported | Type üzerinde `Framework` rolü; `APP_SERVICE_CONTRACT` exact semantik interface bağı. Kalıtımdan endpoint çıkarılmaz. |
| AbpModule / doğrudan DependsOn(typeof(...)) | supported | `MODULE_DEPENDS_ON` kaynak attribute kanıtıyla; module çalıştı demek değildir. |
| IRepository<TEntity,TKey>, IRepository<TEntity>, inherited IBasic/IReadOnly repository | supported | Parameter/field/property/local/interface/invocation üzerinden `REPOSITORY_ENTITY`. Yalnız entity bağı; tablo/schema/context tahmini yok. |
| Açık generic repository | partial | Hedef null, unresolved; generic substitution yapılmaz. Closed generic entity ayrıdır. |
| DI marker, Dependency lifetime ctor, DisableConventionalRegistration, ExposeServices | partial | `registration` + inferred REGISTERS/IMPLEMENTED_BY. Default interface adı convention'ı uygulanır; her interface expose edilmez. |
| ConventionalControllers.Create(typeof(T).Assembly) | partial | Aynı assembly'deki public, nongeneric, nonabstract IRemoteService sınıflarının doğrudan public instance metotları `endpointCandidate`; exposure ve HANDLED_BY inferred. Config ve method/type ayrı kaynak kanıtları saklanır. |
| RemoteService false, inherited false, method false, NonAction/NonController | supported filtre | Type false'u method true açmaz. IsMetadataEnabled=false disable değildir. Interface attribute'u class'a kopyalanmaz. |
| Static assembly config bulunmaması / runtime assembly değeri | unresolved | Conventional endpoint adayı üretilmez; dış konfigürasyon olabileceği tanıda açık kalır. Yokluk garantisi yok. |
| URL conventions, RootPath, HTTP attribute/custom route override, TypePredicate | unresolved | Bu sürüm route/verb hesaplamaz; candidate RoutePattern/HttpMethod null. HTTP attribute varlığı tek başına kesin exposure sayılmaz. |
| Custom/keyed exposure, scanning/callback/ReplaceServices, inherited action enumeration, external assembly config | unsupported | DI/runtime seçim veya route sonucu üretilmez; limitations ve partial tanıları korunur. |

**Bilinen yanlış-pozitif riski:** TypePredicate, controller removal veya çalışmayan
module declaration'ı candidate/DI adayını gerçekte devre dışı bırakabilir. Bu yüzden
adaylar inferred ve `Activation=candidateOnly`, kesin endpoint değildir. Bağımsız
explicit ASP.NET MVC selector'ları Faz 7 kurallarıyla ayrıca analiz edilir; ABP
RemoteService convention filtresi bütün MVC route'larını silmez.

ABP kabulü: `AbpContractsModulesAndGenericRepositoryUseRealPackageSymbols`,
`AbpExposureOverridesMetadataAndConfigurationStaySeparate`,
`AbpWithoutStaticAssemblyRegistrationNeverInventsEndpoint`,
`AbpDiConventionsRespectLifetimeExposureAndDisableOverrides`, ABP compilation-error
vakası ve `Phase9AbpRealPackagePersistsContractsRepositoriesAndInferredCandidates`.

## EF Core destek matrisi

| Özellik | Durum | Çıktı ve sınır |
| --- | --- | --- |
| DbContext kalıtımı, doğrudan DbSet<TEntity> property | supported | Context rolü; context/member → entity `CONTEXT_ENTITY`. Set adı tablo adı olmaz. |
| ModelBuilder.Entity<T>() / Entity(typeof(T)) | supported | Bound entity declaration; runtime model üyeliği değildir. |
| IEntityTypeConfiguration<TEntity> | supported | `CONFIGURES_ENTITY`; kullanılmayan configuration bir context'e bağlanmaz. |
| [Table]/[Column], generic EntityTypeBuilder<T>.ToTable(name,schema)/ToView | supported bildirim | Ayrı `modelMapping` düğümleri; `MAPS_ENTITY` gerçek entity'ye bağlanır. Table/View ayrı alanlardır. Annotation/fluent override bildirimleri silinmez veya tek efektif modele birleştirilmez. |
| Property(b => b.Property).HasColumnName(literal) | supported | Yalnız lambda parameter'ına doğrudan bağlı tek property. Captured/hesaplanan expression association unresolved. |
| Runtime table/schema/column, koşullu mapping | partial | Literal bilinen alan tutulabilir; dinamik alan null, mapping confidence unresolved, koşul/eksik alan açıklanır. Entity bağı biliniyorsa exact kalabilir. |
| ApplyConfiguration/ApplyConfigurationsFromAssembly, Ignore, owned/split/non-generic/string builders, custom model extensions | unsupported | Gözlenen gerçek model-builder API'leri ve model-builder parametreli custom wrapper çağrıları unresolved mapping kaydı/tanı üretir. Çağrı sırası/scanning/custom kod yürütülmez. ModelBuilder içermeyen opaque wrapper içi görülmüyorsa destek kapsamı dışında kalır. |
| DbContext oluşturma, migration, provider conventions, efektif model, SQL/query plan, fiziksel tablo erişimi | unsupported | Hiçbiri çalıştırılmaz veya türetilmez. LINQ yalnız mevcut semantik CALLS/USES_TYPE kapsamında. |

**Bilinen yanlış-pozitif riski:** source mapping daha sonra override/Ignore edilebilir,
çalışmayan configuration içinde bulunabilir. `Exact` yalnız o bildirimdeki sabit
metadata ve entity association anlamındadır; efektif DB modeli değildir. Schema
belirtilmediyse `dbo`/`public` varsayılmaz. `[Table]` görülmesi entity'nin context'e
eklendiğini kanıtlamaz. EF repository bağı ABP modülünün gerçek generic argument
kanıtından gelir; repository → belirli DbContext bağı uydurulmaz.

EF kabulü: `EfContextDbSetsAndConfigurationContractsAreNotEffectiveModelClaims`,
`EfExplicitMappingsPreserveAnnotationAndFluentOverrideDeclarations`,
`EfRuntimeConfigurationScanningAndUnsupportedBuilderCannotBecomeExactMapping`,
`EfComputedAndCapturedPropertyExpressionsAndRuntimeColumnAreUnresolved`, EF
compilation-error vakası ve `Phase9EfRealPackagePersistsMappingsAndUnresolvedRuntimeConfiguration`.

## Refit destek matrisi

| Özellik | Durum | Çıktı ve sınır |
| --- | --- | --- |
| Gerçek built-in Get/Post/Put/Delete/Patch/Head/Options | supported | Abstract instance interface method → `HTTP_CONTRACT`; method/route template metadata exact. Bu düğüm server endpoint değildir. |
| Call site → attributed interface method | supported | `INVOKES_HTTP` contract düğümüne; CALLS compile-time metoda bağlanmaya devam eder. |
| Interface inheritance / new shadowing | supported | Bağlanan method symbol kullanılır; parent route child shadowing method'a kopyalanmaz. |
| Aynı route, farklı contract/servis | supported ayırım | Kimlik project context + gerçek method identity + attribute içerir; aynı path tek servise indirgenmez. |
| Custom HttpMethodAttribute subclass, birden fazla HTTP attribute | unresolved | Custom Path/Method kodu veya runtime first-attribute seçimi çalıştırılmaz; method/route null. |
| Default interface body / static interface method | unsupported HTTP | Refit runtime abstract instance seçimine göre contract çıkarılmaz. Static HTTP attribute negatif testi gerçek DLL semantik compilation'ında, generator kapalıdır; Refit 8 generator bu şekle CS0539 üretir. Normal fixture generator açık derlenir. |
| Base address, HttpClient mutation/settings, final URI substitution | unresolved | BaseAddress her zaman null; RestService.For gözlemi config tanısı üretir. Constant URL data flow da bu sürümün kapsamı dışındadır. |
| Remote/local endpoint matching | unresolved | Her contract'tan null-target POSSIBLE_ENDPOINT. Salt route benzerliğiyle exact veya inferred local servis bağlantısı kurulmaz. |
| Concrete generated client dispatch, generic closed-client substitution, custom serializers/auth handlers | unsupported | Interface sözleşmesi dışındaki runtime davranış genişletilmez. |

**Bilinen yanlış-pozitif riski:** interface referansı farklı implementation alabilir,
request hiç yürümeyebilir; route template gerçek request adresi değildir. Eşleşen
local MVC route ve iki farklı Refit interface fixture'da birlikte bulunur; bağlantı
hala unresolved kalır. Source generator varsayılan analizde çalıştırılmaz ve
snapshot eksikliği açıkça raporlar.

Refit kabulü: `RefitSamePathDifferentServicesStayDistinctAndCallsBindToContract`,
`RefitRuntimeBaseAddressCustomAttributeAndNonAbstractMethodsRemainUnknown`,
`RefitMultipleAndStaticHttpAttributesAreNotAssertedAsExecutableContracts`, Refit
compilation-error vakası ve `Phase9RefitRealPackageKeepsSamePathServicesSeparateInContextAndExport`.

## Saklama ve sorgu sözleşmesi

Ürün 0.9.0; JSON v4, SQLite schema 3, delivery v1 korunur. Optional `Framework`,
`ModelMapping`, `HttpContract` metadata'sı mevcut JSON payload içinde atomik
saklanır. Yeni ilişki yönleri Core `RelationshipContract` ve merkezi testte
sabitlenmiştir. Dependencies/impact/path/context/export mevcut bütçe/güven
kurallarını kullanır. Inferred traversal opt-in gerekir; unresolved null hedef
üzerinden sahte yol kurulmaz. Her modülün framework tanıları snapshot'ı partial
yapar; modüller birbirinin eksikliğini gizlemez.
