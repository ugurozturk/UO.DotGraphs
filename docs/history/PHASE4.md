# Gerçekleşen durum

Son güncelleme: 2026-10-09.

**Faz 4, aşağıdaki macOS ARM64 graph sorgusu kabul kapsamında tamamlandı.**
Faz 5–11 başlamadı. [Faz 3 kanıt kaydı](PHASE3.md) arşivlendi.
Önceki workspace, kimlik, güven/SDK/assets ve semantik ilişki sınırları korunuyor.

## Uygulananlar

- Kalıcı graph'tan `query find`, `symbol`, `callers`, `callees`, `dependencies`,
  `path`, `impact`. Sorgular MSBuild yüklemez, runtime restore/download yapmaz.
- Tam ID önceliği, tam ad/nitelikli ad çözümü; belirsizlikte aday JSON'u ve exit 7,
  kesin ID seçme isteği. Limit 1 ikinci adayın sessizce seçilmesine yol açmaz.
  Bulunamayan sembol exit 8 ve JSON tanı döndürür.
- Açık yön, edge türleri, depth, result limit, inferred dahil etme, edge çalışma
  bütçesi ve occurrence kanıt limiti. Parametreli SQL; kullanıcı girdisinden
  shell komutu oluşturma/çalıştırma yok.
- Deterministik BFS, cycle/self-call/diamond desteği; path tek bounded shortest
  path. Her traversal/impact düğümünün orijinal ilişki ve kaynak kanıt yolu var.
- Impact yalnızca gelen CALLS/USES_TYPE/INHERITS/IMPLEMENTS/OVERRIDES/
  PROJECT_REFERENCES alt kümesinde yürür. REFERENCES/DECLARES yayılmaz.
  Potential static impact tanısı; runtime garanti iddiası yok.
- JSON v4 envelope: schemaVersion, snapshotId, analysisContext, completeness,
  stale, truncated, items, diagnostics; etkin options, limits/reasons ve outcome.
  `stale: notChecked` freshness doğrulaması değildir. Kısmi analiz, boş sonuç ve
  sorgu/kanıt bütçesi kesilmesi ayrı korunur.
- Ürün 0.4.0, SQLite schema 3: endpoint node/relationship/occurrence FK'leri,
  source/target adjacency ve occurrence indeksleri, tek read-only transaction.
  Komşuluk/kanıt sorguları parametreli ve sınırlı; bütün graph belleğe yüklenmez.
- Eski schema 1/2 status okuması sürer. Yeni query açık yeniden index ister;
  read akışında migration yok. Başarılı replacement atomik schema 3 yükseltir;
  başarısız yazma önceki veri ve schema'yı korur.
- Yeni paket yok. Mevcut 50 doğrudan/geçişli paketin lisans denetimi başarılı.

Tam komut/envelope ve sınır sözleşmesi: [QUERIES](../QUERIES.md).
Semantik analiz sınırları: [RELATIONSHIPS](../RELATIONSHIPS.md).

## Gerçek doğrulama

Ortam: macOS 27.0 ARM64 / osx-arm64, SDK 10.0.201, MSBuild 18.3.0,
runtime 10.0.5; Roslyn assembly 5.0.0.0.

