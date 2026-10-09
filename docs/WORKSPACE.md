# Faz 2 workspace ve yürütme sözleşmesi

SDK-style C# `.csproj`, `.sln` ve `.slnx` desteklenir. Solution klasörleri proje
sayılmaz; VB/F#/C++ ve eski non-SDK C# girişleri açık tanı üretir. Hiç hazırlanabilir
proje kalmazsa exit 4, hiç compilation alınamazsa exit 5; eski snapshot korunur.

`--trust-project`, input değerlendirmesinden önce kontrol edilir. MSBuild target,
import ve task'ları kod çalıştırabilir. Bu izin sandbox veya ağ engeli değildir.
Runtime restore/download/update yoktur. SDK seçimi her hedef proje klasöründe
`dotnet --version` ile global.json çözümlemesini kullanır. MSBuild'den gerçek
`MSBuildToolsPath`, `MSBuildVersion`, `ProjectAssetsFile`, `TargetFramework(s)`,
`ProjectReference` ve `Compile` öğeleri değerlendirilir. Kök projenin eksik
SDK/assets önkoşulu işlemi durdurur; yüklenemeyen dependency kısmi sonuç sebebidir.
Bir snapshot içinde farklı SDK kurulumları desteklenmez; ayrı proses gerekir.

Locator, workspace API'sine geçilmeden register edilir; `CanRegister` kontrolü,
proses içi kayıt kilidi ve `NoInlining` sınırı bulunur. Haricen/farklı SDK için
register edilmiş Locator kabul edilmez. Microsoft.Build runtime asset'leri ürünle
kopyalanmaz. Common/CSharp/Workspaces/MSBuild Roslyn assembly sürümleri yüklemede
karşılaştırılır. Sabit ürün paketi seti Roslyn 5.0.0 + Locator 1.11.2'dir.
Gerçek uyumluluk kabulü .NET SDK 10.0.201 / MSBuild 18.3.0 / macOS ARM64 için yapıldı.
[Locator kayıt sırası](https://learn.microsoft.com/en-us/visualstudio/msbuild/find-and-use-msbuild-versions)
ve [Roslyn Release inlining kaydı](https://github.com/dotnet/roslyn/issues/78478)
başlatma sınırının gerekçesidir.

## Configuration ve target framework

Varsayılan `--configuration Debug`; Release ve projedeki özel configuration
adları açık seçilebilir. Koşullu ProjectReference/Compile/DefineConstants MSBuild
bağlamında değerlendirilir; gerçek Roslyn preprocessor sembolleri saklanır.
Multi-target projelerde `--framework` zorunludur; her TFM ayrı çağrıyla indekslenir.
Seçilen TFM her project context'te, diğerleri `skippedTargetFrameworks` içinde
raporlanır. Tek hedefli diğer solution projeleri kendi TFM'sini korur.

Homojen solution bağlamlarında gerçek `OpenSolutionAsync`, farklı TFM'li veya
kısmen desteklenmeyen solution'larda listelenen projeler için `OpenProjectAsync`
kullanılır. Analiz configuration seçimini tüm projelere global uygular; solution'ın
farklı proje configuration/platform eşleme matrisini uygulama iddiası yoktur.
ProjectReference'ler metadata-only moda çevrilmez. Referans compilation'ının
preprocessor sembolleri ve output yolu `referenceContexts` içinde ayrı tutulur;
indekslenen hedef bağlamla uyuşmayan veya yüklenemeyen referans `partial` üretir.
Farklı bağlamlar tek ID'ye birleştirilmez. RID/platform matrisi bu fazda seçilebilir
değildir; özelleştirilmiş solution eşlemeleri sınır olarak görünürdür.

## Diagnostics ve kısmi kayıt

Workspace diagnostics, compilation warning/error, eksik Compile dosyaları,
proje yükleme hataları, generator yükleme/çalışma tanıları snapshot'ta saklanır.
En az bir compilation alınırsa eldeki tanımlar atomik olarak kaydedilir; eksik
bölümler `coverage: partial` ile görünür. Bu, önceki snapshot'ı değiştiren açık
kısmi kayıt politikasıdır. İşlem başarıyla kaydedilirse exit 0, coverage partial
olabilir; tüketici daima `analysis.coverage` okumalıdır. Hazırlık/fatal hatalarda
DB değiştirilmez. Hiçbir durumda partial boş sonuç kesin yokluk kanıtı değildir.

## Generator ve analyzer politikası

Varsayılan olarak **tüm** workspace projelerinden AnalyzerReference'ler compilation
öncesinde kaldırılır. Assembly keşfi dahi generator çalıştırma izni yokken yapılmaz.
SDK'dan gelenler dahil generator/analyzer referansı varsa, hangi assembly'nin
çıktı üreteceği çalıştırmadan bilinmediğinden `generatorsSkipped` + `partial`
raporlanır. Bu temkinli sınıflandırma basit SDK projelerinde de görülebilir.
`--trust-project`, `--allow-generators` yerine geçmez.

`--allow-generators`, referansların yüklenmesine ve source generator kodunun
çalışmasına izin verir. Roslyn GeneratorDriver; gerçek parse options,
AdditionalFiles ve AnalyzerConfigOptions ile bağımlılık sırasına göre çalışır.
Çıktılar bellekte workspace'e eklenir; dependency'nin generated tipleri tüketici
compilation'ında kullanılabilir. Generator exception/load failure ve diagnostic'leri
kısmi analize yansır. DiagnosticAnalyzer çalıştıran `WithAnalyzers` çağrısı yoktur;
MSBuild `RunAnalyzers`/`RunAnalyzersDuringBuild=false` kullanılır. Aynı assembly'den
generator yüklemek assembly kodunun yan etkisiz olduğu garantisi değildir.
Custom MSBuild mantığı kendi executable'larını çalıştırabilir; izin bayrakları OS
izolasyonu sağlamaz. Generator'a süre/bellek sandbox'ı bu fazda eklenmedi.

`--generated exclude` varsayılandır. `--generated include`, yalnızca workspace'in
Compile dokümanlarını ve izinle üretilen Roslyn çıktılarını indeksler. obj/bin
klasörleri taranmaz. Workspace dokümanlarında obj/bin yol parçası, `.g.cs`,
`.generated.cs`, `.designer.cs` uzantısı veya ilk 2048 karakterde `<auto-generated`
etiketi generated sayılır. Bu tanıma uymayan elle eklenmiş generated dosyalar
normal kaynak kabul edilir. Dışlanan her dosya `generatedExcluded` tanısıyla
belirtilir; generated kod semantik compilation'da kalır, sadece declaration
indeksinden çıkarılır. `completeWithinDeclaredScope`, seçilen bu politikayla sınırlıdır.

Generator kanıt yolları `.uographs-generated/<relative-project>/<generator>/<hint>`
altında **sanal** yollardır; disk dosyası yaratılmaz, hash gerçek SourceText'tendir.
`SourceEvidence.generated` generated kaynağı işaretler.

## Gerçek fixture'lar

`samples/Phase2`: iki solution formatı; `Library` net10.0/netstandard2.1,
`App` ProjectReference; overload/generic/partial/nested/operator/extension,
file-local tipler ve lexical local function'lar; `Broken` bilerek eksik proje/dosya;
`Generator` ve `GeneratedConsumer` izin, generated politika ve exception testleri.
Generator fixture'ı netstandard2.1'dir ve yalnızca ürünün .NET 10 host'unda kullanılır.
Genel VS/.NET Framework analyzer dağıtımı değildir. RS1041 (portable compiler host)
ve RS1036 (yan etki işaretleyicili test fixture'ı) uyarıları bastırılmaz; fixture
çalışması integration testinde doğrulanır. netstandard2.0 denemesinin getirdiği
Microsoft.NETCore.Platforms 1.1.0 permissive olmayan eski lisansı nihai bağımlılık
setinden çıkarılmıştır. NuGet audit servis erişimi ile lisans denetimi ayrı kontrollerdir.
