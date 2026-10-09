# Yerel yazma sınırı ve hata dayanıklılığı

UO.Graphs sorgu/context/export sırasında MSBuild başlatmaz. Kaynak `.cs`, proje,
solution ve config dosyalarını uygulama koduyla değiştirmez. Index SQLite ve
`.writer.lock`, `-wal`, `-shm`, `-journal` sidecar'larını yalnız seçilen `--db`
hedefinin yanında oluşturur. Varsayılan hedef çalışma dizinindeki `.uographs`'tır.
Progress/error stderr, JSON stdout'tur; otomatik log dosyası yoktur. Ölçüm ve test
scriptleri yeni, açıkça belirtilen evidence dizinine yazar; sentetik üretici mevcut
hedefi reddeder. Fixture kaynak değişimleri yalnız benchmark üreticisinin kendi
kontrollü dataset'inde yapılır; kullanıcı kaynakları benchmark girdisi değildir.

## MSBuild/custom tasks güven sınırı

`--trust-project` hem full hem incremental için, proje evaluation'ından önce
zorunludur. NuGet restore/download/update CLI tarafından çağrılmaz. Ancak MSBuild
SDK resolver, import, property function, target ve custom task'ları; izin verilmiş
source generator'lar normal kullanıcı yetkileriyle kod çalıştırabilir. Dosya yazma,
proses başlatma ve ağ I/O bu kodlar tarafından yapılabilir. Workspace load SDK'nın
obj altındaki generated dosyalarını üretebilir. Trust bir sandbox değildir; aracı
başlatan kullanıcı, hedef projeye bu yürütme yetkisini verdiğini bilmelidir.

`TrustGatePrecedesMsbuildTargetSideEffects` gerçek hazırlanmış fixture'a
`BeforeTargets=ResolveAssemblyReferences` ve `WriteLinesToFile` ekler. Bayrak yokken
marker oluşmaz; bayrakla workspace yüklenirken kontrollü marker oluşur; kaynak
proje metni aynı kalır. Marker indeks kökünün dışında kontrollü test klasöründedir.
Bu, ürünün custom task yan etkilerini engellediği iddiasının neden yapılamayacağını
somut olarak gösterir. İncelenen girdiler analiz sırasında değişirse publication
reddedilir; önceden commit edilmiş graph korunur.

Kaynağın hiç değişmemesi zorunluysa salt okunur filesystem kopyası ve yazılabilir
ayrı obj/output alanı kurum tarafından hazırlanmalıdır. Ağ/proses/filesystem
izolasyonu OS/container/agent politikasıdır; ürün terminal izinlerini yükseltmez.
Custom task veya generator'ın kötü niyetli kaynak yazımını CLI önleyemez.

## Doğrulanan hata yolları

- Transaction cancellation ve gerçek proses SIGINT (exit 130) önceki snapshot'ı
  korur. Hazırlık sırasında SIGKILL ve eşzamanlı index (exit 10) ayrı test edilir.
  Writer lease analizden SQLite commit'e kadar tutulur. WAL publication ortasındaki
  kill ve eşzamanlı okuyucular mevcut Phase6 regresyon testlerindedir; gerçek koşu
  sonuçları STATUS'ta ayrıdır.
- `PRAGMA max_page_count` üzerinden test-only internal seam ile gerçek SQLite
  **SQLITE_FULL (13)** oluşturulur. Rollback, eski graph ve sonraki başarılı retry
  sınanır. Bu, fiziksel diskin bütününü doldurma testi değildir; disk image/quota
  kabulü **NOT RUN**. Üretim komutunda fault-injection seçeneği yoktur.
- Bilinmeyen schema exit 6; eski desteklenen şemalar gerektiğinde açık re-index
  ister. Bozuk/non-SQLite dosya overwrite edilmez. Bozuk global.json ve MSBuild
  props analiz başlamadan hata verir; kaynak ve DB korunur.
