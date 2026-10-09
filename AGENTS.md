# UO.Graphs çalışma kuralları

- Ürün adı **UO.Graphs**, CLI adı **uographs**. Her fazdan önce bu dosyayı, [durumu](docs/STATUS.md) ve ilgili kodu oku.
- Yalnızca kullanıcının verdiği mevcut fazı uygula; sonraki fazı başlatma. Kapsam: [PRODUCT](docs/PRODUCT.md), tasarım: [ARCHITECTURE](docs/ARCHITECTURE.md), kabul: [ACCEPTANCE](docs/ACCEPTANCE.md).
- CLI-first, yerel ve Copilot'tan bağımsız çalış. MCP, HTTP sunucusu, hosted database, embedding/LLM servisi, API key ve telemetry ekleme; kodu veya graph'ı dışarı gönderme.
- .NET 10 ve kararlı, uyumlu paketler kullan. Gerçek SDK/paket sürümlerini sabitle; doğrudan ve geçişli bağımlılıkların lisanslarını doğrula. MIT ürünle uyumsuz, permissive olmayan veya belirsiz lisanslı bağımlılığı sessizce ekleme.
- Geliştirme/kurulum bağımlılık edinimi ile ürün çalışmasını ayır. Runtime'da otomatik restore/download/update yapma. Eksik SDK/assets için açıklayıcı hata ver.
- İncelenen projeyi MSBuild ile yüklemeden önce açık `--trust-project` iste. Bu seçim sandbox veya ağ engeli değildir; kurum politikalarını ve terminal izinlerini aşma.
- `exact`, `inferred`, `unresolved` ayrımını, kaynak kanıtını, analiz bağlamını ve eksiklikleri koru. Statik kanıtı runtime garantisi gibi sunma; eksik analizden kesin yokluk sonucu çıkarma.
- Başlangıçta Core, Roslyn, Storage.Sqlite ve Cli yeterli. İhtiyaç kanıtlanmadan katman, generic repository, event bus veya uygulama içi mediator ekleme.
- Kullanıcı değişikliklerini ve mevcut talimatları koru. Yıkıcı Git işlemi, otomatik commit/push/publish yapma. Basit belirsizlikleri gerekçeli varsayımla çöz; ürün kapsamı değişikliğini sessizce yapma.
- Hazırlık/Faz 0 dışında her faz sonunda build, ilgili testler ve gerçek CLI smoke test çalıştır. Çalışmayan doğrulamayı gerekçesiyle `NOT RUN` yaz. Test silme, assertion gevşetme, geniş warning suppression veya fixture çıktısını çalışan özellik gibi sunma.
- Her faz sonunda STATUS'u kanıta göre güncelle; değişen dosyaları, gerçek komutları/sonuçları, eksikleri ve önerilen commit mesajını raporla. Test edilmeyen platform veya doğrulanmayan faz için tamamlandı deme.
- Subagent isteğe bağlıdır; ortam destekliyorsa en fazla iki bağımsız inceleme/test ajanı kullan. Dosya sahipliğini ayır, sonuçları bekle ve incele; ortak sözleşmeler ve entegrasyon ana ajana aittir.
