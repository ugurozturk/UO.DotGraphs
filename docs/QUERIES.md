# Faz 4 sorgu sözleşmesi — JSON v4

Sorgular yalnızca yerel kalıcı snapshot'ı okur; MSBuild yüklemez, shell komutu
oluşturmaz, restore/download yapmaz. Varsayılan `.uographs/graph.db`;
`--db <path>` ve `--format json` bütün sorgularda kullanılabilir.

| Komut | Varsayılan anlam |
| --- | --- |
| `uographs query find <text>` | Entity adı/nitelikli adında literal alt metin; ID sırası |
| `uographs query symbol <symbol-id>` | Tek entity veya harici endpoint; declaration kaynakları |
| `uographs query callers <symbol-id>` | Gelen CALLS; statik compile-time caller'lar |
| `uographs query callees <symbol-id>` | Giden CALLS; statik compile-time hedefler |
| `uographs query dependencies <symbol-id>` | Giden bağımlılıklar |
| `uographs query path <from-id> <to-id>` | Seçili yönde bir bounded shortest path |
| `uographs query impact <symbol-id>` | Gelen bağımlılıklardan potansiyel etkilenen düğümler |

`find` dışındaki argümanlarda önce **tam ID**, ardından entity'nin **tam adı veya
nitelikli adı** aranır. Tek eşleşme kullanılabilir. İki veya daha fazla eşleşmede
exit **7**, `outcome: ambiguousSymbol`, `items` içinde adaylar ve kesin ID seçme
isteği gelir. `--limit 1` ikinci adayın sessizce seçilmesine yol açmaz. Aday listesi
kesilmişse `limits.reasons` bunu bildirir. Eşleşme yoksa exit **8** ve
`symbolNotFound` envelope gelir. Path'in ikinci argümanı için aynı kural geçerlidir.
Harici endpoint'ler tam ID ile seçilir, entity gibi taklit edilmez (`entity: null`).
`find` ASCII case folding kullanır; `%`, `_`, `'` literal değerlerdir. ID/ad
çözümü case-sensitive'dir. `--` sonrasında `-` ile başlayan positional metin
verilebilir. CLI kullanıcı girdisini shell'de çalıştırmaz; SQL değerleri parametrelidir.
Shell'in CLI'a ulaşmadan genişlettiği metin için kullanıcının kendi shell quoting
kuralları geçerlidir.

## Seçenekler ve sınırlar

| Seçenek | Varsayılan / sınır |
| --- | --- |
| `--direction outgoing\|incoming\|both` | callers/impact incoming; diğer traversal sorguları outgoing |
| `--edge-types CALLS,USES_TYPE,...` | callers/callees CALLS; dependencies/path/impact aşağıdaki bağımlılık kümesi |
| `--depth N` | callers/callees/dependencies 1; path/impact 5; 0..100 |
| `--limit N` | 100; 1..10000 sonuç/aday |
| `--include-inferred` | Kapalı; açıldığında inferred ilişkiler exact etiketine çevrilmez |
| `--max-edges N` | 10000; 1..1000000 incelenen komşuluk satırı |
| `--evidence-limit N` | Her ilişki için 20; 1..1000 occurrence |

`find` ve `symbol` yalnızca `--limit`, `--db`, `--format` kabul eder; traversal
seçenekleri hatadır. `symbol` için limit, belirsiz aday listesini sınırlar.
Path en fazla **bir** item üretir; `--limit` yol uzunluğu/arama bütçesi değildir,
argument aday listesini de sınırlar. Yol uzunluğu `--depth`, çalışma bütçesi
`--max-edges` ile kontrol edilir. Bir ilişki farklı uçlardan tekrar incelenirse
bütçede tekrar sayılır. SQL'in limit kontrolü için okuduğu bir sentinel satır
`examinedEdges` sayısına dahil değildir. Bu bütçe SQL engine'in bütün fiziksel
sayfa okumalarına ilişkin bir performans garantisi değildir.