- DB yolu bilinçli canonicalize edilir (aynı DB alias'ı writer kilidini atlatmaz).
  Yazma sidecar'ı symlink ise reddedilir. Snippet'te `..`, outside-root,
  symlink/reparse point, secret/config ve hash uyuşmazlığı metin sızıntısını önler.
  Fingerprint symlink girdileri reddeder. MSBuild bağlantılı dış kaynakları
  değerlendirebilir; `--root` bir işletim sistemi erişim sandbox'ı değildir.
- SQL değerleri parametrelidir; literal `%`, `_`, apostrophe ve SQL/shell payload
  regresyonları vardır. SQL tür/yön parçaları kontrollü enum/placeholder'dan gelir.
- Hazırlık için `ProjectPreparation` tarafından başlatılan dotnet/MSBuild
  proseslerinde stdout **32 Mi karakter**, stderr **1 Mi karakter** ile sınırlıdır;
  aşım exit 4, snapshot publication yok. Parent exit sonrasındaki pipe drain dahil
  iki dakika timeout ve cancellation uygulanır. Reader testlerine ek olarak gerçek
  dotnet parent/child ile canlı process-tree iptali, parent çıktıktan sonra child'ın
  tuttuğu stdout/stderr pipe drain iptali ve büyük stderr reddi geçti. Post-exit
  handshake ve 250 ms bekleme kontrolü, anlık scheduling gecikmesinin başarı
  sayılmasını önler. Yeniden parent atanmış child'ı ürünün öldürdüğü iddia edilmez;
  test kendi child'ını ayrıca temizler. Gerçek `IncrementalIndexer` yolunda izinli
  kooperatif generator'ın token'ı alıp iptali gözlemlemesi test edildi. Cancellation
  kooperatiftir; generator token'ı ignore ederse process düzeyindeki kurum timeout'u
  gereklidir. Kötü niyetli generator'ın zorla durdurulması **NOT RUN**.
  Bu stream sınırları ve iki dakika deadline Roslyn `MSBuildWorkspace` BuildHost'una
  veya generator yürütmesine genellenmez. BuildHost'un ayrı başlangıç/IPC yolu
  vardır; aynı proses içindeki generator doğrudan Console'a yazabilir. Güvenilmeyen
  build kodunun bütün çıktı/proses kaynak sınırları kurum izolasyonuyla sağlanmalıdır.
- Context max-chars tüm JSON ve LF'yi sayar. Bütçe altında bütün kayıtlar çıkarılır,
  truncation korunur; minimum zarf sığmazsa stdout boş/exit 2. Export record limitleri
  vardır; export/query için toplam byte sınırı iddiası yoktur. Büyük diagnostic veya
  metadata zarfı büyük olabilir. Kaynak tarafından türetilen metin komut değildir.

## Export ve skill kurulumu

CLI export stdout üretir; shell'in `>` ile mevcut dosyayı truncate etmesini CLI
önleyemez. `scripts/safe_export.py NEW_FILE -- uographs export ...` exclusive
`xb` oluşturur, mevcut dosya veya symlink hedefini reddeder; shell çalıştırmaz.
Başarısız child'ın kısmi dosyası inceleme için kalır ve başarısız exit korunur.
POSIX shell'de alternatif açık `set -C` (noclobber) politikasıdır.

`scripts/install_skill.py` mevcut skill'i birleştirmez/overwrite etmez; symlink
hedefi reddeder. Tool-path testi kullanıcının global kurulumuna dokunmaz. Tüm bu
yol kontrolleri eşzamanlı saldırgan bir kullanıcının filesystem'i değiştirmesine
karşı atomik sandbox garantisi değildir; hedef dizin yalnız yetkili kullanıcıya
yazılabilir olmalıdır.

Kod/graph upload, telemetry, MCP/HTTP/LLM servisi veya API key eklenmedi. Copilot'a
elle bağlam verilmesi ayrı kurum veri politikasına tabidir.
