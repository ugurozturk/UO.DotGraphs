# Faz 7 — Microsoft DI ve ASP.NET Core

UO.Graphs 0.7.0, JSON v4 / SQLite schema 3 / delivery v1. Bu faz **statik
bildirimleri** çıkarır. Kayıt metodunun çağrıldığını, endpoint'in host'ta aktive
edildiğini veya hangi implementation'ın çözüleceğini kanıtlamaz. İncelenen assembly
Reflection ile yüklenmez; uygulama, container, HTTP host veya database çalıştırılmaz.

## Extractor sözleşmesi

Roslyn katmanında iç `IFrameworkExtractor` yalnız `Id` ve
`Extract(FrameworkExtractionContext)` içerir. `microsoft-di-v1` ardından
`aspnet-core-v1` statik olarak kayıtlıdır. DI modülü aynı compilation'da görülen
servis tiplerini HTTP modülüne implicit parametre **adayı** için aktarır. Başka
projelerdeki kayıtlar bu aday kümesine sessizce taşınmaz.

Context, seçilen compilation/document'ları, mevcut symbol identity resolver'ını,
kanıt üretimini ve graph builder'ı sağlar. Depolama veya host erişimi yoktur.
Modüller gerçek framework metadata referanslarıyla bellekte test edilir; gerçek
MSBuild/CLI/SQLite akışı ayrıca integration testidir. Plugin discovery, assembly
loader, marketplace, DI container altyapısı veya yeni ürün projesi yoktur.

## İlişki anlamları

| İlişki | Yön ve güven anlamı |
| --- | --- |
| `REGISTERS` | Kayıt bildirimi → bildirilen service type. Exact, kayıt çağrısının statik argümanını belirtir; execution veya container üyeliği değildir. |
| `IMPLEMENTED_BY` | Kayıt bildirimi → bildirilen implementation type. Runtime seçimini belirtmez. Factory/hesaplanan tipte hedef null ve unresolved. |
| `USES_FACTORY` | Kayıt bildirimi → bağlı factory lambda/metodu. Delegate değişkeninin değeri çözülemiyorsa null. |
| `INJECTS` | Public constructor/handler → parametre service type. Constructor ve `[FromServices]` tipi exact olabilir. Implicit HTTP adayı inferred; implementation seçimi yapılmaz. |
| `HANDLED_BY` | Endpoint bildirimi → action/handler. Statik ilişki exact olabilir; endpoint activation her zaman `notProven`. |
| `ACTIVATES` | Controller action → public constructor adayı. Her zaman inferred; custom activator veya constructor seçimi kanıtlanmaz. |

Mevcut `CALLS` compile-time hedefte kalır. Bir endpoint'in handler'ı içinden
`IMessage.Read()` çağrısı interface slot'una gider; kayıtlı implementasyonların
hepsine exact CALLS eklenmez. `REGISTERS` ve `IMPLEMENTED_BY` aynı kayıt düğümünde
birleşir; service → tek seçilmiş implementation kenarı üretilmez.

Bütün ilişkiler kaynak aralığı, decoded kaynak SHA-256, analysis/context ID,
gerekçe ve constructed `boundTarget` taşır. Compilation hatalı declaration'larda
hedef bağlanmaz; aday yalnız related symbol olarak korunur. Tür düğümleri önceki
identity sözleşmesi gereği generic definition'a normalize edilir; örneğin
`IRepository<string>` occurrence'ın boundTarget alanında kalır.

## Microsoft DI destek matrisi

- Gerçek `ServiceCollectionServiceExtensions` sembolü ve assembly aidiyeti ile
  `AddSingleton`, `AddScoped`, `AddTransient`; extension ve statik çağrı biçimleri.
- Generic self/interface→implementation, `typeof` overload'ları, open generic
  `typeof(IRepository<>) → typeof(Repository<>)` bildirimi.
- Factory method/lambda, instance registration (yalnız açık `new` ifadesinin
  concrete tipi bağlanır), multiple registration. Factory'nin generic dönüş
  constraint'i concrete runtime implementation diye sunulmaz.
- Normal/primary public constructor parametreleri DI eligibility olarak kaydedilir.
  Bütün public constructor adayları kalır; optional/default parametre, overload
  seçimi veya gerçekten DI tarafından instantiate edilme iddiası yoktur.
