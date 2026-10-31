# PC Dükkanı Simülatörü — Yol Haritası

Durum işaretleri: `[x]` bitti · `[~]` yapıldı, test bekliyor · `[ ]` yapılacak · `(öneri)` henüz onaylanmadı, kullanıcı karar verecek

---

## Genel durum (31 Ekim 2026)

**Elimizde olanlar:** dünya ve görsellik çok ilerledi. Şehir, dükkân (Seviye 1), toptancı mağazası, alışveriş arabası + kasa,
dükkân bilgisayarı ve Egea araba modeli hazır.

**Asıl eksik:** oyunun **döngüsü** henüz kapanmıyor. Para harcanabiliyor (toptancı, faturalar) ama **kazanılamıyor**:
araba sürülemiyor, kutular dükkâna gelemiyor, PC toplanamıyor, müşteri yok, satış yok. Yani şu an "gezilebilen bir sahne",
henüz "oynanabilen bir oyun" değil.

**Test borcu:** Aşama 1–3 ve 10'daki neredeyse her şey `[~]` (yapıldı ama Unity'de denenmedi). Üzerine çok şey yığmadan
önce bir **test turu** yapmak, ileride hataların birbirine karışmasını önler.

---

## Öncelik sırası (önerim)

Amaç: en kısa yoldan **ilk oynanabilir döngü** (para kazanıp harcayabilmek). Görsellik sonra derinleşir.

| # | Kilometre taşı | Neden şimdi | Kapsam |
|---|---|---|---|
| 0 | **Test turu** | `[~]` yığını büyüdü | Kutu alma hatası, kapılar, toptancı ödeme, bilgisayar, araba görünümü + bagaj. Hataları düzelt, `[x]` yap. |
| 1 | **Araba sürüşü** | Döngünün ilk halkası | Bin/in, sür, park et; bagajdaki kutular arabayla gider; toptancıya park/yükleme alanı. |
| 2 | **Basit PC toplama** | Satılacak ürün üretmek | Kutu aç → parçalar; tezgâhta kasaya tak; uyumluluk kontrolü; toplanmış PC eşyası. |
| 3 | **Rafa koy + etiket + basit satış** | İlk para girişi | Fiyat etiketi; müşteri gelir, rafa bakar, fiyat makulse alır (pazarlık sonra). |
| 4 | **Gün döngüsü + kayıt** | Oyunu kapatıp açınca ilerleme kalmalı | Saat, açılış/kapanış, gün sonu raporu, kaydet/yükle. |
| ↳ | **İlk oynanabilir sürüm burada** | | Buradan sonra eklenen her şey derinlik. |
| 5 | Pazarlık | Satışa kişilik katar | Müşteri kişiliği, teklif/karşı teklif, sabır. |
| 6 | OS kurulumu ve testler | Toplama ile satış arasında iş | Dünya içi monitör, kurulum, testler, "satışa hazır". |
| 7 | Format ve tamir işleri | Ürün almadan gelir kaynağı | Önce format (sabit fiyat, basit), sonra tamir (teşhis). |
| 8 | Online alışveriş + kamera | Bilgisayarı canlandırır | Kargoyla ürün gelir; güvenlik kamerası. |
| 9 | Seviye 2 dükkân | Hikâyenin hedefi | 250.000 TL ile büyük mağaza. |
| 10 | Cilalama | Son | Ses, menü, kalıcı UI, build. |

---

## Açık kararlar (kullanıcıya sorulacak)

