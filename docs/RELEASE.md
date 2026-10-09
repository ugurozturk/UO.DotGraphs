# Yerel dağıtım ve release readiness

Faz 11, mevcut UO.Graphs 0.9.0 kapsamını sağlamlaştırır. Yeni analiz alanı veya
hosted servis eklemez. Paket **UO.Graphs.Cli**, komut **uographs**; JSON envelope 4,
SQLite 3, context/export 1 korunur. Somut sonuçlar [STATUS](STATUS.md), ölçümler
[PERFORMANCE](PERFORMANCE.md), güven sınırı [SECURITY](SECURITY.md) içindedir.
Tam release kabulü, açık kalan platform/IPC/offline kapıları kapanana kadar verilmez.

Kurulu tool'un workspace timeout'u ayrıca çocuk proses seviyesinde incelendi:
paket ve repo BuildHost'u `.NET 10.0.5` üzerinde başlayıp named pipe açarken
`SocketException (13): Permission denied` ile durdu. İki BuildHost dizininin
20 dosyası SHA-256 bakımından aynı. Bu ortamda eksik paket payload'ı kanıtlanmadı;
kurulu index kabulü, yerel IPC'ye kurumca izin verilen agent'ta yeniden yapılmalı.
Ham stderr ve karşılaştırma `.uographs/phase11-followup/buildhost-probe-locale.json`
içindedir. Genel bir timeout mesajı tek başına bu nedenin kanıtı değildir.

## Gereksinimler ve offline hazırlık

Geliştirme SDK'sı global.json ile **10.0.201**'e sabittir. Tool net10.0 runtime
ister; index ayrıca hedef global.json'ın seçtiği kurulu SDK, reference pack ve
önceden restore edilmiş project.assets.json ister. Bu ortam runtime **10.0.5**.
Doctor hedef projeyi değerlendirmez; kurulu runtime/SDK hakkında bilgi verir.
Başka SDK seçimi fingerprint reuse kapatabilir; destek iddiası STATUS ile sınırlıdır.

Gerekli SDK/runtime/reference pack'leri ve kilitli NuGet paketlerini kurumca izinli
bağlantılı hazırlık ortamında edinin; OS/architecture/RID ve hedef framework'leri
eşleştirin. Cache ve kurum içi feed'i offline makineye kuruluş yöntemiyle taşıyın.
NuGet.Config'de `<clear/>` ve yalnız onaylı yerel feed kullanın. Assets dosyaları
mutlak package root taşıyabildiğinden son konumda açık restore gerekir:

```sh
dotnet restore UO.Graphs.slnx --locked-mode --configfile /approved/offline/NuGet.Config -m:1 -nr:false -p:NuGetAudit=false
dotnet restore samples/Phase2/Fixtures.slnx --locked-mode --configfile /approved/offline/NuGet.Config -m:1 -nr:false -p:NuGetAudit=false
dotnet restore samples/Phase3/Semantics.csproj --locked-mode --configfile /approved/offline/NuGet.Config -m:1 -nr:false -p:NuGetAudit=false
```

Audit=false burada kapalı ağdaki açık bağımlılık hazırlığının parçasıdır; lisans
ve güvenlik değerlendirmesinin yerine geçmez. Runtime index restore/download/update
başlatmaz. Eksik SDK/assets exit 4 verir. Hedef MSBuild/custom task/generator kendi
I/O'sunu yapabilir; ağın gerçekten kesildiği ayrıca altyapı ile doğrulanmalıdır.
Bu tur fiziksel ağ izolasyonu altında smoke **NOT RUN**. Ağ kısıtlı terminal,
`--ignore-failed-sources` veya boş feed tek başına ağ izolasyonu kanıtı değildir.

## Paket üretme ve kurma

```sh
dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet pack src/UO.Graphs.Cli -c Debug --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -o /controlled/local-feed
# NuGet.Config yalnız yukarıdaki feed'i içersin; hedef tool-path yeni/boş olsun.
dotnet tool install UO.Graphs.Cli --version 0.9.0 --tool-path /controlled/new-tool-path --configfile /controlled/NuGet.Config --no-cache
/controlled/new-tool-path/uographs --help
```

