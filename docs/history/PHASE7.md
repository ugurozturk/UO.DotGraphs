# Gerçekleşen durum

Son güncelleme: 2026-10-09.

**Faz 7, aşağıdaki macOS ARM64 statik DI/HTTP kabul kapsamında tamamlandı.** Faz 8–11 başlamadı.
Ürün **UO.Graphs 0.7.0**, CLI **uographs**. Önceki Faz 6 kanıtı
[PHASE6](PHASE6.md)'ya arşivlendi. Bu fazda uygulama, DI container,
HTTP host veya database başlatılmadı; Reflection ile incelenen assembly yüklenmedi.

## Uygulananlar

- Roslyn katmanında küçük iç `IFrameworkExtractor` + kanıt context'i; statik
  Microsoft DI / ASP.NET Core modülleri. Plugin loader/marketplace veya yeni katman yok.
- AddSingleton/AddScoped/AddTransient; generic/self/interface→implementation,
  typeof, instance, factory, multiple registration, open generics. Her kayıt ayrı
  entity; lifetime/form/conditional/type/limitation alanları kalıcıdır.
- REGISTERS / IMPLEMENTED_BY / USES_FACTORY / INJECTS yönleri merkezi sözleşmede.
  Constructor tipi exact olabilir; constructor seçimi ve runtime implementation
  bilinmez. Factory'nin metodu/lambda'sı bağlanır; döndürdüğü implementation unresolved.
  Conditional kayıt, opaque scanning/wrapper, hesaplanan tip ve dynamic davranış
  açık tanı/metadata ile gösterilir. Container çalıştırılmaz.
- Controller/action attribute ve Minimal API MapGet/MapPost/MapMethods;
  route group/nested prefix, immutable local alias, method/lambda/top-level handler,
  sabit olmayan route/method ve handler delegate belirsizliği.
- Endpoint kimliği context + HTTP method + pattern + bildirim ayracı içerir.
  Aynı GET path'inin iki bildirimi korunur; POST ayrı kalır. Collision key kimlik
  yerine geçmez. Route çözülemezse null; handler bağımsız exact olabilir.
- HANDLED_BY, action→constructor ACTIVATES (inferred), constructor/handler→service
  INJECTS ve önceki compile-time CALLS ile kanıtlı yollar. Endpoint metadata'sında
  `activation: notProven`; statik bildirim host aktivasyonu sayılmaz.
- Endpoint/group/attribute kaynakları ve ilişki occurrence'ları SQLite/query/
  context/export'ta korunur. Dependencies/path/impact yeni ilişki türlerini içerir;
  inferred traversal opt-in, callers/callees yalnız CALLS. JSON v4 / SQLite 3 /
  delivery 1 korunur; eski entity'lerde optional yeni alanlar yazılmaz.
- Gerçek ASP.NET Core reference pack kullanan library fixture ve ayrı Web SDK
  top-level MinimalWeb fixture'ı solution build'ine alındı; hiçbiri çalıştırılmaz.
  Yeni NuGet paketi yok; 50 paketlik lisans seti korunur.

Tam anlamlar, destek matrisi ve sınırlar: [FRAMEWORKS](../FRAMEWORKS.md).

## Gerçek doğrulama

Ortam: macOS 27.0 ARM64 / osx-arm64, SDK **10.0.201**, MSBuild **18.3.0**,
.NET / ASP.NET Core runtime ve reference pack **10.0.5**, Roslyn **5.0.0.0**.
`dotnet --info` exit 0. Windows/Linux bu fazda **NOT RUN**.

