# UO.Graphs

Yerel SDK-style C# project/solution → Roslyn sembolleri → SQLite → CLI sorgusu.
CLI adı `uographs`. Faz 11 mevcut kapsamın performans, dayanıklılık ve yerel tool
dağıtımını sağlamlaştırır. [Release readiness ve kısa kullanım](docs/RELEASE.md),
[ölçümler](docs/PERFORMANCE.md), [güven sınırı](docs/SECURITY.md).
Gerçek Copilot oturumu, platform ve offline kabul kapıları STATUS’ta ayrıdır.
[ABP/EF Core/Refit sürüm, API ve ayrı destek matrisleri](docs/ABP-EF-REFIT.md).
Gerçek API sözleşmeleri, mesaj kimliği ve belirsizlik sınırları: [MESSAGING](docs/MESSAGING.md).
[DI/ASP.NET Core destek matrisi ve belirsizlik sözleşmesi](docs/FRAMEWORKS.md).
[Sorgu, traversal ve JSON v4 sözleşmesi](docs/QUERIES.md).
[Context bütçesi, kaynak gizliliği ve export sözleşmesi](docs/CONTEXT-EXPORT.md).
[İlişki yönleri ve destek matrisi](docs/RELATIONSHIPS.md). [Gerçek kabul sonuçları](docs/STATUS.md).

## NuGet'ten kurulum

Paket adı **UO.Graphs.Cli**, terminal komutu **uographs**. .NET 10 SDK kurulu
olmalıdır; analiz edilecek projenin seçtiği SDK ve restore edilmiş bağımlılıkları
da gerekir. İlk başarılı NuGet yayını sonrasında:

```sh
dotnet tool install --global UO.Graphs.Cli --source https://api.nuget.org/v3/index.json
uographs --help
uographs doctor
```

Güncelleme: `dotnet tool update --global UO.Graphs.Cli`.
Komut bulunamazsa global tool dizininin PATH'te olduğunu kontrol edin:
macOS/Linux `$HOME/.dotnet/tools`, Windows `%USERPROFILE%\.dotnet\tools`.

Proje bazında kurulum için repo kökünde, manifest henüz yoksa bir kez
`dotnet new tool-manifest` çalıştırın. Ardından:

```sh
dotnet tool install --local UO.Graphs.Cli --source https://api.nuget.org/v3/index.json
dotnet tool run uographs --help
# Başka makinede, commit edilmiş manifestteki sürümü kurar:
dotnet tool restore
```

