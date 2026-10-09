# Gerçekleşen durum

Son güncelleme: 2026-10-09.

**Faz 6, aşağıdaki macOS ARM64 incremental/atomiklik/freshness kabul kapsamında tamamlandı.**
Faz 7–11 başlamadı. [Faz 5 kanıt kaydı](PHASE5.md) arşivlendi.
Ürün **UO.Graphs 0.6.0**, CLI **uographs**. AGENTS.md ve önceki semantik/kimlik,
coverage, generator izin, query/context/export sözleşmeleri korunuyor.

## Uygulananlar

- `index --incremental` varsayılan, `index --full` zorunlu full; iki mod birlikte
  reddedilir ve ikisi de MSBuild öncesi `--trust-project` ister. Restore/download yok.
- Raw SHA-256 içerik + dosya üyeliği + evaluated/preprocessed context fingerprint.
  Kaynak, project/solution, imported props/targets, ancestor Directory.Build.*,
  Directory.Packages.props, assets/lock, SDK/compiler/tool/analyzer/paket içerikleri,
  TFM/configuration/defines/analysis options, additional/editorconfig inputs.
  Eksik girdilerin sonradan oluşması ve MSBuild user/extensions/CustomBefore/After
  import arama konumları da izlenir. mtime reuse kararı vermez.
- `entireSelectedProjectClosure` granularity: eşit ve tamamlanmış fingerprint'te
  no-op; değişiklikte **tüm seçili projeler ve referans closure** yeniden analiz.
  Seçici project/method reuse iddiası yoktur. Değişmeyen dosyaların binding'i de
  yeniden çıkarılır. Eski entity/edge/occurrence ve inference kayıtları temizlenir.
- Base SDK 10.0.201 deklaratif modelin dışındaki custom build/task/import/unknown
  property/item behavior, dış item pattern'leri ve generator execution için
  dependency eksikleri raporlanır; reuse kapanır, geniş invalidation uygulanır.
  Tamamlık graph coverage'dan ayrıdır. Okunamayan/symlink input taraması fail-closed.
- Yayın öncesi yeniden evaluation/content ve Roslyn'in tükettiği decoded source
  kontrolü. Preparation/preprocess alt süreçleri çağrının başlangıç environment
  kopyasını kullanır; Locator'ın iç ortam değişiklikleri karşılaştırmayı bozmaz.
- Analizden commit'e kadar tek writer OS lease; canonical DB yolları, contention
  exit 10. SQLite WAL + FULL synchronous + tek transaction içinde graph/manifest
  replacement. Concurrent reader tek tutarlı nesil okur. Hata, cancellation veya
  proses ölümü son commit'i korur. Kesilmiş ilk publication'dan sonra yeniden index
  yapılabilir; `--full` eski fingerprint'i kullanmaz.
- Status, query, context ve bütün export formatlarında varsayılan `notChecked`;
  `--check-freshness` yalnız filesystem/environment/content kontrolü yapar.
  `--require-fresh` stale/unknown durumda exit 9 + boş stdout. Read'de MSBuild,
  restore veya otomatik index yok. Metot, gerçek hash file/byte/ms maliyeti,
  gerekçe ve kesilmiş ölçümde `countsComplete: false` görünürdür.
- JSON v4 ve delivery v1'e ek alanlar; SQLite schema 3'te isteğe bağlı atomik
  `fingerprint` tablosu. Eski snapshot için checked freshness `unknown`.
  Yeni paket, servis, katman veya Faz 7–9 framework analyzer'ı eklenmedi.

Ayrıntılı sınırlar ve komutlar: [INCREMENTAL](../INCREMENTAL.md).

## Gerçek doğrulama

Ortam: macOS 27.0 ARM64 / osx-arm64, SDK **10.0.201**, MSBuild **18.3.0**,
runtime **10.0.5**, Roslyn assembly **5.0.0.0** (`dotnet --info` exit 0).

| Gerçek komut/kontrol | Sonuç |
| --- | --- |
| `dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, exit 0; son kaynaklarla **0 warning/error**, 7.76 saniye. |
| `dotnet test UO.Graphs.slnx --no-build --no-restore -m:1 -nr:false --logger 'console;verbosity=normal'` | PASS, exit 0; son kaynaklarla **80 integration + 11 unit = 91/91**, skipped 0. Integration 10 dk 19 sn; unit 1.04 sn. |
| `dotnet test tests/UO.Graphs.IntegrationTests --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName~NewlyCreatedImplicitUserImport\|FullyQualifiedName~FreshnessChecks' --logger 'console;verbosity=normal'` | PASS, exit 0; son kaynaklarla **2/2**, 49.69 saniye. Gerçek filtrede `\|` yerine düz `|` kullanıldı. |
| `python3 scripts/audit_licenses.py --check` | PASS, exit 0; **50** kilitli paket, MIT/Apache-2.0/SQLite public domain. Paket seti değişmedi. |
| Kaynak whitespace, XML/JSON ve Markdown yerel dosya bağlantıları | PASS, exit 0; **57** kaynak/config/belge dosyası; XML/JSON parse ve yerel Markdown bağlantıları. |

