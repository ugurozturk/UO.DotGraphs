# Gerçekleşen durum

Son güncelleme: 2026-10-08.

## Tamamlananlar

**Faz 1, macOS ARM64 üzerindeki dar uçtan uca kabul kapsamında tamamlandı.**
Faz 2–11 başlamadı; geniş sembol/ilişki, incremental index ve tool paketleme uygulanmadı.
Hazırlık/Faz 0 belgeleri korunuyor. Başlangıçta uygulama kodu ve Git deposu yoktu;
Git başlatılmadı, mevcut `.DS_Store` dosyaları ve `AGENTS.md` değiştirilmedi.

- Core: gerçek snapshot, proje bağlamı, varlık, kaynak kanıtı ve confidence sözleşmeleri.
- Roslyn: güven kapısı → kurulu SDK/var olan assets → gerçek MSBuildWorkspace →
  project/type/method tanımları. Proje referansları yüklenir; otomatik restore yoktur.
- SQLite: schema v1, tek snapshot, atomik transaction, salt okunur sorgu/status;
  başarısız analizde eski snapshot korunur, yazma hatasında rollback yapılır.
- CLI: `--help`, `doctor`, `index ... --trust-project`, `status`, `query find ... --format json`,
  `--db`. JSON stdout, ilerleme/hata stderr. Çıkış kodları ve JSON v1 README'de.
- .NET 10.0.201 / C# 14 / nullable / merkezi paketler / sekiz lock dosyası / editorconfig.
- İki test projesi ve gerçek iki projeli `samples/HelloGraph/HelloGraph.slnx`.
- MIT ürün lisansı; 42 doğrudan/geçişli paketin gerçek nuspec lisans kontrolü,
  native SQLite lisans dosyası ve upstream bildirimleri kaydedildi.
- `.uographs/`, build çıktıları, SQLite DB/yan dosyaları Git dışında tutulacak biçimde ayarlandı.

## Gerçek doğrulama sonuçları

Ortam: macOS 27.0, ARM64 / `osx-arm64`; SDK `10.0.201`, MSBuild `18.3.0`, runtime `10.0.5`.
Tüm CLI doğrulamaları gerçek `src/UO.Graphs.Cli/UO.Graphs.Cli.csproj` üzerinden
`dotnet run --no-build --no-restore` ile yapıldı; kurulu tool varsayılmadı.

| Komut/kontrol | Gerçek sonuç |
| --- | --- |
| `dotnet --info` | PASS, exit 0: yukarıdaki kurulu SDK/runtime doğrulandı. |
| `dotnet restore UO.Graphs.slnx --locked-mode -m:1 -nr:false` | PASS, exit 0: 8 proje; lock çözümlemesi değişmeden, NuGet uyarısı olmadan. Bu açık geliştirme restore adımıdır. |
| `dotnet build UO.Graphs.slnx --no-restore -t:Rebuild -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, exit 0: 8 proje yeniden derlendi; 0 warning, 0 error; 9.91 saniye. |
| `dotnet test UO.Graphs.slnx --no-build --no-restore -m:1 -nr:false` | PASS, exit 0: 2 unit + 16 integration = 18/18; skipped 0. Integration yaklaşık 13 saniye. |
| `dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --trust-project --db .uographs/smoke.db` | PASS, exit 0: 2 proje + 2 tip + 2 metot = 6 gerçek varlık. Snapshot `92b5d53237b74d22854c55495ee3c1e0`. |
| Aynı CLI prefix'iyle `query find Greeter --format json --db .uographs/smoke.db` | PASS, exit 0: yeni proseste `T:HelloGraph.Domain.Greeter`, ad `Greeter`, aynı snapshot. |
| Aynı CLI prefix'iyle `query find Greet --format json --db .uographs/smoke.db` | PASS, exit 0: başka yeni proseste `M:HelloGraph.Domain.Greeter.Greet(System.String)`, ad `Greet`, aynı snapshot. |
| Aynı CLI prefix'iyle `status --db .uographs/smoke.db` | PASS, exit 0: 6 varlık / 2 proje; bağımsız JSON assertion'ları geçti. |
| `--help`, `doctor`, `.csproj` input + ProjectReference, varsayılan `.uographs/graph.db` | PASS: integration testlerinde gerçek yeni CLI prosesleriyle doğrulandı. |
| Güven bayrağı yok / hatalı argüman / eksik assets / eksik hedef SDK | PASS: sırasıyla exit 3 / 2 / 4 / 4, hata stderr, stdout boş; assets/DB oluşturulmadı. |
| SQLite rollback | PASS: DELETE ve yeni analysis/ilk entity INSERT sonrasında duplicate PK hatası (SQLite code 19); bağlantı yeniden açıldığında eski analysis ve entity korundu. |
| Başarısız yeniden index | PASS: gerçek geçerli index sonrasında assets eksik hedef index reddedildi; eski snapshot aynı kaldı. |
| Kaynak/kimlik assertions | PASS: tip/metot adı, nitelikli ad, bağımsız SHA256 ile ID, projectId, documentationId, kaynak hash'i, konum, confidence ve snapshot kimliği. |
| `python3 scripts/audit_licenses.py --check` | PASS, exit 0: 42 kilitli paket; MIT / Apache-2.0 / açıkça doğrulanan SQLite public domain. |
| Python XML/son satır/boşluk kontrolü | PASS, exit 0: 8 csproj + 8 lock, proje/merkezi ayar XML'leri geçerli; kaynaklarda satır sonu boşluğu yok. |

Bu checkout için assertion ile bulunan kimlikler (mutlak proje yoluna bağlıdır):

- `Greeter`: `4b75c2e8d37286a64a734e8a2c995c83c6e56ebcbcf37b3d40bcf8a3d915d08b`
- `Greet(string)`: `3674fe5c1a3a67d7834e5ab3f74567b7a21358e1a1448eaaba49bf912ee3fabe`

## Karşılaşılan ve ayrıştırılan sorunlar

- İlk restore, eski geçişli `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 için NU1903 verdi.
  Suppression eklenmedi; bundle 3.0.2 ve lisans dosyası olan native
  `SourceGear.sqlite3` 3.50.4.5 sabitlendi. Son restore/build temiz.