Aynı DotnetTool paketi local manifest (`dotnet new tool-manifest`, `dotnet tool
install --local ...`) veya global `dotnet tool install --global ...` kurulumuna
uygundur. İzole geçici **tool-path** ve ayrı geçici **local manifest** kurulumları
yerel nupkg'den geçti. Local manifest denemesinde `DOTNET_CLI_HOME`, NuGet cache ve
manifest geçici kökteydi; help/query/context başarılıydı. Read smoke repo tarafından
üretilmiş DB'yi kullandı; bu kurulum için index **NOT RUN** (aynı BuildHost IPC
sınırı). Kullanıcının global kurulumu/manifesti değiştirilmedi. Global scope kurulumu
**NOT RUN**. Raw komutlar: `.uographs/phase11-followup/local-manifest/commands.json`.
Debug yapılandırması gerçekten paketlendi; Release configuration bu tur NOT RUN.
SDK'nin ürettiği tool ayarında command name `uographs` ve entry point `uographs.dll`.
BuildHost-netcore, runtime native SQLite varlıkları ve bağımlı ürün DLL'leri pakette.

MIT [LICENSE](../LICENSE), [THIRD-PARTY-NOTICES](../THIRD-PARTY-NOTICES.txt) ve
`docs/licenses/` metinleri nupkg'ye eklenir. Ürün bağımlılığı eklenmedi. ABP LGPL
paketleri yalnız izole test fixture'ıdır, tool paketine taşınmaz. 143 kilitli
paket/sürüm envanteri [DEPENDENCIES](DEPENDENCIES.md)'tedir. Lisans denetimi paket
zafiyet denetimi veya her native RID'de çalıştırma kanıtı değildir.

## Kısa kullanım akışı

Önce kurumca izinli ortamda hedefin SDK/assets hazırlığını tamamlayın. Ardından:

```sh
uographs doctor
uographs index /repo/Example.slnx --trust-project --db /controlled/graph.db
uographs query find Greet --limit 20 --db /controlled/graph.db
uographs context '<exact-symbol-id>' --max-chars 12000 --db /controlled/graph.db
uographs index /repo/Example.slnx --incremental --trust-project --db /controlled/graph.db
uographs status --check-freshness --db /controlled/graph.db
python3 scripts/safe_export.py /controlled/new-export.ndjson -- uographs export --format ndjson --db /controlled/graph.db
```

`--trust-project` MSBuild yürütmesine izin verir, sandbox sağlamaz. Incremental
değişiklikte seçili tüm proje kapsamını yeniden analiz edebilir. `partial`,
`unresolved`, truncation ve `notChecked` kaybolmaz. Exact statik bağlanma runtime
çalışma garantisi değildir. [CLI envelope JSON Schema](schemas/cli-envelope.schema.json)
üst zarfı tanımlar; iç graph metadata'sı genişletilebilirdir. NDJSON/help/stderr
bu tek-JSON schema'nın kapsamı dışındadır.

## Repo barındırmasından bağımsız doğrulama

Hazır SDK/assets ile `python3 scripts/verify_release.py --evidence /controlled/new-evidence`
build, standart testler, lisans, pack, izole install ve index/query/context çalıştırır.
Sonuçlar raw stdout/stderr ve komut listesi olarak yerelde tutulur; hata başarısız
exit verir. Timeout sonrası kendi proses grubunu kapatır. Sonraki adımlar önceki
hata nedeniyle çalıştırılmadıysa başarı sayılmaz. `--skip-tests` açık NOT RUN üretir.

Network namespace'i kurumca hazırlanmış Linux agent'ta isteğe bağlı
`--offline-prefix-json '["unshare","-n","--"]'` kurulu tool çağrılarını verilen
launcher içinde yürütür. Bu script altyapı iznini yükseltmez ve launcher'ın gerçekten
ağı engellediğini tek başına kanıtlamaz. macOS'ta bu launcher kullanılmadı.
[On-prem Azure Pipelines örneği](../ci/azure-pipelines.onprem.yml) self-hosted pool
ve yerel kanıt kullanır; hosted agent, secret, publish veya graph artifact upload yok.
Azure DevOps/Windows/Linux çalıştırmaları **NOT RUN**.

## Açık release kapıları

- 1000 proje, gerçek OS-cache-cold ölçüm ve child-process peak RSS: henüz doğrulanmadı.
- Fiziksel disk dolumu, ağ izolasyonu ve bütün işletim sistemi/kurum agent matrisi açık.
- Paket kurulum başarısı ile paketli index başarı durumu STATUS'ta ayrı gösterilir.
- Copilot gerçek E2E önceki fazdan **NOT RUN**; dosya format testi onun yerine geçmez.
- Publish/push/commit yapılmadı. Ürün için "production-ready" etiketi verilmez.
