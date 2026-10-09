# Gerçekleşen durum

Son güncelleme: 2026-10-09.

**Faz 9 uygulandı; üç modülün aşağıda belirtilen sınırlı macOS statik kabulü geçti.**
Standart VSTest ve alt proses workspace entegrasyonu açık kaldığı için Faz 9'un
**tam doğrulama kapısı kapatılmadı**. Faz 10–11 başlamadı. Ürün **UO.Graphs 0.9.0**,
CLI **uographs**. Önceki [Faz 8 kanıtı](../history/PHASE8.md) arşivlendi.

## Uygulananlar ve ayrı kapsam

- **ABP 10.1.1:** ApplicationService/IApplicationService sözleşmeleri, module
  DependsOn, conventional DI adayları, ExposeServices/Dependency/Disable override,
  IRepository ve inherited IBasic/IReadOnly generic entity kullanımı çıkarılır.
  ConventionalControllers.Create assembly kanıtı yoksa conventional endpoint
  adayı üretilmez. RemoteService/NonAction filtreleri uygulanır. Configuration
  kanıtlı servisler `endpointCandidate` ve inferred kalır; convention/custom route
  çözümlenmediği için verb/route null. Repository'den tablo/schema tahmini yok.
- **EF Core 10.0.5:** DbContext, DbSet, ModelBuilder.Entity, configuration contract,
  Table/Column annotation ve dar fluent ToTable/ToView/HasColumnName bildirimleri
  modellenir. Annotation/fluent override ayrı kaynak bildirimidir. Runtime
  table/schema/column, scanning, Ignore, owned mapping ve custom builder wrapper
  yolu açık unresolved kayıt/tanı üretir. Efektif model veya SQL iddiası yok.
- **Refit 8.0.0:** Gerçek metadata attribute + bound interface method kullanılır;
  HTTP_CONTRACT ve INVOKES_HTTP bağları vardır. Aynı route'a sahip iki servis
  ayrıdır. BaseAddress ve remote endpoint unresolved; local MVC route benzerliği
  ilişki kurmaz. Custom/multiple HTTP attribute, static/default method ve runtime
  client configuration negatifleri ayrıdır.
- Core'a dokuz ilişki yönü ve optional Framework/ModelMapping/HttpContract metadata
  eklendi. Mevcut SQLite transaction/payload, dependencies/impact/path/context/export
  hattı kullanılır. JSON v4 / SQLite 3 / delivery 1 değişmedi; yeni ürün paketi/katman yok.
- ABP'nin **LGPL-3.0-only** bağımlılıkları yalnız ayrı, non-packable fixture içindedir.
  Ürün/test/runner dependency closure'ına girmeleri audit hatasıdır. EF/Refit MIT;
  NUglify BSD/Apache ve bundled MyCSharp MIT ayrıca doğrulandı. Toplam 143 lock
  package/version kaydı için lisans kanıtı var; [DEPENDENCIES](../DEPENDENCIES.md).

Tam sürüm/API kaynakları, her modülün supported/partial/unsupported matrisi,
yanlış-pozitif riskleri ve test isimleri: [ABP-EF-REFIT](../ABP-EF-REFIT.md).

## Gerçek doğrulama

macOS 27.0 ARM64 / osx-arm64; SDK **10.0.201**, MSBuild **18.3.0**,
.NET runtime/reference pack **10.0.5**, Roslyn **5.0.0.0**.

