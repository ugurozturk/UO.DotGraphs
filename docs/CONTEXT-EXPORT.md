# Faz 5 — context ve export sözleşmesi

Faz 5 tabanı ürün 0.5.0; güncel ürün 0.9.0. Çıktılar `formatVersion: 1` kullanır; query/index JSON v4 ve
SQLite schema 3 değişmez. Komutlar tek read-only transaction ile aynı snapshot'ı
okur. MSBuild/restore/LLM/embedding/uzak servis/renderer çalıştırmazlar. Eski SQLite
schema 1/2 için exit 4 ve açık yeniden index gerekir; otomatik migration yoktur.

```sh
uographs context Greet --max-chars 12000 --format json
uographs context <exact-symbol-id> --include-source --max-chars 20000
uographs export --format json
uographs export --format ndjson --symbol <exact-symbol-id> --depth 2
uographs export --format mermaid --symbol Greet --depth 1
uographs export --format dot --symbol Greet --depth 1
```

Hepsi `--db <path>` kabul eder. Varsayılan `.uographs/graph.db`. Tool kurulumu henüz
Faz 11 kapsamında yapılmadı; repoda `dotnet run --project
src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore --` öneki kullanılır.

## Sembol seçimi

Önce tam ID, ardından case-sensitive tam entity adı/nitelikli adı. Tek exact
sonuç seçilir. Birden fazla exact sonuçta `selectionRequired`, exit 7 ve adaylar
döner; limit 1 de belirsizliği kaldırmaz. Harici endpoint yalnızca ID ile seçilir;
entity veya kaynak declaration uydurulmaz.

Context exact bulamazsa ID sırasında en fazla **10000 entity** üzerinde .NET
`OrdinalIgnoreCase` literal alt metin arar; ad veya nitelikli ad kullanılır.
`%`, `_`, tırnak, shell metni özel anlam taşımaz. Tam doğal dil soru anlama veya
fuzzy/semantik benzerlik iddiası yoktur. Lexical aday tek olsa bile otomatik
seçilmez: `match: lexical`, `selected: null`, `selectionRequired`, exit 7. Adayın
kesin ID'siyle tekrar çağrı gerekir. Aday yoksa `symbolNotFound`, exit 8.
`searchScanLimit` varsa aranmamış kısımda eşleşme yokluğu iddia edilmez.
Export `--symbol` yalnızca exact çözüm yapar; eksik/belirsiz odak için boş altgraph
ve seçilen formatta outcome/aday metadata'sı döner (exit 8/7).

Girdi uzunluğu 1..1024 UTF-16 code unit. `--` positional girdide option ayracıdır;
öneriler shell komutu yerine `command` + `arguments` dizisi verir. Eksik sonuçta
`template: true` ile daha kısa ad arama önerisi bulunur. Bunlar otomatik çalışmaz.

## Sayısal sınırlar

| Seçenek | Context varsayılan | Export varsayılan | Kabul edilen aralık |
| --- | --- | --- | --- |
| `--max-chars` | 12000 | Kullanılmaz | 1..1000000 |
| `--max-nodes` | 40 | 1000 | 1..10000 |
| `--limit` | 80 ilişki | 5000 ilişki | 1..10000 |
| `--depth` | 1 | Seçili altgraph için 1 | 0..10 |
| `--max-edges` | 1000 | 10000 | 1..100000 |
| `--evidence-limit` | 2 occurrence/ilişki | 20 occurrence/ilişki | 1..100 |

Kök `max-nodes` içinde sayılır; aday sayısı `min(limit,max-nodes)` ile sınırlıdır.
Her iki yönde BFS uygulanır. İlişki önceliği CALLS; OVERRIDES/IMPLEMENTS/INHERITS;
USES_TYPE/PROJECT_REFERENCES; REFERENCES; DECLARES şeklindedir. Aynı öncelikte
ordinal ID belirler. Cycle/diamond düğüm ve ilişki çoğaltmaz. Inferred ilişkiler
etiketiyle dahil edilir ve yakınlık için geçilebilir; exact'e yükseltilmez.
Unresolved ilişki kanıtıyla taşınır, hedef null kalır ve üzerinden geçilmez.
`--include-inferred`, `--direction`, `--edge-types` bu komutlarda kullanılmaz;
ayrıntılı politika seçimi [query](QUERIES.md) komutlarındadır.