Bağımlılık kümesi: **CALLS, USES_TYPE, INHERITS, IMPLEMENTS, OVERRIDES,
PROJECT_REFERENCES, HANDLED_BY, ACTIVATES, INJECTS, REGISTERS, IMPLEMENTED_BY,
USES_FACTORY, HANDLES, CONSUMES, DISPATCHES, PUBLISHES, POSSIBLE_TARGET,
BEHAVIOR_FOR, POSSIBLE_BEHAVIOR**. Faz 8 mesaj yönleri [MESSAGING](MESSAGING.md) içindedir. Faz 7 framework yönleri [FRAMEWORKS](FRAMEWORKS.md) içindedir. Genel traversal sorgularında DECLARES ve REFERENCES açık
seçimle kullanılabilir. `impact` yalnızca bu bağımlılık kümesinin alt kümelerini ve
incoming yönünü kabul eder; aksi exit 2'dir. `REFERENCES` salt sembol kullanımı,
`DECLARES` sahiplik bilgisidir; bunları gelişigüzel ters yürüyüp bütün solution'ı
etkilenmiş saymaz. Tür değişikliklerinden bütün üyelerine otomatik genişleme,
runtime dispatch uygulanmaz; inferred framework adayları yalnız `--include-inferred` ile yürünür. Bir method değişikliği için
çağıranları, bir type değişikliği için ona bağımlı declaration'ları gösterir.
Bu politika potansiyel statik etki kapsamıdır, bütün gerçek etkilerin garantisi değildir.

Unresolved ilişkilerin kesin hedefi olmadığından hiçbir traversal onları geçmez;
inferred bayrağı unresolved adayları bağlamaz. Analiz eksikleri ve kapsamı envelope'da
kalır. Symbol çıktısı declaration kanıtını; traversal çıktısı seçilen yolun ilişki
kanıtlarını verir. Aynı hedefe birden fazla ilişki/yol varsa her düğüm için yalnızca
bir deterministik en kısa açıklama yolu sunulur; bu bir bütün ilişkiler export'u değildir.

## Traversal ve deterministik sıra

Ağırlıksız breadth-first search, FIFO frontier, komşulukta ordinal ilişki ID sırası.
Düğümler keşif sırasıyla, bir kez döner; diamond'ın ortak hedefi çoğaltılmaz.
Kök visited kümesine baştan eklenir; cycle onu yeniden genişletmez. Callers/callees/
dependencies'de doğrudan self-edge kökü distance 1 ile bir kez gösterebilir;
impact kökü etkilenenler listesine eklemez. Derinlik 0 komşu döndürmez.
`path A A` distance 0, boş path ile bulunan yoldur.

Path ilk bulunan en kısa yolu döndürür; eşit uzunlukta yollar arasında yukarıdaki
sıra belirleyicidir. Bütün yolları arama yoktur. Path bulunamazsa, erişilebilir graph
seçili edge/yön politikasında tüketilmişse `notFound`; depth/work sınırı durdurmuşsa
`boundedSearchIncomplete` döner. İkisi de exit 0'dır. Bu sonuçlar runtime'da
ulaşılmazlık veya eksik graph'ta kesin yokluk ispatı değildir.

Derinlik sınırında daha önce ziyaret edilmemiş uygun bir komşu görüldüğünde `depth`,
sonuç limitinden fazla yeni sonuç görüldüğünde `limit`, edge bütçesinden fazla
komşuluk satırı görüldüğünde `maxEdges` kaydedilir. Cycle'ın yalnızca ziyaret edilmiş
komşuları derinlik kesilmesi sayılmaz. Tam limite eşit, gerçekten tükenmiş sonuç
kümesi kesilmiş sayılmaz. Her kanıt listesi occurrence ID sırasında, limit+1 probe
ile okunur; kesilirse hem step `evidenceTruncated` hem envelope `evidenceLimit` verir.

## Versioned envelope

Bütün başarılı query çıktıları ve domain seçim hataları **tek JSON belge**dir.
Syntax/seçenek, eksik DB/eski schema, SQLite/IO/bozuk JSON hatasında stdout boştur,
stderr açıklama içerir. Exit 2/4/6 mevcut anlamlarını korur. Query envelope version'ı
SQLite tablo version'ından bağımsızdır.

