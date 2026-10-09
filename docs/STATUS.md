# Gerçekleşen durum

Son güncelleme: **2026-10-10**.

**Faz 11 sağlamlaştırması uygulandı; bütün release kabul kapıları kapatılmadı.**
Tanımlı sürüm **UO.Graphs 0.9.0**, paket **UO.Graphs.Cli**, komut **uographs**.
JSON envelope **4** / SQLite **3** / context-export **1** korunur.
Yeni ürün analiz alanı, servis veya dependency eklenmedi.
[Önceki faz](history/PHASE10.md), [release readiness ve kısa kullanım](RELEASE.md),
[ölçümler](PERFORMANCE.md), [güven sınırı](SECURITY.md).

## Ortak fixture SDK seçimi — 2026-10-10

[Linux CI 37991790585](https://github.com/ugurozturk/UO.DotGraphs/actions/runs/37991790585)
unit **55/55 PASS**, integration **94 PASS / 4 FAIL** verdi. Tam log'da
freshness hatasının sonunda `SDK outside the validated base-SDK dependency model`
tanısı da bulunuyor. Generator dışındaki üç hata aynı fingerprint eksikliğini
gösteriyor: `Complete` false, `--require-fresh` unknown reddi ve noOp yerine rebuild.

`CopyFixture` yalnız `samples/Phase2` altını geçici dizine kopyalıyordu; repo
kökündeki `global.json` kapsam dışında kalıyordu. Birden çok SDK bulunan runner'da
geçici proje için yapılan SDK seçimi repo pin'inden ayrılabiliyordu. Ortak yardımcı
artık repo `global.json` dosyasını fixture köküne de kopyalar. Önceki generator
düzeltmesindeki pin korunur. Ürünün bilinmeyen dependency durumunda reuse'u kapatan
kontrolü, freshness beklentileri veya timeout sınırları değiştirilmedi.

Yeni `CopiedFixtureSelectsRepositorySdkOutsideCheckout` regresyonu, geçici üst
dizinde çakışan `99.0.100` SDK talebi oluşturarak gerçek `dotnet --version` seçimini
sınar. Böylece yalnız tek SDK kurulu geliştirme makinesinde de eksik pin yakalanır.
Test düzeltme öncesinde **FAIL**: SDK resolver üst dizindeki `99.0.100` talebini
seçti (`sdk-selection-red.trx`). İlk test yazımındaki eksik temporary directory
hazırlığı ayrıca düzeltildi; o ilk failure SDK regresyonunun kanıtı sayılmaz.
Phase6 Complete assertion'ları artık fingerprint limitation nedenlerini gösterir;
incremental oracle'a ilk snapshot için Complete assertion'ı da eklendi.

Yerel macOS ARM64 / SDK 10.0.201 kayıtları `.uographs/ci-sdk-fixture-fix/` altında:

- `dotnet build UO.Graphs.slnx -c Release --no-restore -p:ContinuousIntegrationBuild=true -m:1 -nr:false -p:UseSharedCompilation=false`:
  **PASS**, 17.31 sn, 0 warning / 0 error.
- `dotnet test tests/UO.Graphs.UnitTests -c Release --no-build --no-restore -m:1 -nr:false --logger 'trx;LogFileName=unit.trx' --results-directory .uographs/ci-sdk-fixture-fix`:
  **PASS 55/55**, skipped 0, 6 sn.
- Kullanıcının açık onayıyla `dotnet msbuild samples/HelloGraph/HelloGraph.Domain/HelloGraph.Domain.csproj -nologo -getProperty:MSBuildToolsPath,MSBuildExtensionsPath,MSBuildSDKsPath`:
  **PASS**; tools/extensions `/usr/local/share/dotnet/sdk/10.0.201`, SDKs aynı
  dizinin `Sdks` altı. Restore/build hedefi verilmedi; bu macOS gözlemidir.

Son ortak-fixture düzeltmesinin Linux/Windows ve GitHub CI doğrulaması **NOT RUN**.
Yeni dependency/restore, commit/push/publish yok. Değişen dosyalar:
`Phase2Tests.cs`, `Phase6Tests.cs`, `docs/STATUS.md`.
Önerilen commit: `fix(tests): preserve pinned SDK in copied integration fixtures`

## Generator cancellation CI testi — 2026-10-10

Paylaşılan Linux CI çıktısında unit **55/55 PASS**; integration generator testi
başlangıç marker'ını 90 saniyede göremedi. Bu çıktı generator cancellation'ının
başarısız olduğunu değil, cancellation assertion'ına ulaşılamadığını gösterir.
Eski test macOS ARM64 üzerinde normal, `DOTNET_PROCESSOR_COUNT=2` ve `=1`
koşullarında ayrı ayrı **1/1 PASS**; Linux timeout'u yerelde yeniden üretilmedi.

`Phase11GeneratorCancellationTests.cs` içindeki test düzeni sağlamlaştırıldı:

- In-process index testi, process-wide MSBuild ortamını değiştiren CLI testleriyle
  çakışmaması için paralel çalışmayan ayrı xUnit collection'a alındı.
- Repo dışında oluşturulan fixture'a repo `global.json` dosyası kopyalanır;
  runner'ın en yeni SDK'sının örtük seçilmesi önlenir.
- Senkron generator ve marker/cancel bekleyişi xUnit synchronization context'ini
  paylaşmaz; gerçek `IncrementalIndexer.RunAsync` ayrı task'ta çalışır.
- Ürün ilerleme satırları süreleriyle test çıktısına ve timeout mesajına eklenir.
  90 saniyelik başlangıç, 10 saniyelik cancellation, 20 saniyelik cleanup sınırları
  ve token/observed/exception assertion'ları korunur. Ürün kodu değişmedi.

Doğrulama kayıtları `.uographs/generator-ci-fix/` altında. Release build:
`dotnet build UO.Graphs.slnx -c Release --no-restore -p:ContinuousIntegrationBuild=true -m:1 -nr:false -p:UseSharedCompilation=false`
**PASS**, 23.65 sn, 0 warning / 0 error.

Odaklı test komutları (macOS ARM64, SDK 10.0.201):

```sh
DOTNET_PROCESSOR_COUNT=2 dotnet test tests/UO.Graphs.IntegrationTests -c Release --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName~Phase11GeneratorCancellationTests|FullyQualifiedName~GeneratorsNeedPermissionAndGeneratedPolicyDoesNotRunDiagnosticAnalyzers|FullyQualifiedName~NewlyCreatedImplicitUserImportIsStaleAndRebindsCompilation|FullyQualifiedName~RealSolutionIndexPersistsTypeAndMethodAcrossProcesses' --logger 'console;verbosity=normal' --logger 'trx;LogFileName=focused.trx' --results-directory .uographs/generator-ci-fix
DOTNET_PROCESSOR_COUNT=1 dotnet test tests/UO.Graphs.IntegrationTests -c Release --no-build --no-restore -m:1 -nr:false --filter FullyQualifiedName~Phase11GeneratorCancellationTests --logger 'console;verbosity=normal' --logger 'trx;LogFileName=single-cpu.trx' --results-directory .uographs/generator-ci-fix
```

- İki işlemci koşulu: **PASS 4/4**, skipped 0, toplam 69.88 sn; generator **3.749 sn**.
  Gerçek CLI HelloGraph index ve ayrı proseslerde query/status smoke dahil.
  Aynı koşuda generator izin politikası ve geçici MSBuild user import testi geçti.
- Tek işlemci koşulu: **PASS 1/1**, skipped 0, toplam 6.07 sn.
- `focused.trx` içindeki generator çıktısında fingerprint/invalidation ve workspace
  loading satırlarının süreleri bulunduğu ayrıca kontrol edildi.
- `git diff --check`: **PASS**.

Linux/GitHub tekrar koşusu **NOT RUN**; bu değişiklik Linux kök nedeninin
doğrulandığı veya CI kabulünün kapandığı anlamına gelmez. Tam .NET suite tekrar
koşulmadı; yukarıdaki odaklı testler ilgili davranışları kapsar.
Yeni bağımlılık, restore, commit/push/publish yok.
Değişen dosyalar: integration `Phase11GeneratorCancellationTests.cs`, `docs/STATUS.md`.
Önerilen commit: `fix(tests): isolate generator cancellation acceptance from runner state`

## GitHub/NuGet yayın otomasyonu — 2026-10-09

Kullanıcının isteğiyle GitHub Actions build/test/pack ve NuGet Trusted Publishing
eklendi. `main` push başarılıysa `uozturk` hesabıyla otomatik yayınlanacak;
PR ve manuel CI publish yapmaz. Kalıcı API key yok. NuGet policy alanları ve
kurulum/yayın adımları [RELEASE](RELEASE.md#github-actions-ve-nuget-trusted-publishing)
ve [README](../README.md)'dedir. Canlı GitHub/OIDC/NuGet çalıştırması **NOT RUN**;
workflow oluşturulması NuGet policy'nin oluşturulduğu veya paketin yayımlandığı
anlamına gelmez. Commit/push/publish yapılmadı.

- `ci.yml`: Ubuntu 24.04, sabit SDK, locked restore, Release build ve standart
  VSTest, lisans kontrolü, pack ve yerel feed'den kurulan gerçek tool smoke.
- `publish-nuget.yml`: yalnız ilk taraf başarılı main push CI'ın run-id'sindeki
  artifact; kimlik/sürüm/commit/SHA-256 tekrar kontrolü; yeniden derleme yok.
- Kullanıcının düzeltmesiyle sürüm doğrudan `Directory.Build.props` içindeki
  `<Version>` değeridir; CI çalıştırma numarası eklenmez. Aynı sürüm restore/build/
  test/pack boyunca korunur. CLI help ve yeni graph `toolVersion` assembly sürümünü okur.
- Test harness'ler CLI ve contract fixture DLL'lerini test assembly'sinin gerçek
  Debug/Release yapılandırmasından seçer. Analiz edilen hedefin varsayılan Debug
  bağlamı değişmedi. Generator fixture referansı açıkça Debug olduğundan CI onu
  ayrıca Debug derler. Assertion veya test kaldırılmadı.
- Upload kapsamı nupkg, manifest ve TRX; graph/ham context/yerel evidence yok.
  Ürün dependency'si, runtime upload/restore veya yeni analiz alanı eklenmedi.

### Sabit sürüm düzeltmesinin doğrulaması

`Directory.Build.props` değiştirilmedi; mevcut `<Version>0.9.0</Version>` aynen
kullanılır. CI ve publish workflow'larından run-number girdisi kaldırıldı.
Yeni sürüm için kullanıcı Version değerini elle değiştirir; aynı sürüm NuGet'te
varsa `--skip-duplicate` ile atlanır. README ve RELEASE buna göre güncellendi.

Yerel macOS ARM64 kontrolleri, `.uographs/static-version/`:

- `python3 -B scripts/test_ci_package.py`: **PASS 9/9**; farklı CI run değerlerinde
  props sürümünün aynen korunduğu ve eksik/geçersiz sürümün reddedildiği dahil.
- `python3 -B scripts/verify_ci_package.py --print-version`: **0.9.0**.
- `dotnet build UO.Graphs.slnx -c Release --no-restore -p:ContinuousIntegrationBuild=true -m:1 -nr:false -p:UseSharedCompilation=false`:
  **PASS 21.93 sn**, 0 warning / 0 error; Version override verilmedi.
- `dotnet pack src/UO.Graphs.Cli -c Release --no-build --no-restore -p:ContinuousIntegrationBuild=true -m:1 -nr:false --output .uographs/static-version/packages`:
  **PASS**, `UO.Graphs.Cli.0.9.0.nupkg`.
- Paket doğrulayıcı `--version 0.9.0 --commit <HEAD> --write-manifest` ve
  `--verify-manifest`: **PASS**, SHA-256
  `5d236ea8e24d83f13480d5dc39e2aface9cbf35fa93c3d8391285f9de643aa3a`.
- `python3 -B scripts/smoke_package.py .uographs/static-version/packages --version 0.9.0 --evidence .uographs/static-version/smoke`:
  **PASS**, izole yerel install/help/doctor/trust reddi, yeni DB üzerinde
  index **8.357 sn / 15 entity**, query/context; help ve graph sürümü **0.9.0**.
- İki workflow YAML parse ve 11 shell bloğunda `bash -n`; `git diff --check`: **PASS**.
  Ürün C# kodu değişmediğinden tam .NET suite bu düzeltmede yeniden çalıştırılmadı.
  GitHub/OIDC/NuGet canlı yayın **NOT RUN**; commit/push/publish yapılmadı.

Bu düzeltmede değişen dosyalar: `ci.yml`, `publish-nuget.yml`,
`verify_ci_package.py`, `test_ci_package.py`, README, RELEASE ve STATUS.
Önerilen commit: `fix(ci): use the exact version from Directory.Build.props`

### Önceki yayın otomasyonu kontrolleri

Aşağıdaki 0.9.1 sürümü önceki yerel denemelerde açık komut parametresiyle verildi;
güncel sürüm kaynağı yukarıdaki props değeridir.
Yerel doğrulama (macOS ARM64), `.uographs/trusted-publishing/`:

| Komut/kontrol | Sonuç |
| --- | --- |
| `dotnet build UO.Graphs.slnx -c Release --no-restore -p:Version=0.9.1 -p:ContinuousIntegrationBuild=true -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, 0 warning / 0 error, ilk build 22.70 sn. |
| `dotnet test tests/UO.Graphs.UnitTests -c Release --no-build --no-restore -p:Version=0.9.1 -m:1 -nr:false --logger 'trx;LogFileName=release-unit.trx' --results-directory .uographs/trusted-publishing/vstest` | PASS 55/55, skipped 0, 6 sn. |
| `dotnet test tests/UO.Graphs.IntegrationTests -c Release --no-build --no-restore -p:Version=0.9.1 -m:1 -nr:false --logger 'trx;LogFileName=release-integration.trx' --results-directory .uographs/trusted-publishing/vstest` | PASS 98/98, skipped 0, 11m23s. Son sürüm çıktısı düzeltmesinden önceki binary; bu sırada rebuild/pack yapılmadı. |
| `dotnet pack src/UO.Graphs.Cli -c Release --no-build --no-restore -p:Version=0.9.1 -p:ContinuousIntegrationBuild=true -m:1 -nr:false --output .uographs/trusted-publishing/packages` | PASS, 218 entry; yerel 0.9.1 deneme paketi. |
| `verify_ci_package.py ... --version 0.9.1 --commit <HEAD> --write-manifest`, ardından `--verify-manifest` | PASS: paket kimliği/sürümü, MIT, repository commit, CLI/BuildHost/native SQLite payload, fixture dışlama ve SHA-256. |
| `python3 scripts/smoke_package.py .uographs/trusted-publishing/packages --version 0.9.1 --evidence .uographs/trusted-publishing/smoke` | İlk paket PASS: install/help/doctor, trust exit 3, yeni DB'de index 15 entity, query/context. Index 10.070 sn. Son sürüm çıktısı düzeltmesi sonrası tekrar aşağıdadır. |
| `python3 scripts/test_ci_package.py` / `python3 scripts/test_release_tools.py` | PASS 8/8 / 2/2. Sentetik paket negatifleri gerçek kurulumun yerine kullanılmadı. |
| `python3 scripts/audit_licenses.py --check` | PASS 143 kilitli paket; mevcut lisans kapanımları korunur. |
| Temiz kaynak kopyasında CI'ın üç restore hedefi, `--locked-mode -p:Version=0.9.1 -p:NuGetAudit=false -m:1 -nr:false` | PASS; solution, Phase2 fixtures ve Phase3. Yerel cache kontrolü; canlı NuGet vulnerability audit iddiası değil. CI audit'i kapatmaz. |
| YAML parse, 11 workflow shell bloğunda `bash -n`, Python AST, `git diff --check` | PASS. GitHub Actions runtime veya OIDC kabulü değildir. |

Son sürüm çıktısı değişikliği sonrası, önceki tam suite tamamlandıktan sonra:

| Takip kontrolü | Sonuç |
| --- | --- |
| Aynı Release build komutu, son kaynaklar | PASS 8.89 sn, 0 warning / 0 error. |
| Aynı unit komutu, `final-unit.trx` | PASS 55/55, skipped 0, 7 sn. |
| Aynı integration komutu; `--filter 'FullyQualifiedName~RealSolutionIndexPersistsTypeAndMethodAcrossProcesses\|FullyQualifiedName~HelpAndDoctorRunWithoutLoadingTarget'`, `final-version-cli.trx` | PASS 2/2, skipped 0, 12 sn; help ve graph sürüm assertion'ları dahil. Tam 98 test son sürüm metni değişikliğinden sonra yeniden çalıştırılmadı. |
| Aynı pack/verifier komutları, çıktı `final-packages/` | PASS; final manifest SHA-256 `b7c0a48c27fc393af532c31f965791ad19f0b68c6c8e8cfc40683cf95d528f24`. |
| `python3 scripts/smoke_package.py .uographs/trusted-publishing/final-packages --version 0.9.1 --evidence .uographs/trusted-publishing/final-smoke` | PASS: yerel-only install 0.791 sn; help/graph sürümü 0.9.1; trust reddi; sıfırdan index 8.593 sn / 15 entity; ayrı query/context prosesleri. |
| Son lisans/diff/yerel Markdown link kontrolü | PASS; 143 paket, eksik yerel link yok. |

Test deneme paketi, henüz commit edilmemiş çalışma ağacından üretildi; yerel
nuspec commit alanı mevcut HEAD'i gösterir, bu bir yayımlanmış commit/paket değildir.
GitHub CI temiz checkout'un commit'ini ve aynı çalışmanın paketini doğrular.
Eski Faz 11 IPC başarısızlıkları aşağıda tarihsel kanıt olarak korunur; bu tur
izinli yerel koşulda kurulu tool index'i geçti. Linux CI, Windows, fiziksel offline
ve gerçek NuGet yayın kapıları bu yerel başarıdan çıkarılmaz.

Değişen dosyalar: iki `.github/workflows` dosyası; `verify_ci_package.py`,
`smoke_package.py`, `test_ci_package.py`; CLI csproj/Program, Core/ProductVersion,
Roslyn/ProjectIndexer; integration CliTests/Phase6Tests ve unit MessagingTests;
README, RELEASE, STATUS, DEPENDENCIES ve lisans envanteri scriptinin başlık metni.
Önerilen commit: `ci: publish verified UO.Graphs tool packages with NuGet trusted publishing`

## Değişiklikler ve somut kanıt

- Parametrik sentetik solution üreticisi: 10/100/1000 proje, sınıf/method boyutu,
  star project dependency topolojisi. Üç dataset üretildi; 10 ve 100 gerçek analiz
  edildi. 1000 index **NOT RUN**; üretim analiz başarısı sayılmadı.
- Ölçülmüş repeated-solution-load maliyeti için aynı TFM, generator kapalı
  solution'da bir workspace ve analyzer-free immutable Solution paylaşılır.
  Karışık TFM/generator/project input yolu ayrı kalır. Initial shared-workspace
  load başarısızsa aynı hatayı N proje için yeniden beklemeden exit 5 verilir.
- Aynı kaynağın farklı reference bağlamları artık aliases/EmbedInteropTypes,
  PE module MVID ve recursive compilation referanslarıyla ayrılır. Yanlış exact
  bağlam birleşimine karşı unit regresyonu eklendi.
- Context bütçesinde seçili sembole yakın akış/framework ve uncertainty kanıtı
  uzak generic ilişkilerden önce korunur. Önceki Refit 50.000 karakter testi
  assertion veya bütçe gevşetilmeden geçti.
- `ProjectPreparation` dotnet/MSBuild subprocess stdout/stderr'ı sınırlı;
  timeout/cancellation parent exit sonrası pipe
  drain'i de kapsar. Gerçek parent/child, post-exit pipe handshake ve büyük stderr
  regresyonları geçti. Gerçek index yolundaki kooperatif generator token'ı alıp
  cancellation'ı gözlemledi; token'ı yok sayan generator için zorla durdurma iddiası yok.
- SQLite FULL rollback/retry, bozuk config/non-DB, sidecar symlink reddi testleri;
  gerçek SIGINT/SIGKILL/concurrent-writer scripti. Export için exclusive helper.
- Tool paket metadata'sı, yerel nupkg feed ve izole temporary tool-path kurulum.
  MIT/third-party metinleri, SDK/offline hazırlık, self-hosted Azure örneği;
  standalone Python doğrulama scriptleri. Ayrı geçici local manifest kurulum/read
  smoke da geçti; kullanıcının global tool/manifest'i değiştirilmedi.
- README/help/schema/skill komutları sürüm 0.9.0 sözleşmesiyle uyumlu.
  Schema üst zarf içindir, iç graph metadata'sı genişletilebilir. Skill/template
  parity ve gerçek read akışı geçti; Copilot model E2E hâlâ **NOT RUN**.

## Performans — ölçüm kapsamı

macOS **27.0.1 ARM64**, **24 logical CPU / 64 GiB RAM**, SDK **10.0.201**,
runtime **10.0.5**. CPU model sysctl/proses listesi ps izinleri yok.
Raw kayıtlar `.uographs/phase11-evidence/` altında; donanım/SDK, boyut,
önceden kaydedilmiş bütçeler, gerçek argv/stdout/stderr ve tekrar sayıları korunur.

| Koşu | Fingerprint ms | Analysis + verify ms | Sonuç |
| --- | ---: | ---: | --- |
| 10 proje, paylaşım öncesi full | 12667 | 27338 | PASS; 401 / 1305 / 1335 |
| 10 proje, paylaşım sonrası full | 13360 | 16129 | PASS; aynı graph içerikleri |
| 10 proje warm incremental | 14356 | 0 | PASS; noOp / 0 proje |
| 10 proje leaf değişimi | 13903 | 17035 | PASS; bütün 10 proje yeniden analiz |
| 10 proje dependent invalidation | 13933 | 16334 | PASS; bütün 10 proje yeniden analiz |
| 100 proje full | 122481 | 136951 | PASS; 4001 / 13635 / 13935 |
| 100 proje warm incremental | 111733 | 0 | PASS; noOp / 0 proje |

Index senaryoları birer başarılı tekrar. Süreler CLI timer'ıdır; startup ve SQLite
publication dahil wall time değildir. Boş DB/full ile warm/no-op ayrıdır;
OS cache boşaltılmadı, fiziksel cache-cold **NOT RUN**. Karşılaştırmalar paralel
çalışma/cache koşulları nedeniyle istatistiksel yüzde iyileşme iddiası değildir.
10-proje SQLite **6.631.424 B**, 100-proje **53.084.160 B**.
10-proje query/context üçer tekrar **0.118–0.225 sn**; 100-proje query/context
(50.000 karakter) üçer tekrar **0.213–0.218 sn**. Yeni CLI prosesleri ve 100 ms
harness polling dahil yaklaşık wall süresi; SQL-only microbenchmark değildir.
100-proje 12.000 karakter context, minimum zarf 27.146 karakter olduğu için
beklenen exit 2 verdi; bu negatif koşu daha büyük bütçeli başarıdan ayrı tutulur.
Peak process/child-process memory **NOT RUN**; fiziksel RAM proses tüketimi değildir.
Token tasarrufu ölçülmedi/iddia edilmedi.

## Gerçek doğrulama

Standart VSTest ve reflection sonuçları birbirine karıştırılmadı.
İlk teslim koşuları aşağıda korunur; son test kaynaklarıyla takip sonuçları ayrı
tabloda yer alır.

| Komut/kontrol | Sonuç |
| --- | --- |
| `dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, 0 warning / 0 error; final build **12.83 sn**. |
| `dotnet test tests/UO.Graphs.UnitTests --no-build --no-restore -m:1 -nr:false --logger 'trx;LogFileName=release-unit.trx' --results-directory .uographs/phase11-evidence/vstest` | PASS **52/52**, skipped 0, yaklaşık 5 sn. |
| Final binary odaklı integration, `release-integration.trx` | PASS **16/16**, 48 sn; gerçek index, trust/custom target, config/storage, Phase9 ve context regression. |
| Tam integration suite, `full-integration.trx` | **94 PASS / 3 FAIL**, 97 test, **11m25s**. Ara koşu; aşağıdaki düzeltme/tekrarlarla karıştırılmaz. |
| Incremental/full oracle tekrar, `final-incremental-oracle.trx` | PASS **1/1**, **5m1s**; mutation dizisi/no-op, sabit binary ile. |
| Phase9 gerçek paket testleri, `context-regression.trx` | PASS **3/3**, 26 sn. ABP, EF Core, Refit. Önceki fazdaki assertion kapısı bu gerçek koşuda kapandı. |
| `final-focused.trx` | PASS **11/11**, 16 sn; context budget/bounds, farklı TFM, bozuk config, SQLITE_FULL, symlink/non-DB. |
| `hardening.trx` | PASS **14/14**, 539 ms; storage schema/SQL/rollback/concurrency + yeni storage hata testleri. |
| `regression.trx` | PASS **8/8**, 20 sn; gerçek HelloGraph, MSBuild marker/trust, Phase6 storage/parse. |
| `python3 scripts/test_process_safety.py ...` | PASS: gerçek SIGINT **130**, SIGKILL **-9**, concurrent writer **10**, gerçek DB backup snapshot korunur; iki ayrı koşu. |
| `python3 scripts/test_skill.py --db .uographs/phase11-live.db --evidence .uographs/phase11-evidence/skill-read-tests.json` | PASS **10/10**, 14.168 sn. |
| `python3 scripts/test_release_tools.py` | PASS **2/2**, generator boyut/ref/overwrite + export exclusive/literal/symlink. |
| `python3 scripts/audit_licenses.py --check` | PASS **143** kilitli paket/sürüm; yeni dependency yok. |
| `python3 scripts/check_contracts.py --db .uographs/phase11-evidence/perf-direct-100-final.db --index-raw .uographs/phase11-evidence/optimized-direct-10.raw --evidence .uographs/phase11-evidence/contracts` | PASS **6 gerçek envelope + 6 yanlış sürüm negatif**. Yalnız schema'da kullanılan keyword subset'i; genel JSON Schema validator conformance iddiası değil. |
| Kaynak bütünlüğü / belge kontrolü | PASS: HelloGraph’ın 5 özgün kaynak/proje/solution dosyası ilk index hash’leriyle aynı; Python AST, yerel Markdown linkleri, whitespace, pipeline YAML parse. CI execution değildir. |
| Bağımsız correctness ve security/test-gap ajanları | İki gerçek salt-okunur inceleme. Reference context, cancellation/output, context pruning ve harness false-pass bulguları giderildi. |

### Takip doğrulamaları

Raw kanıtlar `.uographs/phase11-followup/` altında, komutlar `commands.json` ve
VSTest sonuçları `vstest/` içindedir. Ürün binary'leri değişmedi; testler eklendi.

| Kontrol | Sonuç |
| --- | --- |
| Aynı build komutu, son test kaynakları | PASS **14.31 sn**, 0 warning / 0 error. |
| Son unit suite, `followup-final-unit.trx` | PASS **55/55**, skipped 0; gerçek subprocess testleri dahil. |
| Sabit binary ile tam integration, `followup-full-integration.trx` | PASS **98/98**, skipped 0, **11m35s**; generator cancellation **4.994 sn**. |
| Son test kaynaklarıyla tam integration, `followup-final-full-integration.trx` | PASS **98/98**, skipped 0, **11m34s**. Eşzamanlı build/pack yapılmadı. |
| Odaklı generator invocation, `generator-cancellation.trx` ve `final-generator-cancellation.trx` | VSTest socket bind **Permission denied** nedeniyle test başlamadan **ABORTED**; başarılı tam suite koşusundan ayrı tutulur. |
| `test_release_tools.py` / `audit_licenses.py --check` | PASS **2/2** / **143** kilitli paket. |
| Güncellenmiş skill/template ve gerçek CLI read testleri | PASS **10/10**, **12.831 sn**; `skill-read-tests.json`. |
| Belge kontrolü | **29** Markdown belgesinde eksik yerel link yok; arşivdeki 3 göreli link düzeltildi. Python AST, whitespace ve skill parity geçti. |
| İzole local manifest install/help/query/context | PASS; ayrı CLI home/cache, yalnız yerel nupkg. Repo tarafından üretilmiş DB okunur; index **NOT RUN**. |
| Bağımsız tekrar incelemesi | Pipe scheduling false-pass ve cleanup timeout açığı giderildi; son kaynak incelemesinde kritik bulgu yok. |

İnceleme sonrası unit pipe handshake ve test cleanup kontrolleri güçlendirildi.
Son unit ve tam integration suite bu kaynakları çalıştırdı; önceki koşular silinmedi.
Ürün DLL'leri ve HelloGraph'ın 5 özgün kaynak/proje/solution dosyası değişmedi:
`stable-inputs-final-build.json` ve `current-package-parity.json`.

Tam suite'deki üç hata: Refit context kırpması (düzeltildi, 3/3 tekrar PASS), farklı
TFM testi sırasında binary rebuild (input guard reddi, 11/11 tekrar PASS), incremental
oracle sırasında pack'in publish altındaki binary'leri değiştirmesi (input guard
reddi; sabit binary tekrar **1/1 PASS**). Eşzamanlı build/pack ile index çalıştırılmaması gerekir. Test/assertion
silinmedi veya gevşetilmedi. Önceki 94/97 sonucu takipteki temiz 98/98 koşusuyla
karıştırılmaz; yeni test nedeniyle toplam sayı değişti.

Tam suite'de **WAL publication ortasında process kill**, analysis sırasında kill,
storage cancellation, literal SQL/shell payload, source snippet/traversal,
large-output/context bütçeleri ve concurrent reader/writer testleri geçti.
SQLITE_FULL gerçek SQLite hata kodu 13 enjeksiyonudur; fiziksel disk doldurma kabulü
değildir. MSBuild custom target gerçek controlled marker yazdı; trust yokken
çalışmadı, kaynak proje metni değişmedi. Kurum sandbox'ının yerine geçmez.

## Paket ve smoke

`dotnet pack src/UO.Graphs.Cli -c Debug --no-build --no-restore -m:1 -nr:false -o ...`
ile local/global-tool biçiminde nupkg üretildi. Başlangıç arşivi 187 entry /
46.219.482 B; teslim arşivi **218 entry / 46.350.948 B**, SHA-256
`4180eb28775f4c1fd0179159194e493de83933514899802c73feb4529caf7041`.
İlk teslim arşivi: `.uographs/phase11-evidence/delivery-feed/UO.Graphs.Cli.0.9.0.nupkg`;
manifest `package-final.json`. Dağıtılmış skill kurulum/overwrite reddi ayrıca geçti. Native SQLite RID ve
BuildHost-netcore payload'ı denetlendi, ABP fixture DLL'leri üründe yok.

Güncellenmiş belge ve skill başvurusunu taşıyan takip arşivi:
`.uographs/phase11-followup/feed/UO.Graphs.Cli.0.9.0.nupkg`.
Arşiv SHA-256, entry listesi, dört ürün DLL'i ve belge parity sonuçları
`phase11-followup/package.json`; izole tool-path install/read smoke raw sonuçları
`phase11-followup/refreshed-tool/commands.json` içindedir. Önceki arşiv korunur.

Yerel-only NuGet.Config üzerinden `dotnet tool install UO.Graphs.Cli --version 0.9.0
--tool-path <fresh-temp>/bin --configfile <fresh-temp>/NuGet.Config --no-cache` PASS.
Final paket ayrı temporary tool-path’e, yalnız yerel feed ve ayrı temporary NuGet
cache ile yeniden kuruldu. Global kurulum değiştirilmedi. Kurulu `uographs --help`, gerçek snapshot üzerinde
query ve context PASS. **Kurulu tool'un index denemesi FAIL exit 5 (workspace IPC
timeout)**; final teslim paketi denemesi **63.939 sn**, `delivery-tool-smoke/raw.jsonl`.
Paketli index→query→context zinciri tamamlandı diye sunulmaz.
Takip incelemesinde kurulu ve repo BuildHost ayrı, süre sınırlı başlangıç
probe'larında runtime **10.0.5** açtı; ikisi de named pipe bind sırasında
**SocketException (13): Permission denied**, exit **-6** verdi. BuildHost'un
**20/20 dosyası SHA-256 eşit**. Raw stderr/hash kanıtı:
`.uographs/phase11-followup/buildhost-probe-locale.json`. Yerel IPC reddi bu
invocation için doğrulandı; izin aşılmadı, paketli index kabulü hâlâ açık.
Doğrudan repo CLI HelloGraph full PASS **15 / 20 / 23**; aynı DB ayrı proseslerde
query/context/skill testlerinde okundu. Paket read smoke'ı repo tarafından üretilmiş
DB kullanır; bu ayrım açıktır.

## Açık kapılar / NOT RUN / backlog

- Windows/Linux build/test/tool runtime ve Azure self-hosted pipeline **NOT RUN**.
  Global scope kurulumu **NOT RUN**; temporary tool-path ve ayrı geçici local
  manifest kurulumları canlı test edildi. Local manifest help/query/context PASS,
  index **NOT RUN**; repo DB'si kullanıldı. Raw komutlar ve çıktılar
  `phase11-followup/local-manifest/` altında. NuGet publish/git push yapılmadı.
- Fiziksel ağ izolasyonu altında hazır SDK/assets offline smoke **NOT RUN**.
  Fiziksel disk dolumu, process/child RSS ve OS-cache-cold **NOT RUN**.
- 1000-project analiz; 100-project leaf/dependent koşuları **NOT RUN**.
  Template/generator bunun yerine başarı sayılmaz. Kooperatif generator ve gerçek
  post-exit pipe cancellation testleri geçti; kötü niyetli generator'ın zorla
  durdurulması doğrulanmadı. Yeniden parent atanmış child için kurum process
  izolasyonu/cleanup gerekir.
- Python/redirected/DLL workspace invocation bazı koşullarda timeout verir.
  Başarılı direct CLI sonuçları bunları ortadan kaldırmaz. Kısıtlı terminalde
  process inventory/signal izni sınırlı. Önceki uzun redirected 100-project
  denemesinde `kill -INT 62720` reddedilmişti. Takip kontrolünde `lsof -p 62720`
  kayıt döndürmedi ve `perf-100.db.writer.lock` üzerinde nonblocking exclusive
  lease alınabildi/bırakıldı (`phase11-followup/prior-lease.json`). Eski PID'ye
  tekrar sinyal gönderilmedi; bu başarısız index koşusu başarı sayılmaz.
- Copilot E2E önceki fazdan **NOT RUN**. Test edilmeyen CI/OS'a başarı atfedilmez.
- İlk Faz 11 koşusunda Git metadata yoktu; o koşuda Git diff **NOT RUN**.
  Yayın otomasyonu turunda Git repository ve origin mevcut, diff kontrol edildi.
  AGENTS.md korunur; otomatik commit/push/publish yok.
- Backlog: izinli izole agent'ta paketli/offline/OS matrisi; güvenilir process-tree
  RSS; 1000-project baseline; metadata zarf büyümesi; SDK/binary hashing ve
  project başına evaluation maliyetini correctness'tan ödün vermeden azaltma.

## Değişen dosyalar

- Ürün: `src/UO.Graphs.Roslyn/ProjectIndexer.cs`, `ProjectPreparation.cs`,
  `src/UO.Graphs.Core/DeliveryWriter.cs`, `src/UO.Graphs.Storage.Sqlite/GraphStore.cs`,
  `AssemblyInfo.cs`, `src/UO.Graphs.Cli/UO.Graphs.Cli.csproj`.
- Test: iki test projesindeki `Phase11Tests.cs`, unit `Phase11ProcessTests.cs`,
  integration `Phase11GeneratorCancellationTests.cs`; mevcut testler korunur.
- Script: `synthetic_solution.py`, `benchmark.py`, `verify_release.py`,
  `safe_export.py`, `test_release_tools.py`, `test_process_safety.py`, `check_contracts.py`.
- Dağıtım/belge: `ci/azure-pipelines.onprem.yml`, README, ARCHITECTURE,
  CONTEXT-EXPORT, RELEASE, PERFORMANCE, SECURITY, STATUS, history/PHASE10,
  schemas/cli-envelope.schema.json; repo skill/template CLI başvurusu.

Önerilen commit: `fix: harden indexing and package the uographs CLI with release evidence`