## Gerçek CLI smoke ve no-op ölçümü

Aşağıdaki her satır gerçek ayrı CLI prosesidir; tümü **exit 0**:

```sh
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --full --trust-project --db .uographs/phase6-final.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --incremental --trust-project --db .uographs/phase6-final.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- status --db .uographs/phase6-final.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- status --require-fresh --db .uographs/phase6-final.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- query callers Greet --require-fresh --db .uographs/phase6-final.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- context Greet --require-fresh --max-chars 12000 --db .uographs/phase6-final.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- export --format json --symbol Greet --require-fresh --db .uographs/phase6-final.db
```

Snapshot `d04f1120b4d547499c1d36aadeba1de8`; **15 entity / 20 ilişki / 23 occurrence**.
Full ve no-op ID aynı. Generator kapalı olduğu için graph coverage **partial**;
dependency modelinin tamamlığı ve kaynak freshness bunun yerine geçmez.

| Ölçülen alan | Full | Incremental no-op |
| --- | ---: | ---: |
| Yeniden analiz edilen proje | 2 | **0** |
| Fingerprint (SDK/evaluation/preprocess/content), ms | 3506 | **3284** |
| Analiz + yayın öncesi doğrulama, ms | 5874 | **0** |
| Hashlenen gerçek dosya sayısı (full iki tarama) | 13394 | 6697 |
| Hashlenen byte (full iki tarama) | 1681017320 | 840508660 |
| Outcome | rebuiltAllProjects | noOp |

No-op stderr'de workspace `Loading` / `Reading symbols` yok; DB replacement yok.
SDK/paket/tool içerikleri geniş tarandığı için no-op hâlâ yaklaşık **840 MB** okuyor
ve MSBuild evaluation/preprocess çalıştırıyor. Bu maliyet saklanmıyor; sıfır I/O,
method-level incremental veya büyük-repo performans kabulü iddiası yok.

Varsayılan status `notChecked` ve 0 hash file/byte/ms verdi. `--require-fresh`
status/callers/context/export kontrolleri `fresh`, **6697 dosya / 840508660 byte**;
sırasıyla **1431 / 1429 / 1424 / 1207 ms**. İlk üç kontrol birlikte çalıştırıldı;
bu değerler tek kullanıcı gecikme kıyaslaması değildir. Bütün okumalardan önce ve
sonra DB SHA-256 aynı:
`087d7b8bb61b9d9b344b595f781b7a5f4d4858ccb589f8a289f943cef8e2c139`.
JSON kanıtları yerel `.uographs/phase6-evidence/` altındadır.

## Kabul testlerinin kapsamı

Önceki 79 vaka korunur; **11 integration + 1 unit = 12 yeni vaka** eklendi.

- Temiz ayrı DB'ye full index ile **15 senaryoda** bütün entity/relationship/
  occurrence ve analysis context eşitliği. Yalnız analysis ID, CreatedAtUtc ve
  occurrence AnalysisId normalize edilir; fingerprint/operasyon ölçümleri graph
  içeriği yerine karşılaştırılmaz. No-op, dosya ekleme, aynı mtime ile method body,
  public signature, yeni overload, global using ekleme/değişimi, dosya taşıma/silme,
  configuration + project dependency, lock/assets içeriği, import ekleme/değişimi,
  framework context. Overload için değişmeyen consumer'ın yeni statik hedefi ve
  silinmiş API için eski entity/edge yokluğu ayrıca assert edilir.
- No-op snapshot ID korunması, 0 analiz sayısı/süresi, workspace/symbol progress
  yokluğu; content hash byte sayısı sıfır değil. Tarih sabitleme optimizasyonu yok.
- Default/checked/required freshness, fresh/stale/unknown, status/query/context/
  export reddi ve boş stdout; DB bytes korunması. Unknown custom build'de tekrar
  no-op olmaması; güven bayrağının incremental'da da gerekmesi.
- Başlangıçta mevcut olmayan **dış MSBuild user import** dizinine gerçek target
  eklenmesi: stale, yeniden binding, yeni `EXTERNAL_INPUT` define ve full eşitliği.
