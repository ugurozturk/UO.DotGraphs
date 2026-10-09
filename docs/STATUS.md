# Gerçekleşen durum

Son güncelleme: **2026-10-09**.

**Faz 11 sağlamlaştırması uygulandı; bütün release kabul kapıları kapatılmadı.**
Ürün **UO.Graphs 0.9.0**, paket **UO.Graphs.Cli**, komut **uographs**.
JSON envelope **4** / SQLite **3** / context-export **1** korunur.
Yeni ürün analiz alanı, servis veya dependency eklenmedi.
[Önceki faz](history/PHASE10.md), [release readiness ve kısa kullanım](RELEASE.md),
[ölçümler](PERFORMANCE.md), [güven sınırı](SECURITY.md).

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
- Git metadata yok; `git status --short` exit 128, Git diff **NOT RUN**.
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
