# PC Dükkanı Simülatörü — Claude Code Talimatları

Kullanıcıyla **Türkçe** konuş. Kod yorumları ve oyun içi metinler de Türkçe.
Kullanıcı oyunu kodlamayı sana bırakıyor; o Unity'de test edip sonucu (ve Console hatalarını) sana söylüyor.
Adımları sade anlat, Unity'de neye tıklayacağını tek tek yaz.

Yol haritası ve aşamaların durumu: **PLAN.md**. Her iş bitince orada işaretle.

## Oyun

Birinci şahıs (3D) bilgisayar dükkânı simülatörü. **Sadece PC (Windows), klavye + fare. Mobil YOK.**

Oyun döngüsü:
1. Arabayla **toptancıya** git, bilgisayar bileşenleri ve PlayZone 5 / PlayZone 5 Pro konsollarını satın al.
2. Kutuları **arabanın bagajına** yerleştir, dükkâna geri sür.
3. Kutuları **arka odadaki tamirhaneye** taşı, bilgisayarı **topla**.
4. **Windows kur**, tüm **testleri** yap.
5. Bilgisayarı **rafa** koy, üzerine **fiyat etiketi** yapıştır. Konsollar da kâr eklenip etiketle rafa konur.
6. **Müşteri** gelir, biraz **pazarlık** yapar, fiyatta anlaşırsa satın alır.
7. **Tamir işi:** Müşteri kendi bilgisayarını getirir ve sorunu söyler (ör. "ekran gelmiyor"). Aletlerle sorunu bulur, çözeriz, teslim ederiz. Müşteri pazarlık eder, az pazarlıktan sonra mecburen öder.
8. **Format işi:** Müşteri bilgisayarını format için getirir. **Sabit fiyat, pazarlık yok.**
9. **Dükkân bilgisayarı** (tamirhanenin köşesinde): dükkân yükseltmeleri, fatura ödeme, güvenlik kamerası satın alma. İçinde Amazon'u andıran ama **farklı isimli** bir online alışveriş uygulaması olacak (isim henüz seçilmedi).

## Teknik

