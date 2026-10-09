# Incremental indexing ve güncellik (Faz 6)

Ürün 0.6.0; JSON v4'e eklenen alanlar, SQLite schema 3'te isteğe bağlı `fingerprint`
tablosu. Eski snapshot okunur; manifest yoksa kontrol sonucu `unknown`, ilk index
bütün seçili proje kümesini yeniler. Context/export format v1 korunur.

```sh
uographs index App.slnx --trust-project --incremental
uographs index App.slnx --trust-project --full
uographs status --check-freshness
uographs query callers MyMethod --require-fresh
uographs context MyMethod --require-fresh
uographs export --format json --check-freshness
```

## Index sözleşmesi

- Varsayılan `--incremental`; `--full` ile birlikte verilemez. İki mod da MSBuild
  değerlendirmesinden önce açık `--trust-project` ister. Otomatik restore yoktur.
- Granularity: **entireSelectedProjectClosure**. Bir girdi/context değişirse tüm
  seçili projeler ve proje referansları yeniden analiz edilir. Değişmeyen dosyalar
  da yeniden bağlanır. Method/file-level reuse veya seçici project reuse yoktur.
- Yalnızca dependency manifesti tamamlanabilmiş, içeriği ve değerlendirmesi aynı
  snapshot için `outcome: noOp`, `projectsAnalyzed: 0`, `analysisMilliseconds: 0`.
  Roslyn workspace, compilation, symbol/relationship analizi ve DB replacement
  yapılmaz; snapshot ID korunur. SDK kontrolü, MSBuild evaluation/preprocess ve
  SHA-256 taraması yapılır. No-op sıfır I/O veya sıfır MSBuild maliyeti değildir.
- Diğer durumlarda `rebuiltAllProjects`; full modu her zaman bu yolu kullanır.
  Dosya/simge ekleme, silme, taşıma, imza/overload, global using, public API,
  configuration/TFM/defines ve dependency değişimleri aynı geniş invalidation'a
  tabidir. Eski entity, edge, occurrence ve inference sonuçları transaction'da
  tamamen değiştirilir. Faz 7–9 framework analyzer'ları bu fazda eklenmez.
- Index JSON'da mod, granularity, outcome, analiz edilen proje sayısı, fingerprint/
  analiz milisaniyeleri, hashlenen dosya/byte sayısı ve dependency eksikleri vardır.
  Süreler measured wall-clock; analysis süresi yayın öncesi doğrulamayı da içerir.
  Index cevabındaki freshness yayın öncesi validation sonucudur; yöntemi
  `indexEvaluationAndContentValidation`, süresi evaluation + analiz + son kontrol
  toplamıdır (SQLite commit hariç). Kalıcı freshness etiketi sonraki read için
  tekrar kullanılmaz; sonraki read varsayılan olarak `notChecked` döner.

## Dependency fingerprint

Dosya tarihleri yeniden kullanım kararı vermez. Kaynak ve girdiler **ham dosya
byte SHA-256** ile, dosya isimleri de sıralı inventory ile karşılaştırılır. Böylece
aynı mtime/uzunlukla içerik değişimi, ekleme/silme/taşıma yakalanır.

- Root altı kaynak/input inventory; solution/project dosyaları, additional inputs,
  editorconfig, ancestor Directory.Build.props/targets, Directory.Packages.props,
  global.json ve NuGet config. Eksik ancestor girdilerinin sonradan oluşması izlenir.
  MSBuild user/extensions/SDK/Roslyn dizinleri ve evaluated CustomBefore/After
  import yolları da izlenir; henüz mevcut olmayan örtük importlar atlanmaz.
- MSBuild preprocess'ten gerçek import dosyaları ve preprocess digest'i; evaluated
  Compile/ProjectReference/AdditionalFiles/Analyzer/EditorConfigFiles girdileri,
  TFM/configuration/defines, language/nullable/optimization gibi evaluated options.
  MSBuild'in item metadata'sındaki access/create/modify tarihleri digest dışındadır.
- Assets/lock ve NuGet'in ürettiği props/targets; assets içindeki package köklerinin
  içerikleri. Seçili SDK, reference packs, workload manifestleri ve çalışan tool
  dizininin içerikleri; SDK installation üyeliği ve dotnet host. Böylece compiler,
  analyzer, tool/çıkarım kuralları ve referans assembly değişimleri geçersiz kılar.
- Bütün index options, runtime/OS/architecture/culture/çalışma dizini ve process environment digest'i; ortam değerleri/secret'lar
  kaydedilmez. İç MSBuildLocator başlangıcının process environment'a yaptığı
  ayarlar dış kullanıcı girdisi sayılmaz; çağrının başlangıç ortamının digest'i saklanır.
  Preparation/preprocess alt süreçleri aynı başlangıç environment kopyasıyla
  çalıştırılır; Locator'ın iç ayarları ikinci değerlendirmeyi değiştirmez.