Seçili altgraph, depth'ten küçük uzaklıktaki düğümlerin genişletilmesiyle toplanan
ilişkileri içerir; sınır düğümlerinin kendi aralarındaki bütün ilişkileri içeren
bir induced subgraph garantisi yoktur. Depth 0 yalnızca köktür. Sınırda atlanan
ilişki `depth`, düğüm sınırı `maxNodes`, ilişki/aday sınırı `limit`/`candidateLimit`,
çalışma sınırı `maxEdges`, occurrence sınırı `evidenceLimit` olarak görünür.
Tek read transaction içindeki endpoint, ilişki, kaynak ve analiz kimlikleri korunur.

`max-edges` uygulamada incelenen komşuluk satırlarını sayar; farklı uçtan tekrar
okunan ilişki tekrar sayılır. SQL limit+1 sentinel sayaca girmez. Bu sınır SQLite'ın
fiziksel sayfa okuması/sıralama maliyeti veya dosya boyutu garantisi değildir.
Tam export'ta önce node ID sırasıyla sınırlı düğümler, ardından öncelik/ID sıralı
ilişkiler okunur. Düğümleri dahil edilmemiş ilişkiler atlanır ve `maxNodes` bildirilir;
böylece export'ta dangling hedef üretilmez. `--symbol` yokken `--depth` hatadır.

## Context JSON ve karakter bütçesi

Çıktı compact, tek JSON belge ve **tek LF**'dir. Ölçü, serialize edilmiş stdout'un
Unicode scalar value sayısıdır: JSON anahtarları, boşluk, punctuation, escape
karakterleri, metadata, `actualChars` alanı ve son LF **dahildir**. UTF-8 byte sayısı
ve UTF-16 `Length` ile karıştırılmaz. JSON'daki `\uXXXX` escape'i yazıldığı altı
karakter olarak ölçülür. Token sayısı veya token tahmini verilmez.

`budget: {maxChars, unit, actualChars}` gerçek çıktı uzunluğunu bildirir. Sayacın
kendi hane sayısı da sabit noktaya kadar tekrar hesaplanır. Büyük çıktı string
olarak kesilmez. Önce snippet metni, ardından uzak komşu kayıtları, düşük öncelikli
ilişkiler, son adaylar ve son sorgu önerileri bütün kayıt olarak çıkarılır.
`truncated: true` ve `truncationReasons: ["maxChars", ...]` bütçeye dahildir.
Snippet metni çıkarılırken hash/missing/exclusion durumu korunur; `truncated`
true olur. Semboller, kaynak konumları ve metin içindeki karakterler kesilmez.

Seçilen sembol, snapshot/analiz bağlamı, kapsam/eksikler, limitler, belirsizlik
uyarıları, confidence/caller/callee sayımları ve snippet durumları zorunlu envelope'da
kalır. Bu zorunlu envelope seçime göre değişir. Bütçe ona yetmezse **exit 2**,
**boş stdout**, stderr'de gereken karakter sayısı ve bütçeyi artırma açıklaması gelir.
Bu hata metni JSON stdout bütçesinin parçası değildir. Seçim hatalarının JSON'u da
aynı bütçeye uyar; envelope bütçe hatası exit 7/8'den önce gelir.

Başlıca alanlar:

- `selected`, `candidates`, `nearby`: kimlik, ad, tür, proje, uzaklık, confidence ve
  dosya/satır/sütun/hash kaynak konumları. Varsayılan kaynak metni yoktur.