- Kayıt başına ayrı `registration` entity: lifetime, form, conditional,
  service/implementation type, openGeneric, limitations. Ayrı collection'lar veya
  aynı service için kayıtlar birleştirilmez; collection alias/order analizi yoktur.
- `if`, switch, döngü, conditional access ve kısa devre bağlamları conditional
  olarak işaretlenir; expression evaluation veya reachability çalıştırılmaz.
- Statik tipi `IServiceCollection` olan receiver/argümanlı destek dışı çağrılar
  (scanning/wrapper/descriptor/mutation dahil) opaque REGISTERS/null üretir.
  Bu bir **aday** bildirimi; her collection çağrısının kayıt yaptığı iddiası değildir.
- Dynamic invocation, framework davranışını gizleyebileceği için tanı üretir;
  bilinmeyen hedef/registration uydurulmaz. Factory return-flow, scanning expansion,
  keyed registration, conditional outcome ve gerçek resolution desteklenmez.

## ASP.NET Core destek matrisi

Minimal API: gerçek `EndpointRouteBuilderExtensions.MapGet/MapPost/MapMethods`.
`MapMethods` sabit inline array ve C# collection expression için method başına
ayrı endpoint üretir. Sabit olmayan method collection/null/empty collection
çözümlenmez. Lambda, method group, yerel/top-level handler desteklenir; delegate
variable data flow çözümlenmez.

`MapGroup` zinciri ve initializer'ı tek olan, yeniden atanmayan/ref kaçışı olmayan
local alias'lar izlenir (en fazla 16 adım). Prefix ve route Roslyn constant value
olmalıdır. WebApplication root'u tanınır. Keyfi IEndpointRouteBuilder parametresi,
mutasyona uğramış group, route-group field/property/helper dönüşü veya runtime-computed prefix
bilinmiyorsa route null olur. Prefix bildirimlerinin kaynakları endpoint Sources'a
eklenir. Application/group convention'ları ve endpoint filter davranışı uygulanmaz.

Controller: doğrudan bildirilen public, nonabstract, nongeneric controller class
ve public instance nongeneric action'lar; MVC built-in Route, HttpGet/Post/Put/
Delete/Patch/Head/Options, AcceptVerbs, ActionName ve Area. Controller/action
prefix kombinasyonları, absolute `/` ve `~/`, `[controller]`, `[action]`, `[area]`
token'ları; NonController/NonAction dışlaması. Action Async suffix'i varsayılan MVC
convention'ıyla çıkarılır; özel convention/transformer çalıştırılmaz. Token expansion
bildirim yorumudur, son host route'u değildir.

Conventional routing için route uydurulmaz. Özel source/metadata base controller inheritance
ve custom route attribute provider'ları açık tanı/unresolved route üretir;
inherited action keşfi, base metadata route genişlemesi, application parts,
custom convention ve tüm MVC selector combinasyonları tam destek iddiası yoktur.
Explicit FromServices exact tip bağlantısı kurar; aynı compilation'daki statik
kayıtla eşleşen implicit parametre inferred adaydır. Model binding precedence ve
cross-project runtime service availability kanıtlanmaz.

## Endpoint kimliği ve belirsizlik

`GraphIdentity.Canonical`/SHA-256 ile kimlik; project context (root-relative project,
TFM/configuration), HTTP method, birleşik pattern, handler ve kaynak bildirim ayracı
kullanılır. Bildirim ayracı relative file + syntax child ordinal yoludur; mutlak
checkout yoluna veya satır numarasına bağlı değildir. Düzenlenen syntax yapısı kimliği
değiştirebilir. Prefix bildirimi de ayraca dahildir.

`collisionKey` yalnız context + method + route'dur; endpoint ID değildir. Aynı GET
route'u iki yerde bildirilirse iki entity korunur ve duplicate limitation eklenir.
Aynı path'teki POST ayrı kalır. Method kısıtı olmayan controller route'u `*` taşır.
Route/method çözülemezse collisionKey null ve endpoint confidence unresolved olur;
handler bağımsız biçimde exact bağlanabilir. Route değerinin null olması literal
`<unknown-route>` isimli gerçek bir endpoint olduğu anlamına gelmez.

