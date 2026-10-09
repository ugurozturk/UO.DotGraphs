# Gerçekleşen durum

Son güncelleme: 2026-10-08.

**Faz 3, aşağıdaki macOS ARM64 semantik ilişki kabul kapsamında tamamlandı.**
Faz 4–11 başlamadı.
[Faz 2 kanıt kaydı](PHASE2.md) arşivlendi. Önceki workspace, kimlik,
güven/SDK/assets ve generator sınırları korunuyor.

## Uygulananlar

- Merkezi yön/anlam sözleşmesiyle DECLARES, PROJECT_REFERENCES, INHERITS,
  IMPLEMENTS, OVERRIDES, USES_TYPE, REFERENCES, CALLS.
- `SemanticModel`, Roslyn symbol binding, `IInvocationOperation`, object creation,
  member references ve accessor/operation API'leri. Metin/regex/ad benzerliğiyle
  çağrı hedefi eşleştirmesi yok.
- CALLS caller → compile-time target. Interface/virtual slot, explicit implementation
  ve override ayrı tutulur. Runtime implementasyonlar exact CALLS yapılmaz;
  MAY_DISPATCH_TO/inferred expansion bu fazda üretilmez.
- Tekil ilişki ve occurrence ayrımı; aynı iki metot arasındaki farklı konumlar,
  source span/hash, analysis/context ID, bound generic imza, gerekçe ve adaylar korunur.
- Method group REFERENCES; delegate/dynamic/reflection runtime hedefi unresolved.
  Local function/lambda bağımsız caller. Harici/örtük hedefler sahte source entity
  üretmeden assembly/declaration endpoint'i taşır.
- Compilation hatası içeren executable declaration'da recovery binding exact
  yapılmaz. Bağımsız geçerli member analiz edilebilir. Farklı TFM/kaynak/parse
  bağlamındaki hedef seçili source entity'ye birleştirilmez; proje referansı da
  unresolved kalır.
- JSON v3, ürün 0.3.0, SQLite schema v2; relationship/occurrence tabloları atomik
  replacement içinde. Schema v1 okunur ve gerçek schema sürümü raporlanır;
  başarılı re-index atomik yükseltir, başarısızlık eski veriyi/şemayı korur.
- CLI index/status ilişki ve occurrence sayıları; query find eşleşmelerine bitişik
  ilişki kanıtı. Faz 4 callers/callees/traversal veya Faz 5 export eklenmedi.
- Yeni paket yok. Phase3 fixture net10.0 ve boş bağımlılık lock dosyası; mevcut
  50 doğrudan/geçişli paketin lisans denetimi başarılı.

Yönler ve destek matrisi: [RELATIONSHIPS](../RELATIONSHIPS.md).
Kimlik/kullanım: [IDENTITY](../IDENTITY.md), [README](../../README.md).

## Gerçek doğrulama

Ortam: macOS 27.0 ARM64 / osx-arm64, SDK 10.0.201, MSBuild 18.3.0,
runtime 10.0.5; Roslyn assembly 5.0.0.0.

| Gerçek komut/kontrol | Sonuç |
| --- | --- |
| `dotnet --info` | PASS, exit 0; yukarıdaki kurulu sürümler. |
| `dotnet restore samples/Phase3/Semantics.csproj --locked-mode -m:1 -nr:false` | PASS, exit 0; geliştirme fixture hazırlığı, ürün runtime restore yapmadı. |
| `dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, son kaynaklarla exit 0; 0 warning/error, 7.50 saniye. |
| `dotnet build samples/Phase3/Semantics.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, exit 0; 0 warning/error, 1.31 saniye. |
| `dotnet test UO.Graphs.slnx --no-build --no-restore -m:1 -nr:false` | PASS, exit 0; **30 integration + 10 unit = 40/40**, skipped 0; integration 1 dk 27 sn, unit 730 ms. |
| `python3 scripts/audit_licenses.py --check` | PASS, exit 0; 50 kilitli paket, MIT/Apache-2.0/SQLite public domain. |
| Python XML/JSON ve kaynak whitespace kontrolü | PASS, exit 0; son kontrolde 37 source/config/document dosyası ve teslim bağlantıları. |

Gerçek CLI smoke (kurulu tool varsayılmadı):

```sh
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/Phase3/Semantics.csproj --trust-project --allow-generators --db .uographs/phase3-final.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- query find Overload --format json --db .uographs/phase3-final.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- status --db .uographs/phase3-final.db
```

Üçü de **PASS / exit 0**. Son binary: **41 entity, 117 ilişki, 142 occurrence**,
completeWithinDeclaredScope, JSON v3 / schema v2. Query iki doğru overload'ı, 9 bitişik
ilişkiyi ve 10 occurrence'ı SQLite'tan yeni prosesle okudu. Index/query/status aynı
snapshot'ı doğruladı: `ae45a0f760264507aca6f54b50dce6bc`.

Ek gerçek HelloGraph solution smoke:

```sh
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --trust-project --allow-generators --db .uographs/phase3-hello-direct.db
```