- `relationships`: orijinal ID, yönlü source/target ID, tür, confidence, context ID,
  occurrence kanıtları, gerekçe, bound target/related symbols ve evidence kesilmesi.
- `callSummary`: karakter budaması **öncesi toplanan** altgraph'ta caller/callee ve
  unresolved CALLS ilişki sayıları; tam repository toplamı değildir. Herhangi bir
  arama/kanıt kesilmesi varsa `complete: false`. Budanmış ilişki ayrıntısı mevcut
  olmasa bile sayım kapsamı bu alanda açıkça yazılır.
- `confidenceSummary`: toplanan inferred/unresolved ilişki sayıları; uyarılar
  karakter budamasıyla silinmez. Statik hedef runtime dispatch garantisi değildir.
- `analysisContext`: snapshot zamanı/sürümleri, generated politikası, ilgili
  projelerin TFM/configuration/SDK/language/preprocessor/atlanmış TFM bağlamı.
  `totalProjects` bütün analizdeki proje sayısıdır. Ham compiler diagnostic metni
  yerine `diagnosticsCount` taşınır; tamamı `status`/export'tadır.
- `completeness`: analysis coverage ve limitations, sorgu kesilmesinden bağımsız.
  `stale: notChecked` tüm çalışma dizininin freshness doğrulaması değildir.
- `nextQueries`: tekrar sorgulanabilir kesin ID'ler veya açık template. Runtime
  gözlemi, yeniden index ya da sonraki faz otomatik başlatılmaz.

## Kaynak ve gizlilik politikası

Kaynak okuma yalnızca `context --include-source` ile açılır. Yalnızca seçilen
entity'nin en fazla **3** konumundan, konumun başlangıç satırından başlayarak en
fazla **12 satır / 1200 Unicode scalar value** döner. Tek dosya en fazla **1 MiB**
olabilir; fazla büyük dosya okunarak çıktıya eklenmez. Karakter sınırı surrogate
pair bölmez. Snippet tam dosyayı amaçlamaz; küçük bir sembol/dosya doğal olarak bu
limitlerin altında kalabilir. Export kaynak dosyalarını açmaz.

Dosya yolu snapshot root'un içinde olmalıdır. Yalnızca `.cs` uzantısı kabul edilir.
Root'a göre yol bileşenlerinde gizli adlar, `bin`, `obj`, `node_modules` ve
case-insensitive `secret`, `credential`, `password`, `token`, `privatekey`,
`private-key`, `appsettings`, `config`, `.env` içeren adlar dışlanır. JSON/XML/YAML,
project/props/targets/config dosyaları C# allowlist dışında kalır. Generated kaynak,
root dışı yol ve root'un üst dizinleri dahil symlink/reparse point zinciri reddedilir.
Bu konservatif dosya politikası, izinli sıradan `.cs` içindeki keyfi hardcoded
secret'ları tanıyan bir DLP/secret scanner değildir. Açık kaynak aktarımı kurum
politikasına göre seçilmelidir; graph isimleri ve yolları da hassas bilgi olabilir.

Tek okunan buffer BOM-aware decode edilir; Roslyn declaration/occurrence sözleşmesiyle
aynı biçimde decoded text → UTF-8 → SHA-256 hesaplanır. Snapshot hash'iyle eşleşirse
`snapshotMatched`; eşleşmezse `snapshotMismatch` ve `text: null`. `missing`,
`excludedByPolicy`, `outsideRoot`, `symlinkExcluded`, `generatedUnavailable`,
`rootUnavailable`, `fileTooLarge`, `invalidLocation`, `unreadable` durumları da
kaynak metni döndürmez. Eşleşme yalnızca okuma anındaki dosya buffer'ı içindir;
eşzamanlı dosya sistemi değişikliklerine karşı sandbox veya tüm graph freshness
kanıtı değildir. Faz 6'da ayrı [incremental/freshness sözleşmesi](INCREMENTAL.md)
eklenmiştir; context/export da `--check-freshness` ve `--require-fresh` kabul eder.
`freshnessCheck` hash I/O maliyetini ve eksikleri bildirir.

