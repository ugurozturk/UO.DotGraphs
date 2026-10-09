# Başlangıç mimarisi

## Projeler ve bağımlılık yönü

| Proje | Sorumluluk | Proje referansı |
| --- | --- | --- |
| `UO.Graphs.Core` | Graph kavramları, kanıt ve analiz durumu, ihtiyaç oldukça ortak sorgu sözleşmeleri | Yok |
| `UO.Graphs.Roslyn` | Güven kontrollü workspace yükleme, sembol/ilişki çıkarımı ve analiz tanıları | Core |
| `UO.Graphs.Storage.Sqlite` | Yerel şema, sürümleme, işlemler ve graph okuma/yazma | Core |
| `UO.Graphs.Cli` | `uographs` komutları, giriş doğrulama, bileşim ve çıktı/exit code sözleşmesi | Core, Roslyn, Storage.Sqlite |

Core, Roslyn ve SQLite paketlerinden bağımsız kalır. Analiz katmanı depolamayı doğrudan yönetmez; CLI akışı birleştirir. Test projeleri bu dört ürün projesine eklenebilir. Yeni katman, generic repository, uygulama içi mediator veya event bus başlangıç gereksinimi değildir. Dar arayüzler yalnızca gerçek kullanım ihtiyacı doğduğunda eklenir.

Akış: yerel giriş → güven/önkoşul kontrolü → Roslyn analizi → kanıtlı graph kaydı → SQLite → CLI sorgusu/export. İndekslenmiş graph sorgusu yeniden MSBuild yüklemeyi gerektirmez.

## Saklanacak bilginin asgari anlamı

Bu liste nihai tablo/API şeması değildir; ilgili fazda somutlaştırılacak doğruluk gereksinimleridir.

- **Analiz kaydı:** Analiz kimliği, zaman, araç sürümü, solution/proje kapsamı, ilgili SDK/Roslyn sürümleri, configuration/target framework/preprocessor gibi sonucu etkileyen bağlam ve tanılar.
- **Varlık:** Bağlamı içinde ayırt edilebilir kimlik, sembol türü, adı, proje/assembly aidiyeti ve kaynak konumları. Overload, partial tanım ve çoklu target framework tek bir yanıltıcı sembole indirgenmez.
- **İlişki:** Kaynak, varsa hedef, ilişki türü, `exact`/`inferred`/`unresolved`, kaynak kanıtı ve analiz kaydı. Çözülemeyen aday, uydurma hedefe bağlanmaz.
- **Kanıt:** Dosya, kaynak aralığı, ilişkinin gerekçesi/çıkarım kuralı ve kanıtın ait olduğu kaynak sürümünü belirleyebilen bilgi. Kaynak değiştiğinde eski kanıt güncel diye sunulmaz.
- **Kapsam/durum:** Tamamlanan, kısmen analiz edilen, başarısız veya atlanan bölümler ve nedenleri. İlişkinin güven sınıfı ile analiz kapsamının tamamlığı ayrı boyutlardır.

Schema version ile analiz kimliği ayrı tutulur. Yazma akışı transaction kullanır; başarısız/yetersiz analiz önceki geçerli graph'ı sessizce “tam ve güncel” bir boş sonuçla değiştirmez. Kısmi kayıt politikası uygulandığı fazda açıkça tanımlanır ve test edilir.

Sorgular kullanılan analiz kaydını ve kapsam sınırlamalarını görünür kılar. Kaynakların sonradan değişmesi veya incremental indexing eksikleri saklanmaz. İndekslenmiş snapshot hakkında bilinenler ile çalışma dizininin güncelliği hakkında doğrulananlar ayrılır.

## Workspace güven sınırı

`--trust-project` denetimi, incelenen proje için MSBuild değerlendirmesi dahil yükleme başlamadan yapılır. Bayrak verilmezse tam analiz açıklayıcı hata ile durur. Hazırlık aşaması ayrıca bir güvenilmeyen-kod analiz modu taahhüt etmez.

Gerekli SDK ve assets kullanıcı tarafından önceden hazırlanır. Analiz motoru restore/download/update başlatmaz. Hedef framework veya referans çözümleme sorunları analiz tanısı ve kısmi/başarısız durum olarak saklanır. Güven verilen projenin özel build mantığı için sandbox veya ağ izolasyonu iddiası yoktur.