| Gerçek komut/kontrol | Sonuç |
| --- | --- |
| `dotnet --info` | PASS, ortam yukarıda. |
| `dotnet restore UO.Graphs.slnx --ignore-failed-sources -p:NuGetAudit=false -m:1 -nr:false` | İlk koşu Refit fixture PackageId çakışması NU1108; ayrı PackageId düzeltildi, açık development restore geçti. |
| `dotnet restore UO.Graphs.slnx --locked-mode --ignore-failed-sources -m:1 -nr:false -p:NuGetAudit=false` | PASS exit 0. Runtime restore/download eklenmedi. |
| `dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS exit 0; **0 warning / 0 error**, son koşu **8.71 sn**; üç gerçek paket fixture da derlendi. |
| `dotnet restore scripts/TestRunner/TestRunner.csproj --ignore-failed-sources -p:NuGetAudit=false -m:1 -nr:false` | PASS exit 0; yalnız mevcut test bağımlılıkları. |
| `dotnet build scripts/TestRunner/TestRunner.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS exit 0, 0 warning / 0 error, **1.97 sn**. |
| `dotnet scripts/TestRunner/bin/Debug/net10.0/TestRunner.dll UnitTests .uographs/phase9-evidence/unit-final.json` | PASS exit 0; **49/49**, failed=0. Aynı xUnit assertion'larının soketsiz, sıralı invocation'ı; VSTest sonucu değildir. |
| `dotnet test tests/UO.Graphs.UnitTests --no-build --no-restore -m:1 -nr:false --filter FullyQualifiedName~Phase9 --logger 'trx;LogFileName=phase9-initial.trx' --results-directory .uographs/phase9-tests` | **ABORTED exit 1 / test execution NOT RUN**; VSTest SocketException(13), yerel TCP listener izni yok. |
| `dotnet scripts/TestRunner/bin/Debug/net10.0/TestRunner.dll CliTests.Phase9 .uographs/phase9-evidence/integration-initial.json` | **FAIL exit 1, 0/3**; her testin başlattığı CLI workspace load ~63 sn sonra timeout/exit 5. Assertion gevşetilmedi. |
| Doğrudan üç CLI index, aşağıdaki komutlar | PASS, **3/3 exit 0**; son binary ve gerçek fixture kaynakları. |
| `python3 scripts/phase9_smoke.py` | PASS exit 0; **ABP / EF / Refit 3 ayrı kabul**, **12 ayrı CLI proses**. |
| `python3 scripts/audit_licenses.py --check` | PASS exit 0; **143** kilitli paket/sürüm, fixture LGPL ayrımı denetimli. |
| Source/config parse, whitespace, yerel Markdown bağlantıları | PASS; **184 dosya** incelendi. Git metadata yok, dosya tabanlı kontrol. |

49 birim vakanın **14'ü yeni Faz 9 vakasıdır**: ABP **5**, EF **5**, Refit **4**.
Üç compilation-error theory satırı ilgili modülün sayısına dahildir. İkinci kez
sayılmaz. Önceki 35 vaka da korunur ve geçer. Yeni üç integration testi kaynakta
vardır fakat bu ortamda başarılı sayılmaz; eski integration suite bu tur **NOT RUN**.

Soketsiz koşucu [TestRunner](../../scripts/TestRunner/README.md), mevcut test methodlarını
Fact/InlineData ile fresh instance üzerinde çağırır, Task bekler, IDisposable kapatır,
unsupported test lifecycle/data/skip davranışını reddeder. xUnit discovery,
parallelization veya standart VSTest çalışmasını doğruladığı iddia edilmez.

## Gerçek CLI smoke

Doğrudan çağrıların öneki:
`dotnet run --project src/UO.Graphs.Cli --no-build --no-restore --`.

| Argümanlar | Sonuç: entity / relationship / occurrence |
| --- | --- |
| `index samples/Phase9/Abp/Abp.csproj --full --trust-project --db .uographs/phase9-abp.db` | exit 0; **47 / 220 / 323** |
| `index samples/Phase9/EfCore/EfCore.csproj --full --trust-project --db .uographs/phase9-efcore.db` | exit 0; **53 / 200 / 247** |
| `index samples/Phase9/Refit/Refit.csproj --full --trust-project --db .uographs/phase9-refit.db` | exit 0; **42 / 147 / 163** |

Son snapshot'lar: ABP `9b841a6252664013b88b80419fcbee81`,
EF `164a0081932c4a5bb3bc00d7377c99a1`, Refit `c029944fc1e545c7acb7a4ea05ab90f3`.
Üçü de partial; configuration/runtime/generator eksikleri açık. Freshness unknown:
custom MSBuild/package input closure kanıtlanmadığından reuse kapalı. Faz 6 no-op
veya performans kapsamı bu fazda genişletilmedi.

Smoke script bu gerçek DB'lerde ayrı `dotnet .../uographs.dll` prosesleriyle:

- ABP'de inferred HANDLED_BY → exact REPOSITORY_ENTITY iki adımlı path, inferred
  kapalıyken boş path, context, hidden-service negatifleri, module/repository export;
- EF'de annotation/fluent çakışması, doğru column, dinamik schema, custom/scanning
  belirsizliği, captured/computed property negatifleri ve context → entity sorgusu;
- Refit'te aynı route'lu iki contract, local endpoint'e hedef bağlanmaması,
  call → contract path, base address belirsizliği ve context/export