- 30000 entity replacement ile concurrent reader'ın yalnız eski/yeni tutarlı
  sayıları görmesi; aynı süreçte ve ayrı CLI prosesinde writer contention.
- SQLite WAL sayfaları yazıldıktan sonra token cancellation ve ayrı CLI prosesinin
  öldürülmesi: eski graph eksiksiz kalır; lease yeniden alınabilir. Analiz sırasında
  proses öldürme ayrıca test edilir. Mevcut constraint-failure rollback testleri korunur.
- Eski inferred edge/occurrence'ın yalnız başarılı replacement'ta temizlenmesi.
  Bu storage inference lifecycle testidir; çalışan framework analyzer'ı iddiası yok.
- Boş DB/kesilmiş ilk publication'dan sonra yeniden index. Gerçek hash file/byte
  sayısı; eksik-dosya marker'larını saymama; symlink okuma hatasında unknown,
  kısmi maliyet ve require-fresh reddi. Geçersiz yeni seçenekler DB açılmadan reddedilir.

## Ara doğrulamalar ve çözülen sorunlar

- İlk Faz 6 odaklı grup **9/9** (4 dk 38 sn), ilk tam koşu **79 integration +
  10 unit = 89/89** (integration 10 dk 1 sn) geçti.
- Örtük MSBuild import konumlarının fingerprint'e eklenmesinden sonra bir ara
  tam koşuda **20/80 integration başarısız**, **11/11 unit başarılı** oldu (exit 1).
  Locator'ın süreç environment'ını değiştirmesi ikinci evaluation'ı farklı
  gösteriyordu. Fail-closed yayın kontrolü önceki snapshot'ı korudu. Alt süreçlerin
  başlangıç environment'ı sabitlendi; karşılaştırma/test assertion'ı gevşetilmedi.
  Düzeltme sonrasındaki odaklı **2/2**, gerçek full/no-op/read smoke ve son tam koşu
  yukarıda ayrı kaydediliyor.
- İlk iki sandbox binary/shell-wrapper smoke biçiminde MSBuild BuildHost timeout
  (exit 5) görüldü. Doğrudan yukarıdaki `dotnet run` çağrıları çalıştı; timeout'lar
  başarılı kabul sayılmadı. Başarısız index'ler snapshot yayınlamadı.

## Sınırlar / NOT RUN

- Windows/Linux build/test/CLI: **NOT RUN**; bu kabul macOS ARM64 kapsamındadır.
- Gerçek ağ kesilmiş offline çalışma, kurulu tool package kabulü, Release JIT ve
  büyük solution bellek/performans bütçesi: **NOT RUN**; Faz 11 yerine sayılmaz.
- Ağ dosya sistemi, farklı hardlink isimlerinden aynı DB'ye yazma, disk dolması,
  fiziksel güç kesilmesi: **NOT RUN**. Process kill ve cancellation gerçekten denendi.
- Kaynak dosya sistemi atomik snapshot sağlamaz; kontrol sonrası kaynak değişebilir.
  Stabil input gerekir. Gözlenen concurrent değişiklik yayın öncesi reddedilir.
- Seçici değişen-project reuse yoktur; bütün seçili kapsam invalidation'ı bilerek
  kullanılır. Custom build/generator dependency'leri kanıtlanamıyorsa fresh/no-op
  verilmez. Gelecek framework analyzer'ları uygulanmadı; inference kayıt ömrü test edildi.
- Git metadata yok; başlangıç `git status --short` exit 128. Git diff kontrolü
  **NOT RUN**; kaynak/belge kontrolleri kullanılıyor. Commit/push/publish yapılmadı.

## Değişen dosyalar

- Core: yeni `IndexFingerprint.cs`; `GraphSnapshot.cs`, `DeliveryWriter.cs`.
- Roslyn: yeni `IncrementalIndexer.cs`; `ProjectPreparation.cs`, `ProjectIndexer.cs`.
- Storage.Sqlite: `GraphStore.cs`, `GraphQueries.cs`, `GraphDelivery.cs`.
- CLI/sürüm: `src/UO.Graphs.Cli/Program.cs`, `Directory.Build.props`.
- Test: yeni `tests/UO.Graphs.IntegrationTests/Phase6Tests.cs`,
  `tests/UO.Graphs.UnitTests/FingerprintTests.cs`.
- Belge: yeni `docs/INCREMENTAL.md`, `docs/history/PHASE5.md`;
  `README.md`, `docs/PRODUCT.md`, `docs/ARCHITECTURE.md`, `docs/QUERIES.md`,
  `docs/CONTEXT-EXPORT.md`, `docs/STATUS.md`.

Önerilen commit: `feat: add conservative incremental indexing and atomic snapshots`
