# Gerçekleşen durum

Son güncelleme: 2026-10-08.

**Faz 2, aşağıdaki macOS ARM64 workspace/sembol kabul kapsamında tamamlandı.**
Faz 3–11 başlamadı. Faz 1'in gerçek index → SQLite → ayrı proses query/status
akışı korunuyor. [Faz 1 kanıt kaydı](PHASE1.md) arşivlendi; oradaki mutlak
yol kimlikleri ve 6 düğümlü kapsam tarihsel bilgidir.

## Uygulananlar

- SDK-style C# `.sln`, `.slnx`, `.csproj`, gerçek çok projeli/çok TFM'li fixture'lar.
- Güven kapısı; kurulu SDK/global.json seçimi; değerlendirilmiş MSBuild path/version;
  Locator register-before-load, NoInlining ve Roslyn assembly sürüm eşliği kontrolü.
- Configuration/TFM/conditional Compile ve ProjectReference; seçilmeyen TFM listesi.
  Referansın compilation bağlamı ile indekslenen hedef farklıysa ayrı kayıt + partial.
- Solution/project/assembly/namespace/type/method/constructor/property/field/event;
  primary constructor, accessor ve local function; partial tanımlarda çoklu kaynak.
- Canonical identity v2; kök ve boş satırdan bağımsızlık, arity/overload/explicit
  interface/nested/operator/extension/file-local/lexical scope ayrımı.
- Workspace/compilation/document/project/generator diagnostics; eksik dependency,
  desteklenmeyen proje ve kısmi analiz. Hazırlık/fatal hatada eski snapshot korunur;
  kullanılabilir kısmi analiz açık `partial` etiketiyle atomik olarak kaydedilebilir.
- Generator yürütme ayrı `--allow-generators` izni; varsayılanda tüm projelerden
  analyzer referansları kaldırılır, olası eksik generated çıktı partial ile görünür.
  GeneratorDriver diagnostics ve AdditionalFiles/analyzer config kullanımı; diagnostic
  analyzer çalıştırılmaz. Generated include/exclude yalnızca workspace dokümanlarını
  kullanır, obj/bin taraması yoktur.
- JSON sözleşmesi v2, SQLite tablo şeması v1; eski snapshot kimlik sürümü v1 kalır.
- Ürün paket sürümleri değişmedi. Fixture için CSharp 5.0.0 doğrudan sabitlendi;
  14 lock dosyasındaki 50 doğrudan/geçişli paket ve native lisanslar denetlendi.

Sözleşmeler: [IDENTITY](../IDENTITY.md), [WORKSPACE](../WORKSPACE.md), [README](../../README.md).

## Gerçek doğrulama

Ortam: macOS 27.0 ARM64 / osx-arm64, SDK 10.0.201, MSBuild 18.3.0,
runtime 10.0.5; Roslyn runtime assembly seti 5.0.0.0.

| Gerçek komut/kontrol | Sonuç |
| --- | --- |
| `dotnet --info` | PASS, exit 0; yukarıdaki kurulu sürümler. |
| `dotnet restore UO.Graphs.slnx --locked-mode -m:1 -nr:false` | PASS, exit 0; 8 proje, NuGet warning/error yok. |
| `dotnet restore samples/Phase2/Fixtures.slnx --locked-mode -m:1 -nr:false` | PASS, exit 0; 6 proje. Bilerek eksik Missing.csproj bildirilir; son restore NuGet warning/error yok. |
| `dotnet build UO.Graphs.slnx --no-restore -t:Rebuild -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, exit 0; 0 warning/error, 10.45 saniye. |
| `dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, son kaynak/test değişiklikleriyle exit 0; 0 warning/error, 10.02 saniye. |
| `dotnet build src/UO.Graphs.Cli/UO.Graphs.Cli.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, exit 0; 0 warning/error, 5.48 saniye. |
| `dotnet build samples/Phase2/Generator/Generator.csproj --no-restore -t:Rebuild -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, exit 0; 4 açık fixture warning (2 × RS1041, 2 × RS1036), 0 error; 0.81 saniye. Warning suppression yok; host sınırı WORKSPACE'de. |
| `dotnet test UO.Graphs.slnx --no-build --no-restore -m:1 -nr:false` | PASS, exit 0; 25 integration + 3 unit = **28/28**, skipped 0. Integration 1 dk 21 sn. |
| `python3 scripts/audit_licenses.py --check` | PASS, exit 0; **50** kilitli paket, MIT/Apache-2.0/SQLite public domain. |
| XML ve kaynak whitespace kontrolü (Python) | PASS, exit 0; 22 XML dosyası ve 14 lock dosyası. |

