# Faz 3 — Semantik ilişki sözleşmesi

Merkezi sözleşme: `src/UO.Graphs.Core/Relationships.cs` içindeki
`RelationshipKind` ve `RelationshipContract.Definitions`. JSON tür adları aşağıdaki
büyük harfli adlardır. Contract v3; declaration identity v2 değişmez.

| Tür | Kaynak → hedef | Anlam |
| --- | --- | --- |
| DECLARES | doğrudan sahip → declaration | Solution/project/assembly/namespace/type/member sahipliği; partial konumları ayrı occurrence |
| PROJECT_REFERENCES | referans veren proje → referans verilen proje | Seçilen bağlamdaki değerlendirilmiş MSBuild ProjectReference; eksik veya bağlamı uyuşmayan hedef unresolved |
| INHERITS | türeyen tip → doğrudan base class/interface | Yazılı base-list bağlaması; interface→interface dahil, transitif kapanış değil |
| IMPLEMENTS | implementing class/struct → doğrudan interface veya implementing member → interface member | Roslyn `FindImplementationForInterfaceMember`; explicit, inherited ve default member eşlemeleri Roslyn'in sonucudur |
| OVERRIDES | overriding üye → hemen overridden üye | Method/property/event sembolünün `Overridden*` hedefi |
| USES_TYPE | kapsayan declaration → bağlanan tip | Kaynakta type syntax ve object creation; generic argümanlar ayrıca kaydedilir |
| REFERENCES | kapsayan declaration → başvurulan üye | Field/property/event veya method group; invocation iddiası içermez |
| CALLS | caller → compile-time target | Invocation, constructor, accessor veya desteklenen operator bağlaması; runtime dispatch garantisi değildir |

`exact` bu tablodaki **statik anlamı** kanıtlar. Interface çağrısı interface slotuna,
virtual çağrı seçilen base/override slotuna bağlanır; receiver çalışma anında hangi
nesne olacak sorusu çözülmez. `base.M()` ile virtual `x.M()` occurrence gerekçeleri
ayrıdır. Bütün implementasyonlar `CALLS` hedeflerine genişletilmez. Bu faz
`MAY_DISPATCH_TO` veya başka `inferred` edge üretmez; güven modeli inferred değerini
korur. Dynamic/delegate/reflection runtime hedefi kanıtsız tahmin edilmez.

## İlişki ve occurrence

- Tekil ilişki anahtarı: kind + source symbol ID + target symbol ID (veya boş) +
  project context ID + confidence. Aynı caller/target çifti tek ilişkidir.
- Occurrence: ilişki ID, analysis ID, context ID, dosya, 1 tabanlı satır/sütun
  aralığı (son hariç), analiz edilen metnin SHA-256 özeti, generated bayrağı,
  gerekçe, `boundTarget` ve `relatedSymbols`.
- Aynı iki metot arasındaki iki çağrı **iki occurrence** olarak kalır. Tek span'deki
  getter/setter iki ayrı ilişkidir. Aynı sahibin farklı unresolved çağrıları tek
  unresolved ilişki altında ayrı occurrence taşır. Unresolved `target` daima null;
  aday semboller yalnızca `relatedSymbols` içindedir, kanıtlanmış hedef değildir.
- Generic/reduced extension hedefi original definition'a normalize edilir;
  çağrıdaki constructed imza `boundTarget` içinde korunur. Partial method çağrısı
  definition part kimliğine normalize edilir.
- Endpoint, declaration key, display name, assembly identity, varsa project ID ve
  varsa entity ID taşır. Metadata/örtük constructor/generated-excluded hedefler için
  sahte source declaration yaratılmaz; `entityId` null kalabilir. Harici ID analiz
  eden proje bağlamı + assembly identity + declaration key üzerinden üretilir.
- Kaynak proje hedefi ancak Roslyn compilation reference kimliğiyle bulunur; ad
  benzerliği kullanılmaz. İndekslenen compilation ile assembly, parse seçenekleri,
  kaynak içeriği ve temel compilation seçenekleri uyuşmuyorsa hedef ayrı reference
  sembolü olarak kalır, `relationshipContextMismatch` ve partial raporlanır.