## SDK, paketler ve lisans

2026-10-08 tarihinde bu çalışma ortamında `dotnet --list-sdks` ve `dotnet --info` ile **.NET SDK 10.0.201**, runtime **10.0.5**, RID **osx-arm64** gözlendi. Faz 1'de `global.json` SDK 10.0.201'e sabitlendi; `allowPrerelease: false` ve `rollForward: disable` kullanılıyor. Dört ürün projesi, iki test projesi ve iki örnek proje net10.0 hedefliyor.

Faz 1 paketleri merkezi olarak sabitlendi; sekiz lock dosyası ve [lisans envanteri](DEPENDENCIES.md) eklendi. Gerçek doğrulama sonuçları [STATUS](STATUS.md), JSON/CLI sözleşmesi [README](../README.md) içindedir. Paket eklenen her fazda:

1. Gerçek yayımlanmış kararlı sürüm ve .NET 10 uyumluluğu resmi paket/proje kaynaklarından doğrulanır.
2. Doğrudan sürümler kesin yazılır; geçişli çözümleme lock dosyalarıyla sabitlenir ve locked restore geliştirme doğrulamasına alınır.
3. Doğrudan ve geçişli bağımlılıklar, varsa taşınan native bileşenler dahil, gerçek lisans metni/ifadesi, sürüm, kaynak URL ve bildirim yükümlülükleriyle envantere alınır. Paket adı veya sağlayıcı adına bakarak lisans varsayılmaz.
4. Permissive olmayan veya belirsiz lisans sessizce kabul edilmez; uygun alternatif aranır, kapsamı etkileyen çözümsüz karar kullanıcıya sunulur.

Geliştirme restore/build adımları ile kurulmuş `uographs` komutunun offline çalışma doğrulaması ayrı raporlanır. Ürün MIT lisans metni Faz 1'de eklendi; üçüncü taraf lisansları ürünün MIT beyanıyla değiştirilmiş olmaz.

## Genişleme sınırı

Semantik ve framework ilişkileri aynı kanıt/durum sözleşmesine uyar. DI, HTTP, mediator ve framework çıkarımları runtime eşleşmesini kanıtlayamıyorsa `inferred` veya `unresolved` kalır. İlgili fazlarda desteklenen kalıplar ve sınırlar açıkça listelenir.

AI context/export CLI çıktısı üretir; uzak modele bağlantı kurmaz. Copilot skill bu CLI'ın tüketicisidir. Incremental indexing doğruluğu aynı snapshot'ın tam indeks sonucu ile karşılaştırılır; watcher gerektirmez. Paketleme stratejisi ve performans bütçeleri Faz 11'de ölçümle netleşir; NativeAOT zorunlu değildir.

## Faz 2 gerçekleşen sözleşme

Dört ürün projesi korunur; yeni katman veya servis yoktur. Sürüm 0.2.0 ve JSON v2,
SQLite'ın mevcut atomik schema v1 tablolarını kullanır. Eski snapshot'ın identity v1
bilgisi korunur; yeni index canonical v2 üretir. Hazırlık/fatal hata eski snapshot'ı
korur, analiz edilebilir kısmi sonuç açık `partial` etiketiyle kaydedilebilir.

[Kimlik v2](IDENTITY.md) root-relative project + TFM/configuration + Roslyn declaration
key kullanır. [Workspace sözleşmesi](WORKSPACE.md) SDK/Locator başlatma sırasını,
TFM/configuration seçim ve referans bağlamlarını, generator iznini ve generated
politikayı tanımlar. Generated kod disk taramasıyla toplanmaz; generator çıktıları
izinle ve bellekte üretilir. Bu paragraf Faz 2 tabanını kaydeder; Faz 3 ekleri aşağıdadır.

Ürün paket seti aynı kaldı; test fixture bağımlılıkları dahil 50 kilitli paketin
lisans kanıtı DEPENDENCIES'te güncellendi. Güncel kabul ve platform sınırları STATUS'tadır.

## Faz 3 gerçekleşen sözleşme