- İlk build MSBL001 verdi: MSBuild.Framework runtime asset'i Locator ile çakışıyordu.
  İlgili projelerde `ExcludeAssets="runtime" PrivateAssets="all"` ayarlandı;
  değerlendirme için hedefin kurulu SDK'sı kullanılıyor.
- Normal paralel solution restore bu sandbox'ta ilerlemedi; iptal sinyali gönderildi.
  Tek worker / node reuse kapalı restore ve rebuild başarılı oldu.
- Python/shell sarmalayıcısıyla smoke denemeleri Roslyn build-host IPC zaman aşımı
  (exit 5) verdi. Aynı ürünün doğrudan CLI index'i ve tam test koşusu başarılı.
  Python smoke sarmalayıcısı teslim edilmedi; senaryo gerçek proses açan integration
  testlerinde tutuldu. Bu denemeler başarılı offline kabul sayılmadı.
- Ayrı filtreli test komutu:
  `dotnet test tests/UO.Graphs.IntegrationTests/UO.Graphs.IntegrationTests.csproj --no-build --no-restore -m:1 -nr:false --filter FullyQualifiedName~RealSolutionIndexPersistsTypeAndMethodAcrossProcesses`
  → **NOT RUN / test başlamadan engellendi**, exit 1: VSTest `SocketException (13): Permission denied`.
  Aynı smoke testinin son tam solution koşusunda geçtiği ayrıca yukarıda kayıtlıdır.
  Ortam izinleri aşılmadı, assertion veya test gevşetilmedi.

## Sınırlar / NOT RUN

- Windows ve Linux build/test/CLI çalıştırma: **NOT RUN**, bu ortam macOS.
- Ağ fiziksel/işletim sistemi düzeyinde kapalıyken çalışma: **NOT RUN**.
  Ürün restore/download/update çağırmıyor; eksik assets'te dosya yaratmadığı testli.
  Bu kanıt, güven verilen custom MSBuild mantığının ağ kullanamayacağı anlamına gelmez.
- Geniş gerçek dünya solution matrisi, multi-TFM, farklı configuration/SDK bağlamları,
  source generators, ilişkiler ve güncellik takibi doğrulanmadı/uygulanmadı.
- `coverage` yalnızca tanımlı Faz 1 kapsamıdır; `freshness` daima `notChecked`.
  Kaynak değişimi sonrası eski snapshot'ın güncel olduğu iddia edilmez.
- Tool paketlemesi/publish ve dağıtım platform matrisi yapılmadı.
- Git repository'si yok; `git diff --check` uygulanamadı. Kaynak boşluk kontrolü ayrıca yapıldı.

## Değişen dosyalar ve commit önerisi

- Yeni: `UO.Graphs.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`,
  `NuGet.Config`, `.editorconfig`, `.gitignore`, `LICENSE`, `README.md`.
- Yeni: `src/UO.Graphs.{Core,Roslyn,Storage.Sqlite,Cli}/` proje/kaynak/lock dosyaları.
- Yeni: `tests/UO.Graphs.{UnitTests,IntegrationTests}/` proje/test/lock dosyaları.
- Yeni: `samples/HelloGraph/` iki proje, iki C# dosyası, solution ve lock dosyaları.
- Yeni: `scripts/audit_licenses.py`, `docs/DEPENDENCIES.md`, `THIRD-PARTY-NOTICES.txt`.
- Güncellendi: `docs/STATUS.md`, `docs/ARCHITECTURE.md` (gerçekleşen SDK/paket durumu).

Önerilen commit mesajı:
`feat: implement phase 1 local Roslyn to SQLite CLI pipeline`

Commit/push/publish yapılmadı. Sonraki faz için kullanıcı talimatı beklenir.
