# Gerçekleşen durum

Son güncelleme: 2026-10-09.

**Faz 10 uygulandı; tüm kabul kapıları kapatılmadı. Copilot E2E: NOT RUN.**
Repo skill'i, dağıtılabilir template, güvenli kurulum, subprocess testleri ve manuel
kabul eklendi. **Faz 11 başlamadı.** CLI/kütüphane davranışı ve paketler değişmedi:
**UO.Graphs 0.9.0**, CLI **uographs**, JSON 4 / SQLite 3 / context 1.
[Faz 9 durumu](PHASE9.md) arşivlendi; önceki üç framework integration
başarısızlığı bu fazda kapatılmadı.

## Uygulananlar

- `.github/skills/uographs/SKILL.md`: gerekli name/description YAML ve göreli
  CLI başvurusu; help/doctor/status → küçük find/context → ihtiyaç kadar
  callers/callees/impact/path → ilgili kaynak satırları akışı.
- Partial/stale/unknown/notChecked, truncation, analysis context ve
  exact/inferred/unresolved gerekçeleri korunur. MSBuild güveni ve olası bütün
  proje kapsamı maliyeti onaylanmadan index yok; onaylı güncelleme incremental.
- Kaynak/snippet/label/diagnostic veri kabul edilir. Graph'tan shell komutu kopyalayıp
  çalıştırma, repo dump, blanket auto-approve, policy bypass veya otomatik restore yok.
  MCP'nin kapalı olması skill/terminal onayı sayılmaz. Yerel graph Copilot'u
  offline yapmaz; AI bağlamındaki veri kurum politikasına tabidir.
- `templates/copilot/uographs/` bağımsız dağıtım birimidir; repo skill'iyle aynı
  iki dosya. `scripts/install_skill.py` mevcut hedefi ve symlink yönlendirmesini
  reddeder, exclusive dosya oluşturur; overwrite/merge veya ayar değişikliği yapmaz.
- [Kurulum, ortam kanıtı ve manuel kabul](../COPILOT-SKILL.md) belgelendi. README
  örnekleri gerçek parser ile sınandı; ID yer tutucuları tırnaklandı,
  exit 9/10/130 açıklamaları eklendi.
- Ürüne yeni katman, MCP, HTTP/LLM servisi, paket veya telemetry eklenmedi.
  CLI bağımsız kalır; mevcut AGENTS.md korunur.

## Kurulu VS Code format doğrulaması

