# ID Lab

**A native 3D interactive science laboratory for desktop and mobile.** ID Lab brings high-school Physics,
Chemistry and Biology experiments into one app, as a 3D next-generation take on PhET-style simulations.

* **Engine:** Unity 6 (C#). Targets Windows, macOS, Linux, iOS and Android.
* **Smart-board edition:** a single, Unity-free Windows `IDLab.exe` (raylib front-end, Turkish touch UI) over
  the same core. See the Turkish section below.
* **Simulation core:** a pure C# library with no Unity dependency. It is unit tested with `dotnet test` and
  shared with Unity as a local UPM package.

See **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** for the full architecture, data model, frame loop and
module roadmap.

## Akıllı tahta sürümü (Windows .exe) — Unity gerekmez

`desktop/IDLab.Desktop`, aynı simülasyon çekirdeğini kullanan, Unity'siz, yerel bir Windows uygulamasıdır.
Sürüm **1.0**. Tek bir `IDLab.exe` dosyasıdır (yaklaşık 13 MB): .NET çalışma ortamı, grafik kütüphanesi (raylib) ve Türkçe font içine
gömülüdür. **Kurulum gerekmez.**

* **Çalıştırma:** `IDLab.exe` dosyasını tahtanın bilgisayarına kopyalayın ve çift tıklayın. Uygulama tam
  ekran açılır. İlk açılışta Windows "Bilgisayarınız korundu" uyarısı gösterebilir, çünkü dosya imzasızdır.
  **Ek bilgi → Yine de çalıştır** ile açın.
* **Gereksinim:** Windows 10/11 (64 bit) ve OpenGL 3.3 destekleyen ekran kartı. Akıllı tahtalardaki
  Intel/AMD tümleşik grafik kartları yeterlidir.
* **Kimlik:** mor–pembe ID Lab logosu uygulama simgesi, açılış ekranı (yükleme çubuğuyla) ve üst çubukta yer alır;
  renk teması logodan türetilmiştir. Üst çubuktaki logoya dokunmak "Hakkında" kartını açar (sürüm, kimyasal ve deney
  sayısı, kısa kullanım, veri kaynakları).
* **Sekmeler:** üst çubukta dört sekme vardır.
  * **Laboratuvar:** gerçekçi bir laboratuvar odasında 3D deney tezgâhı.
  * **Kimyasallar (kimyasal dolabı):** 40 000'den fazla kimyasal: iyonik bileşikler, hidratlar ve mineraller, asitler,
    bazlar, 118 elementin tamamı (radyoaktif ve sentetik olanlar işaretli; güvenlik gereği tezgâha eklenmez), organik
    ve biyokimyasal bileşikler, indikatörler. ~31 600 organik bileşik PubChem tablosundan (erime/kaynama noktasıyla)
    sıkıştırılmış olarak gömülüdür. Yaklaşık 7300 bileşik CRC Handbook of
    Chemistry and Physics tablolarından (`chemicals` Python paketi) alınmıştır: inorganik tuzlar, oksitler ve
    hidratlar ad → katyon + anyon çözümlemesiyle, organik bileşikler PubChem formülüyle, Türkçe IUPAC adlarıyla.
    Bu kayıtlarda tehlike ve çözünürlük verisi yoksa kart bunu açıkça söyler. Türkçe ya da İngilizce adı, yaygın adı
    ("göztaşı", "kezzap"), formülü (CCl4) ya da CAS numarasını (58-08-2) yazarak arayın; tahtada ekran klavyesi açılır. "Elementlerden seç" ile
    periyodik tablodan elementlere dokunarak o elementleri içeren bileşikleri, "İyonlardan bileşik yap" ile bir katyon
    ve bir anyon seçerek yük dengesiyle oluşan formülü görün. Seçilen kimyasal çözelti şişesi, saf sıvı şişesi ya da
    katı kavanozu olarak tezgâha eklenir. Tepkimeler (çökelme, nötrleşme, gaz çıkışı, metal + asit, yer değiştirme,
    aktif metal + su) iyon kurallarıyla tüm katalogda çalışır.
    **Nasıl elde edilir?** Her bileşiğin kartında simülasyonun gerçekten yürüttüğü yollar denkleştirilmiş denklemleriyle
    listelenir (elementi havada yakma, doğrudan birleşme Fe + S → FeS, metal + asit, nötrleşme, oksit/karbonat + asit,
    çöktürme, yer değiştirme, metal/oksit + su). Elementin kartında "Bu elementten neler yapılır?" bölümü vardır.
    "Tezgâhta hazırla" gereken şişe/kavanozları, su dolu beheri ve gerekiyorsa Bunsen bekini tezgâha koyar.
  * **Periyodik Tablo:** 118 elementin 3D tablosu. Bir elemente dokunun: dönen Bohr atom modeli (proton, nötron,
    elektron katmanları) ve bilgi kartı açılır. Kartta atom numarası, kütle, grup/periyot/blok, elektron dizilimi,
    elektronegatiflik, erime/kaynama noktası, yoğunluk, keşif yılı ve kısa bir not bulunur. Sütun yüksekliği atom
    kütlesi, elektronegatiflik, yoğunluk ya da erime noktasını gösterebilir. Kategori düğmeleri tabloyu süzer.
  * **Bilgi ("Bilmemiz Gerekenler"):** laboratuvar güvenliği, GHS tehlike işaretleri, malzemeler, pH ölçeği ve
    indikatörler, tepkime türleri, metallerin aktiflik sırası, mol ve formüller, ısı ve hâl değişimi, ayırma
    yöntemleri, periyodik özellikler ve fizik formülleri.
* **Malzemeler:** 50'den fazla malzeme ve kimyasal resimli bir listede, kategori düğmeleriyle (Cam, Isıtıcı, Ölçüm,
  Sıvılar, İndikatör, Katılar) bulunur.
  * Cam malzemeler: beherler, erlenmayer, balon joje, dereceli silindir, deney tüpü ve tüplük, petri kabı.
  * Isıtıcılar ve ölçüm aletleri: Bunsen beki, ısıtıcı tabla, hassas terazi (dara), büret (musluk, verilen hacim
    sayacı, "Doldur"), termometre ve pH probu.
  * Kimyasallar: 17 sıvı (asitler, bazlar, tuz çözeltileri, H₂O₂, etanol, çamaşır suyu...), indikatörler ve 11 katı
    (tuz, karbonat, mermer, CuSO₄/KMnO₄ kristalleri, Mg, Zn, Fe, Cu, Na).