Gerçek CLI smoke komutları (kurulu tool varsayılmadı):

```sh
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --trust-project --db .uographs/phase2-hello.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- query find Greet --format json --db .uographs/phase2-hello.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- status --db .uographs/phase2-hello.db
```

Üçü de **PASS / exit 0**. Index: 2 proje, 15 düğüm; snapshot
`7d50697401dc4814a627a62aecf757fd`. Bağımsız yeni proseslerde aynı snapshot ve
`M:HelloGraph.Domain.Greeter.Greet(System.String)` bulundu; identity v2 ve düğüm
sayısı ayrıca JSON assertion ile doğrulandı. Varsayılan generator yasağı nedeniyle
coverage `partial`; eksik çıktıyı gizleyen complete sonucu üretilmedi.

Faz 2 fixture CLI smoke:

```sh
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/Phase2/Phase2.slnx --trust-project --framework net10.0 --allow-generators --db .uographs/phase2.db
```

**PASS / exit 0**; o anki fixture revizyonunda 50 düğüm, 2 proje ve
`completeWithinDeclaredScope`. Daha sonra eklenen file-local/primary constructor
ve conditional reference fixture'ları son 28/28 test koşusunda doğrulandı.

Release JIT/Locator smoke:

```sh
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj -c Release --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --trust-project --allow-generators --db .uographs/phase2-release-direct.db
```

**PASS / exit 0**; 15 düğüm, 2 proje, `completeWithinDeclaredScope`, snapshot
`ee6852d229b14f0383ccfce07ed66f5a`. Release binary'de Locator registration/yükleme
ve Roslyn paketi uyumu gerçek CLI çalışmasıyla doğrulandı.

## Kabul testlerinin kapsamı

- `.sln`, `.slnx`, `.csproj` gerçek CLI prosesleri aynı source/project kimliklerini
  üretir; ProjectReference takip edilir, SDK/MSBuild/Roslyn sürümleri assert edilir.
- İki ayrı köke kopyalanan fixture ve bütün C# dosyalarına eklenen boş satırlarda
  tüm declaration ID'leri aynıdır; partial tip iki kaynak konumunu korur, satır/hash
  gerçekten değişir. File-local ve local function kimlikleri de karşılaştırılır.
- Overload/generic arity/explicit interface/operator/extension/nested/partial
  assertions; constructor, accessor, field, enum member, event, delegate kapsamı.
- net10.0/Debug ve netstandard2.1/Release karışmaz; conditional type, Compile öğesi
  ve Release-only ProjectReference gerçek compilation üzerinden doğrulanır.
- Aynı solution'da referansın seçtiği TFM farklıysa `referenceContextMismatch` + partial.
- Eksik proje/dosya/CS0246 ve unsupported legacy/VB solution öğeleri complete üretmez.
- Generator izinsizken marker dosyası oluşmaz; izinle oluşur ve generated tip çıkar.
  Diagnostic analyzer marker'ı izinli senaryoda da oluşmaz. Generated exclude,
  include, disk taramama ve exception diagnostics davranışları assert edilir.
- Eski güven/SDK/assets/CLI hata, kalıcılık, literal arama, snapshot koruma ve gerçek
  SQLite constraint rollback testleri korunur. Assertion gevşetilmedi/test silinmedi.

## Karşılaşılan ve çözülen sorunlar

- İlk test koşusu eksik Compile dosyasının Roslyn tarafından sessizce atlandığını
  gösterdi; değerlendirilmiş Compile öğeleri ve workspace file existence denetimi eklendi.
- Fixture'ın dış Directory.Packages.props import'u taşınma testini bozdu; fixture
  kendi sabit paket dosyasıyla bağımsız hale getirildi. Son taşınma testi geçti.