- **Unity 6.6 (6000.6.7f1)**, **URP** (Universal 3D şablonu), **Input System** paketi kurulu.
- Projeyi Unity'de çalıştıramazsın. Değişiklikten sonra kullanıcıya nasıl test edeceğini yaz; hataları kullanıcı Console'dan yapıştırır.
- Sahne kodla kuruluyor: üst menü **PC Shop → Prototip Sahnesini Kur** (`Assets/Scripts/Editor/PrototypeSceneBuilder.cs`). Sahneye kalıcı bir şey ekleyeceksen **bu kurucuyu güncelle**, kullanıcıdan elle obje dizmesini isteme.
- Şehir (yollar, binalar, taş yaya yolları) kurucunun ikinci dosyasında: `Assets/Scripts/Editor/PrototypeSceneBuilder.City.cs` (partial class). Dokular kodla üretilip `Assets/Data/Textures` içine PNG olarak kaydedilir; dosya varsa yeniden üretilmez.
- Gerçekçi eşya araçları: `PrototypeSceneBuilder.Props.cs` — `RBox` (kenarı yuvarlatılmış kutu mesh'i), `Pbr` (smoothness/metallic/normal map malzeme), `CombinedMeshes`, renk+kabartma doku çiftleri (`LaminatePair`, `BrickPair`, `WoodPair`, `SlatwallPair`…), `ShopLight` (gölgeli), `ReflectionProbeBox`. Yeni eşyada düz `SBox` yerine bunları kullan. Gölge atlası sınırlı: gölgeli ışık sayısını az tut.
- Bizim dükkân (Seviye 1): `PrototypeSceneBuilder.Shop.cs`. Hikâye: oyuncu para biriktirip Seviye 2'ye geçecek, o yüzden Seviye 1 bilerek ucuz/sade ama özenli. Duvarlar çift katman (`TexWall`: dışı tuğla + camlar, içi oda boyası). Yuva adları (PC Rafı n, Konsol Rafı n, Vitrin n, Kasa 1-2, Tezgâh n, Kurulum n, Depo Rafı n) korunuyor.
- Toptancı mağazası: `PrototypeSceneBuilder.Wholesale.cs`. Kök dünyada (-102,0,44), 90° dönük; içindeki tüm ölçüler köke göre yerel (ön cephe yerel +z).
- Derleme kontrolü: Unity açmadan `dotnet build` ile Unity 6.6 DLL'lerine (`Editor/Data/Managed/UnityEngine/*.dll`) karşı derlenebiliyor (geçici csproj, `UnityEditor.dll` ekleme — modüllerle çakışıyor).

## Mimari (Assets/Scripts)

| Dosya | Görev |
|---|---|
| `Core/IInteractable.cs` | Bakılıp [E] ile etkileşilen her şey bunu uygular (`CanInteract`, `GetPrompt`, `Interact`). |
| `Core/GameInput.cs` | **Tüm** tuş/fare okumaları buradan. Yeni + eski Input sistemi, Türkçe klavye düzeni desteği. Başka yerde `Input.` / `Keyboard.current` kullanma. |
| `Core/IHighlightable.cs` | Bakılan nesneyi parlatma arayüzü. `Carryable` uyguluyor ama **PlayerInteractor henüz çağırmıyor**. |
| `Player/FirstPersonController.cs` | Yürüme, bakış, `SetInputLocked(bool)` (menü açılınca hareketi durdurur). |
| `Player/PlayerInteractor.cs` | Raycast + SphereCast ile hedef bulma, E/sol tık etkileşim, G/sağ tık bırakma, elde taşıma. |
| `Items/Carryable.cs` | Taşınabilir eşya: yerde / elde / yuvada durumları. |
| `Items/ItemBox.cs` | İçinde `ItemData` olan ürün kutusu. |
| `Items/PlacementSlot.cs` | Raf gözü, bagaj, tezgâh yuvası. Dolu yuvada etkileşimi içindeki eşyaya devreder. `ItemPlaced` / `ItemRemoved` olayları var. |
| `Data/ItemData.cs`, `Data/ComponentData.cs` | ScriptableObject ürün verileri (fiyat, soket, RAM tipi, güç, performans). Örnekler `Assets/Data/Items`. |
| `Economy/MoneyManager.cs` | Singleton kasa: `Add`, `TrySpend`, `Format` (TL). |
| `UI/GameHUD.cs` | **Geçici** OnGUI arayüz (nişangâh, ipucu, para, bildirim). İleride Canvas/UI Toolkit ile değişecek. |
| `Shop/ShopComputer.cs` | Tamirhanedeki bilgisayar (bölme duvarında, x = -4): "Pencere OS" masaüstü (OnGUI). Dükkânım (Seviye 2 = 250.000 TL hedefi), Faturalar, Hesap Makinesi, Not Defteri; Online Alışveriş / Kamera yakında. Açıkken `ShopComputer.IsOpen` → HUD gizlenir. |
| `Shop/SignText.cs` + `Shaders/Text3D.shader` | 3D tabela yazıları (duvar arkasından görünmesin diye). |
| `World/Door.cs` | Menteşeli açılır kapı (dükkânın giriş, tamirhane, depo ve arka kapılarında). |
| `World/InfoInteractable.cs` | Genel "yakında" yer tutucusu. `ShopComputer` ile aynı işi yapıyor, birleştirilebilir. |
| `Wholesale/ShoppingCart.cs` | Alışveriş arabası: itme (fizikli), sepet yuvaları, ödenmemiş toplam. `PlayerInteractor.PushedCart` / `HandsBusy`. |
| `Wholesale/ShelfProduct.cs` | Raf bölümü: [E] ile pasif şablondan yeni (ödenmemiş) `ItemBox` çıkarır → arabaya ya da ele. |
| `Wholesale/CheckoutCounter.cs` | Kasa: şeritteki arabalar + eldeki ödenmemiş kutuları toplar, öder (`ItemBox.SetPaid`). |
| `Wholesale/AutomaticDoor.cs` | Kayan cam kapı; içeride ödenmemiş ürün varken açılmaz. |
| `Vehicle/CarController.cs` | Arcade sürüş + bin/in. `CarController.Current` = sürülen araba (HUD okur). Binince oyuncu "Sürücü Gözü"ne bağlanır, `PlayerInteractor.Suspended = true`, FPC ve CharacterController kapanır. |
| `Vehicle/CarTrunk.cs` | Bagaj kapağı (menteşe, yerel x ekseninde döner); kapalıyken bagaj yuvalarının çarpıştırıcıları kapalı. |
| `Editor/EgeaGeometry.cs` | Oyuncunun arabası (Fiat Egea görünümlü sedan) kodla: z boyunca kesit halkaları (loft), yüzeye ışınla yansıtılan "çıkartma"lar (far, stop, ızgara, çizgiler), tekerlek/iç mekân. **Unity'nin native API'lerini kullanmaz** (Quaternion.Euler, Matrix4x4.TRS, AnimationCurve yok) ki Unity'siz önizlenebilsin. Araç uzayı: +z ön. |
| `Editor/PrototypeSceneBuilder.Car.cs` | EgeaGeometry → Mesh asset'leri (`Assets/Data/Meshes/Egea`), araba malzemeleri, çarpıştırıcılar, bagaj, plakalar. |

**Oyuncunun arabası (güncel):** Sketchfab'den "Fiat Tipo Wagon" (Nieve5677, **CC BY 4.0** — `Assets/Models/Egea/KAYNAK.txt`, Emeği Geçenler'e yazılmalı).
Dönüştürme: `Tools~/Blender/tipo.py --src <gltf> --fbx Assets/Models/Egea/Egea.fbx` → ters/mm modeli çevirir, plakaları siler, tekerlekleri
(`Tekerlek_OnSol_Donen` / `_Kaliper`), direksiyonu, arka kapağı (`Bagaj_Kapagi`, tavandan menteşeli) ayırır, üçgeni ~800 bine indirir,
malzemeleri `CarMat` anahtarlarına çevirir, ölçüleri `Egea_ayar.json`'a yazar. Unity: `BuildWagonColliders` / `BuildWagonTrunk` bu json'u kullanır.
Kaynak glTF repoda yok (kullanıcının İndirilenler'inde: `fiat-tipo-wagon (1).zip`).

**Önceki kodla üretilen Egea (yedek):** `Tools~/Blender/egea.py` (Blender 5.2, `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`).
`blender -b --factory-startup --python egea.py -- --fbx "../../Assets/Models/Egea/Egea.fbx" --blend Egea.blend [--render klasör --views 1_on_sol,4_yan]`.
FBX'te hiyerarşi düz (her parça kökün çocuğu, dönme noktası = parçanın origin'i: tekerlek `Tekerlek_OnSol_Donen` göbekte, `Bagaj_Kapagi` menteşede, `Direksiyon` simit merkezinde).
Unity'de `BuildCarVisualFromModel` modeli yerleştirir, yön/ölçek/aynalamayı tekerlek konumlarından kendisi düzeltir, malzemeleri isimle (`boya`, `cam`…) `CarMat` ile değiştirir, hareketli parçalar için kendi pivotlarını kurar. FBX yoksa `EgeaGeometry` yedeği kullanılır.