Core merkezi ilişki yönlerini, typed endpoint'leri ve tekil relationship/occurrence
ayrımını taşır. Roslyn `SemanticModel`, symbol binding ve IOperation üzerinden
çıkarır; ad/metin benzerliğiyle CALLS eşleştirmesi yoktur. Kaynak referans compilation'ı
seçilen indeks bağlamıyla uyuşmadığında semboller birleştirilmez. External semboller
source entity taklidi yapmadan endpoint olarak saklanır.

Sürüm 0.3.0, JSON v3, SQLite schema v2. Eski v1 kayıtlar okunur; yeniden index
ilişkiler ve occurrence'larla birlikte atomik replacement yapar. CLI find çıktısına
kanıt eklendi; Faz 3 kapsamında traversal yapılmadı. Faz 4 ekleri aşağıdadır. Runtime dispatch expansion yoktur.
Yönler, destek matrisi ve eksikler [RELATIONSHIPS](RELATIONSHIPS.md) içindedir.
Paket sürümleri ve ürün bağımlılık seti değişmedi.

## Faz 4 gerçekleşen sözleşme

Core `GraphQuery.cs` sorgu/options/envelope/evidence-path sözleşmesini taşır.
Storage `GraphQueries.cs` read-only tek transaction içinde parametreli, indeksli
komşuluk/kanıt okumalarıyla bounded BFS uygular; CLI graph'ı belleğe toplamaz.
Cycle/diamond visited kümesi, deterministik komşuluk sırası ve açık depth/result/
edge/evidence bütçeleri kullanılır. Impact yalnızca seçili bağımlılık ilişkilerini
ters yürür; REFERENCES/DECLARES yayılımı yoktur.

Ürün 0.4.0, JSON v4, SQLite schema v3. Endpoint node FK'leri ve yön/kanıt indeksleri
atomik replacement'ta oluşturulur. Eski v1/v2 için status okuması sürer; yeni query
komutları açık yeniden index ister ve migration yapmaz. Yeni paket veya katman yok.
[QUERIES](QUERIES.md) versiyonlu envelope ve yön/limit/path/impact sözleşmesidir.

## Faz 5 gerçekleşen sözleşme

Core `GraphDelivery.cs`, `DeliveryWriter.cs`, `SourceSnippets.cs` sınırları,
context/export format v1'i, tüm stdout bütçesini ve opt-in kaynak politikasını
barındırır. Storage `GraphDelivery.cs` tek read-only transaction içinde exact
çözüm, sınırlı lexical aday ve öncelikli BFS/altgraph toplama yapar. CLI iki yeni
komutu birleştirir; yeni katman/paket yoktur. JSON query v4 ve SQLite v3 korunur.

Context'te kaynak varsayılan kapalıdır; snippet hash'i decoded kaynak metninden
hesaplanır, uyumsuz kaynak metni gösterilmez. Bu kontrol incremental indexing veya
graph'ın genel freshness kanıtı değildir. Mermaid/DOT yorumları da orijinal kanıt ve
analiz metadata'sını korur. Sınırlar ve format sözleşmesi [CONTEXT-EXPORT](CONTEXT-EXPORT.md),
gerçek kabul ve ölçümler [STATUS](STATUS.md)'tadır.


## Faz 6 gerçekleşen sözleşme

Core `IndexFingerprint` içerik inventory, eksikler ve freshness kontrolünü taşır.
Roslyn `IncrementalIndexer` trust sonrası evaluation/preprocess + SHA-256 doğrular;
kapalı dependency modeli eşitse workspace/compilation açmadan no-op verir. Aksi
halde bütün seçili project closure yeniden analiz edilir. MSBuild'in item tarih
metadata'sı fingerprint dışında kalır. Custom build/generator girdileri kanıtlanamazsa
reuse kapatılır; method-level incremental iddiası yoktur.

Storage'ın OS writer lease'i analizden commit'e kadar tutulur; WAL transaction'ı
graph ile isteğe bağlı fingerprint tablosunu atomik yayınlar. Her query kendi read
transaction'ındaki manifesti kullanır. Freshness kontrolü pure filesystem/content
okumasıdır; varsayılan notChecked, require-fresh stale/unknown durumunda exit 9.
Ürün 0.6.0; JSON v4, SQLite schema 3 ve delivery v1'e uyumlu ek alanlar. Yeni paket,
katman veya framework inference analyzer'ı yoktur. Ayrıntılar [INCREMENTAL](INCREMENTAL.md).


