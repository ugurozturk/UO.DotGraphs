# UO.Graphs ürün kapsamı

## Amaç

UO.Graphs, C#/.NET solution'larını Roslyn ile analiz eden, kod varlıkları ve ilişkilerini yerel SQLite veritabanında saklayan bir code knowledge graph engine'dir. Komut adı `uographs` olur. Diyagram üretmek ürünün ana hedefi değildir.

Kullanıcı terminalden sembol bulabilir, callers/callees ve bağımlılıkları sorgulayabilir, değişiklik etkisini ve iki varlık arasındaki bağlantı yolunu inceleyebilir. AI ajanı için küçük, kaynak kanıtına dayalı context ve yerel JSON/NDJSON/Mermaid/DOT export Faz 5'te eklendi. Faz 6 içerik tabanlı incremental no-op ve atomik snapshot yayınlaması ekler. Faz 7 Microsoft DI ve ASP.NET Core statik bildirimlerini çıkarır. Faz 8 UO.Mediator, MediatR ve MassTransit mesaj sözleşmelerini ve inferred handler/consumer adaylarını modeller. Faz 9 ABP/EF Core/Refit kaynak bildirimlerini, candidate exposure ve unresolved runtime configuration sınırlarıyla modeller. Güncel sınırlar ve gerçek kabul STATUS'tadır.

## Kullanım ortamı

- CLI bağımsız çalışır; GitHub, Copilot veya uzak bir servis hesabı gerektirmez.
- On-prem Azure DevOps'tan alınmış yerel checkout dahil yerel kaynaklarla çalışır. Git metadata'sı temel analiz için zorunlu değildir.
- Windows, Linux ve macOS hedeflenir. Bir platformun destek hedefi olması, orada test edildiği anlamına gelmez.
- Hedef framework .NET 10'dur. Ürün MIT lisanslı tasarlanır; lisans metni ve paket lisans envanteri uygulama/paketleme işinin parçasıdır.

## Yerellik, güven ve gizlilik

Ürün analiz ve sorgu sırasında kodu veya graph'ı dışarı yüklemez. MCP, HTTP sunucusu, hosted database, embedding servisi, LLM API, API key ve telemetry içermez. SDK/NuGet paketlerinin kurulum ve geliştirme sırasında edinilmesi, ürünün offline çalışma sözleşmesinden ayrı belgelenir.

Runtime otomatik restore, SDK/paket indirme veya güncelleme başlatmaz. Tam analiz için gerekli SDK ya da restore edilmiş assets yoksa eksikliği açıkça raporlar; kullanıcının uygun ortamda bağımlılıkları hazırlaması gerekir. İncelenen proje üzerinde otomatik düzeltme veya restore yapılmaz.

MSBuild proje değerlendirmesi/yüklemesi, proje tarafından tanımlanan davranışları çalıştırabilir. Bu nedenle tam workspace analizi açık `--trust-project` seçimi gerektirir. Seçenek yoksa MSBuild yüklemesi başlamaz. Bu onay güvenlik sandbox'ı veya ağ engeli sağlamaz; güvenilmeyen kodu güvenli hale getirmez. Offline ürün davranışı, güven verilen projenin çalıştırabileceği özel MSBuild görevleri için ağ izolasyonu garantisi değildir.

Copilot skill CLI'ı tüketen ayrı bir entegrasyondur. Kullanıcı CLI çıktısını Copilot'a verdiğinde bu bağlam yerel aracın sınırından çıkar ve ilgili sağlayıcı/kurum politikalarına tabi olur. Yerel analiz gizliliği, Copilot'a aktarılan bağlamın yerel kalacağı anlamına gelmez. Skill kurum politikalarını veya terminal izinlerini aşmaz.

Faz 10 repo skill'i, dağıtılabilir template, overwrite etmeyen kurulum ve bounded
CLI iş akışı eklendi. Kurulu VS Code format desteği ile gerçek Copilot E2E kabulü
ayrıdır; [kurulum ve manuel kabul](COPILOT-SKILL.md), [gerçek durum](STATUS.md).

## Doğruluk sözleşmesi

- `exact`: Belirtilen statik ilişkinin analiz bağlamında kanıtlandığını belirtir; runtime'da kesin çağrı veya çalışma garantisi değildir.
- `inferred`: Bir kural veya sezgisel çıkarımdır; kuralı, gerekçesi ve dayandığı kanıt korunur.
- `unresolved`: İlişki adayı görülmüştür ancak hedef veya anlam çözülememiştir; nedeni korunur.
- Sonuçlar kaynak konumu, analiz bağlamı ve kapsam/eksiklik bilgisi taşır. Bozuk veya eksik workspace sonucu boş liste, kesin “bağlantı yok” olarak sunulmaz.
- Tam analizdeki yokluk iddiası bile yalnızca indekslenen kapsam, desteklenen ilişki türleri ve belirtilen statik bağlam için geçerlidir.

## İlk sürüm kapsamı dışında

Frontend, web graph viewer, çok dilli parser, çoklu-repo federation, watcher, plugin marketplace ve zorunlu NativeAOT yoktur. Uygulamanın ABP ile yazılması gerekmez. Genel amaçlı runtime izleme veya bütün dinamik çağrıları kesin çözme garantisi verilmez.

Faz 7–9'daki DI/HTTP, mediator/messaging ve ABP/EF Core/Refit başlıkları **incelenen uygulamanın ilişkilerinin analizi** içindir. UO.Graphs'e HTTP sunucusu, mediator, event bus veya ABP altyapısı ekleme talebi değildir.

## Teslim sırası

0. Hazırlık belgeleri.
1. Çalışan uçtan uca iskelet.
2. Workspace ve semboller.
3. Semantik ilişkiler.
4. Graph sorguları.
5. AI context ve export.
6. Incremental indexing.
7. DI/HTTP analizi.
8. Mediator/messaging analizi.
9. ABP/EF Core/Refit analizi.
10. VS Code GitHub Copilot Agent Skill.
11. Performans, paketleme ve son denetim.

Her faz kullanıcının sonraki talimatıyla başlar. Ayrıntılı özellik/komut sözleşmeleri ilgili fazda netleşir; bu yol haritası ilerideki fazları şimdi uygulama izni değildir.
