# Gerçekleşen durum

Son güncelleme: 2026-10-09.

**Faz 5, aşağıdaki macOS ARM64 context/export kabul kapsamında tamamlandı.**
Faz 6–11 başlamadı. [Faz 4 kanıt kaydı](PHASE4.md) arşivlendi.
Önceki workspace, kimlik, trust/SDK/assets, semantik ilişki ve graph query
sözleşmeleri korunuyor. AGENTS.md değiştirilmedi.

## Uygulananlar

- `uographs context <symbol-id-or-text> --max-chars <n> --format json`.
  Önce exact ID/ad; sonra en fazla 10000 entity üzerinde literal lexical arama.
  Lexical aday tek olsa da seçilmez; aday seçimi exit 7, bulunamayan sembol exit 8.
- Seçilen sembol, yakın ilişkiler, caller/callee özeti, dosya:satır/sütun/hash
  konumları, inferred/unresolved sayımları ve uyarılar, sonraki sorgu önerileri.
  Inferred/exact ayrımı ve unresolved null hedefleri korunur.
- Depth/node/ilişki/çalışma/occurrence ve karakter bütçeleri. Bütün JSON stdout,
  escape karakterleri, sayacın kendisi ve son LF Unicode scalar value olarak
  ölçülür. Token sayısı iddiası yoktur. Kayıtlar bütün olarak budanır; geçerli JSON,
  `truncated`, nedenler ve seçilen sembol korunur. Minimum envelope'a yetmezse
  gerekli boyutu bildiren stderr, exit 2 ve boş stdout.
- Kaynak metni varsayılan kapalı. `--include-source`: en fazla 3 C# snippet,
  her biri 12 satır/1200 karakter; dosya en fazla 1 MiB. Secret/config/generated,
  root dışı ve symlink yolları dışlanır. Decoded kaynak hash'i snapshot'a uymuyorsa
  metin verilmez; missing/exclusion/mismatch durumları görünürdür. Kaynak yorumları
  ve repository kaynaklı veriler açıkça güvenilmeyen veri olarak etiketlenir.
- `uographs export --format json|ndjson|mermaid|dot`; `--symbol <id-or-exact-name>`
  ile seçili altgraph ve `--depth` sınırı. Full graph export da açık node/edge/
  occurrence limitlerine tabidir; kesilme saklanmaz.
- Dört formatta kimlik, kanıt, confidence, analysis context ve eksikler korunur.
  Mermaid/DOT'ta orijinal veriler JSON yorumları içinde yer alır. Deterministik
  alias/ID sırası, label escaping, Unicode; unresolved için sahte hedef yoktur.
- Context/export format v1, ürün 0.5.0. Query/index JSON v4 ve SQLite schema 3
  değişmedi. Yeni paket/katman/LLM/embedding/uzak servis/renderer yok. Context
  bütün graph'ı yüklemez; bounded SQL + tek read-only transaction kullanır.

Tam komut, bütçe, kaynak ve format sözleşmesi: [CONTEXT-EXPORT](../CONTEXT-EXPORT.md).
Önceki sorgular: [QUERIES](../QUERIES.md); semantik kapsam: [RELATIONSHIPS](../RELATIONSHIPS.md).

## Gerçek doğrulama

Ortam: macOS 27.0 ARM64 / osx-arm64, SDK **10.0.201**, MSBuild **18.3.0**,
runtime **10.0.5**, Roslyn assembly **5.0.0.0**.