## Faz 7 gerçekleşen sözleşme

Roslyn içindeki küçük `IFrameworkExtractor` ve ortak kanıt context'i, statik kayıtlı
Microsoft DI ve ASP.NET Core modüllerini çalıştırır. Yeni katman/paket veya plugin
loader yoktur. Core'a optional registration/HTTP metadata'sı ve altı yönlü ilişki
sözleşmesi eklendi; existing SQLite JSON payload ve atomik replacement kullanılır.
Ürün 0.7.0, JSON v4 / SQLite 3 / delivery 1 korunur. Source compilation ve framework
sembol aidiyeti kanıttır; uygulama/container/host çalıştırılmaz. Ayrıntılı kapsam,
kimlik ve exact/inferred/unresolved ayrımı [FRAMEWORKS](FRAMEWORKS.md)'tedir.


## Faz 8 gerçekleşen sözleşme

Mevcut Roslyn framework extractor hattına `MessagingExtractor` eklendi. Gerçek UO.Mediator.Shared
2.4.2, MediatR 12.4.1 ve MassTransit.Abstractions 8.3.6 sözleşmeleri kullanılır. Core'daki
optional Message metadata'sı ve yedi yeni ilişki; SQLite'ın mevcut atomik JSON payload'ı,
query/path/impact ve context/export ile taşınır. Closed mesaj tipleri assembly/declaration/
context/type arguments ile ayrılır. Runtime aday kümesi unresolved, statik sözleşme
uyumlu adaylar inferred kalır; CALLS compile-time anlamını korur.
Ürün 0.8.0; JSON v4 / SQLite 3 / delivery 1. Ürün bağımlılığı değişmedi; gerçek paket
fixture'ları ile kaynak contract fixture'ları ayrı test edilir. Kapsam: [MESSAGING](MESSAGING.md).


## Faz 9 gerçekleşen sözleşme

Mevcut extractor hattına ayrı AbpExtractor, EfCoreExtractor, RefitExtractor eklendi.
Yeni katman veya ürün dependency'si yok. Core Framework/ModelMapping/HttpContract
metadata ve dokuz ilişki yönü mevcut SQLite JSON payload/query/export hattında korunur.
ABP inferred candidate, EF source mapping ve Refit client contract birbirinden ayrıdır;
runtime exposure/model/remote-target bilinmezliği partial kalır. Ürün 0.9.0;
JSON v4 / SQLite 3 / delivery 1. Ayrı kapsam/test matrisi [ABP-EF-REFIT](ABP-EF-REFIT.md).

## Faz 10 — terminal skill tüketicisi

`.github/skills/uographs/SKILL.md` ve göreli CLI başvurusu mevcut CLI'ı tüketir;
Core/Roslyn/Storage/Cli katmanlarına Copilot bağımlılığı eklemez. Aynı iki dosyanın
dağıtım template'i ve stdlib kurulum aracı repo dışında kullanılabilir. Kurulum
mevcut skill'i birleştirmez/overwrite etmez, VS Code veya terminal izin ayarlarını
değiştirmez. Sorgu ve güncelleme ayrı yetkilendirilir; incremental tüm seçili
kapsamı analiz edebilir. [Format, veri sınırı ve kabul](COPILOT-SKILL.md).

## Faz 11 — sağlamlaştırma

Homojen TFM solution, generator kapalı olduğunda bir workspace ve immutable
analyzer-free Solution paylaşır. Farklı inner-build/generator yolları ayrı kalır;
referans bağlamı eşitliği aliases/EmbedInteropTypes, PE module MVID ve recursive
compilation referanslarını da kontrol eder. Method/project granular incremental
eklenmez; değişiklik bütün seçili closure’ı invalid eder. MSBuild stream’leri
sınırlı ve timeout/cancellation pipe drain’i kapsar. Native SQLite transactional
publication ve OS writer lease korunur. Tool paketi aynı dört katmanı dağıtır.
[Ölçüm](PERFORMANCE.md), [güven sınırı](SECURITY.md), [yerel dağıtım](RELEASE.md).
