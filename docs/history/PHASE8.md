# Gerçekleşen durum

Son güncelleme: 2026-10-09.

**Faz 8, aşağıdaki macOS ARM64 statik messaging kabul kapsamında tamamlandı.**
Faz 9–11 başlamadı. Ürün **UO.Graphs 0.8.0**, CLI **uographs**.
Önceki [Faz 7 kanıtı](PHASE7.md) arşivlendi.
Broker, ağ servisi, application host, DI container veya mediator runtime başlatılmadı.

## Uygulananlar

- Gerçek UO.Mediator repository sözleşmesi ve **UO.Mediator.Shared 2.4.2** paketi
  incelendi: `IRequestDispatcher.DispatchAsync`, `HandleAsync`, Unit/no-response,
  `IRequestBehavior` / `RequestHandlerNext`. UO için olmayan notification/Publish
  API'si uydurulmadı.
- **MediatR 12.4.1 / MediatR.Contracts 2.0.1** ve **MassTransit.Abstractions 8.3.6**
  gerçek paket + tag kaynakları incelendi; API/assembly binding ile modüller eklendi.
  Bunlar permissive lisanslı fixture bağımlılıklarıdır; ürün paketleri değişmedi.
- HANDLES, DISPATCHES, PUBLISHES, CONSUMES, POSSIBLE_TARGET,
  BEHAVIOR_FOR, POSSIBLE_BEHAVIOR. CALLS compile-time anlamını korur.
- Mesaj entity kimliği framework, gerçek assembly/declaration/source-context ve
  recursive closed generic arguments ile belirlenir. Aynı adlı farklı namespace/
  assembly mesajlar ve `Request<int>` / `Request<string>` karışmaz.
- Exact API/sözleşme ile inferred handler/consumer adayları ayrıdır. Runtime seçim
  ayrıca unresolved kalır; publish tek consumer garantisi vermez. Kayıt bulunmaması
  veya başka servisteki consumer'ın görülmemesi yokluk kanıtı sayılmaz.
- Generic doğrudan Microsoft DI kayıtları aday ilişkiye ayrı kaynak occurrence'ı
  ekler; conditional kayıt belirsizliği korunur. Scanning/topology/container execution
  çözülmez. Closed behavior ayrı adaydır; open generic substitution ve sıra unresolved.
- Mevcut SQLite transaction/payload, query path/dependencies/impact ve context/export
  yeni metadata/ilişkileri korur. JSON v4 / SQLite 3 / delivery 1 değişmedi.
- Gerçek paket fixture'ları ve kaynak contract fixture'ları ayrı tutuldu; eşdeğer
  flow shape testi gerçek DLL testinin yerine geçmez.

Tam sözleşme, kaynaklar ve destek sınırları: [MESSAGING](../MESSAGING.md).

## Gerçek doğrulama

macOS 27.0 ARM64 / osx-arm64, SDK **10.0.201**, MSBuild **18.3.0**,
.NET runtime/reference pack **10.0.5**, Roslyn **5.0.0.0**.

| Gerçek komut/kontrol | Sonuç |
| --- | --- |
| `dotnet --info` | PASS; ortam yukarıda. |
| `dotnet restore samples/Phase8/Uo/Uo.csproj --ignore-failed-sources -p:NuGetAudit=false -m:1 -nr:false` | PASS; gerçek cache UO paketi. |
| `dotnet restore samples/Phase8/PackageCompatibility/PackageCompatibility.csproj --ignore-failed-sources -p:NuGetAudit=false -m:1 -nr:false` | PASS; gerçek MediatR/MassTransit paketleri edinildi. |
| `dotnet restore UO.Graphs.slnx --locked-mode --ignore-failed-sources -m:1 -nr:false -p:NuGetAudit=false` | PASS exit 0; development restore, runtime edinimi değil. |
| `dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS exit 0, **0 warning / 0 error**, son koşu 6.08 sn. |
| Unit TRX koşusu | PASS exit 0, **35/35**, yaklaşık 4 sn. |
| İki yeni messaging integration TRX koşusu | PASS exit 0, **2/2**, yaklaşık 14 sn. |
| Tüm integration regresyonları | PASS exit 0; **84/84**, **10 dk 57 sn**. |
| `python3 scripts/phase8_smoke.py` | PASS exit 0; doğrudan oluşturulan iki gerçek paket graph'ı üzerinde **7 ayrı CLI proses**, altı adımlı path/context/export. |
| `python3 scripts/audit_licenses.py --check` | PASS exit 0; **55** kilitli paket, MIT/Apache-2.0/doğrulanmış SQLite public domain. |
| Kaynak/config parse, whitespace, yerel Markdown bağlantıları | PASS exit 0; **138** kaynak/config/belge dosyası. |

Gerçek test komutları:

```sh
dotnet test tests/UO.Graphs.UnitTests --no-build --no-restore -m:1 -nr:false --logger 'trx;LogFileName=unit-final.trx' --results-directory .uographs/phase8-tests
dotnet test tests/UO.Graphs.IntegrationTests --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName~UoRealPackageEndpoint|FullyQualifiedName~RealMediatrAndMassTransit' --logger 'trx;LogFileName=messaging-final.trx' --results-directory .uographs/phase8-tests
dotnet test tests/UO.Graphs.IntegrationTests --no-build --no-restore -m:1 -nr:false --logger 'trx;LogFileName=integration-final.trx' --results-directory .uographs/phase8-tests
```

Son binary ile **35 unit + 84 integration = 119 ayrı vaka** geçti. TRX XML sayaçları
ayrıca okundu: failed/error/notExecuted=0. İki messaging vakası ayrıca odaklı koşuldu;
toplama ikinci kez eklenmedi.

10 yeni unit vaka ve iki yeni integration vaka vardır. Önceki testler korunur;
central ilişki yönü testi yeni yedi yönü de sabitler. Gerçek package compilation,
response/no-response, A→B dispatch, çoklu notification/consumer, pipeline, açık generic,
conditional registration, object/computed Type, explicit typeof ve interface initializer,
aynı isimli farklı namespace/assembly, lookalike ve compilation error sınanır.

## Gerçek CLI smoke

Doğrudan indeksleme için gerçek önek:
`dotnet run --project src/UO.Graphs.Cli --no-build --no-restore --`.

| Gerçek argümanlar | Sonuç |
| --- | --- |
| `index samples/Phase8/Uo/Uo.csproj --full --trust-project --db .uographs/phase8-direct-uo.db` | exit 0; **43 entity / 198 relationship / 260 occurrence**. |
| `index samples/Phase8/PackageCompatibility/PackageCompatibility.csproj --full --trust-project --db .uographs/phase8-direct-packages.db` | exit 0; **44 entity / 219 relationship / 279 occurrence**. |

Query smoke betiği `dotnet src/UO.Graphs.Cli/bin/Debug/net10.0/uographs.dll` ile
ayrı prosesler açar. Tam komut dizileri/exit code'lar `.uographs/phase8-evidence/commands.json`;
status, path, context, iki tam export ve event dependencies JSON dosyaları aynı dizindedir.
Export'larda truncation=false; UO 5, diğer paketler 7 ayrı mesaj entity'si üretir.

Path'in gerçek ilişkileri:

```text
GET /phase8/start → FlowController.Start → Service.Run
  → RequestA message → HandlerA.HandleAsync → RequestB message → HandlerB.HandleAsync