- DECLARES için declaration span'ı, örtük namespace/assembly için proje dosyası
  kanıtı kullanılır. PROJECT_REFERENCES için değerlendirilmiş öğe ve proje dosyasının
  hash'i kullanılır; `1:1–1:1` **XML öğesinin hassas konumu değildir**. Gerekçe bunu
  belirtir. Call-site kayıtları gerçek Roslyn syntax span'larıdır.
- İlişki ID'si konumdan bağımsızdır; occurrence kimliği kaynak konumu/hash/gerekçeye
  bağlıdır. Occurrence ID'leri taşınabilir declaration ID garantisinedahil değildir.

## Destek matrisi

| Kalıp | Bu fazdaki davranış | Sınır |
| --- | --- | --- |
| Normal invocation / overload | `IInvocationOperation.TargetMethod` ile exact CALLS | CandidateSymbol hedefe yükseltilmez |
| Generic invocation | OriginalDefinition hedefi, constructed imza occurrence | Generic argüman runtime tip analizi yapılmaz |
| Extension method | ReducedFrom/original static declaration hedefi | Receiver tipi/metot adı üzerinden eşleştirme yok |
| Object creation, target-typed new, constructor initializer | `IObjectCreationOperation.Constructor` / invocation ile exact CALLS | Roslyn operation'da görülen örtük base initializer span'ı declaration olabilir; sentezlenen record/primary-constructor lowering tam kapsamda değil |
| Property/indexer read | REFERENCES property + CALLS getter | Getter gövdesi kendi accessor caller'ına aittir |
| Simple write, compound write, increment/decrement | Setter; read/write halinde getter ve setter | Ref-return write getter kullanır; setter uydurulmaz |
| Event add/remove | REFERENCES event + CALLS add/remove accessor | Event invoke delegate hedefi unresolved |
| Method group / delegate creation | `IMethodReferenceOperation` ile REFERENCES | Conversion metodu invocation değildir; hedef metoda CALLS eklenmez |
| Delegate invocation | CALLS unresolved; Invoke signature related symbol | Atama/data-flow/multicast hedef çözümü yok |
| Local function | Ayrı lexical declaration; çağrılar semantik hedefe | Aynı adlı farklı lexical scope'lar birleşmez |
| Lambda / anonymous method | Ayrı lexical lambda declaration; gövde çağrıları lambda'dan çıkar | Delegate üzerinden runtime invocation unresolved; gövde dış metoda taşınmaz. Expression-tree içindeki bağlama da çalıştırma garantisi vermez |
| Interface / virtual / explicit implementation | Compile-time slot CALLS; ayrı IMPLEMENTS / OVERRIDES | Runtime implementation expansion yok |
| Partial type/method | Tek declaration; bütün kaynak occurrence'ları | Eksik/generated-excluded kaynaklar tamamlanmış sayılmaz |
| External library call | Assembly identity ve documentation key taşıyan exact endpoint | Harici library gövdesi analiz edilmez |
| Dynamic invocation/creation/member | Hedefsiz unresolved CALLS/REFERENCES | İsim benzerliğiyle hedef üretilmez |
| Reflection / Delegate.DynamicInvoke | API çağrısı exact; runtime hedef ayrıca unresolved | Standart MethodBase/MethodInfo.Invoke ve Delegate.DynamicInvoke tanınır; diğer reflection kalıpları için hedef çözümü taahhüdü yok |
| `nameof` | İç üyeler REFERENCES olabilir; CALLS üretilmez | Compile-time işlem |
| User-defined binary/unary/conversion operator | IOperation.OperatorMethod CALLS | Diğer örtük lowering/protokoller kapsam dışı |
| Hatalı/ambiguous invocation | Hedefsiz unresolved, adaylar ve gerekçe | Hatalı executable declaration içindeki başarılı görünen recovery binding de exact yapılmaz |