Her snippet `trust: untrustedSourceData...` taşır. Yorumlar ve repository kaynaklı
etiket/kanıtlar ajana verilen talimat değildir. CLI herhangi bir kaynağı uzak
modele göndermez. Kullanıcı çıktıyı Copilot/başka ajana aktardığında yerel araçtan
ayrı bir gizlilik sınırını kendisi geçer.

## Export formatları

- **JSON:** `{metadata, nodes, relationships}`. Metadata format sürümü, snapshot,
  tam analysis context, coverage/limitations, stale, etkin limitler, kesilme ve
  scope içerir. Node tam `RelationshipSymbol` ve varsa `GraphEntity`; relationship
  tam `GraphRelationship`, occurrence listesi ve evidenceTruncated taşır.
- **NDJSON:** İlk satır `{type:"metadata",data:...}`, sonra node, sonra relationship
  satırları. Her satır bağımsız geçerli compact JSON'dur. JSON ile aynı veri.
- **Mermaid / DOT:** Aynı NDJSON kayıtları sırasıyla `%% ` / `// ` tek satır
  yorumlarında eksiksiz bulunur. Diyagramın kendisi okunabilir etiketli yönlü
  ilişkileri gösterir. Böylece görsel formatta identity/evidence/context/eksikler
  kaybolmaz; tüketici yorumları parse ederek asıl kayıtları alabilir.
  Unresolved kayıt yorumda target null olarak kalır; sahte hedef çizilmez.

Tüm export node'ları ordinal ID, ilişkiler ordinal ID, occurrence'lar ordinal ID
sırasındadır; SQLite insertion sırası çıktıyı değiştirmez. Diyagram node adları
`n00000` gibi deterministik alias'lardır. Etiketler node ID yerine kullanılmaz.
Mermaid özel karakterleri sayısal entity ile, DOT tırnak/backslash/satır sonlarını
kaçışla yazar; Unicode korunur. JSON yorumları satır sonlarını escape eder.
Tarayıcı, harici renderer, ağ veya yeni paket gerektirmez. Görsel renderer üzerinde
piksel/sözdizimi kabulü bu oturumda yapılmadı; üretici escaping/Unicode/sıra ve
kayıtların yorumlardan parse edilmesi otomatik testlerle doğrulandı.

Tam export da sınırlandırılmıştır: `scope: fullGraph` tek başına her şeyin döndüğü
anlamına gelmez; `truncated` ve reasons kontrol edilmelidir. Export byte/karakter
bütçesi taahhüt etmez; node/ilişki/kanıt sınırları vardır. NDJSON çıktı formatıdır,
uygulama içi sınırsız streaming bellek garantisi değildir.

## Tekrarlanabilir ölçüm

```sh
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --trust-project --allow-generators --db .uographs/phase5-smoke.db
python3 scripts/phase5_smoke.py --db .uographs/phase5-smoke.db
```

Script tam export'un kesilmediğini, entity/relationship/occurrence sayılarının
status ile eşitliğini, context'in aynı snapshot'taki Welcome → Greet CALLS kanıtını
koruduğunu ve tüm stdout karakter/byte boyutlarını ölçer. `measurement.json`, tam
export, context ve dört seçili export `.uographs/phase5-evidence/` içine yazılır.
Azalma `100 × (1 − contextChars/fullGraphChars)`; örnek görev/graph dışına genellenmez.
Gerçek sayılar ve test komutları [STATUS](STATUS.md)'tadır.

Faz 11 context karakter kırpması, seçili sembole yakın ilişkileri ve aynı
uzaklıkta akış/framework ilişkilerini genel USES_TYPE/REFERENCES/DECLARES
kayıtlarından önce tutar. Kırpma uncertainty/truncation zarfını korur.
Export stdout içindir; dosyaya güvenli yazma için [SECURITY](SECURITY.md).