| Gerçek komut/kontrol | Sonuç |
| --- | --- |
| `dotnet restore UO.Graphs.slnx --locked-mode --ignore-failed-sources -m:1 -nr:false -p:NuGetAudit=false` | PASS exit 0; mevcut kilitli paketler. Açık geliştirme restore'u; ürün runtime restore yapmaz. |
| `dotnet restore samples/Phase7/Frameworks.csproj --ignore-failed-sources -m:1 -nr:false -p:NuGetAudit=false` | PASS exit 0; yeni boş paket lock dosyası. |
| `dotnet restore samples/Phase7/MinimalWeb/MinimalWeb.csproj --ignore-failed-sources -m:1 -nr:false -p:NuGetAudit=false` | PASS exit 0; yeni boş paket lock dosyası. macOS CSSM_ModuleLoad mesajı geldi; restore tamamlandı. |
| `dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS exit 0, **0 warning / 0 error**, 10.65 saniye. İki framework fixture dahil. |
| Son unit koşusu (TRX komutu aşağıda) | PASS exit 0, **25/25**, 5.33 saniye. |
| `dotnet test UO.Graphs.slnx --no-build --no-restore -m:1 -nr:false --logger 'console;verbosity=normal'` | Son terminal oturumu `Unknown process id` verdi; toplam sonuç doğrulanamadı. Görülen iki yeni integration vaka geçti. Kesin toplam aşağıdaki TRX gruplarıyla doğrulandı. |
| Integration TRX grupları: uzun incremental vaka ve kalan tüm vakalar | PASS, iki proses de exit 0: **1/1** (5 dk 37 sn) + **81/81** (5 dk 47 sn) = **82/82**. |
| Son MVC sınır düzeltmesi sonrası Faz 7 integration tekrar koşusu | PASS exit 0, **2/2**, 17.62 saniye. |
| `python3 scripts/audit_licenses.py --check` | PASS exit 0, **50** kilitli paket. Yeni fixture FrameworkReference lisans kanıtı FRAMEWORKS'te. |
| Whitespace, XML/JSON parse ve Markdown yerel dosya bağlantıları | PASS exit 0; **112** kaynak/config/belge dosyası. |

TRX raporları `.uographs/phase7-tests/` altında; XML sayaçları ayrıca okundu,
failed/error/skipped yok. 82 integration regresyonu son küçük MVC inheritance
sınır düzeltmesinden önce geçti; düzeltmeden sonra tüm **25 unit + 2 framework
integration**, build ve CLI smoke yeniden çalıştırıldı. Böylece 107 ayrı vaka
kapsanır; tek bir son-binary 107-vaka koşusu iddia edilmez.

Gerçek TRX komutları:

```sh
dotnet test tests/UO.Graphs.IntegrationTests --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName~IncrementalMatchesCleanFullForEveryMutationAndNoOpSkipsSemanticAnalysis' --logger 'trx;LogFileName=incremental.trx' --results-directory .uographs/phase7-tests --logger 'console;verbosity=normal'
dotnet test tests/UO.Graphs.IntegrationTests --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName!~IncrementalMatchesCleanFullForEveryMutationAndNoOpSkipsSemanticAnalysis' --logger 'trx;LogFileName=integration.trx' --results-directory .uographs/phase7-tests --logger 'console;verbosity=normal'
dotnet test tests/UO.Graphs.UnitTests --no-build --no-restore -m:1 -nr:false --logger 'trx;LogFileName=unit-final.trx' --results-directory .uographs/phase7-tests --logger 'console;verbosity=normal'
dotnet test tests/UO.Graphs.IntegrationTests --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName~FrameworkDeclarations|FullyQualifiedName~MinimalWebSdk' --logger 'trx;LogFileName=framework-final.trx' --results-directory .uographs/phase7-tests --logger 'console;verbosity=normal'
```

## Gerçek CLI smoke

Her satır ayrı gerçek CLI prosesidir. Aşağıdaki `uographs` kısaltması için kullanılan
**gerçek önek**: `dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore --`.

| Önek sonrası gerçek argümanlar | Sonuç |
| --- | --- |
| `index samples/Phase7/Frameworks.csproj --full --trust-project --db .uographs/phase7-frameworks.db` | exit 0; 69 entity / 243 relationship / 301 occurrence. |
| `index samples/Phase7/MinimalWeb/MinimalWeb.csproj --full --trust-project --db .uographs/phase7-web.db` | exit 0; 12 entity / 47 relationship / 50 occurrence. |
| `query find AddScoped --db .uographs/phase7-frameworks.db` | exit 0; üç ayrı scoped bildirim, açık generic ve unresolved implementation korunur. |
| `query dependencies 'GET /api/time' --depth 3 --db .uographs/phase7-web.db` | exit 0; HANDLED_BY → INJECTS/IClock ve CALLS/IClock.Now yolu. |
| `query dependencies 'GET /api/Messages/{id}' --depth 3 --include-inferred --db .uographs/phase7-frameworks.db` | exit 0; HANDLED_BY exact → ACTIVATES inferred → INJECTS exact/IMessage. |
| `query symbol 'GET /duplicate' --db .uographs/phase7-frameworks.db` | **beklenen exit 7**, ambiguousSymbol ve iki aday; birleştirme yok. |
| `context 'GET /api/time' --max-chars 30000 --db .uographs/phase7-web.db` | exit 0; kanıtlı context JSON. |
| `export --format json --db .uographs/phase7-frameworks.db` | exit 0; 20 endpoint, 10 kayıt; tüm metadata korunur. |
| `export --format json --db .uographs/phase7-web.db` | exit 0; 2 endpoint, 1 kayıt; activation notProven. |

Snapshot'lar: framework `2d069f6bd5f0498cbfa88a6ea2d142b7`,
web `b6b0d688b0f248ef840144df6f67a26e`. Çıktılar `.uographs/phase7-evidence/` altında.
Her iki snapshot **partial**: generator kapalı politikası ve library negatiflerinin
framework belirsizlikleri görünür. Freshness index sırasında **unknown**: Faz 6'nın
Compile Remove/custom item ve Web SDK dependency model sınırı reuse'u kapatıyor.
Bu faz onu gevşetmez; no-op/fresh iddiası yapılmaz.

Gerçek export'tan örnekler (namespace kısaltıldı):

| Bildirim | Statik sonuç |
| --- | --- |
| `AddSingleton<IMessage, Message>()` | REGISTERS→IMessage exact; IMPLEMENTED_BY→Message exact; runtime selection bilinmiyor. |
| `AddScoped<IMessage, OtherMessage>()` | Ayrı kayıt; önceki singleton kaybolmuyor. |
| `AddScoped(typeof(IRepository<>), typeof(Repository<>))` | OpenGeneric=true; iki tip bağı exact; closed runtime instantiation yok. |
| `AddSingleton<IMessage>(sp => new Message())` | USES_FACTORY→lambda exact; IMPLEMENTED_BY target=null/unresolved. |
| `if (enabled) AddTransient<IMessage, OtherMessage>()` | Conditional=true; statik tipler exact, branch sonucu bilinmiyor. |
| `AddScoped(typeof(IMessage), dynamicType)` | REGISTERS exact; IMPLEMENTED_BY unresolved. |
| `services.Scan()` | Opaque aday; REGISTERS target=null/unresolved. |
| MinimalWeb `GET /api/time` ve `POST /api/time` | İki ayrı endpoint → lambda/local handler → IClock; activation=notProven. |

## Test kapsamı ve ara sonuçlar

**14 yeni unit vaka + 2 yeni integration vaka**. Önceki testler korunur.

- Gerçek framework symbol binding; benzer adlı kullanıcı metotları çıkarıma girmez.
- Lifetimes, bir servisin yedi farklı bildirimi, açık generic, normal/primary
  constructor tipi ve constructed signature; implementation seçilmemesi.
- Lambda/method/generic factory dönüşünün unresolved olması; conditional/opaque
  scanning/hesaplanan service ve implementation tipleri.
- Nested route group, GET/POST ve MapMethods array/collection; duplicate GET'in
  ayrı ID, aynı collision key, farklı POST'un ayrı key olması.
- Runtime-computed route, değişen group, dinamik prefix ve HTTP method collection;
  null key, açık unresolved. Delegate variable handler için hedef uydurulmaması.
- Controller prefix/token/absolute route, NonAction, aliases; endpoint→handler→
  constructor→service kanıtları; conventional ve inherited route için yanlış `/`
  üretilmemesi, yinelenen controller prefix bildiriminin korunması; metadata base controller
  belirsizliği ve miras alınan NonController/NonAction dışlamaları.
- Compilation hatasında target/key exact yapılmaması; relocation kimlik eşitliği
  ve farklı context ayrımı. CLI/SQLite/query/export kalıcılığı; gerçek Web SDK
  top-level lambda ve local-function handler'ı. Host/container çalıştırılmaması.

Ara build/test hataları saklanmıyor: nullable array hatası giderildi; fixture restore
bitmeden başlatılan build eksik assets verdi. İlk paralel varsayılan restore ilerleme
vermedi ve iptal edildi (exit 1); tek worker/no-reuse açık restore başarılı oldu.
ASP.NET analyzer'ın duplicate route tanısı suppression ile gizlenmedi: negatif
bildirimler iki ayrı kayıt metodunda korundu. İlk unit koşudaki fixture sayımı ve
kültüre bağlı sıralama beklentileri düzeltildi; tüm davranış assertion'ları korundu.
Integration testinin root metadata'yı traversal item'ında araması `find` sorgusuna
ayrıldı; export'a yanlış verilmiş context-only `--max-chars` seçeneği kaldırıldı.
Son smoke'un shell `> file` stdout yönlendirmeli iki index çağrısı MSBuild
BuildHost yüklemesinde timeout/exit 5 verdi; önceki snapshot'lar korundu. Aynı
index komutları shell yönlendirmesi olmadan tekrar çalıştırıldı; sonuç yukarıdaki
son smoke kaydına aittir. Yönlendirme ile timeout arasındaki nedensellik bu fazda
kanıtlanmadı. Bu ara başarısız koşular son kabul yerine sayılmaz.

## Sınırlar / NOT RUN

- Host aktivasyonu, gerçek HTTP isteği, DI container resolution/constructor seçimi,
  factory sonucu, scanning expansion ve database erişimi **çalıştırılmadı**;
  bunlar bu statik analiz fazının kabulü değildir.
- Inherited/custom MVC davranışı, keyfi route group/data flow, keyed services ve
  bütün framework extension'ları çözülmez. Eksikler matriste ve tanılarda açık.
- Windows/Linux, paketlenmiş tool, gerçek ağ kesilmiş offline kabul, büyük solution
  performansı: **NOT RUN**. Sonraki fazların tamamlandığı iddia edilmez.
- Git metadata yok: `git status --short` exit 128. Git diff **NOT RUN**; yerel kaynak
  kontrolleri yapıldı. Commit/push/publish ve AGENTS.md değişikliği yapılmadı.

## Değişen dosyalar

- Core: `GraphSnapshot.cs`, `Relationships.cs`, `GraphQuery.cs`.
- Roslyn: yeni `Frameworks/FrameworkExtraction.cs`, `Frameworks/MicrosoftDiExtractor.cs`,
  `Frameworks/AspNetCoreExtractor.cs`; `ProjectIndexer.cs`, `RelationshipCollector.cs`.
- CLI/sürüm/solution: `src/UO.Graphs.Cli/Program.cs`, `Directory.Build.props`, `UO.Graphs.slnx`.
- Fixture: yeni `samples/Phase7/Frameworks.cs`, `Frameworks.csproj`, `packages.lock.json`;
  `samples/Phase7/MinimalWeb/Program.cs`, `MinimalWeb.csproj`, `packages.lock.json`.
- Test: yeni `tests/UO.Graphs.UnitTests/FrameworkTests.cs`,
  `tests/UO.Graphs.IntegrationTests/Phase7Tests.cs`; unit `RelationshipTests.cs`,
  `UO.Graphs.UnitTests.csproj`.
- Belge: yeni `docs/FRAMEWORKS.md`, `docs/history/PHASE6.md`; `README.md`,
  `docs/PRODUCT.md`, `docs/ARCHITECTURE.md`, `docs/RELATIONSHIPS.md`, `docs/QUERIES.md`,
  `docs/STATUS.md`.

Önerilen commit: `feat: add evidence-based Microsoft DI and ASP.NET Core extraction`