| Alan | Sabit anlam |
| --- | --- |
| `schemaVersion` | Query JSON sözleşmesi: **4** |
| `snapshotId` | Okunan analiz kaydının ID'si; şema veya sorgu kimliği değil |
| `analysisContext` | Snapshot'ın tam AnalysisSnapshot kaydı; TFM/configuration/SDK, source policy ve projeler |
| `completeness` | Analiz `coverage` ve `limitations`; query'nin bütçe kesilmesinden bağımsız |
| `stale` | Varsayılan `notChecked`; açık `--check-freshness`/`--require-fresh` ile `fresh`, `stale` veya `unknown`. [Faz 6 sözleşmesi](INCREMENTAL.md); MSBuild/restore yok |
| `truncated` | Arama, aday/sonuç veya occurrence sınırlarından en az biri sonucu kesti |
| `items` | `{symbol, entity, distance, path}`; boş sonuç da aynı şekildedir |
| `diagnostics` | `{code,message}`; analiz tanıları ve sorguya özgü seçim/etki/yokluk açıklamaları |
| `command`, `arguments`, `options` | Sorgu ve normalize edilmiş bütün etkin seçenekler; varsayılanlar dahil |
| `limits` | Etkin depth/limit/maxEdges/evidenceLimit, returned, examinedEdges ve sıralı reasons |
| `outcome` | ok, found, notFound, boundedSearchIncomplete, ambiguousSymbol, symbolNotFound |

`path[]` kökten sonuç düğümüne gider. Her step `fromId`, `toId`, `reversed`, orijinal
`relationship`, `evidence[]`, `evidenceTruncated` taşır. İlişkinin kaynak/hedefi ters
traversal'da değiştirilmez; terslik step'te belirtilir. Confidence/context ID,
occurrence analysis/context ID, file/span/hash, gerekçe, bound target ve adaylar
korunur. Analiz zaman/metadata alanları dışında aynı snapshot ve sorgu deterministiktir;
query yeni zaman/rasgele kimlik eklemez.

JSON v3 `results/relationships/occurrences` find biçimi v4'te değiştirilmiştir.
Tüketici `items[].entity` ve traversal `items[].path[]` kullanmalıdır. Index/status/
doctor `contractVersion: 4` taşır; onların mevcut alan yapıları korunur. Index/status
graph boyutlarını bildirir; status `schemaVersion` SQLite schema **3** anlamındadır.

## SQLite akışı

Schema 3: node endpoint tablosu, entity'ye nullable FK; relationships source/target
node FK; occurrences relationship FK. Unknown endpoint source entity olarak
uydurulmaz. Exact/inferred hedef zorunlu, unresolved hedef null kısıtı bulunur.
Entity name/qualified name, ilişkinin iki yönü `(endpoint,kind,confidence,id)` ve
occurrence `(relationship_id,id)` indekslenir. Substring find B-tree araması değildir;
SQL tarafında taranabilir, fakat uygulamaya yalnızca limit+1 sonuç yüklenir.

Her CLI query read-only connection + tek read transaction kullanır. Entity/komşuluk/
kanıt okumaları ID/parametre/limit ile yapılır; graph'ın tamamını belleğe yüklemek
gerekmez. Büyük bağlı bileşen yalnızca açık bütçeye kadar ziyaret edilir. Bozuk seçili
JSON ve tutarsız seçili endpoint/kanıt hata üretir; her query bütün DB'nin integrity
scan'ini yapmaz. Yazmada FK'ler açıktır; read-only query DB'yi onarmaz veya değiştirmez.

Schema 1/2 için status ve eski depolama okuma API'si çalışır; yeni query komutları
exit 4 ile açık yeniden index ister. Query sırasında migration yoktur. Başarılı
`index` atomik schema 3 replacement yapar; hata eski schema ve snapshot'ı korur.
Yeni paket yok; mevcut lisans/lock sözleşmesi sürer.


Faz 9 dependencies/impact varsayılanları APP_SERVICE_CONTRACT, MODULE_DEPENDS_ON,
REPOSITORY_ENTITY, CONTEXT_ENTITY, CONFIGURES_ENTITY, MAPS_ENTITY, HTTP_CONTRACT,
INVOKES_HTTP, POSSIBLE_ENDPOINT yönlerini de kapsar. CALLS anlamı değişmez.
ABP candidate path için --include-inferred gerekir; unresolved endpoint hedefi
üzerinden traversal yapılamaz. [Modül sözleşmeleri](ABP-EF-REFIT.md).