* **Tehlike işaretleri:** etiketlerde ve bilgi sayfalarında resmî GHS01–GHS09 sembolleri ve resmî Türkçe adları
  (Patlayıcı, Yanıcı, Oksitleyici, Basınçlı gaz, Aşındırıcı, Akut toksik, Tahriş edici, Ciddi sağlık tehlikesi, Çevre
  için tehlikeli) kullanılır.
* **Yangın ve patlama:** alevin üstünde ısınan yanıcı sıvıların buharı tutuşur; eter gibi çok uçucu sıvıların buharı
  patlar; ısıtıcı tablanın kızgın yüzeyi düşük tutuşma sıcaklıklı sıvıları (eter, CS₂, hekzan) tutuşturur. Mg, S, P,
  Na, Fe tozu gibi katılar alevde kendi renkleriyle yanıp oksitlerine dönüşür (Mg → MgO beyaz duman, S → SO₂);
  bakır yalnızca kararır (CuO). Patlayıcılar (TNT, azitler) ısınınca infilak eder; alevdeki hidrojen "pat" diye patlar.
* **Patlamalar:** sodyum suda tepkimeye girerken (kırıntı köpürüp koşturur, ≈0,25 g ve üstü patlar), K/Rb/Cs + su, termit
  (Al + Fe₂O₃ ısıtılınca), amonyum nitratın güçlü ısıtılması, uçucu buharlar ve patlayıcılar patlar. Patlama animasyonu:
  ateş topu, tezgâhta şok halkası, kıvılcımlar, sıçrayan sıvı damlaları, duman sütunu, ekran parlaması ve zıplayan kap.
* **Fil diş macunu:** "Fil Diş Macunu" deneyinde %12 H₂O₂, bulaşık deterjanı, kuru maya (katalaz enzimi), kırmızı gıda boyası
  ve KI vardır. Maya H₂O₂'yi parçalar (ılıkta hızlı, soğukta yavaş, 60–75 °C'de enzim bozulur); deterjan varsa çıkan oksijen
  kaptan fışkıran renkli bir köpük kulesi olur ve tezgâha yayılır. Tepkime ısı verir (termometreyle izlenebilir).
  Köpük tek parça, yumuşak bir gövde olarak simüle edilir: önce kabı sıvı yüzeyinden doldurur, ağızdan oksijen hacmi ÷
  ağız alanı hızıyla çıkan bir sütun olur, kendi ağırlığıyla eğilip devrilir, camın dışından sarkar ve tezgâhta kıvrılarak
  yığılır (kabarcık dokulu yüzey, kendine ve diğer malzemelere çarpar, zamanla söner). Hızlı tepkimede tepeden köpük
  parçaları sıçrar.