- Eski generator API/host denemesi RS1042 verdi; fixture incremental generator oldu.
  netstandard2.0 denemesindeki Microsoft.NETCore.Platforms 1.1.0 eski Microsoft
  lisansı sessizce kabul edilmedi; bu bağımlılık nihai setten çıkarıldı. Fixture
  netstandard2.1 ile ürünün .NET 10 host'unda test edildi. RS1041/RS1036 saklanıyor.
- Ara restore'da NU1900 (NuGet audit servis erişimi) ve geçici merkezi paket
  değişikliğinde NU1004 görüldü. Fixture paketleri izole edildi; son iki locked
  restore warning/error olmadan geçti. Paket lisans denetimi ayrıca başarılı.

- Ayrı Release denemesi:
  `dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj -c Release --no-build --no-restore -- index samples/Phase2/Phase2.sln --trust-project --framework net10.0 --allow-generators --db .uographs/phase2-release-cli.db > .uographs/phase2-release-cli.json`
  → **FAIL / exit 5**: iki proje için build-host yükleme zaman aşımı; compilation
  alınamadı ve snapshot yazılmadı. Ardından yukarıdaki doğrudan Release CLI çalıştı.
  Bu, yönlendirmeli çağrının nedenine ilişkin kesin teşhis veya başarılı kabul
  olarak sunulmaz; Faz 1'de benzer ortam davranışı da kaydedilmişti.

## Sınırlar / NOT RUN

- Windows/Linux build/test/CLI: **NOT RUN**, bu ortam macOS.
- Ağ işletim sistemi düzeyinde kapalıyken kabul: **NOT RUN**. Ürün otomatik edinim
  çağırmaz; güven verilen custom MSBuild/generator için ağ izolasyonu iddiası yoktur.
- Başka SDK/MSBuild/Roslyn sürümleri ve farklı SDK'lı karma solution: **NOT RUN**;
  karma SDK açık reddedilir. Paket uyumluluğu doğrulanan sabit setle sınırlıdır.
- Solution'ın proje bazında farklı configuration/platform eşlemeleri ve RID
  matrisi uygulanmaz; seçilen configuration globaldir, snapshot limitation taşır.
- Generated sınıflandırması belgelenen path/header kurallarıyla sınırlı; sadece
  seçili TFM/konfigürasyon analiz edilir. Local function yapısal scope düzenlemeleri
  kimliği değiştirebilir. Örtük üyeler/lambdalar ve ilişkiler kapsam dışıdır.
- Generator fixture taşınabilir VS/.NET Framework analyzer paketi değildir.
  Uyarılar bastırılmadı; ürünün .NET 10 host'undaki çalışma testlidir.
- Git repository'si yok; `git diff --check`: **NOT RUN**, git status exit 128.
  XML/whitespace ayrıca kontrol edildi. Tool publish/platform dağıtımı yapılmadı.

## Değişen dosyalar ve commit önerisi

- Güncellendi: `Directory.Build.props` (ürün sürümü 0.2.0), `README.md`.
- Güncellendi: `src/UO.Graphs.Core/GraphSnapshot.cs`, Roslyn `ProjectPreparation.cs`,
  `ProjectIndexer.cs`, CLI `Program.cs`; yeni `SymbolCollector.cs`.
- Güncellendi: mevcut unit/CLI tests; yeni `tests/UO.Graphs.IntegrationTests/Phase2Tests.cs`.
- Yeni: `samples/Phase2/` bağımsız ayarlar, solution'lar, 6 proje, gerçek kaynaklar ve lock dosyaları.
- Yeni: `docs/IDENTITY.md`, `docs/WORKSPACE.md`, `docs/history/PHASE1.md`.
- Güncellendi: `docs/STATUS.md`, `docs/ARCHITECTURE.md`, `scripts/audit_licenses.py`,
  `docs/DEPENDENCIES.md`, `THIRD-PARTY-NOTICES.txt`.
- `AGENTS.md`, kullanıcı dosyaları ve SQLite storage uygulaması korunur.

Önerilen commit: `feat: implement phase 2 workspace contexts and portable symbol identities`

Commit/push/publish yapılmadı. Faz 3 başlatılmadı.