- `bin`, `.git`, `.uographs` olağan kaynak taramasından dışlanır. `obj` altında
  kaynak C#, editorconfig, JSON/props/targets izlenir; keyfi binary/cache taranmaz.
  SDK'nın ilk workspace açılışında oluşturduğu standart obj kaynaklarının Roslyn
  tarafından tüketilmiş decoded içeriği diskle doğrulanır, son raw hash'leri saklanır.
  Hedef DB, WAL/SHM/journal ve writer lock dosyaları inventory dışıdır.

Tamamlık, doğrulanmış **10.0.201 base Microsoft.NET.Sdk deklaratif input modeli**
içindir; her MSBuild programının bütün dış dünyasını bildiğimiz iddiası değildir.
Custom imports/targets/tasks, property functions/existence koşulları, bilinmeyen
MSBuild property'leri, external/çıktı dizini item pattern'leri, doğrudan assembly
Reference, base dışı SDK ve generator execution dependency kümesini belirsiz
bırakır. Bilinen dosyalar yine hashlenir; `dependencyFingerprint.complete: false`
ve somut nedenler kaydedilir. **No-op kapatılır, her index tüm kapsamı analiz eder.**
Generator izinli olduğunda arbitrary dış dosya/saat/environment girdileri nedeniyle
reuse yapılmaz. Bu güvenli fallback bilerek agresiftir; kolay genişletilebilir bir
hash cache veya yalnız dosya tarihine dayalı iyimser reuse değildir.

Tarama okunamayan dosya/symlink yüzünden yapılamıyorsa işlem açıklayıcı hatayla
biter; doğrulanmamış snapshot yayınlanmaz. Hash maliyeti geniş inventory nedeniyle
büyük olabilir; performans bütçesi Faz 11 yerine geçirilmez.

## Güncellik

Status, query, context ve tüm export formatları aynı freshness sözleşmesini kullanır:

| Seçenek/sonuç | Anlam |
| --- | --- |
| Varsayılan `notChecked` | Dosya hash kontrolü yok; `method: none`, 0 hash file/byte/ms. |
| `--check-freshness` | Manifest inventory/content ve environment denetlenir; MSBuild/restore/subprocess yok. |
| `fresh` | Kontrol sırasında izlenen girdiler eşit ve manifest kapsamı tamamlanmış. |
| `stale` | İzlenen içerik, üyelik veya ortam değişmiş. |
| `unknown` | Eski snapshot/eksik manifest, dependency eksikleri veya okuma hatası. |
| `--require-fresh` | Kontrolü açar; stale/unknown için **exit 9**, stderr ve boş stdout. |

`freshnessCheck` metot, gerçekten hashlenen file/byte sayısı, elapsed ms ve nedenleri açıklar.
Eksik-dosya/üyelik işaretleri dosya sayısına dahil edilmez. Okuma kesilirse
`countsComplete: false`; sayılar tamamlanan dosyaların ölçülen alt sınırıdır. Kontrol
snapshot'ın kendi transaction'ı içindeki manifestle yapılır; farklı nesil graph ve
manifest karışmaz. Read komutları DB'yi değiştirmez. Freshness ile coverage ayrı:
compiler hataları/generator atlama yüzünden partial snapshot'ın izlenen girdileri
fresh olabilir; bu, eksik analizin tamamlandığı anlamına gelmez.

Freshness kontrolü bir dosya sistemi transaction'ı değildir. Kontrolden sonra
kaynak değişebilir; indeksleme sırasında kaynakların stabil tutulması gerekir.
Yayın öncesi ikinci evaluation/content ve tüketilmiş kaynak kontrolü gözlenen
concurrent değişiklikte index'i reddeder; otomatik sonsuz retry yapmaz.

## Atomiklik ve concurrency

Yerel DB için processler arası exclusive writer lease analizden önce alınır ve
commit'e kadar tutulur. Başka writer **exit 10** alır. Lock dosyasının varlığı
kilit anlamına gelmez; OS dosya handle kilidi proses sonlanınca bırakılır. Dosya
silinmez, böylece farklı lock inode'ları üzerinden yarış oluşmaz.

SQLite WAL + tek transaction graph ve manifesti birlikte yayınlar. Her reader tek
read transaction kullanır: eski veya yeni nesli görür, yarım graph görmez. Mevcut
reader yazıcı sırasında eski nesli okumaya devam eder. Hata/iptalde rollback;
proses ölümünde SQLite recovery son commit'i korur. Ctrl-C exit 130; cancellation
preparation/workspace ve publication sınırlarında işlenir. İlk index kesilirse
henüz tamamlanmış snapshot yoktur. Destek yerel dosya sistemidir; network filesystem
ve farklı hardlink isimleriyle aynı DB'ye erişim bu kabulün dışındadır.
