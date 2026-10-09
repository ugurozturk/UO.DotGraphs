# Faz 11 ölçüm yöntemi ve gerçek sonuçlar

Ölçüm 2026-10-09, macOS **27.0.1 ARM64**, **24 logical CPU**, **64 GiB RAM**;
SDK **10.0.201**, runtime **10.0.5**, Python **3.14**. CPU modeline `sysctl` erişimi
ve proses listesine `ps` erişimi engellendi. Runtime/SDK kurulu, assets explicit
restore ile hazırlandı. Native process/child-process RSS: **NOT RUN**; RAM kapasitesi
proses tüketimi değildir. Windows/Linux için sonuç çıkarılmaz.

## Dataset ve koşullar

`scripts/synthetic_solution.py DEST --projects 10|100|1000 --classes 4 --methods 8`
mevcut hedefi reddeden parametrik üreticidir. Star topolojisinde tüm consumer'lar
P0000'a referans verir, son proje leaf'tir. C# kodu gerçek compiler ile analiz edilir;
yalnız fixture üretimi analiz başarısı sayılmaz. Directory.Build/Packages config'leri
repo üst ayarlarından izole edilir. Paket bağımlılığı yok, net10.0 reference pack gerekir.

| Proje | Kaynak dosya | Method | Kaynak byte | Gerçek durum |
| --- | ---: | ---: | ---: | --- |
| 10 | 40 | 320 | 16.648 | Full/no-op/leaf/dependent ve query/context çalıştı |
| 100 | 400 | 3.200 | 171.088 | Full PASS; 4001 / 13635 / 13935 graph kaydı |
| 1000 | 4000 | 32.000 | 1.715.488 | Üretildi; restore/index/latency **NOT RUN** |

1000-proje senaryosu tanımlı fakat bu tur çalıştırılmadı: 100-proje koşusu ve IPC
tekrarları uzun sürdü; büyük boyut için kontrolsüz eşzamanlı çalışma başlatılmadı.
Bu, 1000 projenin RAM'e sığmadığı veya desteklenmediği kanıtı değildir.

Önceden kaydedilen araştırma bütçeleri `.uographs/phase11-evidence/budgets.json`:
full 10/100/1000 için 120/600/3600 sn; warm 60/300/1800 sn; query/context 2 sn;
process tree 8 GiB. Bunlar ölçülmüş SLA değildir. Ölçülemeyen kriter geçilmiş sayılmaz.

- **Cold/full:** yeni CLI prosesi ve boş DB, `--full`. SDK/assets hazır.
  OS filesystem cache'i boşaltılmadı: gerçek cache-cold **NOT RUN**.
- **Warm/no-op:** aynı argv/environment/kapsam, mevcut snapshot, yeni proses;
  `outcome=noOp`, projectsAnalyzed=0 doğrulanır. MSBuild evaluation ve içerik hash
  maliyeti vardır. Disk timestamp cache'i yoktur.
- Leaf ve P0000 değişimleri üreticinin kendi kaynak dosyalarında bir expression
  literal değişimiyle uygulanır. Değişiklikten sonra **bütün 10 proje** analiz edilir;
  project-granular incremental iddiası yoktur. Değişiklikler sonunda geri alınır.
- Index tablolarındaki süreler CLI'ın iki gerçek timer'ıdır: fingerprint ilk
  preparation/hash; analysis compilation + ikinci preparation/hash. Startup ve
  SQLite publication bu timer'larda yoktur; toplam wall-clock diye sunulmaz.
- Query/context wall süreleri yeni doğrudan DLL subprocess'larıyla ölçüldü.
  Harness 100 ms polling içerir; küçük süreleri yaklaşık üst sınır gibi okuyun.
  Proses içi SQL microbenchmark değildir. Paralel test yükü/cache denetlenmedi;
  istatistiksel veya yüzde iyileşme iddiası yapılmaz.

## 10 proje raw ölçümleri

Index senaryoları **birer tekrar**, query/context **üçer tekrar**. Script varsayılan
üç warm/read tekrarını destekler; burada çalıştırılan sayı raw kayıttaki kadardır.

| Koşul | Fingerprint ms | Analysis + verify ms | Sonuç / analiz edilen proje |
| --- | ---: | ---: | --- |
| Workspace paylaşımı öncesi full | 12667 | 27338 | rebuiltAllProjects / 10 |
| Paylaşım sonrası full | 13360 | 16129 | rebuiltAllProjects / 10 |
| Warm incremental | 14356 | 0 | noOp / 0 |
| Leaf değişimi | 13903 | 17035 | rebuiltAllProjects / 10 |
| Dependency P0000 değişimi | 13933 | 16334 | rebuiltAllProjects / 10 |

Her başarılı graph: **401 entity / 1305 relationship / 1335 occurrence**.
SQLite **6.631.424 byte**; WAL 0, SHM 32.768 byte ayrı kayıtlıdır.
No-op bile **6978 dosya / 940.927.624 byte** hash okudu. SDK/packs/binary içerik
kontrolü ve project başına MSBuild evaluation kalan ölçülmüş maliyettir; mtime'a
dayanarak correctness zayıflatılmadı.

