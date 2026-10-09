# Faz 8 — mediator ve messaging

UO.Graphs 0.8.0; JSON v4 / SQLite 3 / delivery v1 korunur. Ürün paketlerine
mediator/event bus eklenmedi. Çıkarım yalnız Roslyn sembolleri ve IOperation kullanır;
incelenen assembly yüklenmez, uygulama/DI container/broker çalıştırılmaz.

## İncelenen gerçek sözleşmeler

| Modül | Gerçek kanıt / kabul sürümü | Desteklenen yüzey |
| --- | --- | --- |
| UO.Mediator | Yerel repository `src/UO.Mediator.Shared/RequestContracts.cs`, `src/UO.Mediator/UOMediatorServiceCollectionExtensions.cs`; gerçek UO.Mediator.Shared **2.4.2** DLL/XML/nuspec, MIT | `UO.Mediator.Dispatching.IRequestDispatcher.DispatchAsync`; `IRequest<TResponse>`, `IRequest : IRequest<Unit>`; `IRequestHandler<TRequest,TResponse>.HandleAsync`, no-response `IRequestHandler<TRequest>.HandleAsync`; `IRequestBehavior<TRequest,TResponse>` / `RequestHandlerNext.InvokeAsync` |
| MediatR | Gerçek **12.4.1** NuGet DLL + tag kaynakları; **MediatR.Contracts 2.0.1**, Apache-2.0 | `ISender.Send`, response/no-response `IRequestHandler`, `IPublisher.Publish`, `INotificationHandler`, `IPipelineBehavior` |
| MassTransit | Gerçek **MassTransit.Abstractions 8.3.6** NuGet DLL + tag kaynakları, Apache-2.0 | `IPublishEndpoint.Publish`, `ISendEndpoint.Send`, `IConsumer<T>.Consume(ConsumeContext<T>)`; generic, object ve `Type` overload'ları |

