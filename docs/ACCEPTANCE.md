# Faz bazlı kabul kriterleri

Bu belge doğrulanacak tabanı tanımlar; uygulama fazlarının ayrıntıları kullanıcının ilgili faz talimatıyla netleşir. Burada yer alması bir özelliğin tamamlandığı veya sonraki fazın başladığı anlamına gelmez.

## Ortak kapı

Faz 0 dışında her fazda gerçek build, ilgili otomatik testler ve gerçek CLI smoke test çalıştırılır. Komut, exit code, ortam ve sonuç STATUS'a yazılır. Çalıştırılamayan kontrol gerekçesiyle `NOT RUN` kalır; başarısızlık ve eksiklik gizlenmez. Statik inceleme veya derleme tek başına uçtan uca kabul değildir.

Testler gerçek analiz/depolama/sorgu akışının beklenen davranışını sınar; hardcoded fixture çıktısı üretim implementasyonu yerine kullanılamaz. Negatif ve kısmi analiz senaryoları ilgili faza dahildir. Fixture'lar yerel ve kontrollü olabilir; test başarısı tüm gerçek solution'larda başarı iddiası değildir.

SDK/paket eklenen veya değişen fazda kararlı sürüm, uyumluluk, kesin sürüm/lock ve lisans kanıtı kontrol edilir. Restore geliştirme adımı olarak açıkça ayrılır. Runtime restore/download/update çağırmama davranışı doğrulanır; gerçek offline kabul yapılmadıysa ayrıca `NOT RUN` yazılır. `--trust-project` olmadan hedef proje MSBuild ile yüklenemez.

Windows/Linux/macOS sonuçları ayrı tutulur. Bir platformdaki build/test diğer platformların testi yerine geçmez. Doğrulanmamış zorunlu kriteri kalan faz tamamlandı işaretlenmez; son platform matrisi Faz 11 kapısında değerlendirilir.

## Faz 0 — Hazırlık

- Mevcut klasör, kullanıcı dosyaları, Git durumu ve kurulu SDK incelenir; dosyalar korunur.
- AGENTS ve dört ürün/teknik/durum belgesi oluşturulur; sınırlar, sıralama ve doğruluk sözleşmesi tutarlıdır.
- Mevcut olmayan uygulama/test sonucu iddia edilmez; bir sonraki faz açıkça kaydedilir. Build/test/CLI smoke zorunlu değildir.

## Faz 1 — Çalışan uçtan uca iskelet

- Dört ürün projesi .NET 10 ile derlenir; SDK ve gerçek, kararlı bağımlılık sürümleri sabittir. MIT metni ve bağımlılık lisans kanıtları bulunur.
- `uographs` çalışır. Küçük gerçek C# fixture'ından Roslyn ile çıkarılan asgari bir varlık, güven onaylı akışta SQLite'a yazılır ve ayrı CLI çağrısıyla geri okunur.
- Help ve hatalı girdi davranışı, SQLite kalıcılığı, güven bayrağı yokluğu ve eksik önkoşul hatası test edilir. Komut isimleri/çıktı/exit code sözleşmesi bu fazda belgelenir.
- Geniş sembol/ilişki desteği tamamlanmış gibi sunulmaz.

## Faz 2 — Workspace ve semboller

- Gerçek çok projeli solution yüklenir; desteklenen sembol türleri ve kapsam belgelenir, bulunup sorgulanır.
- Overload, partial tanım, projeler arası kimlik ve birden çok target framework/configuration bağlamı karıştırılmaz; seçilen/atlanmış bağlam görünürdür.
- Eksik SDK/assets, çözülemeyen referans ve yüklenemeyen proje testleri açık tanı/kısmi-başarısız durum üretir. Boş sonuç tam analiz gibi sunulmaz.

## Faz 3 — Semantik ilişkiler

- Çağrı, tip bağımlılığı, kalıtım ve implementasyon için desteklenen statik kalıplar kaynak kanıtıyla kaydedilir.
- Overload çözümü, interface/virtual çağrı ve dinamik/çözülemeyen aday testleri `exact`/`inferred`/`unresolved` ayrımını doğrular. Statik hedef runtime dispatch garantisi olarak sunulmaz.
- Kaynak aralığı, analiz bağlamı ve çıkarım gerekçesi depolama ve CLI çıktısında korunur.

## Faz 4 — Graph sorguları

- Sembol bulma, callers/callees, bağımlılık, etki analizi ve bağlantı yolu gerçek indeks üzerinde beklenen sonuçları verir.
- Döngü, yön, derinlik/sonuç sınırı, birden fazla aday ve erişilemeyen hedef test edilir; sınırlama/kesilme görünürdür.
- Kısmi indeks ve gerçekten boş sonuç ayrılır; yokluk sonucu yalnızca belirtilen snapshot, kapsam ve desteklenen ilişkiler için verilir.