**Araba önizleme (Unity'siz, eski kod modeli):** `Tools~/ArabaOnizleme/run.ps1` → EgeaGeometry'yi yazılım çiziciyle `out/*.png` olarak çizer. Arabanın şeklini değiştirince önce buna bak.

Sahne planı ve oda yerleşimi: `PrototypeSceneBuilder.cs` başındaki yorum ve `BENIOKU.md`.

## Kurallar

- **Araba:** Kullanıcı Fiat Egea'ya benzemesini istedi (şekil, oran, detaylar); logo / model yazısı konmadı.
- **Markalar kurgusal:** PlayZone 5 / 5 Pro, Voltrix, Gryphon, Kronos, MemoCore, Ampera, Fluxdisk, Kasa Pro. Gerçek marka adı (PlayStation, AMD, NVIDIA, Amazon, Windows logosu vb.) kullanma. Oyun içi işletim sistemi de "Windows'u andıran" kurgusal bir isimle sunulabilir.
- Yeni ürünleri kodla değil **ItemData/ComponentData asset'i** olarak ekle.
- Unity 6.6 uyumu: `FindFirstObjectByType` değil **`FindAnyObjectByType`**; `Rigidbody.velocity` değil **`linearVelocity`** (`#if UNITY_6000_0_OR_NEWER` ile).
- `PlacementSlot` nesnelerinin ölçeği (scale) **1,1,1** olmalı; görsel gövde ayrı çocuk nesne.
- Para işlemleri hep `MoneyManager` üzerinden.
- Her aşamayı küçük, test edilebilir parçalar hâlinde yap; her parçadan sonra kullanıcıya "şunu dene" de.
- Büyük mimari kararlardan (örn. araba fiziği yöntemi, UI sistemi seçimi) önce kullanıcıya kısa seçenek sun.

## Bilinen durum

- Kullanıcı şunu bildirdi: **kutuları sadece koşarken (Shift basılıyken) alabiliyordu.** v2'de sol tık ile alma, klavye düzeni desteği ve SphereCast eklendi. **Henüz test edilmedi.** İlk iş bunu doğrulamak.
- Kullanıcı ilk ekran görüntüsünde sahne kurucuyu henüz çalıştırmamıştı (boş SampleScene açıktı).