Kaynaklar: [UO.Mediator](https://github.com/ugurozturk/UO.Mediator),
[MediatR 12.4.1 API](https://github.com/LuckyPennySoftware/MediatR/tree/v12.4.1/src/MediatR),
[MassTransit 8.3.6 API](https://github.com/MassTransit/MassTransit/tree/v8.3.6/src/MassTransit.Abstractions).
Paket nuspec repository commit'leri sırasıyla
`3556291c4cbb8f6191983054298257ca94365cba`,
`fb309026775ef953a64fb5339d074426c1ad2c37`,
`1ac5197c10ae9834b68c5721d28480e5c65ae876`.
Lisanslar, geçişli bağımlılıklar ve bildirimler [DEPENDENCIES](DEPENDENCIES.md)'tedir.
Sürümler merkezi ve fixture `packages.lock.json` dosyalarında kilitlidir.
Yeni paketlerin tamamı test/fixture bağımlılığıdır; ürün runtime restore/download yapmaz.

UO.Mediator'ın incelenen sözleşmesinde notification/Publish API'si yoktur.
MediatR 14.2.0 cache'de bulunmasına rağmen bu fazın lisans/uyumluluk tabanı seçilmedi.
Tanıma metadata namespace + assembly + bound interface/member üzerinden yapılır;
başlık/Send/Handle ad benzerliği yeterli değildir. Assembly major kapısı UO 2,
MediatR 12 ve MassTransit 8'dir; bu, bu major içindeki **her paketin test edildiği**
anlamına gelmez. Kabul yalnız yukarıdaki kesin sürümlerdir. Framework API
sembolleri metadata reference olmalıdır; framework sözleşmesinin source ProjectReference
olarak geldiği çözüm için messaging uyumluluğu bu fazda doğrulanmadı.

## Yönler ve güven

| İlişki | Yön | Anlam |
| --- | --- | --- |
| HANDLES | handler metodu → mesaj | Roslyn interface implementasyonu, exact statik sözleşme |
| CONSUMES | notification handler/consumer metodu → mesaj | exact sözleşme; subscription/delivery garantisi değil |
| DISPATCHES | caller → mesaj | bağlı dispatch/send API + statik mesaj sözleşmesi; tip çözülemezse target=null |
| PUBLISHES | caller → mesaj | bağlı publish API; tek consumer seçmez |
| POSSIBLE_TARGET | mesaj → handler/consumer metodu | contract-compatible yerel aday inferred; runtime hedef ayrıca unresolved |
| BEHAVIOR_FOR | behavior metodu → mesaj | exact kapalı pipeline sözleşmesi; open generic unresolved |
| POSSIBLE_BEHAVIOR | mesaj → behavior metodu | inferred katılımcı; sıra veya continuation garantisi yok |

`CALLS` aynı kalır: compile-time API hedefidir. Dispatch occurrence reason ayrıca
bağlı API'yi taşır. Exact API çağrısı runtime handler seçimini exact yapmaz.
Adaylar kayıt kanıtı olmadan yalnız sözleşme uyumluluğudur; kayıtlı/aktif handler
listesi sayılmaz. Doğrudan generic Microsoft DI
`AddSingleton/AddScoped/AddTransient<contract,implementation>` bildirimi bulunduğunda
aday ilişkiye **ayrı registration occurrence'ı** eklenir. Conditional bilgisi,
kayıt dosyası/satırı/hash'i korunur; yine inferred kalır. Assembly scanning,
AddUOMediator/AddMediatR wrapper'ları, container identity/order, factory sonucu,
MassTransit receive endpoint/topology ve servis sınırları çözümlenmez.

Her mesajın runtime hedef kümesi açık olduğundan `POSSIBLE_TARGET target=null`
kanıtı korunur; yerel aday varsa da kaldırılmaz. Analiz partial ve scope tanısı taşır.
Başka servis/repository'deki consumer'lar görülmez; sıfır yerel aday “consumer yok”
anlamına gelmez. Çoklu request handler kaybolmaz; hangisinin seçileceği veya
registration hatası olacağı söylenmez.

Pipeline behavior kapalı sözleşmesi ayrıdır. Behavior'lar handler yerine konmaz;
handler'a giden yol zorunlu olarak behavior'dan geçirilmez. `next` hiç, bir veya
çok kez çağrılabilir; sıra, koşul ve continuation runtime akışı çözülmez. Açık generic
behavior/handler için kapalı substitution/constraint expansion yoktur; hedefi null
olan ilişki ve tanı üretilir.

## Mesaj kimliği ve kapsam

`message` canonical kimliği framework + gerçek assembly identity + Roslyn declaration
ID + varsa eşleşen source project context + recursive closed type arguments/containing
type içerir. `Request<int>` ve `Request<string>`, aynı kısa/qualified adı taşıyan
farklı assembly'ler, namespace'ler ve framework'ler birleşmez. Salt görünen ad veya
kaynak metin eşleşmesi kullanılmaz. Source reference compilation bağlamı önceki
identity kurallarıyla korunur. Metadata-only sözleşmeler assembly identity ile ayrılır;
multi-repo federation yapılmaz.

Optional `Message` metadata'sı framework, constructed type, assembly, canonical
contract identity, scope ve limitations taşır. Declaration/call kanıtları kaynak
hash'iyle birleştirilir. Aynı caller/kind/null target için graph ilişkisi tek olabilir;
farklı çağrı ve framework kanıtları ayrı occurrence'larda korunur.

Mediator runtime tip üzerinden seçtiği için `new Request()` veya sealed/value statik
tip kanıtı kullanılır. Interface/object, açık type parameter ve unsealed variable
runtime tipi unresolved kalır; arbitrary data-flow yoktur. MassTransit generic `T`
statik yayın sözleşmesidir; `Publish<IEvent>(values)` interface tipiyle modellenir.
Object + `typeof(Event)` explicit sözleşme verir; hesaplanan `Type` unresolved kalır.
Sınırsız variance/hierarchy/topology yayılımı üretilmez. Stream APIs, custom extension/
wrapper, concrete dispatcher overload'ları, MassTransit request client/saga/filter ve
runtime-generated messages mesaj çıkarım kapsamı dışındadır; CALLS kanıtları korunur.

## Query/context kabulü

`dependencies`, `path`, `impact` yeni ilişkileri kullanabilir. Inferred hedefler
query'de `--include-inferred` gerektirir; callers/callees varsayılanı yalnız CALLS.
Context/export tüm güven sınıflarını kanıtlarıyla korur. Inferred yol runtime teslim
veya handler çalıştırma garantisi değildir.

Gerçek UO paketi fixture'ında altı adımlı akış:

```text
GET /phase8/start
  --HANDLED_BY exact--> FlowController.Start
  --CALLS exact-------> Service.Run
  --DISPATCHES exact--> RequestA message
  --POSSIBLE_TARGET inferred--> HandlerA.HandleAsync
  --DISPATCHES exact--> RequestB message
  --POSSIBLE_TARGET inferred--> HandlerB.HandleAsync
```

```sh
dotnet run --project src/UO.Graphs.Cli --no-build --no-restore -- index samples/Phase8/Uo/Uo.csproj --full --trust-project --db .uographs/phase8-uo.db
dotnet run --project src/UO.Graphs.Cli --no-build --no-restore -- query path 'GET /phase8/start' 'UoFlows.HandlerB.HandleAsync(UoFlows.RequestB)' --depth 10 --edge-types HANDLED_BY,CALLS,DISPATCHES,POSSIBLE_TARGET --include-inferred --db .uographs/phase8-uo.db
dotnet run --project src/UO.Graphs.Cli --no-build --no-restore -- context 'GET /phase8/start' --depth 8 --max-nodes 300 --limit 800 --max-edges 10000 --max-chars 900000 --db .uographs/phase8-uo.db
```

Gerçek paket fixture'ları `samples/Phase8/Uo` ve `samples/Phase8/PackageCompatibility`.
[ContractFixtures](../samples/Phase8/ContractFixtures/README.md) ayrı kaynak tabanlı
test girdileridir. Bunların geçmesi tek başına paket uyumluluğu sayılmaz; unit test
gerçek DLL'lerle ayrıca derler ve integration test ayrı gerçek paket projelerini
CLI/SQLite/query/context/export üzerinden doğrular. Broker, ağ servisi, uygulama
host'u veya mediator runtime çözümlemesi hiçbir testte başlatılmaz.
