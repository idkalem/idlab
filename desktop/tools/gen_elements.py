"""Generates desktop/IDLab.Desktop/Elements.g.cs (all 118 elements) from the `mendeleev` package plus the
Turkish names, categories and short usage notes below.

    python -m venv venv && venv/bin/pip install mendeleev && venv/bin/python desktop/tools/gen_elements.py
"""
import json, os, sys, warnings
warnings.filterwarnings("ignore")

TR = """H|Hidrojen|Evrendeki en bol element; yakıt pillerinde, amonyak ve margarin üretiminde kullanılır.
He|Helyum|Balonlarda, MR cihazlarının soğutulmasında ve dalış tüplerinde kullanılır.
Li|Lityum|Telefon ve elektrikli araç pillerinin temel elementi.
Be|Berilyum|Hafif ve sert alaşımlarda, uzay teleskoplarının aynalarında kullanılır.
B|Bor|Türkiye dünya bor rezervlerinin büyük kısmına sahiptir; cam, deterjan ve mıknatıslarda kullanılır.
C|Karbon|Canlıların temel yapı taşı; elmas, grafit ve grafen biçimleri vardır.
N|Azot|Havanın %78'i; gübre yapımında ve gıdaların korunmasında kullanılır.
O|Oksijen|Havanın %21'i; solunum ve yanma için gereklidir.
F|Flor|Diş macunlarında (florür) ve teflon kaplamalarda bulunur; en elektronegatif element.
Ne|Neon|Kırmızı-turuncu ışık veren reklam tabelalarında kullanılır.
Na|Sodyum|Sofra tuzunun (NaCl) bileşeni; suyla şiddetli tepkime verir.
Mg|Magnezyum|Parlak beyaz alevle yanar; hafif alaşımlarda ve klorofilde bulunur.
Al|Alüminyum|Yer kabuğunda en bol metal; kutu, folyo ve uçak gövdelerinde kullanılır.
Si|Silisyum|Bilgisayar çiplerinin ve güneş panellerinin temel maddesi; kumda (SiO₂) bulunur.
P|Fosfor|DNA'nın ve kemiklerin yapısında bulunur; kibrit ve gübrelerde kullanılır.
S|Kükürt|Sarı katı; sülfürik asit ve lastik üretiminde kullanılır.
Cl|Klor|Suyun dezenfeksiyonunda ve PVC üretiminde kullanılır; gazı zehirlidir.
Ar|Argon|Havanın %0,93'ü; ampullerde ve kaynakta koruyucu gaz olarak kullanılır.
K|Potasyum|Bitkiler için temel besin; muz ve patateste bol bulunur.
Ca|Kalsiyum|Kemik ve dişlerin yapısında; kireç taşı ve mermerde (CaCO₃) bulunur.
Sc|Skandiyum|Bisiklet kadrolarında ve stadyum lambalarında alüminyumla alaşım yapar.
Ti|Titanyum|Hafif ve korozyona dayanıklı; protezlerde ve uçaklarda kullanılır.
V|Vanadyum|Çeliği güçlendirmek için katılır; büyük piller (akış pilleri) için umut vaat eder.
Cr|Krom|Paslanmaz çeliğe dayanıklılık, nesnelere parlak kaplama sağlar.
Mn|Mangan|Çelik üretiminde ve alkalin pillerde kullanılır.
Fe|Demir|Çeliğin ana bileşeni; kanda oksijen taşıyan hemoglobinde bulunur.
Co|Kobalt|Lityum iyon pillerde ve mavi cam/seramik boyalarında kullanılır.
Ni|Nikel|Paslanmaz çelikte, madeni paralarda ve şarj edilebilir pillerde kullanılır.
Cu|Bakır|Elektrik kablolarının metali; pirinç ve bronz alaşımlarında bulunur.
Zn|Çinko|Demiri paslanmaya karşı kaplar (galvaniz); bağışıklık için gereklidir.
Ga|Galyum|Elde eriyen metal (29,8 °C); LED ve yarı iletkenlerde kullanılır.
Ge|Germanyum|Yarı iletken; fiber optik ve kızılötesi optikte kullanılır.
As|Arsenik|Zehirli yarı metal; yarı iletkenlerde (GaAs) kullanılır.
Se|Selenyum|Fotokopi makinelerinde ve vitamin takviyelerinde bulunur.
Br|Brom|Oda sıcaklığında sıvı olan tek ametal; alev geciktiricilerde kullanılır.
Kr|Kripton|Yüksek performanslı lambalarda ve fotoğraf flaşlarında kullanılır.
Rb|Rubidyum|Atom saatlerinde kullanılır; suyla çok şiddetli tepkime verir.
Sr|Stronsiyum|Havai fişeklere kırmızı renk verir.
Y|İtriyum|LED'lerde ve süperiletkenlerde kullanılır.
Zr|Zirkonyum|Nükleer reaktör yakıt çubuklarının kaplamasında ve diş protezlerinde kullanılır.
Nb|Niyobyum|Süperiletken mıknatıslarda (MR) ve jet motorlarında kullanılır.
Mo|Molibden|Yüksek dayanımlı çelik alaşımlarında kullanılır.
Tc|Teknesyum|İlk yapay element; tıbbi görüntülemede (Tc-99m) kullanılır.
Ru|Rutenyum|Elektrik kontaklarında ve katalizörlerde kullanılır.
Rh|Rodyum|Araç egzoz katalizörlerinde kullanılan çok pahalı metal.
Pd|Paladyum|Katalitik konvertörlerde ve elektronik devrelerde kullanılır.
Ag|Gümüş|Elektriği en iyi ileten metal; takı, ayna ve fotoğrafçılıkta kullanılır.
Cd|Kadmiyum|Ni-Cd pillerde kullanılır; zehirlidir.
In|İndiyum|Dokunmatik ekranlardaki şeffaf iletken katmanda (ITO) bulunur.
Sn|Kalay|Lehimde ve teneke kutuların kaplamasında kullanılır.
Sb|Antimon|Alev geciktiricilerde ve kurşun alaşımlarında kullanılır.
Te|Tellür|Güneş panellerinde (CdTe) ve termoelektrik cihazlarda kullanılır.
I|İyot|Tiroid hormonları için gereklidir; yara antiseptiklerinde kullanılır.
Xe|Ksenon|Araç farlarında ve uzay araçlarının iyon motorlarında kullanılır.
Cs|Sezyum|Atom saatleri saniyeyi tanımlar; suyla patlayarak tepkime verir.
Ba|Baryum|Havai fişeklere yeşil renk verir; mide filmlerinde kullanılır.
La|Lantan|Kamera lenslerinde ve hibrit araç pillerinde kullanılır.
Ce|Seryum|Çakmak taşlarında ve cam parlatmada kullanılır.
Pr|Praseodim|Güçlü mıknatıslarda ve kaynakçı gözlüklerinde kullanılır.
Nd|Neodim|En güçlü kalıcı mıknatısların (NdFeB) temel elementi.
Pm|Prometyum|Radyoaktif; ışıklı boyalarda ve nükleer pillerde kullanılmıştır.
Sm|Samaryum|Yüksek sıcaklığa dayanıklı mıknatıslarda kullanılır.
Eu|Evropiyum|Euro banknotlarındaki ışıyan güvenlik mürekkebinde bulunur.
Gd|Gadolinyum|MR görüntülemede kontrast maddesi olarak kullanılır.
Tb|Terbiyum|Yeşil ışık veren fosforlarda ve SSD'lerde kullanılır.
Dy|Disprosyum|Elektrikli araç motorlarındaki mıknatıslarda kullanılır.
Ho|Holmiyum|En güçlü manyetik momentlerden birine sahiptir; tıbbi lazerlerde kullanılır.
Er|Erbiyum|Fiber optik kabloların sinyal güçlendiricilerinde kullanılır.
Tm|Tulyum|Taşınabilir röntgen cihazlarında kullanılır.
Yb|İterbiyum|Çok hassas atom saatlerinde ve lazerlerde kullanılır.
Lu|Lutesyum|PET tarayıcılarının dedektörlerinde kullanılır.
Hf|Hafniyum|Nükleer reaktör kontrol çubuklarında ve işlemci çiplerinde kullanılır.
Ta|Tantal|Cep telefonlarındaki küçük kapasitörlerde kullanılır.
W|Tungsten|Erime noktası en yüksek metal (3422 °C); ampul telleri ve matkap uçlarında.
Re|Renyum|Jet motoru türbin kanatlarında kullanılır.
Os|Osmiyum|En yoğun element; dolma kalem uçlarında kullanılır.
Ir|İridyum|Buji uçlarında kullanılır; dinozorların yok oluşunun izlerini taşır.
Pt|Platin|Katalizörlerde, takılarda ve kanser ilaçlarında kullanılır.
Au|Altın|Paslanmaz, çok iyi iletken; takı ve elektronik bağlantılarda kullanılır.
Hg|Cıva|Oda sıcaklığında sıvı tek metal; zehirlidir, eski termometrelerde kullanılırdı.
Tl|Talyum|Çok zehirlidir; özel optik camlarda kullanılır.
Pb|Kurşun|Akülerde ve radyasyon kalkanlarında kullanılır; zehirlidir.
Bi|Bizmut|Mide ilaçlarında kullanılır; kristalleri gökkuşağı renklidir.
Po|Polonyum|Marie Curie'nin keşfettiği çok radyoaktif element.
At|Astatin|Yer kabuğunda en nadir doğal element; çok radyoaktif.
Rn|Radon|Radyoaktif soy gaz; kapalı bodrumlarda birikip sağlığa zarar verebilir.
Fr|Fransiyum|Çok kararsız; doğada yalnızca birkaç gram bulunur.
Ra|Radyum|Curie'ler keşfetti; eskiden ışıklı saat kadranlarında kullanılırdı.
Ac|Aktinyum|Karanlıkta mavi ışıldar; kanser tedavisi araştırmalarında kullanılır.
Th|Toryum|Gelecekteki nükleer yakıt adayı; eski gaz lambası fitillerinde kullanılırdı.
Pa|Protaktinyum|Nadir ve çok radyoaktif; yalnızca araştırmalarda kullanılır.
U|Uranyum|Nükleer santrallerin yakıtı.
Np|Neptünyum|Yapay olarak ilk üretilen transuranyum elementi.
Pu|Plütonyum|Nükleer reaktörlerde ve uzay sondalarının güç kaynaklarında kullanılır.
Am|Amerikyum|Ev tipi duman dedektörlerinde kullanılır.
Cm|Küriyum|Mars gezginlerinin X-ışını spektrometrelerinde kullanılmıştır.
Bk|Berkelyum|Yalnızca araştırma amaçlı, çok az miktarda üretilir.
Cf|Kaliforniyum|Güçlü nötron kaynağı; petrol arama ve kanser tedavisinde kullanılır.
Es|Aynştaynyum|İlk kez bir hidrojen bombası denemesinin kalıntılarında bulundu.
Fm|Fermiyum|Enrico Fermi'nin adını taşır; yalnızca araştırma amaçlı.
Md|Mendelevyum|Periyodik tablonun mucidi Mendeleyev'in adını taşır.
No|Nobelyum|Alfred Nobel'in adını taşır; yalnızca birkaç atom üretilmiştir.
Lr|Lavrensiyum|Siklotronun mucidi Lawrence'ın adını taşır.
Rf|Rutherfordiyum|Yapay, çok kararsız; ömrü birkaç saattir.
Db|Dubniyum|Rusya'daki Dubna araştırma merkezinin adını taşır.
Sg|Seaborgiyum|Yaşarken adı verilen ilk bilim insanı Glenn Seaborg'un adını taşır.
Bh|Bohriyum|Atom modeliyle bilinen Niels Bohr'un adını taşır.
Hs|Hassiyum|Almanya'nın Hessen eyaletinin adını taşır.
Mt|Meitneriyum|Nükleer fisyonu açıklayan Lise Meitner'in adını taşır.
Ds|Darmstadtiyum|Keşfedildiği Alman şehri Darmstadt'ın adını taşır.
Rg|Röntgenyum|X ışınlarını keşfeden Wilhelm Röntgen'in adını taşır.
Cn|Kopernikyum|Gökbilimci Kopernik'in adını taşır.
Nh|Nihonyum|Japonya'da keşfedildi; adı "Japonya" anlamına gelir.
Fl|Flerovyum|Rus fizikçi Georgy Flerov'un adını taşır.
Mc|Moskovyum|Moskova bölgesinin adını taşır.
Lv|Livermoryum|ABD'deki Lawrence Livermore Laboratuvarı'nın adını taşır.
Ts|Tennessin|ABD'nin Tennessee eyaletinin adını taşır.
Og|Oganesson|Bilinen en ağır element; Yuri Oganessian'ın adını taşır."""