Bu lexical duplicate kontrolüdür; route constraint, case, precedence veya farklı
pattern'lerin runtime çakışma analizi değildir. Endpoint'in salt kaynak bildirimi,
host'un onu aktive ettiğinin kanıtı değildir (`activation: notProven`).

Framework belirsizlik tanıları `framework:*` ile görünür ve coverage partial olur.
Generator kapalı/eksik compilation gibi önceki coverage sebepleri de korunur.
`completeWithinDeclaredScope` bile yalnız bu destek matrisi içinde anlamlıdır.

## CLI örnekleri ve gerçek framework referansı

Geliştirme build/restore solution üzerinden yapılır. `samples/Phase7/Frameworks.csproj`
gerçek `Microsoft.AspNetCore.App` FrameworkReference ile ServiceCollection ve
controller/Minimal API negatif fixture'ını derler. `samples/Phase7/MinimalWeb/MinimalWeb.csproj`
gerçek Web SDK ile top-level minimal uygulamayı derler. Hiçbiri testlerde çalıştırılmaz.
SDK 10.0.201'in seçtiği targeting pack **10.0.5** ile CLI doğrulanmıştır.

```sh
uographs index samples/Phase7/Frameworks.csproj --trust-project --full --db .uographs/phase7-frameworks.db
uographs query find AddScoped --db .uographs/phase7-frameworks.db
uographs index samples/Phase7/MinimalWeb/MinimalWeb.csproj --trust-project --full --db .uographs/phase7-web.db
uographs query dependencies 'GET /api/time' --depth 3 --db .uographs/phase7-web.db
uographs export --format json --db .uographs/phase7-web.db
```

Repo içindeki gerçek `dotnet run --project ... --no-build --no-restore --` öneki ve
ölçülen sonuçlar [STATUS](STATUS.md)'tadır. Controller constructor adayından geçen
path için `--include-inferred` gerekir. Dependencies/path/impact varsayılan ilişki
kümesi yeni altı framework ilişkisini de içerir; callers/callees hâlâ yalnız CALLS.
SQLite mevcut entity JSON payload'ına optional `registration`/`httpEndpoint`
metadata'sı eklenir; eski entity'lerde null alanlar yazılmaz. Eski snapshot'ı yeniden
index etmek yeni ilişkileri üretir. Incremental replacement bütün framework entity,
edge ve occurrence'larını da atomik değiştirir; kısmi framework bilgisi runtime
container seçimi için cache değildir. Web SDK için Faz 6'nın fail-closed reuse
sınırı korunur: dependency modeli unproven ise no-op/fresh iddiası yoktur.

Resmî semantik kaynakları:
[.NET DI](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview),
[Minimal route handlers](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/route-handlers?view=aspnetcore-10.0),
[Controller routing](https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/routing?view=aspnetcore-10.0).
Ürün paket lisans seti [DEPENDENCIES](DEPENDENCIES.md)'tedir.

## Geliştirme framework lisans kanıtı

Yeni NuGet paketi yoktur; 50 kilitli paketin lisans envanteri değişmedi. İki yeni
fixture'ın lock dosyası boş net10.0 paket kümesini sabitler. ASP.NET Core
FrameworkReference yalnız fixture/unit test tarafındadır; ürün projelerine
ASP.NET runtime bağımlılığı eklenmez. SDK 10.0.201 ve kurulu
Microsoft.AspNetCore.App.Ref/Microsoft.AspNetCore.App **10.0.5** gerçek build ve
index bağlamında doğrulandı. Framework kurulum bağımlılığıdır; ürün onu indirmez
ve paketine kopyalamaz.

Sürümün resmî [ASP.NET Core MIT metni](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.5/LICENSE.txt)
ve [runtime MIT metni](https://raw.githubusercontent.com/dotnet/runtime/v10.0.5/LICENSE.TXT)
2026-10-09'da okundu. Dağıtılan kopyalarda copyright ve izin metni korunmalıdır;
framework'ün kendi third-party bildirimleri ürün MIT beyanıyla değiştirilmez.
Kurulu `/usr/local/share/dotnet/LICENSE.txt` SHA-256:
`cfc21f5e8bd655ae997eec916138b707b1d290b83272c02a95c9f821b8c87310`;
aynı kökte `ThirdPartyNotices.txt` SHA-256:
`2dc8f8c5a39401e928b5784ab564eb8b3ceb99ead3df8f260e0cab7e0bbecc7a`.