HANDLED_BY(exact), CALLS(exact), DISPATCHES(exact), POSSIBLE_TARGET(inferred),
DISPATCHES(exact), POSSIBLE_TARGET(inferred)
```

Inferred opt-in olmadan bu edge policy ile path bulunmaz; integration testi bunu da
sınar. Context aynı zinciri, kaynak kanıtlarını ve harici consumer kapsamını taşır.
MediatR Event için First/Second, MassTransit Event için Consumer1/Consumer2 iki ayrı
inferred hedeftir; her mesajın unresolved runtime hedef kaydı da kalır.

UO snapshot: `ef7d776004ec4287ae6e82b8f89153ba`.
Diğer paket snapshot: `c170d7275dc74ed8a51d98341383c241`.
İkisi de **partial**; runtime belirsizlikleri ve generator politikası açıktır.
Freshness **unknown**: custom item/external Compile girdileri nedeniyle Faz 6 reuse
politikası kapalıdır. Bu faz freshness veya no-op garantisini genişletmez.

## Ara başarısızlıklar ve sınırlar

- İlk `curl` paket indirme denemesi DNS çözümleyemedi (exit 6). Ardından açık
  development `dotnet restore` başarılı oldu; paketler ve nuspec/lock'lar doğrulandı.
- İlk unit koşusu 32/35, integration 0/2 idi: central yön listesi yeni enum'larla
  genişletildi; yeni testlerde ters string assertion, relationship/occurrence ayrımı,
  JSON options içindeki edge adı ve export'un desteklemediği max-chars kullanımı
  düzeltildi. Üretim assertion'ı gevşetilmedi/test silinmedi; son koşular yukarıda.
- Python tarafından stdout/stderr yakalanan ilk index, workspace yüklemede timeout
  ile exit 5 verdi. Aynı fixture'ların doğrudan terminal index'leri exit 0; query
  smoke mevcut bu DB'lerde geçti. Bu ortam/invocation farkı giderilmiş sayılmaz.
- Windows/Linux, paketlenmiş tool, ağ fiziksel olarak kapalı offline kabul ve büyük
  solution performansı **NOT RUN**. Runtime broker/host teslim testi bilinçli kapsam dışı.
- Concrete dispatcher/custom extension/stream API, open generic substitution,
  registration scanning ve MassTransit topology expansion kapsam dışıdır.
  Destek yalnız belirtilen paket sürümleri ve [matris](../MESSAGING.md) içindir.
- `git status --short` exit 128: checkout'ta Git metadata yok. Git diff **NOT RUN**;
  dosya tabanlı kontroller uygulanır. AGENTS.md değiştirilmedi; commit/push/publish yok.

## Değişen dosyalar

- Core: `GraphSnapshot.cs`, `Relationships.cs`, `GraphQuery.cs`.
- Roslyn: yeni `Frameworks/MessagingExtractor.cs`; `Frameworks/FrameworkExtraction.cs`,
  `ProjectIndexer.cs`. CLI: `Program.cs` sürüm bilgisi.
- Config: `Directory.Build.props`, `Directory.Packages.props`, `UO.Graphs.slnx`;
  restore'un güncellediği ilgili lock dosyaları.
- Yeni fixture'lar: `samples/Phase8/Uo`, `PackageCompatibility`, `Contracts`,
  `ContractFixtures/MediatR`, `ContractFixtures/MassTransit`; kaynak provenance/lisanslar.
- Test: yeni `MessagingTests.cs`, `Phase8Tests.cs`; `RelationshipTests.cs` yön tablosu.
- Script: yeni `scripts/phase8_smoke.py`; `scripts/audit_licenses.py` envanter başlığı.
- Belge: yeni `docs/MESSAGING.md`, `docs/history/PHASE7.md`; `README.md`,
  `docs/PRODUCT.md`, `ARCHITECTURE.md`, `RELATIONSHIPS.md`, `QUERIES.md`, `STATUS.md`,
  `DEPENDENCIES.md`, `THIRD-PARTY-NOTICES.txt`.

Önerilen commit: `feat: model mediator and messaging flows with semantic evidence`