## Faz 5 — AI context ve export

- Seçilen sembol/soru odağı için boyutu sınırlandırılmış, deterministik ve kaynak referanslı bağlam üretilir; bütçe birimi ve kesilme davranışı belgelenir/test edilir.
- Export kimlik, kanıt, güven sınıfı, analiz bağlamı ve eksiklikleri korur; format sürümü ve tüketim sözleşmesi tanımlıdır.
- LLM/API bağlantısı gerekmez. Copilot'a aktarımın ayrı gizlilik sınırı olduğu belgelenir.

## Faz 6 — Incremental indexing

- Ekleme, düzenleme, silme, yeniden adlandırma ve proje/referans/analiz bağlamı değişikliklerinde etkilenen kayıtlar yenilenir; eski ilişkiler temizlenir.
- Aynı son kaynak snapshot'ı için incremental ve temiz tam indeksin anlamsal sonuçları karşılaştırılıp eşitliği doğrulanır.
- Değişiklik yokken tekrar indeksleme, yarıda kesilme/hata ve geçersizleşme senaryolarında tutarlılık ile güncellik durumu korunur. Watcher gerekmez.

## Faz 7 — DI/HTTP analizi

- Desteklenen DI kayıt/çözümleme ve HTTP endpoint/client kalıpları incelenen uygulamadan çıkarılır; destek matrisi ve kapsam dışı dinamik kalıplar belgelenir.
- Somut fixture'larda olumlu/olumsuz örnekler, çoklu kayıt ve belirsiz endpoint eşleşmesi test edilir. Sezgisel eşleşmeler `exact` diye etiketlenmez.
- UO.Graphs'e HTTP sunucusu veya ağ üzerinden endpoint keşfi eklenmez.

## Faz 8 — Mediator/messaging analizi

- İlgili fazda belirlenen kütüphane/kalıplarda request/handler ve publish/consumer bağlantıları kanıt ve gerekçeyle çıkarılır.
- Handler bulunamaması, birden çok consumer ve dinamik kayıt senaryoları belirsizliği korur; runtime teslim garantisi verilmez.
- Ürünün kendi mimarisine mediator, event bus veya broker zorunluluğu eklenmez.

## Faz 9 — ABP/EF Core/Refit analizi

- İlgili fazda sınırlandırılan ABP, EF Core ve Refit kalıpları için desteklenen sürüm/kalıp matrisi, fixture'lar ve kaynak kanıtları bulunur.
- Dolaylı veya convention tabanlı ilişkilerin çıkarım gerekçesi görünürdür; desteklenmeyen/dinamik yapı exact sonuç üretmez.
- Analiz için migration çalıştırılmaz, gerçek veritabanı/HTTP servisi çağrılmaz. Ürün ABP uygulamasına dönüştürülmez.

## Faz 10 — VS Code GitHub Copilot Agent Skill

- Skill yalnızca belgelenmiş CLI komutlarını kullanır; MCP veya servis gerektirmez. Kurulum/keşif adımları ve küçük bağlam kullanımı belgelenir.
- Eksik CLI/SDK/assets, güven onayı yokluğu, kısmi/eski indeks ve komut hataları için talimatlar denenir; skill sessizce güven bayrağı eklemez, izin/politika aşmaz.
- VS Code/Copilot üzerinde gerçek skill kullanımı ayrı doğrulanır. Yalnızca dosya incelemesi veya CLI testi varsa entegrasyon `NOT RUN` kalır.
- Dışarı aktarılan bağlamın gizlilik sınırı açıkça anlatılır; CLI Copilot olmadan da çalışır.

## Faz 11 — Performans, paketleme ve son denetim

- Boyutu/özellikleri belirtilmiş temsili solution'larda tam/incremental indeks süresi, bellek, SQLite boyutu ve sorgu gecikmesi ölçülür; kabul bütçeleri ölçümden önce kaydedilir.
- Temiz kurulum, `uographs` çağrısı, sürüm/help, indeks ve sorgu paket üzerinden denenir. SDK ve bağımlılıklar önceden hazırlanmışken offline çalışma gerçek koşullarda doğrulanır; otomatik runtime edinimi yoktur.
- Paket içeriği, MIT/üçüncü taraf bildirimleri, sürümler/lock dosyaları ve varsa native varlıkların lisans/platform uyumu denetlenir.
- Windows, Linux ve macOS üzerinde kurulum/build/ilgili test/CLI smoke sonuçları ayrı kaydedilir. Erişilemeyen platform `NOT RUN` ve açık eksik olarak kalır; çapraz derleme çalışma testi sayılmaz.
- Yerellik, güven kontrolü, eksik analiz bildirimi, kapsam dışı işler ve Copilot gizlilik sınırı son kez denetlenir; kalan zorunlu eksikler kapatılmadan tam kabul verilmez.
