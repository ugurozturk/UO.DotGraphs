# Canonical identity v2

Kimlikler snapshot, süreç, satır veya mutlak checkout yoluna bağlı değildir.
`analysis.identityVersion = "2"`; her varlık `canonicalIdentity` değerini taşır.
ID = canonical string'in BOM'suz UTF-8 byte'larının SHA-256 özeti, küçük harf hex.

`C(parts...) = "uographs:2:" + concat(UTF8ByteCount(part) + ":" + part)`.
Uzunluk ondalıktır; Unicode normalize edilmez, harf duyarlılığı korunur.
Örneğin `C("é", "a:b") = "uographs:2:2:é3:a:b"`.
Delimiter içeren alanlar çakışmaz. SHA-256 teorik çakışma olasılığı sıfır kabul edilmez.

| Varlık | Canonical içerik |
| --- | --- |
| project | `C("project", relativeProjectPath, targetFramework, configuration)` |
| solution | `C("solution", relativeInputPath, configuration, selectedFrameworkOrDefault)` |
| assembly | `C("symbol", projectId, "A:" + RoslynAssemblyIdentity)` |
| namespace/type/member | `C("symbol", projectId, declarationKey)` |

`relativeProjectPath`, `analysis.rootPath` altındaki göreli yoldur; `\` → `/`.
`--root` verilirse input ve tüm projeleri içermelidir. Varsayılan, input klasörü
ve ulaşılan projelerin ortak üst klasörüdür. Aynı repo farklı köke kopyalandığında
aynı göreli yerleşim ve seçenekler aynı kimliği üretir. Farklı giriş altkümeleriyle
karşılaştırma için aynı mantıksal `--root` seçilmelidir. Farklı bağımsız repolardaki
aynı yerleşimi küresel olarak ayırma/federation bu fazın kapsamı değildir.

Declaration key normal durumda Roslyn documentation comment ID'sidir:

- Namespace: `N:Fixture`; global namespace `N:`. Proje/TFM bağlamında ayrılır.
- Tip generic arity: `T:Fixture.Box` ve `T:Fixture.Box\`1`.
- Nested tip: `T:Fixture.Box\`1.Nested\`1`.
- Overload: `M:Fixture.Box\`1.Run(System.Int32)` ve `...Run(System.String)`.
- Generic metot arity ve type parameter ordinal'ları Roslyn formatından gelir.
- Constructor `.ctor/.cctor`, indexer parametreleri, ref parametreleri,
  explicit interface isimleri (`#` escaping), operator metadata isimleri ve
  conversion return tipi Roslyn formatıyla ayrılır. Extension method normal
  static method kimliğini ve ilk parametresini kullanır.
- Accessor: `accessor:<owner documentation ID>:<Roslyn MethodKind>`; get/set/init
  ve event add/remove sahibinden ayrılır. `init`, Roslyn'in `PropertySet`
  kategorisindedir; set→init değişimi tek başına yeni kimlik üretmez.
- File-local tip ve üyeleri: `C("file-local", relativeSourcePath, documentationId)`.
- Local function: `C("local", containingDeclarationKey, relativeSourcePath,
  syntaxChildNodeOrdinalPath, fullSignature)`. Ordinal yol trivia/token satır
  numarası kullanmaz; aynı üyedeki iki ayrı blokta aynı isim/imzayı ayırır.
  İlgili syntax hiyerarşisini yeniden düzenlemek veya önceki kardeş düğümleri
  değiştirmek local function kimliğini değiştirebilir. Boş satırlar değiştirmez.

Partial type ve partial method aynı declaration key ile tek varlıktır; tüm
indekslenen tanım konumları `sources` içinde kalır. Kaynak hash'i kimliğe katılmaz.
Auto/expression-bodied property/event accessors sahibi üzerinde kaynak kanıtı
alır; yazılı accessor ayrıca kendi kaynak konumunu taşır. Örtük constructor,
backing field ve record'un compiler tarafından sentezlenen üyeleri
çıkarılmaz; yazılı primary constructor parametre listesi constructor varlığı üretir. Bu sınırlar snapshot'ın declaration scope'una dahildir.

Yeniden adlandırma veya Roslyn documentation ID'sini değiştiren imza düzenlemesi
(ör. parametre tipi/arity) yeni ID üretir. Tip/namespace rename, içerdiği üyelerin
ID'lerini de değiştirir. Parametre adı, generic parametre adı, whitespace ve
sıradan method return type değişimi documentation ID'yi değiştirmiyorsa ID sabit
kalır; conversion return tipi bu kuralın istisnasıdır. Proje göreli yolu, TFM veya
configuration değişirse o bağlamdaki bütün sembol ID'leri değişir. Assembly adı
assembly ID'sini değiştirir; kaynak declaration ID'lerinin sahibi proje bağlamıdır.

Faz 3 JSON çıktı sözleşmesi v3, SQLite schema v2'dir;
eski snapshot okunurken `identityVersion` varsayılanı `1` kalır. Yeni index atomik
olarak v2 snapshot ile değiştirir; eski ID'leri yeni kimlikmiş gibi yeniden yorumlamaz.
Kaynak yolları/satırları mutlak ve güncel analiz metnine bağlı olabilir; bunlar
kimlik değil kanıttır. `freshness = notChecked` korunur.

Faz 3 lambda/anonymous method kimliği local function ile aynı lexical yol
mekanizmasını `C("lambda", ...)` discriminator'ıyla kullanır. Type parameter
endpoint kimliği owner declaration key + TypeParameterKind + ordinal içerir.
İlişki/occurrence ve external endpoint kimlikleri [RELATIONSHIPS](RELATIONSHIPS.md)
içindedir; varlıkların mevcut identity v2 kimlikleri korunur.