`USES_TYPE` bir bütün-program tip/veri akışı analizi değildir. Explicit type syntax,
`var` için Roslyn'in bağladığı tip ve creation tipi kapsamdadır; tamamen örtük
conversion/generic-inference bağımlılıklarının eksiksiz kapanışı taahhüt edilmez.
Local variable/parameter referanslarının kendi başına graph varlığı olması bu faza
dahil değildir. Array/pointer tip bağımlılığı element tipe indirgenir.

Foreach/await/using disposal, pattern/deconstruction lowering, function pointers,
collection expressions ve synthesized members için eksiksiz çağrı çıkarımı yoktur.
Bu kaynaklardaki normal explicit invocation'lar yine analiz edilir. Snapshot
limitations ve bu matris dışında **ilişki yokluğu sonucu çıkarılamaz**.
Derleme hataları analysis diagnostics ve partial kapsamında kalır. Hata içermeyen
bağımsız bir declaration analiz edilebilir; bozuk bir member tüm projeyi sahte
başarılı veya bütünüyle boş graph'a dönüştürmez.

## Kalıcılık ve CLI sınırı

SQLite schema v2, `relationships` ve `occurrences` tablolarını ekler. Analysis,
entities ve iki yeni tablo aynı transaction içinde değiştirilir. Occurrence foreign
key denetlenir; başarısız replacement eski snapshot'ı ve schema sürümünü korur.
Schema v1 salt okunur sorgulanabilir; ilişkiler boş döner ve eski analysis sürümü/
limitations korunur. Başarılı yeniden index v2'ye atomik geçer. Bilinmeyen schema
sürümleri değiştirilmez.

`index` ve `status`: `relationshipCount`, `occurrenceCount`.
`query find`: eşleşen entity'lere bitişik ilişkiler ve bu ilişkilerin occurrence'ları,
`relationships`/`occurrences` alanlarında döner. Endpoint gömülü sembol bilgisi taşır;
sonuç entity listesi komşu düğümlerle genişletilmez. Bu ekleme Faz 3 kanıt okumasıdır;
callers/callees, traversal, impact, export veya diğer Faz 4–5 komutları eklenmedi.

Test kanıtı: `RelationshipTests`, `Phase3Tests`, `StorageTests` ve korunan Faz 2
kabul testleri. Beklenen hedefler ve yasak bağlantılar ayrı assertion'larla sınanır;
caller'ın exact CALLS hedef kümesinin tamamı da karşılaştırılır. Gerçek koşu
sonuçları [STATUS](STATUS.md) içindedir.


## Faz 7 ekleri

REGISTERS, IMPLEMENTED_BY, USES_FACTORY, INJECTS, HANDLED_BY ve ACTIVATES merkezi
ilişki sözleşmesine eklendi. Kaynak/kanıt kuralları değişmez. `CALLS` runtime DI
implementation'ına genişletilmez. Yönler, metadata, güven sınıfları ve desteklenen
kalıplar [FRAMEWORKS](FRAMEWORKS.md)'tedir.


## Faz 8 ekleri

HANDLES / CONSUMES / DISPATCHES / PUBLISHES mesaj sözleşmelerine bağlanır.
POSSIBLE_TARGET mesajdan handler/consumer metoduna inferred aday yönüdür;
BEHAVIOR_FOR / POSSIBLE_BEHAVIOR pipeline sözleşmesini ve olası katılımcıyı ayırır.
Runtime seçimi unresolved kalır. Tam yön ve kimlik sözleşmesi: [MESSAGING](MESSAGING.md).


## Faz 9 ekleri

APP_SERVICE_CONTRACT: service → interface; MODULE_DEPENDS_ON: module → dependency;
REPOSITORY_ENTITY: use/declaration → entity; CONTEXT_ENTITY: context/member → entity;
CONFIGURES_ENTITY: configuration → entity; MAPS_ENTITY: mapping declaration → entity;
HTTP_CONTRACT: Refit method → client contract; INVOKES_HTTP: caller → client contract;
POSSIBLE_ENDPOINT: client contract → unresolved remote endpoint. Exact kaynak
bildirimi runtime garanti değildir. ABP candidate HANDLED_BY inferred kalır.
Sürüm, anlam ve destek sınırları: [ABP-EF-REFIT](ABP-EF-REFIT.md).