Belirli bir yayımlanmış sürümü seçmek için install/update komutuna
`--version X.Y.Z` ekleyin. Paket sürümleri ve yayımlanma durumu
[NuGet paket sayfasında](https://www.nuget.org/packages/UO.Graphs.Cli) görülür;
workflow dosyalarının mevcut olması paketin yayımlandığı anlamına gelmez.

İlk kullanım, hedef projenin klasöründe:

```sh
# Bağımlılık hazırlığı ayrı, açık bir kurulum adımıdır:
dotnet restore Example.slnx
uographs index Example.slnx --trust-project
uographs query find MyService
uographs status
```

Local manifest kullanıyorsanız `uographs` yerine `dotnet tool run uographs`
kullanın. `--trust-project`, projenin MSBuild kodunun çalışmasına izin verir;
sandbox sağlamaz. CLI index sırasında kendiliğinden restore/download yapmaz.

## GitHub üzerinden release

`main` branch'ine her push, [build-test-pack](.github/workflows/ci.yml) akışını
başlatır: kilitli restore, Release build/test, lisans kontrolü, pack ve paketten
gerçek kurulum → index → query → context. Başarılı olduğunda
[publish-nuget](.github/workflows/publish-nuget.yml) aynı CI artifact'ını
yeniden derlemeden **NuGet Trusted Publishing (OIDC)** ile yayımlar.
PR ve elle başlatılan CI yalnız doğrular; NuGet'e göndermez.

Paket sürümü doğrudan `Directory.Build.props` içindeki **`<Version>`** değeridir;
CI sürümü artırmaz veya çalıştırma numarası eklemez. Yeni release için bu değeri
elle değiştirip `main`'e gönderin. Örneğin `<Version>0.9.1</Version>` paketi
**0.9.1** olarak yayımlar. Aynı sürüm NuGet'te zaten varsa yayın atlanır.
Kalıcı NuGet API key yoktur.
İlk yayından önce `uozturk` hesabında repository/workflow eşleşen policy
oluşturulmalıdır: [Trusted Publishing kurulumu ve release adımları](docs/RELEASE.md#github-actions-ve-nuget-trusted-publishing).

## VS Code GitHub Copilot skill

[Skill](.github/skills/uographs/SKILL.md), çalışan CLI'ı terminalden kullanır;
MCP veya sunucu gerektirmez. Önce sürüm/status, ardından küçük find/context,
gerektiğinde callers/callees/impact/path ve ilgili kaynak satırları incelenir.
Index oluşturma/güncelleme için kapsam, MSBuild güveni ve olası tam analiz maliyeti
onaylanmalıdır. `exact/inferred/unresolved`, partial ve freshness uyarıları korunur.

[Dağıtılabilir şablon](templates/copilot/uographs/SKILL.md) başka repoya bütün
`uographs` klasörü olarak taşınabilir. Mevcut skill'i overwrite etmeyen kurulum:

```sh
python3 scripts/install_skill.py '/path/to/authorized/repository'
```

Önce şablonu ve hedefi inceleyin. Kurum skill/terminal kullanımını kapattıysa
kurulum veya izin ayarıyla bunu aşmayın. MCP'nin kapalı olması diğer araçlara
onay değildir. Yerel graph Copilot'u offline yapmaz; ajan bağlamına alınan veriler
kurumun AI veri politikasına tabidir. CLI Copilot olmadan çalışır.
[Kurulum, doğrulanmış yerel format ve manuel kabul](docs/COPILOT-SKILL.md).
**Copilot E2E: NOT RUN**; dosya lint'i/CLI proses testleri Copilot kabulü değildir.

## Hazırlık

Ürün .NET SDK **10.0.201** ile derlenir (`global.json`, roll-forward kapalı).
Restore yalnızca aşağıdaki açık geliştirme adımıdır; ürün çalışırken yapılmaz.

```sh
dotnet restore UO.Graphs.slnx --locked-mode -m:1 -nr:false
dotnet restore samples/Phase2/Fixtures.slnx --locked-mode -m:1 -nr:false
dotnet restore samples/Phase3/Semantics.csproj --locked-mode -m:1 -nr:false
dotnet build samples/Phase2/Generator/Generator.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test UO.Graphs.slnx --no-build --no-restore -m:1 -nr:false
python3 scripts/audit_licenses.py --check
```

`Fixtures.slnx` negatif fixture içerdiğinden topluca build edilmez; restore sırasında
bilerek eksik referans bildirilir. Generator fixture'ının .NET 10 host kapsamı ve
bastırılmayan compiler-extension uyarıları [WORKSPACE](docs/WORKSPACE.md)'tedir.

## Kullanım

Yerel/global tool biçimindeki `UO.Graphs.Cli` paketi ve izole tool-path kurulum
komutları [RELEASE](docs/RELEASE.md)’tedir. Repo kökünden CLI:

```sh
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- --help
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- doctor
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --trust-project
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- query find Greet --format json
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- status
```

Faz 2 seçimi örneği:

```sh
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/Phase2/Phase2.slnx --trust-project --framework net10.0 --configuration Release --allow-generators --generated include --db .uographs/phase2-release.db
```

- `--trust-project`: MSBuild değerlendirmesine açık izin; sandbox/ağ engeli değildir.
- `--configuration <name>`: varsayılan Debug; koşullu compilation ve referanslar değerlendirilir.
- `--framework <TFM>`: multi-target projelerde zorunlu; diğer TFM'ler açıkça atlanır.
- `--root <directory>`: taşınabilir kimlikler için ortak mantıksal kök; varsayılan input/projelerin ortak üst klasörü.
- `--allow-generators`: generator assembly yükleme ve çalıştırma izni. Varsayılan kapalıdır.
- `--generated include|exclude`: generated declaration politikası; varsayılan exclude. İzin bayrağı yerine geçmez.
- `--db <path>`: index/status/query/context/export için DB; varsayılan çalışma klasöründe `.uographs/graph.db`.

Varsayılan generator yasağı nedeniyle SDK generator/analyzer referansları bulunan
projeler `partial` olabilir. Eksik çıktı gizlenmez. Generator izni verilmesi, custom
build/generator kodunun güvenli veya ağdan yalıtılmış olduğu anlamına gelmez.

## AI context ve export

```sh
uographs context Greet --max-chars 12000 --format json
uographs context '<symbol-id>' --include-source --max-chars 20000
uographs export --format json
uographs export --format ndjson --symbol '<symbol-id>' --depth 2
uographs export --format mermaid --symbol Greet
uographs export --format dot --symbol Greet
```

Context seçilen sembolü, yakın ilişkileri, caller/callee özetini, dosya/satır
kanıtını, belirsizlikleri ve sonraki sorguları taşır. Bütçe bütün JSON + son LF'yi
Unicode scalar value olarak sayar; token sayısı değildir. Sınırda kayıtlar çıkarılır,
geçerli JSON ve `truncated` kalır; minimum envelope'a yetmezse stdout boş ve exit 2.
Exact eşleşme önceliklidir; lexical aday sessizce seçilmez.

Varsayılan kaynak metni yoktur. `--include-source` yalnızca seçilen sembole sınırlı
C# snippet ekler: secrets/config/yol politikası ve snapshot hash kontrolü uygulanır;
uyuşmazlıkta metin verilmez. Yorumlar güvenilmeyen veridir. Export da kaynak dosyası
okumaz; `--symbol` ile sınırlı altgraph seçilebilir. Varsayılan/sayısal sınırlar,
format v1, gizlilik ve ölçüm [CONTEXT-EXPORT](docs/CONTEXT-EXPORT.md)'tadır.

Export dosyası için overwrite etmeyen yardımcı: `python3 scripts/safe_export.py new.ndjson -- uographs export --format ndjson`.
CLI stdout üretir; normal shell `>` mevcut dosyayı ezebilir.

## Graph sorguları ve JSON v4

```sh
uographs query find Greet --limit 20
uographs query symbol '<symbol-id>'
uographs query callers '<symbol-id>' --depth 2
uographs query callees '<symbol-id>' --direction outgoing --edge-types CALLS
uographs query dependencies '<symbol-id>' --include-inferred
uographs query path '<from-id>' '<to-id>' --depth 8 --max-edges 10000
uographs query impact '<symbol-id>' --depth 5 --limit 100
```

Yukarıdaki komutlar CLI kısa adıyla gösterilir; paket kurulmadıysa önceki bölümdeki
`dotnet run ... --` önekini kullanın. Tırnaklı ID yer tutucularını seçilmiş gerçek
ID ile değiştirin; shell yönlendirmesi değildir. Belirsiz isimlerde adaylar döner ve kesin ID
seçimi istenir; ilk aday seçilmez. Path tek bounded shortest path üretir. Impact
her düğüm için ters bağımlılık kanıt yolunu verir; REFERENCES/DECLARES yayılımı ve
runtime etki garantisi yoktur.

[Makinece okunabilir envelope schema](docs/schemas/cli-envelope.schema.json).
Query envelope: `schemaVersion: 4`, `snapshotId`, `analysisContext`, `completeness`,
`stale`, `truncated`, `items`, `diagnostics`; ayrıca etkin `options`, `limits` ve
`outcome` bulunur. `stale: notChecked` kaynakların güncel olduğunu **iddia etmez**.
`--check-freshness` content hashing ile kontrol eder; `--require-fresh` stale/unknown
durumunda exit 9 verir. Sorgular MSBuild/restore çalıştırmaz.
Derinlik/sonuç/çalışma/kanıt kesilmeleri kaybolmaz. `items[].entity` declaration'ı,
`items[].path[]` ilişki/occurrence kanıtını taşır. Ayrıntılı anlamlar, sınırlar,
deterministik sıra ve v3 geçişi [QUERIES](docs/QUERIES.md)'tedir.

Help düz metin; export seçilen JSON/NDJSON/Mermaid/DOT formatını üretir.
Diğer başarılı komutlar tek JSON stdout, ilerleme/hata stderr.
Fatal hatalarda stdout boştur. Index/status/doctor `contractVersion: 4` taşır;
SQLite tablo şeması v3'tür. Index/status analiz bağlamını `analysis` altında ve
entity/relationship/occurrence sayılarını korur.

Varlıklar: solution, project, assembly, namespace, type, method, constructor,
property, field, event, accessor, localFunction, lambda. Partial tanımlar bir ID ve birden
çok kaynak konumudur. Kimlikler ada/satıra/mutlak köke dayanmaz;
[canonical v2, rename ve imza sözleşmesi](docs/IDENTITY.md) testlerle sabittir.

DB başına tek snapshot vardır. Kısmi analiz kaydedilebilir ve önceki snapshot'ı
atomik değiştirir; `coverage: partial` index/status/query'de korunur. Hazırlık/fatal
analiz/yazma hatası önceki snapshot'ı korur. Yazma transaction, sorgular read-only'dir.
Schema v1/v2 status ile okunabilir; yeni query komutları açık yeniden index ister.
Başarılı yeniden index atomik olarak v3 yapar. Bilinmeyen dolu schema değiştirilmez. Eski identity v1 snapshot'ları okunabilir;
yeni index v2 üretir. Eski graph'ın kaynak güncelliği kontrol edilmez.

`query find` literal ad/nitelikli ad alt metin arar, ID'ye göre sıralar. SQLite ASCII
case folding kullanır; `%`, `_`, `'` parametreli sorguda SQL kalıbı değildir.
Boş sonuç yalnızca snapshot'ın belirtilen kapsamındaki eşleşme yokluğudur.

| Exit | Anlam |
| --- | --- |
| 0 | İşlem başarılı; **partial kayıt ve boş sorgu dahil**, coverage ayrıca okunmalıdır |
| 2 | Hatalı komut/seçenek/girdi/kök veya minimum context envelope için yetersiz bütçe |
| 3 | Güven bayrağı yok; hedef değerlendirilmedi |
| 4 | SDK/assets/index önkoşulu, desteklenmeyen giriş veya eksik TFM seçimi |
| 5 | Hiç analiz edilebilir compilation yok veya fatal analiz hatası |
| 6 | SQLite/şema/dosya erişim veya bozuk graph JSON hatası |
| 7 | Belirsiz sembol veya lexical aday; seçilen formatta adaylar, kesin ID seçimi gerekir |
| 8 | İstenen sembol aranan snapshot/kapsamda bulunamadı; outcome/metadata |
| 9 | `--require-fresh` güncellik şartı sağlanmadı; stale veya unknown |
| 10 / 130 | Başka writer var / işlem iptal edildi; otomatik retry yok |

`exact` belirtilen statik declaration/ilişki anlamının kanıtıdır, runtime garantisi değildir.
`completeWithinDeclaredScope` seçilen bağlam ve declaration/generated politikasıyla
sınırlıdır; freshness varsayılan `notChecked`, açık kontrolle raporlanır. Dynamic/delegate/reflection runtime hedefleri
unresolved kalır; olası dispatch implementasyonları exact CALLS yapılmaz. [Workspace/generator sınırları](docs/WORKSPACE.md),
[lisans envanteri](docs/DEPENDENCIES.md), [bildirimler](THIRD-PARTY-NOTICES.txt).

## Incremental indexing

`index --incremental` varsayılandır; `--full` her zaman bütün kapsamı yeniler.
İki modda da `--trust-project` gerekir. İçerik/dependency fingerprint'i eşitse
no-op Roslyn analizini atlar; değişiklikte tüm seçili projeler ve dependent'ları
analiz edilir. Custom build/generator dependency kümesi tamamlanamıyorsa gerekçe
raporlanır ve reuse yapılmaz. SHA-256 kullanılır, dosya tarihine güvenilmez.

Graph ve manifest atomik SQLite WAL transaction'ında yayınlanır. Tek writer lease
analizden commit'e kadar tutulur; concurrent readers tutarlı snapshot okur.
Komutlar, maliyet alanları, fallback ve freshness sınırları:
[Incremental sözleşmesi](docs/INCREMENTAL.md).