PASS, exit 0; 2 proje, 15 entity, 20 ilişki, 23 occurrence,
completeWithinDeclaredScope. Cross-project Greet hedefi gerçek Domain entity ID'sine
bağlandı; constructor harici/örtük endpoint modeliyle korundu. Son entegrasyon
suite'inde cross-project yön/kimlik ayrıca test edilir.

## Kabul testlerinin kapsamı

- Merkezi sekiz ilişki yönü/anlamı ve JSON enum adları; tekil ilişki/occurrence.
- Overload/generic/extension, explicit interface implementation, virtual override,
  partial class, lambda/local function, constructor ve external library hedefleri.
- Caller'ın **bütün exact CALLS hedef kümesi**; yasak overload/implementation/
  method-group/outer-lambda/reverse-direction bağlantılarının yokluğu.
- Aynı int overload'a iki kaynak konumu; span'dan çıkarılan gerçek ifade ve hash.
  Virtual base slotun occurrence sayısı; concrete override call-site'ın gerçek ifadesi.
- Getter/setter/read-write/indexer/ref-return, nameof, delegate creation,
  deconstruction-unresolved ve dynamic constructor.
- Bozuk kodun gerçek CLI → SQLite → status partial akışı; hatalı overload/generic/
  constructor/unknown/ambiguous adaylarda hiçbir exact CALLS yok. İç içe constructor
  dahil her unresolved call-site kaynak ifadesi tek tek doğrulanır; candidate symbols
  exact hedef yapılmaz. Bağımsız geçerli member'ın exact ilişkisi korunur.
- SQLite + ayrı CLI query prosesinde endpoint, confidence, analysis/context ID,
  source span/hash/gerekçe, occurrence round trip; partial DECLARES kaynakları.
- Schema v1 okuma/yükseltme/rollback; occurrence foreign-key hatasında bütün snapshot
  rollback; eski storage, güven ve Faz 2 testleri korunur. Test silinmedi.

## Ara koşulardaki sorunlar

- Yönlendirilmiş integration test koşusu ve bir filtreli deneme VSTest
  `SocketException (13): Permission denied` nedeniyle başlayamadı. Yönlendirilmiş
  CLI index iki MSBuild build-host timeout ile exit 5 verdi; snapshot yazılmadı.
  Doğrudan izinli komutlarla test/CLI çalıştı. Ağ/sandbox izolasyonu iddiası yok.
- Ek testin `field` adlı alanı C# 14 contextual keyword davranışı nedeniyle CS8145
  üretti. Fixture alanı `backing` yapıldı; assertion kaldırılmadı.
- Bozuk fixture ilk sayımında nested `new object()` unutulmuştu. Sayı assertion'ı,
  iç içe constructor dahil altı beklenen kaynak ifadesinin tam karşılaştırmasıyla
  güçlendirildi; üretim çıktısı beklenen sayıya zorlanmadı.

## Sınırlar / NOT RUN

- Windows/Linux build/test/CLI: **NOT RUN**, bu oturum yalnızca macOS ARM64 ortamı.
- Gerçek ağ kesilmiş offline çalışma, paket kurulumu, Release JIT ve büyük solution
  performansı: **NOT RUN** bu fazda; Faz 11 kabulü yerine sayılmaz.
- Runtime dispatch, delegate data-flow/reflection hedef çözümü yapılmaz. Foreach/
  await/using/pattern/deconstruction ve synthesized lowering için eksiksiz graph
  taahhüdü yok; [destek matrisi](../RELATIONSHIPS.md) sınırları geçerlidir.
- Kaynak freshness notChecked. PROJECT_REFERENCES kanıtı proje dosyasının hash'i ve
  değerlendirilmiş referanstır; XML öğe konumu hassas span gibi sunulmaz.
- Git metadata bulunmadı; `git status --short` exit 128. Git diff denetimi **NOT RUN**;
  kaynak whitespace/XML/JSON denetlendi. Commit/push/publish yapılmadı.

## Değişen dosyalar

- Core: `GraphSnapshot.cs`; yeni `Relationships.cs`.
- Roslyn: `ProjectIndexer.cs`, `SymbolCollector.cs`; yeni `RelationshipCollector.cs`,
  `AssemblyInfo.cs` (test erişimi).
- Storage/CLI: `GraphStore.cs`, `Program.cs`; sürüm için `Directory.Build.props`.
- Tests: yeni `RelationshipTests.cs`, `Phase3Tests.cs`; `CliTests.cs`, `Phase2Tests.cs`,
  `StorageTests.cs` güncellendi.
- Yeni fixture: `samples/Phase3/Semantics.csproj`, `Semantics.cs`, `Partial.cs`,
  `packages.lock.json`.
- Belgeler: yeni `docs/RELATIONSHIPS.md`, `docs/history/PHASE2.md`; güncellenen
  `README.md`, `docs/ARCHITECTURE.md`, `docs/IDENTITY.md`, `docs/STATUS.md`.
- AGENTS.md, paket sürümleri ve mevcut instruction dosyaları değiştirilmedi.

Önerilen commit: `feat: implement phase 3 semantic relationships and source occurrences`