| Gerçek komut/kontrol | Sonuç |
| --- | --- |
| `dotnet --info` | PASS, exit 0; yukarıdaki kurulu sürümler. |
| `dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, son kaynaklarla exit 0; 0 warning/error, 4.35 saniye. |
| `dotnet test UO.Graphs.slnx --no-build --no-restore -m:1 -nr:false` | PASS, son kaynaklarla exit 0; **49 integration + 10 unit = 59/59**, skipped 0; integration 1 dk 45 sn, unit 741 ms. |
| `python3 scripts/audit_licenses.py --check` | PASS, exit 0; 50 kilitli paket, MIT/Apache-2.0/SQLite public domain. |
| Python whitespace/XML/JSON/Python syntax ve belge bağlantıları | PASS, exit 0; 50 kaynak/config/document dosyası. |

Gerçek index smoke:

```sh
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --trust-project --allow-generators --db .uographs/phase4-smoke.db
python3 scripts/phase4_smoke.py --db .uographs/phase4-smoke.db
```

İkisi de **PASS / exit 0**. Gerçek Roslyn index: **15 entity, 20 ilişki,
23 occurrence**, completeWithinDeclaredScope, JSON v4 / SQLite schema 3.
Snapshot: `6b3ff3606a944e5ca0df7cb1217f0f85`.

Smoke script shell kullanmadan argument list ile 13 ayrı gerçek CLI çağrısı yaptı:
status, iki find, symbol, callers, callees, dependencies, iki aynı path, impact,
ters path, depth 0 path ve bulunamayan symbol. Son negatif symbol beklenen exit 8;
diğer çağrılar exit 0. Her stdout Python `json.loads` ile parse edildi.
`Welcome → Greet` tek adımlı CALLS yolu ve `new Greeter().Greet(name)` kaynak
ifadesi doğrulandı; impact/callers yalnızca Welcome döndürdü. Tekrar path sonucu
aynı, sorgular sonrası DB SHA-256 aynı. Script index/restore yapmaz.

## Kabul testlerinin kapsamı

- Bilinen graph'ta tam callers/callees/impact kümeleri; outgoing/incoming/both,
  self-call, cycle, diamond tekil sonuç ve kaynak occurrence karşılaştırmaları.
- En kısa yol, aynı düğüme sıfır adım, ulaşılamayan hedef, depth 0/1, edge bütçesi,
  deterministik tekrar; açıklama yollarında yön/orijinal ilişki/kanıt tutarlılığı.
- 250 komşulu graph'ta result limit ve tam limite eşit sonuç; limit+1 probe.
  Occurrence kesilmesinde step ve envelope üzerinde evidenceLimit korunması.
- Inferred opt-in, unresolved adayın geçilmemesi, metadata endpoint'in entity
  gibi uydurulmaması; impact REFERENCES/DECLARES'in dışlanması.
- Ambiguous ad/qualified name/tam ID, limit 1 belirsizliği, path'in her iki
  argümanının çözümü; eksik sembol ve geçersiz seçenek/edge/yön/limitler.
- Gerçek ayrı CLI proseslerinde yedi komutun JSON parse'ı; kısmi analysis tanı ve
  kapsamının korunması, byte-stable tekrar, literal SQL/shell girdileri.
- Bozuk SQLite dosyası/JSON, read-only byte kontrolü, ilişkisiz graph satırlarının
  deserialize edilmemesi; sorgular global integrity scan olarak sunulmaz.
- Node/entity ve relationship endpoint FK'leri, occurrence FK rollback,
  EXPLAIN QUERY PLAN adjacency index kullanımı; v1/v2 yükseltme/rollback.
- Gerçek HelloGraph Roslyn → SQLite → query akışında cross-project callers,
  callees/path/impact ve kaynak ifadesi. Faz 3 kanıt round-trip testi yeni
  traversal envelope'a taşındı; ilişki/occurrence assertion'ları korundu.
- Önceki workspace/trust/identity/semantic testler korunur; test silinmedi.

## Ara koşular

- Yeni testte collection size için `Assert.Equal(1, ...)` xUnit2013 nedeniyle
  build'i durdurdu. `Assert.Single` ile düzeltildi; assertion gevşetilmedi.
- Bu başarısız build sonrasında eski binary ile başlayan test koşusu iptal edildi;
  kabul kanıtı sayılmadı. Başarılı build sonrasındaki tam koşu 56/56 geçti;
  ek testler sonrası son koşu 59/59 geçti.

## Sınırlar / NOT RUN

- Windows/Linux build/test/CLI: **NOT RUN**; bu oturum macOS ARM64 ile sınırlı.
- Ağ gerçekten kesilmiş offline çalışma, kurulu tool paket kabulü, Release JIT ve
  büyük solution performans/memory ölçümleri: **NOT RUN**, Faz 11 yerine sayılmaz.
- Source freshness kontrolü/incremental indexing yapılmaz (`notChecked`).
  Runtime dispatch, DI/framework çıkarımları, bütün yolları bulma veya bütün
  ilişkiler export'u bu fazın özelliği değildir.
- Query JSON v3 tüketicileri v4 `items`/`path` yapısına uyarlanmalıdır; eski SQLite
  schema 1/2 üzerinde yeni query komutları için açık yeniden index gerekir.
- Git metadata yok; başlangıç `git status --short` exit 128. Git diff kontrolü
  **NOT RUN**; kaynak whitespace/config/belge kontrolleri yapıldı.
  Commit/push/publish yapılmadı. AGENTS.md değiştirilmedi.

## Değişen dosyalar

- Core: yeni `src/UO.Graphs.Core/GraphQuery.cs`; `GraphSnapshot.cs` JSON sürümü.
- Storage: yeni `src/UO.Graphs.Storage.Sqlite/GraphQueries.cs`; `GraphStore.cs`
  schema/index/FK ve eski schema status sürüm raporu.
- CLI: `src/UO.Graphs.Cli/Program.cs` komutlar, seçenekler, envelope ve exit'ler.
- Sürüm: `Directory.Build.props`, `src/UO.Graphs.Roslyn/ProjectIndexer.cs`.
- Test: yeni `tests/UO.Graphs.IntegrationTests/QueryTests.cs`; `CliTests.cs`,
  `Phase3Tests.cs`, `StorageTests.cs` v4/schema 3 sözleşme uyarlamaları.
- Smoke: yeni `scripts/phase4_smoke.py`.
- Belgeler: yeni `docs/QUERIES.md`, `docs/history/PHASE3.md`; `README.md`,
  `docs/ARCHITECTURE.md`, `docs/STATUS.md`.

Önerilen commit: `feat: implement bounded persistent graph queries with evidence paths`