| İşlem | Tekrar 1 sn | Tekrar 2 sn | Tekrar 3 sn |
| --- | ---: | ---: | ---: |
| query find M0 --limit 20 | 0.117727 | 0.222842 | 0.217710 |
| context P0000.C0.M0() --max-chars 12000 | 0.219144 | 0.223178 | 0.225076 |

## Optimizasyonun kapsamı

Önceden homojen solution'da her proje için yeni workspace ve yeniden
OpenSolutionAsync vardı. Ölçülen 10-proje akışında **10 solution load** yerine tek
workspace ve analyzer-free immutable solution kullanılır. Sadece aynı TFM,
preparation diagnostic'siz, generator kapalı solution yolu paylaşılır. Karışık
inner-build, project input ve generator yolları korunur. İki gerçek SQLite'ın
analysisId dışındaki entity/relationship/occurrence payload'ları eşit çıktı:
`workspace-regression.json`. Compilation reference eşitliği artık referans
properties, module MVID ve recursive compilation girdilerini de sınar.

Karşılaştırma tek tekrar; ilk koşuda generated obj oluşumu, sonrakinde sıcak OS
cache ve farklı test yükü var. **Yüzde performans/token tasarrufu iddiası yoktur.**

## Tekrarlanabilir komutlar ve raw kanıt

```sh
python3 scripts/synthetic_solution.py /controlled/new-p10 --projects 10
# Açık geliştirme hazırlığı, ürün runtime davranışı değil:
dotnet restore /controlled/new-p10/Synthetic.slnx --configfile /controlled/new-p10/NuGet.Config -p:NuGetAudit=false --disable-parallel -m:1 -nr:false
python3 scripts/benchmark.py /controlled/new-p10 /controlled/new-evidence --timeout 600 --repeats 3 --cli /controlled/tool/uographs
```

`.uographs/phase11-evidence/` altında environment, budgets, dataset manifests,
`direct-baseline-10.json`, `*-direct-10.raw`, `performance-summary.json`,
`read-latency/raw.jsonl`, stdout/stderr ve sqlite-sizes bulunur. Dotnet tool ve
schema/validation raporları aynı evidence kökündedir. Gerçek raw kayıtları
hosted artifact olarak yüklenmedi.

Python/redirected index denemeleri MSBuild workspace IPC timeout'una takıldı;
180 sn timeout ile öldürülen koşu `perf-baseline-10c/raw.jsonl` içinde korunur.
Bunlar başarılı direct CLI ölçümüne karıştırılmaz. İlk 100-project denemesinde
analiz sırasında ürün binary'leri yeniden derlendi; input hash guard publication'ı
exit 5 ile reddetti. Bu başarısız girişim performans sonucu sayılmaz; sabit binary
tekrarı ayrı tutulur. Proses envanteri izni olmadığından residual child süreçler
ve belleğin bağımsız denetimi sağlanamadı; kontrollü self-hosted agent'ta tekrarlayın.

## 100 proje

Sabit binary ile full tekrar **PASS**: fingerprint **122481 ms**, analysis + verify
**136951 ms** (bir tekrar); 4001 entity / 13635 relationship / 13935 occurrence.
SQLite **53.084.160 byte**; 16116 dosya / 1.894.229.534 byte hash.
`full-100-summary.json` sayısal stdout alanlarını saklar; `full-direct-100-final.raw`
CLI tool taşıma katmanında ortadan kırpılmıştır ve tam JSON değildir. Snapshot'ın
tam analysis metadata'sı DB'den `full-100-analysis.json` olarak ayrıca okundu.
Bu iki kanıtın farkı saklanır. Leaf/dependent 100-proje senaryoları bu tur **NOT RUN**;
10-proje ölçümleri onların yerine geçmez. Warm sonucu STATUS'ta ayrıca kayıtlıdır.

100-proje incremental ilk tekrarı, pack'in tool base altında `publish/` dosyalarını
 değiştirmesi nedeniyle **no-op olmadı**: fingerprint **115269 ms**, analysis/verify
**133246 ms**, 100 proje yeniden analiz. Bu koşu warm/no-op başarısı sayılmaz.
100-proje read tekrarları (50.000 karakter context): query **0.213082 / 0.216708 /
0.216431 sn**, context **0.218130 / 0.214898 / 0.214983 sn**. Önceki 12.000 karakter
negatifi minimum 27.146 karakter zarf için exit 2 verdi. Script varsayılan context
bütçesi bu ölçüm matrisi için 50.000'dir; 10-proje kaydı açıkça 12.000 kullanır.

Sabit girdilerle sonraki 100-proje warm tekrar **PASS noOp / 0 proje**:
fingerprint **111733 ms**, analysis **0 ms**. `noop-100-summary.json` ve
`noop-direct-100.raw` (tool taşıma katmanında ortadan kırpılmış) ayrı kayıttır.