* **Ekipmanlar:** ısıtıcı tabla (emaye gövde, cam-seramik üst yüzey, kızaran rezistans, tırtıklı düğmeler) ve hassas
  terazi (eğik ön panel, tuşlar) yenilendi; ikisinin de üzerinde gerçek LCD ekran vardır (°C / OFF, "0,00 g").
  Gövdeler yuvarlatılmış kenarlı modellerdir, cam kaplar 96 dilimle çizilir; şişe/kavanoz etiketleri 2048×512, tüm dokular
  anizotropik süzmeyle keskindir.
* **Dökme:** otomatik dökmede eğim dökme modelinden hesaplanır; kap hedefin yanında sabit durur, akış eşit hızda sürer ve
  sonunda yavaşça kesilir (titreme yok).
* **Dokunmatik:** Windows'un dokunmatik "bas-bırak" mesajları doğrudan yakalanır; her şey tek dokunuşla çalışır.
  Sürüklenen nesnenin tabanı parmağın ucunu izler; yalnızca üst yüzeylere (bek tel kafesi, terazi, beher ağzı) oturur,
  uygun bir yer yoksa tezgâhta boş bir yere bırakılır. İnce nesneler (termometre, prob) için dokunma alanı geniştir.
* **Tema ve kalite:** üst çubukta açık/koyu tema (☀/☾) ve "Kalite" düğmesi vardır. Otomatik kalite 3D sahneyi en fazla
  1440 satırda çizer (4K tahtada ~2,3 kat daha az piksel), kare süresi uzarsa çözünürlüğü kendiliğinden düşürür ve kenarları
  FXAA ile yumuşatır; MSAA yalnızca "Yüksek" modda açılır. Ayarlar `%LOCALAPPDATA%\IDLab\ayarlar.txt` dosyasında saklanır.
* **Kolay dökme:** bir şişeyi sürükleyip başka bir kabın üstüne bırakın: 25 mL döker ve yerine döner.
  Ya da şişeyi seçip "Dök → [kap]" düğmesine, sonra tezgâhta dökülecek kaba dokunun ve miktarı seçin.
* **Titrasyon:** erlenmayer büretin tabanına kendiliğinden oturur (büret ucu her zaman kabın ağzına gelir).
  "1 damla / 0,5 mL / 1 mL / 5 mL" düğmeleri tam o kadar NaOH damlatır. Canlı veri panelinde pH – mL titrasyon
  eğrisi çizilir, dönüm noktası işaretlenir ve asit derişimi (M·V/V) hesaplanır.
* **Gerçekçi dökme:** "Dök → 10 mL / 25 mL / 50 mL / Tümü" düğmeleriyle kap kalkar, beherin üstüne gelir, yavaşça
  eğilip istenen miktarı döker ve yerine geri konur. Akışın ucu ağızdan aşağı düşer, dökme bitince kuyruğu kopar.
  Dökülen sıvı kaptan kaba akan bir akış olarak görünür. Yavaş dökünce damlar ve hedef sıvıda
  halkalar oluşur. Eğilen kaptaki sıvı yüzeyi yatay kalır. Metaller ve kristaller katı parçalar hâlinde dökülür ve
  kabın dibinde birikir.
* **Kullanım (yalnızca dokunma):**
  * Malzemeyi soldaki panelden tezgâha sürükleyin. Listeyi yukarı-aşağı sürükleyerek kaydırın.
  * Beheri bekin tel kafesine, termometreyi behere bırakın; yeşil halka yerine oturacağını gösterir.
  * Gaz için kırmızı vanayı sürükleyin ya da alttaki kaydırıcıyı kullanın.
  * **Dökmek** için şişeyi beherin yanına koyup "Dök" kaydırıcısını çekin. Şişe kendiliğinden beherin
    üstüne kalkıp döker, kaydırıcı sıfıra dönünce yerine geri gelir.
  * Paneller kenardaki ◀ ▶ ▼ düğmeleriyle daraltılabilir.
  * Sağdaki panelde canlı grafikler görünür (sıcaklık, pH, terazi kütlesi, büretten verilen hacim). "Kaydet" düğmesi verileri
    `Belgeler\IDLab\*.csv` dosyasına kaydeder.
  * Kamera için sağdaki ◀ ▶ ▲ ▼ + − düğmelerini kullanın.
  * Kapatmak için **Kapat** düğmesine iki kez dokunun. Esc uygulamayı kapatmaz.