- [x] **Sürüş fiziği:** basit arcade ile başlandı (gerekirse sonra WheelCollider'a geçilir)
- [ ] **Online alışveriş uygulamasının adı:** öneriler ParçaSepeti, Kargola, TıkTık Pazar.
- [ ] **Dükkânın adı** (tabela, fatura, gün sonu raporunda geçecek): öneriler "Bit Bilgisayar", "Çip Teknik", "Kasa & Kart".
- [ ] **Gün uzunluğu:** oyun günü gerçek kaç dakika? (önerim: 12–15 dk, dükkân 09:00–21:00)
- [ ] **Zorluk / para dengesi:** başlangıç parası, kâr marjı, fatura tutarları (Seviye 2'ye kaç oyun gününde ulaşılsın? önerim: 20–30 gün).

---

## Teknik borç

- [ ] **Kutu alma hatasını doğrula** (eskiden sadece koşarken alınıyordu; v2'de düzeltildi, denenmedi)
- [ ] Bakılan eşyayı parlat (`IHighlightable` → `PlayerInteractor`'a bağla) — küçük iş, hissi çok iyileştirir
- [ ] `InfoInteractable` artık kullanılmıyor, silinebilir
- [ ] Geçici OnGUI HUD → kalıcı UI (Canvas / UI Toolkit). Etiket, pazarlık ve kurulum ekranları gelmeden **önce** karar vermek iyi olur; yoksa hepsi iki kez yazılır.
- [ ] Performans kontrolü: sahnedeki ışık / gölge sayısı, araba (~105 bin üçgen) için uzaktan sade model (LOD) gerekebilir
- [ ] Sahne kurucu her seferinde sahneyi sıfırdan kuruyor: **kayıt sistemi** gelince oyuncunun ilerlemesi sahneden ayrı tutulmalı

---

## Aşama 1 — Temel sistemler + dükkân binası

- [~] Birinci şahıs karakter (yürü, koş, zıpla, fare ile bak)
- [~] Etkileşim sistemi (`IInteractable`), E / sol tık
- [~] Kutu alma, taşıma, bırakma (`Carryable`, `ItemBox`)
- [~] Yuvalar: bagaj, raf, tezgâh, vitrin, kasa (`PlacementSlot`)
- [~] Ürün verileri (ScriptableObject) + 10 örnek ürün
- [~] Para sistemi (`MoneyManager`)
- [~] Geçici HUD
- [~] Dükkân binası: satış alanı (vitrinler, raflar, kasa), tamirhane (tezgâhlar, kurulum masası, dükkân bilgisayarı), depo, arka kapı, otopark
- [~] Kapılar sahnede (`Door.cs`): cam giriş kapısı, tamirhane, depo, arka kapı
- [~] Dükkân Seviye 1 baştan tasarlandı (`PrototypeSceneBuilder.Shop.cs`): tuğla cephe + tente, laminat/epoksi zemin, asma tavan, yeni raflar, L kasa tezgâhı (yazar kasa, müşteri ekranı, kart terminali, barkod okuyucu), kasanın arkasında bilgisayar masası, tamirhane (delikli alet panosu, antistatik mat), metal depo rafları
- [ ] Depo rafları gerçekten stok tutsun: kutular depoya konabilsin, sayısı görülsün

## Aşama 2 — Toptancı

- [~] Toptancı mağazası (çevre yolunun kuzey dışı): cephe, saçak, tabela, ön meydan, reyonlar, palet rafları, TV duvarı, promosyon masası, 2 kasa + kasiyer, otomatik kapı
- [~] Alışveriş arabası: [E] ile it, [G] ile bırak; raftan doğrudan arabaya ekleme; kutular sepete boyutlarına göre dizilir
- [~] Kasa: şeritteki arabadaki / eldeki ödenmemiş ürünlerin toplamı, `MoneyManager.TrySpend` ile ödeme, müşteri ekranı
- [~] Ödenmemiş ürünle otomatik kapı açılmaz, güvenlik kapısı kırmızı yanar
- [ ] Ürün çeşidi az (10 ürün): her kategoriden 3–4 seviye (giriş / orta / üst) ekle — asset olarak
- [ ] Stok sınırı (şu an sınırsız), gün başında yenilenir
- [ ] Toptancı çalışma saatleri (gün döngüsüyle)
- [ ] Ödenmiş kutuları alışveriş arabasıyla otoparktaki aracın bagajına taşıma

## Aşama 3 — Araba

- [~] Araba modeli: Fiat Egea (sedan) görünümlü, kodla üretilen gövde (`EgeaGeometry.cs`): kemerli tekerlek yuvaları, omuz çizgisi, eğik C direği, farlar (projektör + LED şerit + sinyal), stoplar, sis farları, petek ızgaralar, difüzör, aynalar, kapı kolları, panel aralıkları, depo kapağı, silecekler, 5 çift kollu 16" jant + lastik + disk/kaliper, iç mekân (torpido, direksiyon, koltuklar), TR plakaları. Logo yok (sade amblem).
- [~] Araba modeli: Sketchfab "Fiat Tipo Wagon" (CC BY 4.0, Nieve5677) Blender'da oyuna uyarlandı (`Tools~/Blender/tipo.py`): plakasız, ~800 bin üçgen, ayrı tekerlek/direksiyon/arka kapak, bagaj havuzunda 4 yuva. Logolar şimdilik duruyor (kullanıcıya soruldu).
- [ ] Emeği Geçenler ekranına model yazarını ekle (lisans şartı)
- [~] Bagaj kapağı [E] ile açılır/kapanır (`CarTrunk`), bagaj havuzunda 4 yuva (kapalıyken ulaşılamaz)
- [~] Arabaya bin / in (E), arcade sürüş (`CarController`: ışınlı yaylı süspansiyon, ön çekiş, el freni Space, sürücü koltuğundan fareyle bakış, HUD'da hız). İniş: önce sol kapı, doluysa sağ / arka / ön.
- [~] Sürüş sırasında tekerlekler döner, ön tekerler ve direksiyon simidi döner, süspansiyon çalışır
- [ ] Frenle stop lambaları yanar, geri viteste beyaz lamba, gece farlar yanar
- [~] Bagajdaki kutular araba ile birlikte gider (yuvaya bağlı; araba gövdesiyle çarpışmaları kapatılır)
- [ ] Arka koltuk da 2 yuva olsun (büyük alışverişte ek yer) — kapılar açılınca
- [ ] Kamera seçeneği: sürücü koltuğu / arkadan takip (C tuşu)
- [~] Şehir: çevre yolu + orta cadde + S virajlı bağlantı yolu (yuvarlak köşeler), 10 bina, taş yaya yolları (bordür + babalar), tüm yolların iki yanında 2,5 m kaldırım, 12 zebra yaya geçidi
- [~] Dükkânın yanından (x -12.5 … -8.5) caddeden arka otoparka asfalt yan yol; kaldırımı rampayla geçer
- [ ] Dükkân ↔ toptancı yolu, park yerleri
- [ ] Toptancının önüne araç park/yükleme alanı ve yola bağlantı
- [ ] Şehrin dışına çıkmayı engelleyen sınırlar (görünmez duvar yerine bariyer / çit)

## Aşama 4 — PC toplama

- [ ] Kutu açma → içinden parça çıkması (boş kutu atılır / geri dönüşüm)
- [ ] Tezgâhta kasa: parça yuvaları (anakart, CPU, RAM, GPU, PSU, depolama, soğutucu)
- [ ] Uyumluluk kontrolü: soket, RAM tipi, güç kaynağı yeterliliği — yanlış parçada ne olacağı net olsun (takılmaz / açılmaz)
- [ ] Toplanmış bilgisayar = yeni taşınabilir eşya (parçaları ve değeri kayıtlı)
- [ ] Parçaların gerçek 3D modelleri (kutudan çıkınca küp değil): anakart, ekran kartı, RAM çubuğu, kasa...

## Aşama 5 — İşletim sistemi kurulumu ve testler

- [ ] Kurulum masasında monitör arayüzü (dünya içi ekran)
- [ ] Kurgusal işletim sistemi kurulumu (ilerleme çubuğu, adımlar)
- [ ] Testler: açılış (POST), stres testi, sıcaklık, performans puanı
- [ ] Bilgisayarın "satışa hazır" durumu — test edilmemiş PC daha ucuza satılır / iade riski taşır

## Aşama 6 — Fiyat etiketi ve raf

- [ ] Rafa konan bilgisayar / konsola fiyat etiketi yapıştırma (fiyat girme arayüzü)
- [ ] Etiket görünür olsun (rafta ürünün önünde)
- [ ] Konsollar: alış fiyatına kâr ekleyerek etiketleme
- [ ] Etiket ekranında piyasa fiyatı ve maliyet gösterilsin (oyuncu kârını görsün)

## Aşama 7 — Müşteriler ve pazarlık

- [ ] Müşteri yapay zekâsı (NavMesh): gir, rafları gez, ürün seç, kasaya gel
- [ ] Müşteri modelleri (şimdilik sade insan figürü, sonra çeşitli)
- [ ] Müşterinin aklındaki fiyat: piyasa fiyatı + kişilik (cimri / rahat)
- [ ] Pazarlık arayüzü: teklif, karşı teklif, sabır; anlaşma veya vazgeçip gitme
- [ ] Satış: para kasaya, ürün müşteriye; ödeme nakit / kart (kart terminali zaten var)

## Aşama 8 — Tamir işleri

- [ ] Müşteri kendi bilgisayarıyla gelir, sorunu söyler (ör. "ekran gelmiyor", "açılmıyor", "çok ısınıyor")
- [ ] Gizli arıza: bozuk RAM, yanık PSU, arızalı GPU, kurumuş termal macun vb.
- [ ] Aletler: tornavida, multimetre, test RAM'i / test ekran kartı, termal macun
- [ ] Teşhis → parça değiştir/onar → teslim
- [ ] Teslimde pazarlık (müşteri az pazarlıktan sonra mecburen öder)
- [ ] Tamire bırakılan PC için teslim fişi + teslim günü (geç kalırsan müşteri kızar)

## Aşama 9 — Format işleri

- [ ] Müşteri format için bilgisayar getirir
- [ ] Kurulum masasında format işlemi
- [ ] **Sabit fiyat, pazarlık yok**

## Aşama 10 — Dükkân bilgisayarı

- [~] Kurgusal masaüstü "Pencere OS" (`ShopComputer.cs`, OnGUI): simgeler, başlat menüsü, görev çubuğu, saat
- [~] Uygulamalar: Dükkânım (Seviye 2 hedefi), Faturalar (öde), Hesap Makinesi, Not Defteri
- [ ] Amazon'u andıran ama farklı isimli online alışveriş uygulaması (isim kullanıcıyla seçilecek) — şimdilik "yakında"
- [ ] Faturaların her ay yenilenmesi (gün döngüsü gerekli); ödenmezse ceza / elektrik kesilir
- [ ] Güvenlik kamerası satın alma ve izleme — şimdilik "yakında"
- [ ] **Seviye 2 dükkân** (hikâye hedefi, 250.000 TL): tamamen değişen büyük mağaza. Seviye 1 bilerek sade/ucuz.

## Aşama 11 — Cilalama

- [ ] Gün döngüsü (açılış/kapanış, gün sonu raporu) — *öncelik sırasında 4. sıraya alındı*
- [ ] Kayıt / yükleme — *öncelik sırasında 4. sıraya alındı*
- [ ] Sesler: adım, kapı, kutu, kasa bipi, araba motoru, şehir ambiyansı, dükkân müziği
- [ ] Ana menü, ayarlar (fare hassasiyeti, ses, grafik kalitesi, tuşlar)
- [ ] Canvas / UI Toolkit ile kalıcı arayüz (OnGUI HUD'un yerine)
- [ ] Gece/gündüz ışığı: akşam sokak lambaları, dükkân ışıkları, araba farları
- [ ] Windows build (.exe)

---

## Aşama 12 — Önerilerim (onay bekliyor)

Bunlar planın parçası değil, fikir. Beğendiklerini söylersen uygun aşamaya taşırım.
Her birinin yanında oyuna katkısı ve tahmini büyüklüğü var (küçük / orta / büyük).

### Ekonomi ve işletme
- (öneri) **Dükkân itibarı / yorum puanı (1–5 yıldız):** memnun müşteri puanı artırır, puan arttıkça daha çok ve daha zengin müşteri gelir. Pazarlıkta çok sıkmak, geç tamir, bozuk PC satmak puanı düşürür. Bütün sistemleri birbirine bağlar. — *orta*
- (öneri) **İkinci el alım:** müşteri eski PC'sini / parçasını satmaya gelir; ucuza alıp tamir edip satarsın. Tamir becerisine para kazandırır. — *orta*
- (öneri) **Özel sipariş:** "20.000 TL'ye oyun bilgisayarı istiyorum" diyen müşteri; bütçeye göre toplayıp teslim edersin. PC toplamayı amaçlı hale getirir. — *orta*
- (öneri) **Garanti ve iade:** test edilmeden satılan PC birkaç gün sonra arızayla geri gelebilir. Testleri anlamlı kılar. — *küçük*
- (öneri) **Fiyat dalgalanması ve kampanyalar:** toptancıda haftanın indirimli ürünü, zamanla ucuzlayan eski nesil parçalar. Ne zaman stok yapacağın kararını getirir. — *küçük*
- (öneri) **Kargo ile alım (online uygulama):** toptancıya gitmeden sipariş ver, ertesi gün kapıya gelir ama kargo ücreti var; nadir ürünler sadece online. Arabaya alternatif. — *orta*
- (öneri) **Benzin:** araba yakıt harcar, şehre bir benzinlik eklenir. Gerçekçilik katar ama sıkıcı olabilir; isteğe bağlı açılabilir. — *orta*

### Dükkân hayatı
- (öneri) **Hırsızlık + güvenlik kamerası:** ara sıra müşteri ürün çalmaya çalışır; kamera ve kapıdaki alarm bunu yakalar. Kamera uygulamasına gerçek bir amaç verir. — *orta*
- (öneri) **Vitrin etkisi:** vitrinde pahalı / güzel ürün varsa dışarıdan geçen yayalar içeri girer. Yayaların olduğu şehir gerektirir. — *orta*
- (öneri) **Dükkânı süsleme:** dükkân bilgisayarından RGB şerit, poster, bitki, halı, daha iyi raf satın alma; itibara küçük katkı. Seviye 1 ile 2 arasında ilerleme hissi verir. — *orta*
- (öneri) **Çalışan (Seviye 2'de):** kasiyer ya da teknisyen işe al, maaş öde; büyük mağazayı tek başına yönetmek zorlaşınca anlam kazanır. — *büyük*

### Şehir ve dünya
- (öneri) **Trafik ve yayalar:** caddede yavaş giden birkaç araç, kaldırımda yürüyen insanlar. Şehri canlı gösterir. — *büyük*
- (öneri) **Hava durumu:** yağmurlu günlerde daha az müşteri, ıslak asfalt yansımaları. — *orta*
- (öneri) **Rakip dükkân:** şehirde başka bir bilgisayarcı; fiyatların çok yüksekse müşteri oraya gider. — *orta*

### Rahatlık ve his
- (öneri) **Görev / ipucu listesi (ilk günler):** "Toptancıya git", "İlk PC'ni topla" gibi adımlar; oyunu ilk kez açan nereden başlayacağını bilsin. — *küçük*
- (öneri) **Telefon:** müşteriler arayıp tamir randevusu / özel sipariş verir; banka bildirimi gelir. Dükkân bilgisayarına gitmeden haber alırsın. — *orta*
- (öneri) **İstatistik ekranı:** günlük / haftalık ciro, kâr, en çok satan ürün grafikleri (dükkân bilgisayarında). — *küçük*