Yerel manifestler: **VS Code 1.141.0**, commit
`2a59476c9bfcb90b3ddc372c36762471b7dfad1c`; bundled **GitHub Copilot Chat 0.69.0**,
engine `^1.141.0`. Yerel workbench kodunda `.github/skills`, `SKILL.md` ve Agent
Skills desteği doğrulandı. Zorunlu alanlar, klasör adı ve göreli reference düzeni
[resmi VS Code belgesiyle](https://code.visualstudio.com/docs/agent-customization/agent-skills)
karşılaştırıldı. Kanıt: `.uographs/phase10-evidence/installed-format.json`.

Bu kontrol aktif profil, extension etkinliği, kurum izni veya model oturumunda
skill seçimi kanıtı değildir. VS Code/terminal ayarları değiştirilmedi; Copilot'a
repo/graph verisi gönderilmedi. **Copilot E2E: NOT RUN**; manuel senaryolar gerçek
Copilot oturumunda yürütülmedi.

## Gerçek doğrulama

macOS ARM64; SDK **10.0.201**, runtime **10.0.5**. Mevcut restore edilmiş assets
kullanıldı; bu fazda bağımlılık ekleme/restore yapılmadı.

| Komut/kontrol | Gerçek sonuç |
| --- | --- |
| `dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS exit 0; son build **5.21 sn**, **0 warning / 0 error**. Yeni Faz 10 integration testi dahil. |
| `dotnet test tests/UO.Graphs.UnitTests --no-build --no-restore -m:1 -nr:false --logger 'trx;LogFileName=phase10.trx' --results-directory .uographs/phase10-evidence/vstest` | PASS exit 0, standart VSTest **49/49**, skipped=0, yaklaşık **6 sn**. Reflection fallback kullanılmadı. |
| Aşağıdaki dört standart CLI integration testi | PASS exit 0, **4/4**, yaklaşık **15 sn**. Gerçek subprocess index/persist/query, help/doctor, trust ve missing-assets. |
| `python3 scripts/test_skill.py` | PASS exit 0, **5/5** artifact/installer testi. |
| `python3 scripts/test_skill.py --db .uographs/phase10-live.db --evidence .uographs/phase10-evidence/read-tests.json` | PASS exit 0, **10/10**, **12.989 sn**; 5 artifact + 5 CLI vakası, **45 ayrı CLI prosesi**. |
| `python3 scripts/test_skill.py --db .uographs/phase10-live.db --run-index --evidence .uographs/phase10-evidence/final-tests.json` | **FAIL exit 1**, **10 geçti / 1 başarısız**, **101.437 sn**; 46 proses denemesi. Doğrudan DLL incremental index subprocess 90 sn timeout. |
| `dotnet test tests/UO.Graphs.IntegrationTests --no-build --no-restore -m:1 -nr:false --filter FullyQualifiedName~Phase10ApprovedIncrementalSkillWorkflowAcrossProcesses --logger 'trx;LogFileName=phase10-workflow.trx' --results-directory .uographs/phase10-evidence/vstest` | **ABORTED exit 1 / test execution NOT RUN**; VSTest SocketException(13), TCP listener izni yok. Önceki 4/4 koşudan ayrı çağrı. |
| Aşağıdaki doğrudan CLI smoke | PASS exit 0, incremental rebuild; **15 / 20 / 23**. |
| `python3 scripts/audit_licenses.py --check` | PASS exit 0, **143** kilitli paket/sürüm. Dependency değişmedi. |
| Ruby stdlib `YAML.safe_load` ile iki SKILL frontmatter'ı | PASS; gerçek YAML parse. Mevcut ffi native-extension uyarısı vardı, YAML kontrolü başarılı. |
| skill-creator `quick_validate.py .github/skills/uographs` | **NOT RUN (startup exit 1)**, PyYAML yok. Paket kurulmadı; repo lint'i ve ayrı YAML parser kullanıldı. |
| Python AST / yerel Markdown link / trailing-whitespace | PASS, değişen dosyalarda dosya tabanlı kontrol. |
| Bağımsız salt-okunur inceleme | CLI sözleşmesi, iki policy/injection senaryosu, template parity, overwrite sınırı incelendi. Unknown SQLite schema exit 6 başvuruda netleştirildi. Model davranış testi değildir. |

4/4 geçen integration komutu:

```sh
dotnet test tests/UO.Graphs.IntegrationTests --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName~RealSolutionIndexPersistsTypeAndMethodAcrossProcesses|FullyQualifiedName~HelpAndDoctorRunWithoutLoadingTarget|FullyQualifiedName~TrustIsRequiredBeforeMalformedProjectIsEvaluated|FullyQualifiedName~MissingAssetsFailsWithoutRestoringOrCreatingDatabase' --logger 'trx;LogFileName=phase10-cli.trx' --results-directory .uographs/phase10-evidence/vstest
```

Sayılar ayrı koşulardır; 5/5 artifact testi 10/10 içinde tekrar vardır. README
index örnekleri güven kapısına, metin export örnekleri eksik DB kapısına kadar
parser ile doğrulandı; başarılı index/export sayılmaz. Başvuru örneklerinin
index satırı Python testinde missing-assets negatif yoluyla sınanır; başarılı
index/persist/query yolu yukarıdaki standart integration ve doğrudan smoke'tadır.
Yeni explicit incremental güncelleme testinin açık sonucu ayrıca korunur.

## Gerçek CLI smoke

```sh
dotnet run --project src/UO.Graphs.Cli --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --incremental --trust-project --db .uographs/phase10-live.db
```

Exit **0**, snapshot `b2d1afba5fcd4d8295e95bfa3abca111`; **15 entity / 20 relationship /
23 occurrence**, `mode: incremental`, `outcome: rebuiltAllProjects`, **2 proje**.
Index anında `fresh`, `coverage: partial`; generator eksikleri açık.
Fingerprint **3342 ms**, analiz **5492 ms**; **13394 dosya / 1681271888 byte** hash.
Sonraki status/query varsayılan `notChecked`; eski fresh etiketi onlara taşınmaz.

`.uographs/phase10-evidence/live-status.json` snapshot'ı; `read-tests.json` gerçek
argv/exit dizisini tutar. Testler SQLite backup ile gerçek graph'ı okur, fixture
çıktısını hardcode etmez. Welcome → Greet exact CALLS path'i ve kaynak satırları,
partial bilgisinin context/query'ye taşınması, stale + require-fresh exit 9,
literal saldırgan argüman, lexical/missing-symbol ve budget negatifleri geçti.

Ayrı gerçek subprocess:

```sh
dotnet src/UO.Graphs.Cli/bin/Debug/net10.0/uographs.dll status --db .uographs/phase5-smoke.db --check-freshness
```

Exit **0**, `freshness: unknown` (eski snapshot'ta manifest yok). Çıktı
`unknown-status.json`; bu eski graph'ın yeni analizle üretildiği iddia edilmez.

## Açık kapılar ve sınırlar

- **Copilot E2E: NOT RUN.** [Manuel kabul matrisi](../COPILOT-SKILL.md) kurumca izinli
  gerçek oturumda yürütülmeli; dosya lint'i/CLI testi entegrasyonun yerine geçmez.
- Python DLL index subprocess timeout'u ve yeni Faz 10 VSTest startup hatası açık.
  Aynı turda standart 49 unit + 4 CLI integration ve doğrudan `dotnet run` smoke
  başarılıdır; invocation sonuçları birbirine eşitlenmez. Test silinmedi,
  assertion gevşetilmedi, timeout gizlenmedi. İzin politikası aşılmadı veya
  değiştirilmedi. Uygun IPC ortamında açık iki komut doğrulanmalıdır.
- İlk doğrudan DLL index denemesi iki proje load timeout'u sonrası exit **5**;
  ilk Python probe 90 sn timeout oldu. DB yayınlanmadığından ilk live-db test
  hazırlığı eksik DB hatası verdi. Başarılı `dotnet run` sonrası gerçek yeni DB
  ile 10/10 test geçti. Ara hata sonuçları başarı sayılmadı.
- Windows/Linux, paketlenmiş tool, fiziksel offline ağ izolasyonu, kurum policy
  enforcement ve büyük solution performansı **NOT RUN**. Faz 11 başlatılmadı.
- Faz 9'un üç framework integration testi bu tur **NOT RUN**; geçmiş açık kapısı
  arşivde korunur. Tüm eski integration suite'in geçtiği iddia edilmez.
- `git status --short` exit **128**, Git metadata yok; Git diff **NOT RUN**.
  Commit/push/publish yapılmadı. Kullanıcı projesi veya uygulama host'u çalıştırılmadı.

## Değişen dosyalar

- Yeni repo skill: `.github/skills/uographs/SKILL.md`, `references/cli.md`.
- Yeni dağıtım: `templates/copilot/uographs/SKILL.md`, `references/cli.md`.
- Yeni araç/test: `scripts/install_skill.py`, `scripts/test_skill.py`,
  `tests/UO.Graphs.IntegrationTests/Phase10Tests.cs`.
- Yeni belgeler: `docs/COPILOT-SKILL.md`, `docs/history/PHASE9.md`.
- Güncellenenler: `README.md`, `docs/PRODUCT.md`, `docs/ARCHITECTURE.md`, `docs/STATUS.md`.

Önerilen commit: `feat: add policy-aware Copilot skill for the uographs CLI`