doğrular. Full export'larda truncated=false. Tam komut/exit dizisi
`.uographs/phase9-evidence/smoke-commands.json`, ayrı sonuçlar `smoke-results.json`,
status/path/context/export JSON'ları aynı dizindedir. Fixture output'u hardcode
graph yerine kullanılmaz; SQLite'dan CLI export'ları assertion girdisidir.

## Ara hatalar, açık kapı ve sınırlar

- Refit fixture adı başlangıçta package ile aynıydı (NU1108); AssemblyName/PackageId
  ayrıldı. ABP nullable Lifetime named argument CS0655 verdi; gerçek ctor kullanıldı.
  Refit 8 generator static HTTP method için CS0539 verdi; bu negatif shape gerçek
  DLL semantik compilation testinde, normal fixture ise çalışan generator ile derlenir.
- İlk custom-runner vaka hazırlığında duplicate TPA path hatası düzeltildi; source
  assertions değişmedi. Varsayılan `dotnet run` runner build çağrısı bu ortamda çıktı üretmeden
  bekledi; kesildiğinde exit 1 döndü. Doğrulamalar açık `build -m:1 -nr:false`
  ardından doğrudan DLL ile yapıldı.
- Bağımsız API/kod incelemesi EF captured/computed property, runtime schema/column ve
  Refit çoklu attribute yanlış kesinliklerini buldu; üretim kodu düzeltildi ve ayrı
  negatif testleri geçti. İkinci ajan dosya değiştirmedi/test sonucu iddia etmedi.
- Standart VSTest için sandbox dışı çalıştırma talebi izin politikası tarafından
  **otomatik reddedildi** (`sandbox_approval=false`). İzin/terminal politikası aşılmadı.
  Soketsiz fallback ve doğrudan CLI kabulü bu reddedilen koşunun yerine PASS yazmaz.
- Üç alt-proses integration testi workspace timeout nedeniyle başarısız kaldı.
  **Faz 9 tam kapısı:** IPC'ye izin veren ortamda standart unit + üç yeni integration
  ve ilgili integration regresyonlarını çalıştırıp sonucu kaydetmek gerekir.
- Windows/Linux, paketlenmiş tool, fiziksel offline ağ izolasyonu ve büyük solution
  performansı **NOT RUN**. Runtime host/DB/HTTP kabulü statik analiz kapsamı dışındadır.
- Kullanıcı projesi veya erişilmeyen bir sistem analiz edilmedi. Yalnız fixture'lar
  ve ürün kaynakları incelendi. Uygulama host'u, DbContext, migration, DB bağlantısı,
  HTTP isteği, MCP/servis/LLM/API key/telemetry eklenmedi.
- `git status --short` exit 128: klasörde Git metadata yok. Git diff **NOT RUN**;
  AGENTS.md korunur; commit/push/publish yapılmadı.

## Değişen dosyalar

- Core: `GraphSnapshot.cs`, `Relationships.cs`, `GraphQuery.cs`.
- Roslyn: yeni `Frameworks/AbpExtractor.cs`, `EfCoreExtractor.cs`, `RefitExtractor.cs`,
  `FrameworkSymbols.cs`; `FrameworkExtraction.cs`, `ProjectIndexer.cs`.
- CLI sürümü: `src/UO.Graphs.Cli/Program.cs`; `Directory.Build.props`.
- Config: `Directory.Packages.props`, `UO.Graphs.slnx`, restore ile değişen lock'lar.
- Fixture: `samples/Phase9/{Abp,EfCore,Refit}` csproj/source/lock dosyaları.
- Tests: yeni unit/integration `Phase9Tests.cs`; `RelationshipTests.cs` yön tablosu.
- Script: `phase9_smoke.py`, `TestRunner` csproj/source/lock/README,
  `audit_licenses.py` fixture lisans istisnası ve gerçek lisans doğrulamaları.
- Docs: `ABP-EF-REFIT.md`, `history/PHASE8.md`, `licenses/NUglify-1.21.17.txt`,
  `STATUS.md`, `ARCHITECTURE.md`, `PRODUCT.md`, `RELATIONSHIPS.md`, `QUERIES.md`,
  `DEPENDENCIES.md`, `README.md`, `THIRD-PARTY-NOTICES.txt`.

Önerilen commit: `feat: analyze ABP EF Core and Refit with bounded semantic evidence`