* **Derleme:** `dotnet publish desktop/IDLab.Desktop -c Release -r win-x64 -o dist` komutu `dist/IDLab.exe`
  dosyasını üretir. Linux'tan da derlenebilir, CI her push'ta derler.
* **Test seçenekleri:** `--windowed 1920x1080`,
  `--demo about|gallery|gallery2|heating|acid|safety|titration|precip|metals|pour|cabinet|hotplate|burn|toothpaste|chemicals|chem:<arama>|periodic|info`, `--quality otomatik|yuksek|dengeli|hizli`, `--light`, `--profile`, `--topic N`, `--frames N`,
  `--screenshot dosya.png`, `--trace`, `--camera yaw,pitch,mesafe`.

## What the prototype does

* **3D lab bench** in a dark, modern lab room. All equipment is procedural 3D: glassware with a liquid
  surface that stays level when tilted, a Bunsen burner with a tripod and gauze, and probes with floating
  readouts.
* **Equipment inventory** (left). Drag items onto the bench, or tap to place them. Drop an item back on the
  inventory to remove it.
* **Drag, drop and snap.** Items hover over whatever is beneath them. A beaker snaps onto the burner gauze, and
  thermometers or pH probes snap into beakers. Snapped items move with their host.
* **Bunsen burner.** Turn the red 3D gas valve, or use the inspector slider. The flame, light and gauze glow
  scale with the valve, and heat goes into whatever sits on the gauze.
* **Thermal model.** Heating, convective cooling and boiling, with a temperature plateau at 100 °C while
  water evaporates.
* **Pouring.** Tilt a bottle or beaker with `Q`/`E` or the Pour slider. Liquid reaches the lip at a tilt that
  depends on fill level, and flows into the container below or spills onto the bench.
* **Chemistry.** Mixing, neutralisation heat, pH (strong and weak acids, buffers) and universal indicator
  colours. Hazards include sodium + water (fizz or explosion) and bleach + acid (chlorine gas), shown with
  particle effects, flashes, camera shake and warnings.
* **Live data panel** (right). Every probe on the bench gets a live chart of value vs. time. You can switch
  between a 2-minute and a full window, and clear the data.
* **Micro view inset.** An atomic-scale view of the selected container. Molecules speed up with temperature
  and escape when boiling. Ions dissociate (H₃O⁺/Cl⁻, Na⁺/OH⁻), and weak acids show their real degree of
  dissociation.
* **Simulation speed** of Pause, 1×, 5× or 20×, plus an experiment selector: Free Lab, Heating Curve,
  Acids/Bases & pH, and Lab Safety.

## Getting started

### Run in Unity

1. Install **Unity 6** (6000.0 LTS) with Unity Hub.
2. In Unity Hub, choose **Add → Add project from disk** and select the `app/` folder. The core package is
   linked from `../core/com.idlab.core` through `Packages/manifest.json`.
3. Open any scene, even the default empty one, and press **Play**. `LabBootstrap` builds the whole lab in
   code. There are no binary scene or prefab assets.

Before making a player build, run **ID Lab → Generate Shader References** once (it also runs automatically
before every build). This keeps the shader variants for runtime-created materials in the build.

### Controls

| | Desktop | Touch |
| --- | --- | --- |
| Add equipment | drag from inventory / click | drag from inventory / tap |
| Move / attach | left-drag; release on a green snap ring | one-finger drag |
| Pour | `Q` / `E` (or Inspector → Pour) | Inspector → Pour slider |
| Gas valve | drag the red knob | drag knob / Inspector slider |
| Camera | right-drag or drag empty space · wheel | drag empty space · two-finger drag · pinch |
| Delete | `Delete` / drop on inventory | drop on inventory / Inspector → Remove |

### Test the simulation core (no Unity needed)

```bash
dotnet test core/tests/IDLab.Core.Tests          # 96 unit tests
dotnet build tools/unity-typecheck                # type-checks all Unity scripts against Unity reference assemblies
```

### Headless simulation runner

```bash
dotnet run --project tools/sim-cli -- heating     # heating curve CSV (time, thermometer, liquid temp, volume)
dotnet run --project tools/sim-cli -- neutralize  # pH and temperature while adding NaOH to HCl
```

## Repository layout

```
core/   com.idlab.core (simulation, data model, chemistry, sensors, micro-view model) + tests
app/    Unity 6 project: views, interaction, HUD, micro view, VFX
desktop/ smart-board edition: native Windows .exe (raylib), Turkish touch UI
tools/  unity-typecheck (CI), sim-cli (headless scenarios)
docs/   ARCHITECTURE.md
```