CAT = {
    "Alkali metals": ("AlkaliMetal", "Alkali metal"),
    "Alkaline earth metals": ("AlkalineEarth", "Toprak alkali metal"),
    "Transition metals": ("Transition", "Geçiş metali"),
    "Poor metals": ("PostTransition", "Zayıf metal"),
    "Metalloids": ("Metalloid", "Yarı metal"),
    "Nonmetals": ("Nonmetal", "Ametal"),
    "Halogens": ("Halogen", "Halojen"),
    "Noble gases": ("NobleGas", "Soy gaz"),
    "Lanthanides": ("Lanthanide", "Lantanit"),
    "Actinides": ("Actinide", "Aktinit"),
}

def main():
    from mendeleev import element
    tr = {}
    for line in TR.strip().splitlines():
        sym, name, note = line.split("|", 2)
        tr[sym] = (name, note)
    rows = []
    for z in range(1, 119):
        e = element(z)
        series = e.series
        if e.symbol == "Lu": series = "Lanthanides"
        if e.symbol == "Lr": series = "Actinides"
        try: en = e.electronegativity("pauling")
        except Exception: en = None
        try: shells = list(e.ec.electrons_per_shell().values())
        except Exception: shells = []
        mp, bp = e.melting_point, e.boiling_point
        if mp is not None and 298.15 < mp: phase = "Solid"
        elif mp is not None and bp is not None and 298.15 < bp: phase = "Liquid"
        elif mp is None and bp is not None and bp > 1000: phase = "Solid"   # carbon sublimes
        elif bp is not None and bp <= 298.15: phase = "Gas"
        elif e.symbol in ("Hg", "Br"): phase = "Liquid"
        else: phase = "Unknown"
        if e.symbol in ("H","He","N","O","F","Ne","Cl","Ar","Kr","Xe","Rn"): phase = "Gas"
        group = e.group_id
        period = e.period
        # Table position (18 columns; f-block in rows 9/10).
        if 58 <= z <= 71: col, row = z - 58 + 3, 9
        elif 90 <= z <= 103: col, row = z - 90 + 3, 10
        else: col, row = group, period
        name, note = tr[e.symbol]
        cat_id, cat_tr = CAT[series]
        rows.append(dict(z=z, sym=e.symbol, name=name, note=note, mass=e.atomic_weight, cat=cat_id, col=col, row=row,
                         group=group or 0, period=period, block=e.block, en=en, mp=mp, bp=bp, density=e.density,
                         ec=(e.econf or str(e.ec)), shells=shells, year=e.discovery_year, massNumber=e.mass_number, phase=phase))
    def f(v): return "double.NaN" if v is None else repr(float(v))
    def s(v): return '"' + str(v).replace("\\", "\\\\").replace('"', '\\"') + '"'
    out = ["// <auto-generated> by desktop/tools/gen_elements.py from the mendeleev database. Do not edit by hand. </auto-generated>",
           "namespace IDLab.Desktop", "{", "    internal static partial class Elements", "    {",
           "        public static readonly Element[] All =", "        {"]
    for r in rows:
        shells = "new[] { " + ", ".join(str(x) for x in r["shells"]) + " }"
        out.append(f'            new Element({r["z"]}, {s(r["sym"])}, {s(r["name"])}, {f(r["mass"])}, ElementCategory.{r["cat"]}, {r["col"]}, {r["row"]}, '
                   f'{r["group"]}, {r["period"]}, {s(r["block"])}, {f(r["en"])}, {f(r["mp"])}, {f(r["bp"])}, {f(r["density"])}, '
                   f'{s(r["ec"])}, {shells}, {r["year"] or 0}, {r["massNumber"]}, ElementPhase.{r["phase"]}, {s(r["note"])}),')
    out += ["        };", "    }", "}", ""]
    path = os.path.join(os.path.dirname(__file__), "..", "IDLab.Desktop", "Elements.g.cs")
    open(path, "w", encoding="utf-8").write("\n".join(out))
    print("wrote", os.path.abspath(path), len(rows), "elements")

if __name__ == "__main__":
    main()