| Gerçek komut/kontrol | Sonuç |
| --- | --- |
| `dotnet --info` | PASS, exit 0; yukarıdaki kurulu sürümler. |
| `dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, son kaynaklarla exit 0; **0 warning/error**, 5.30 saniye. |
| `dotnet test UO.Graphs.slnx --no-build --no-restore -m:1 -nr:false` | PASS, son kaynaklarla exit 0; **69 integration + 10 unit = 79/79**, skipped 0; integration 2 dk 5 sn, unit 819 ms. |
| `python3 scripts/audit_licenses.py --check` | PASS, exit 0; 50 kilitli paket, MIT/Apache-2.0/SQLite public domain. Yeni paket yok. |
| Kaynak whitespace, XML/JSON/Python syntax ve Markdown dosya bağlantıları | PASS, exit 0; 96 kaynak/config/belge dosyası, güncel belgeler ve yeni arşiv bağlantıları. |

Gerçek index/smoke komutları:

```sh
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --trust-project --allow-generators --db .uographs/phase5-smoke.db
python3 scripts/phase5_smoke.py --db .uographs/phase5-smoke.db
```

İkisi de **PASS / exit 0**. Roslyn → SQLite snapshot:
`20bc3d8ec952431a9834fc705d0cf993`; **15 entity, 20 ilişki, 23 occurrence**,
completeWithinDeclaredScope. Script ayrı gerçek CLI proseslerinde **17 çağrı**
yaptı; runtime restore/index/download yapmadı. Exit 2/7/8 negatif akışları beklenen
kodları verdi; diğerleri exit 0. Sorgu öncesi/sonrası DB SHA-256 aynı.

## Ölçülen context azalması

Aynı görev: **Greet'i kim çağırıyor; statik caller ve kaynak konumu nedir?**
Tam JSON export ve context aynı snapshot'tan okundu. Tam export'ta
`truncated: false`; node/ilişki/occurrence sayıları status ile karşılaştırıldı.
Her iki çıktıda aynı **Welcome → Greet CALLS**, occurrence ID, dosya/satır/hash ve
gerekçe bulundu. Bu gerçek ürün CLI çalışmasıdır; graph HelloGraph örneğidir.

| Ölçü | Tam graph JSON export | Context (`--max-chars 12000`) |
| --- | --- | --- |
| Bütün stdout karakteri, son LF dahil | **61.420** | **9.547** |
| UTF-8 byte | **61.420** | **9.547** |
| Aynı görev kanıtı | Var | Var |

Gerçek karakter azalması: `100 × (1 − 9547/61420)` = **%84,46**.
Bu oran yalnızca bu snapshot/görev/içerik için ölçüldü; büyük repository veya token
azalması iddiası değildir. JSON escaping nedeniyle bu çıktılarda byte/karakter eşit.

Ek CLI bütçe kontrolü: `--max-chars 6500` → **5531** karakter, geçerli JSON,
`truncated: true` ve `maxChars`. `--max-chars 1` → **exit 2**, boş stdout,
minimum envelope açıklaması. Default snippet yok; `--include-source` ile gerçek
Greet dosyası hash-match ve untrusted etiketiyle **10038** karakterlik çıktı verdi.

Ölçüm ve çıktılar yerel `.uographs/phase5-evidence/` içinde:
`measurement.json`, `full-graph.json`, `context.json`, dört `greet-subgraph.*`.
Smoke script bunları yeniden üretir; `.uographs` Git ignore kapsamındadır.

## Kabul testlerinin kapsamı

Yeni **20** integration vaka (7 Fact + 13 negatif seçenek Theory):

- 1/2000/3500/5000/8000/12000/50000 bütçeler, minimum envelope hatası, bütün stdout
  sayacının eşitliği, exact bütçe sınırı, valid JSON, selected/completeness korunması.
- Exact ID/ad önceliği; overload ambiguity ve limit 1; tek/çok lexical adayda
  otomatik seçim yapılmaması, bulunamayan sembol, sorgu önerisi.
- Cycle/diamond, node/ilişki/depth/çalışma/occurrence sınırları; inferred ve
  unresolved gerekçe/null target; caller/uncertainty özetleri; tam export sayıları.
- JSON/NDJSON ve Mermaid/DOT JSON yorumlarının parse edilmesi, seçilen düğüm/kanıt
  kümeleri; tırnak, slash, newline, kontrol karakteri, Türkçe/CJK/emoji escaping.
- Dört formatta gerçek ayrı CLI proseslerinde byte-stable tekrar, DB byte koruması;
  ters record insertion sırasıyla aynı export; dört formatta truncation metadata'sı.
- Varsayılan context/dört export formatında kaynak marker'ının bulunmaması;
  yalnızca açık include-source ile kaynak/yorum verisi ve untrusted etiketi.
- Gerçek dosya düzenlemesi sonrası CLI `snapshotMismatch` ve kaynak metninin
  kaldırılması; missing, secret/config/gizli/bin, outside-root, generated, root
  unavailable, symlink ve büyük dosya dışlaması. 1200 emoji sınırında surrogate
  pair bölünmemesi. Normal hash-match snippet ve stale: notChecked ayrımı.
- 10000 entity lexical scan sınırı; unrelated corrupt satırı deserialize etmeme,
  seçili bozuk graph'ta exit 6; eski schema için exit 4 ve migration yapılmaması.
- Geçersiz format/seçenek/sınırların DB açılmadan reddi. Önceki **59** test korunur.

Ara koşu: ilk yeni 18 vaka odaklı test komutuyla **18/18** geçti; son iki vaka ve
bütçe budamasındaki toplu kayıt çıkarma iyileştirmesinden sonra yukarıdaki tam
**79/79** koşusu yapıldı. Başarısız build/test/smoke yok; assertion gevşetilmedi.
İlk statik kontrol mevcut `packages.lock.json` dosyalarında final newline olmamasına
takıldı. Lock dosyaları değiştirilmedi; JSON parse doğrulaması korundu, newline/
whitespace kontrolü kaynak/config/belge dosyalarına uygulandı.

## Sınırlar / NOT RUN

- Windows/Linux build/test/CLI: **NOT RUN**; bu kabul macOS ARM64 kapsamındadır.
- Ağ gerçekten kesilmiş offline kabul, kurulu tool paket kabulü, Release JIT,
  büyük solution süre/bellek ölçümü: **NOT RUN**, Faz 11 yerine sayılmaz.
- Harici Mermaid/DOT renderer/browser üzerinde görsel kabul: **NOT RUN**.
  Üretim komutları renderer gerektirmez; escaping/Unicode/parse/sıra testleri geçti.
- Hash kontrolü yalnızca opt-in snippet dosyasının okunan decoded içeriğini doğrular.
  Genel snapshot freshness/incremental indexing yok; `stale: notChecked` korunur.
- Dosya gizlilik politikası izinli normal C# dosyasındaki keyfi hardcoded secret'ı
  tespit eden içerik tarayıcı değildir. Graph metadata'sı da hassas olabilir.
  Kullanıcının başka ajana aktarımı ürünün yerel çalışma sınırından ayrıdır.
- Tam export da sınırlıdır; `truncated` kontrol edilmelidir. NDJSON sınırsız
  streaming/bellek garantisi değildir. Context ham compiler diagnostic'lerini
  sayıya indirger; ayrıntıları status/export'ta kalır.
- Git metadata yok; başlangıç `git status --short` exit 128. Git diff kontrolü
  **NOT RUN**; kaynak/belge kontrolleri yapıldı. Commit/push/publish yapılmadı.

## Değişen dosyalar

- Yeni Core: `src/UO.Graphs.Core/GraphDelivery.cs`, `DeliveryWriter.cs`, `SourceSnippets.cs`.
- Yeni Storage: `src/UO.Graphs.Storage.Sqlite/GraphDelivery.cs`.
- CLI: `src/UO.Graphs.Cli/Program.cs` (iki komut, seçenek doğrulama, help/stdout).
- Sürüm: `Directory.Build.props`, `src/UO.Graphs.Roslyn/ProjectIndexer.cs` (0.5.0).
- Test: yeni `tests/UO.Graphs.IntegrationTests/Phase5Tests.cs`.
- Smoke: yeni `scripts/phase5_smoke.py`.
- Belgeler: yeni `docs/CONTEXT-EXPORT.md`, `docs/history/PHASE4.md`;
  `README.md`, `docs/PRODUCT.md`, `docs/ARCHITECTURE.md`, `docs/STATUS.md`.

Önerilen commit: `feat: add bounded AI context and deterministic graph exports`
