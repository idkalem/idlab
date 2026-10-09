# Imported by gen_chemicals.py (executed in its namespace). Adds thousands of real compounds from the CRC Handbook
# tables shipped with the `chemicals` package (pip install chemicals):
#   * "Physical Constants of Inorganic Compounds": names are parsed into cation + anion (+ water of crystallisation),
#     giving a conventional formula (NdCl₃·6H₂O), Turkish systematic name, phase from the melting/boiling points and
#     the handbook density;
#   * "Physical Constants of Organic Compounds": molecular formula from the PubChem identifier table (by CAS number),
#     Turkish name by IUPAC transliteration, phase and density from the handbook. Hazard and water-solubility data
#     are not in these tables, so those rows are flagged "L" (limited data) and the app says so.
import csv, math, os, re

try:
    import chemicals as _chemicals
    _CHEM = os.path.dirname(_chemicals.__file__)
except ImportError:
    _CHEM = None
    print("chemicals package not installed: skipping CRC compounds")

ROOM = 298.15
WATER_M = molar_mass("H2O")

# ----------------------------------------------------------------------------------------------- Turkish names

EL_TR = {}
for _sym, _en in EN_NAMES.items():
    EL_TR[_en.lower()] = ELEMENTS[_sym]["tr"]
EL_TR.update({"aluminum": ELEMENTS["Al"]["tr"], "cesium": ELEMENTS["Cs"]["tr"], "sulphur": ELEMENTS["S"]["tr"]})
EN_TO_SYM = {v.lower(): k for k, v in EN_NAMES.items()}
EN_TO_SYM.update({"aluminum": "Al", "cesium": "Cs", "sulphur": "S"})

WORD_TR = {
    "acid": "asit", "ether": "eter", "aldehyde": "aldehit", "anhydride": "anhidrit", "chloride": "klorür", "bromide": "bromür",
    "iodide": "iyodür", "fluoride": "florür", "cyanide": "siyanür", "oxide": "oksit", "sulfide": "sülfür", "disulfide": "disülfür",
    "trisulfide": "trisülfür", "hydrochloride": "hidroklorür", "monohydrochloride": "monohidroklorür", "dihydrochloride": "dihidroklorür",
    "hydrobromide": "hidrobromür", "salt": "tuzu", "glycol": "glikol", "alcohol": "alkol", "ketone": "keton", "urea": "üre",
    "glucose": "glikoz", "sucrose": "sükroz", "fructose": "fruktoz", "water": "su", "amide": "amit", "imide": "imit",
    "ammonium": "amonyum", "peroxide": "peroksit", "hydroxide": "hidroksit", "carbonate": "karbonat", "acetate": "asetat",
    "nitrate": "nitrat", "nitrite": "nitrit", "sulfate": "sülfat", "sulfite": "sülfit", "phosphate": "fosfat",
}
SUFFIX_TR = [("hydrochloride", "hidroklorür"), ("chloride", "klorür"), ("bromide", "bromür"), ("iodide", "iyodür"), ("fluoride", "florür"),
             ("cyanide", "siyanür"), ("sulfide", "sülfür"), ("selenide", "selenür"), ("telluride", "tellürür")]


def _translit(word):
    """English chemical word → Turkish spelling (IUPAC-style transliteration used in Turkish textbooks)."""
    w = word
    for a, b in (("iso", "izo"), ("sulph", "sülf"), ("sulf", "sülf"), ("fluor", "flor"), ("hex", "hekz"), ("but", "büt"), ("ph", "f"), ("th", "t"),
                 ("ch", "k"), ("qu", "k"), ("x", "ks"), ("w", "v"), ("ae", "e")):
        w = w.replace(a, b)
    w = re.sub(r"c(?=[eiy])", "s", w)
    w = w.replace("c", "k").replace("y", "i")
    w = re.sub(r"ium\b", "iyum", w)
    w = re.sub(r"ide\b", "it", w)
    w = re.sub(r"(ane|ene|ine|one|ole|ate|ite|ose|ase|ile|ide|ame|ume|ure|yne|ime|ope|ape|ize|ise)\b", lambda m: m.group(1)[:-1], w)
    w = re.sub(r"ose\b", "oz", w)
    w = re.sub(r"os\b", "oz", w) if w.endswith("os") and len(w) > 5 else w
    return w


def tr_word(word):
    lw = word.lower()
    if lw in WORD_TR:
        return WORD_TR[lw]
    if lw in EL_TR:
        return EL_TR[lw].lower()
    for suf, t in SUFFIX_TR:
        if lw.endswith(suf) and len(lw) > len(suf):
            return _translit(lw[: -len(suf)]) + t
    return _translit(lw)


def tr_name(en):
    """'2-Methylpropan-2-ol' → '2-Metilpropan-2-ol', 'Hexyl methacrylate' → 'Hekzil metakrilat'."""
    parts = re.split(r"([A-Za-z]+)", en)
    out = []
    for p in parts:
        if re.fullmatch(r"[A-Za-z]+", p):
            # keep stereo / locant letters and roman numerals as they are
            if p in ("N", "O", "S", "H", "D", "L", "E", "Z", "R", "o", "m", "p", "n", "sec", "tert", "cis", "trans") or re.fullmatch(r"[IVX]+", p):
                out.append(p)
            else:
                out.append(tr_word(p))
        else:
            out.append(p)
    s = "".join(out)
    s = s.replace("sek-", "sek-").replace("tert-", "tert-")
    # capitalise the first letter (Turkish i → İ)
    for i, ch in enumerate(s):
        if ch.isascii() and ch.isalpha() or ch in "çğıöşü":
            up = "İ" if ch == "i" else ch.upper()
            return s[:i] + up + s[i + 1:]
    return s


# ----------------------------------------------------------------------------------------------- dedupe sets

EXISTING_EN = set()
EXISTING_FORMULA = set()
EXISTING_IONIC = set()
for _r in ROWS:
    _f = _r.split("|")
    if _f[2]:
        EXISTING_EN.add(re.sub(r"[^a-z0-9]", "", _f[2].lower()))
    if _f[4]:
        EXISTING_FORMULA.add(norm_formula(_f[4]).replace("·", "."))
    if _f[22] and _f[25]:
        waters = 0
        if "·" in _f[4]:
            m_ = re.search(r"·(\d*)H", norm_formula(_f[4]))
            waters = int(m_.group(1) or 1) if m_ else 0
        EXISTING_IONIC.add((_f[22], _f[25], waters))
for (_c, _a) in EXISTING:
    EXISTING_IONIC.add((_c, _a, 0))

# ----------------------------------------------------------------------------------------------- inorganic

ROMAN = {"I": 1, "II": 2, "III": 3, "IV": 4, "V": 5, "VI": 6, "VII": 7, "VIII": 8}
FIXED_CHARGE = {"Li": 1, "Na": 1, "K": 1, "Rb": 1, "Cs": 1, "Ag": 1, "Be": 2, "Mg": 2, "Ca": 2, "Sr": 2, "Ba": 2, "Zn": 2, "Cd": 2,
                "Al": 3, "Ga": 3, "Sc": 3, "Y": 3, "La": 3, "In": 3, "Zr": 4, "Hf": 4}
for _sym in ("Ce", "Pr", "Nd", "Sm", "Eu", "Gd", "Tb", "Dy", "Ho", "Er", "Tm", "Yb", "Lu"):
    FIXED_CHARGE[_sym] = 3
RADIOACTIVE = {"Ra", "Th", "U", "Pu", "Tc", "Pm", "Po", "Ac", "Np", "Am", "Fr", "At", "Rn", "Pa"}
EXISTING_CAT_KEY = {(c[1], c[2]): c[0] for c in CATIONS}

# English anion name → (key, formula, charge, Turkish, behaves-like key for solubility/hazards)
ANION_EN = {
    "fluoride": ("F", "F", 1, "florür", "F"), "chloride": ("Cl", "Cl", 1, "klorür", "Cl"), "bromide": ("Br", "Br", 1, "bromür", "Br"),
    "iodide": ("I", "I", 1, "iyodür", "I"), "hydroxide": ("OH", "OH", 1, "hidroksit", "OH"), "oxide": ("O", "O", 2, "oksit", "O"),
    "peroxide": ("O2", "O2", 2, "peroksit", "O2"), "superoxide": ("O2_1", "O2", 1, "süperoksit", "O2"),
    "sulfide": ("S", "S", 2, "sülfür", "S"), "selenide": ("Se", "Se", 2, "selenür", "S"), "telluride": ("Te", "Te", 2, "tellürür", "S"),
    "nitride": ("N", "N", 3, "nitrür", "N"), "phosphide": ("P", "P", 3, "fosfür", "N"), "hydride": ("H", "H", 1, "hidrür", "H"),
    "cyanide": ("CN", "CN", 1, "siyanür", "CN"), "cyanate": ("OCN", "OCN", 1, "siyanat", "NO3"), "thiocyanate": ("SCN", "SCN", 1, "tiyosiyanat", "SCN"),
    "azide": ("N3", "N3", 1, "azit", "NO3"), "nitrate": ("NO3", "NO3", 1, "nitrat", "NO3"), "nitrite": ("NO2", "NO2", 1, "nitrit", "NO2"),
    "sulfate": ("SO4", "SO4", 2, "sülfat", "SO4"), "sulfite": ("SO3", "SO3", 2, "sülfit", "SO3"), "thiosulfate": ("S2O3", "S2O3", 2, "tiyosülfat", "S2O3"),
    "hydrogen sulfate": ("HSO4", "HSO4", 1, "hidrojen sülfat", "HSO4"), "hydrogensulfate": ("HSO4", "HSO4", 1, "hidrojen sülfat", "HSO4"),
    "bisulfate": ("HSO4", "HSO4", 1, "bisülfat", "HSO4"), "hydrogen sulfite": ("HSO3", "HSO3", 1, "hidrojen sülfit", "HSO4"),
    "bisulfite": ("HSO3", "HSO3", 1, "bisülfit", "HSO4"), "carbonate": ("CO3", "CO3", 2, "karbonat", "CO3"),
    "hydrogen carbonate": ("HCO3", "HCO3", 1, "hidrojen karbonat", "HCO3"), "bicarbonate": ("HCO3", "HCO3", 1, "bikarbonat", "HCO3"),
    "phosphate": ("PO4", "PO4", 3, "fosfat", "PO4"), "hydrogen phosphate": ("HPO4", "HPO4", 2, "hidrojen fosfat", "HPO4"),
    "dihydrogen phosphate": ("H2PO4", "H2PO4", 1, "dihidrojen fosfat", "H2PO4"), "pyrophosphate": ("P2O7", "P2O7", 4, "pirofosfat", "PO4"),
    "diphosphate": ("P2O7", "P2O7", 4, "difosfat", "PO4"), "metaphosphate": ("PO3", "PO3", 1, "metafosfat", "PO4"),
    "hypophosphite": ("H2PO2", "H2PO2", 1, "hipofosfit", "NO3"), "arsenate": ("AsO4", "AsO4", 3, "arsenat", "AsO4"),
    "arsenite": ("AsO3", "AsO3", 3, "arsenit", "AsO4"), "selenate": ("SeO4", "SeO4", 2, "selenat", "SO4"), "selenite": ("SeO3", "SeO3", 2, "selenit", "SO3"),
    "tellurate": ("TeO4", "TeO4", 2, "tellürat", "SO4"), "tellurite": ("TeO3", "TeO3", 2, "tellürit", "SO3"),
    "chromate": ("CrO4", "CrO4", 2, "kromat", "CrO4"), "dichromate": ("Cr2O7", "Cr2O7", 2, "dikromat", "Cr2O7"),
    "permanganate": ("MnO4", "MnO4", 1, "permanganat", "MnO4"), "manganate": ("MnO4_2", "MnO4", 2, "manganat", "CO3"),
    "molybdate": ("MoO4", "MoO4", 2, "molibdat", "MoO4"), "tungstate": ("WO4", "WO4", 2, "tungstat", "WO4"),
    "metavanadate": ("VO3", "VO3", 1, "metavanadat", "CO3"), "vanadate": ("VO4", "VO4", 3, "vanadat", "PO4"),
    "perrhenate": ("ReO4", "ReO4", 1, "perrenat", "ClO4"), "periodate": ("IO4", "IO4", 1, "periyodat", "ClO4"),
    "iodate": ("IO3", "IO3", 1, "iyodat", "IO3"), "bromate": ("BrO3", "BrO3", 1, "bromat", "BrO3"), "chlorate": ("ClO3", "ClO3", 1, "klorat", "ClO3"),
    "perchlorate": ("ClO4", "ClO4", 1, "perklorat", "ClO4"), "chlorite": ("ClO2", "ClO2", 1, "klorit", "ClO3"),
    "hypochlorite": ("ClO", "ClO", 1, "hipoklorit", "ClO"), "borate": ("BO3", "BO3", 3, "borat", "PO4"), "metaborate": ("BO2", "BO2", 1, "metaborat", "CO3"),
    "tetraborate": ("B4O7", "B4O7", 2, "tetraborat", "B4O7"), "tetrafluoroborate": ("BF4", "BF4", 1, "tetrafloroborat", "NO3"),
    "hexafluorophosphate": ("PF6", "PF6", 1, "hekzaflorofosfat", "NO3"), "hexafluorosilicate": ("SiF6", "SiF6", 2, "hekzaflorosilikat", "SO4"),
    "silicate": ("SiO3", "SiO3", 2, "silikat", "SiO3"), "metasilicate": ("SiO3", "SiO3", 2, "metasilikat", "SiO3"),
    "orthosilicate": ("SiO4", "SiO4", 4, "ortosilikat", "SiO3"), "aluminate": ("AlO2", "AlO2", 1, "alüminat", "CO3"),
    "stannate": ("SnO3", "SnO3", 2, "stannat", "CO3"), "titanate": ("TiO3", "TiO3", 2, "titanat", "CO3"),
    "ferrocyanide": ("FeCN6_4", "Fe(CN)6", 4, "ferrosiyanür", "FeCN6_4"), "hexacyanoferrate(ii)": ("FeCN6_4", "Fe(CN)6", 4, "hekzasiyanoferrat(II)", "FeCN6_4"),
    "ferricyanide": ("FeCN6_3", "Fe(CN)6", 3, "ferrisiyanür", "FeCN6_3"), "hexacyanoferrate(iii)": ("FeCN6_3", "Fe(CN)6", 3, "hekzasiyanoferrat(III)", "FeCN6_3"),
    "acetate": ("CH3COO", "CH3COO", 1, "asetat", "CH3COO"), "formate": ("HCOO", "HCOO", 1, "format", "HCOO"), "oxalate": ("C2O4", "C2O4", 2, "oksalat", "C2O4"),
    "citrate": ("C6H5O7", "C6H5O7", 3, "sitrat", "C6H5O7"), "tartrate": ("C4H4O6", "C4H4O6", 2, "tartarat", "C2O4"),
    "lactate": ("C3H5O3", "C3H5O3", 1, "laktat", "C3H5O3"), "benzoate": ("C6H5COO", "C6H5COO", 1, "benzoat", "C6H5COO"),
    "salicylate": ("C7H5O3", "C7H5O3", 1, "salisilat", "C6H5COO"), "stearate": ("C17H35COO", "C17H35COO", 1, "stearat", "C17H35COO"),
    "propanoate": ("C2H5COO", "C2H5COO", 1, "propanoat", "CH3COO"), "propionate": ("C2H5COO", "C2H5COO", 1, "propiyonat", "CH3COO"),
    "gluconate": ("C6H11O7", "C6H11O7", 1, "glukonat", "CH3COO"), "amide": ("NH2", "NH2", 1, "amit", "H"),
    "fulminate": ("CNO", "CNO", 1, "fulminat", "NO3"), "dithionite": ("S2O4", "S2O4", 2, "ditiyonit", "S2O3"),
    "peroxydisulfate": ("S2O8", "S2O8", 2, "peroksidisülfat", "SO4"), "persulfate": ("S2O8", "S2O8", 2, "persülfat", "SO4"),
}
HYDRATE = {"hemihydrate": (0.5, "hemihidrat"), "monohydrate": (1, "monohidrat"), "sesquihydrate": (1.5, "seskihidrat"),
           "dihydrate": (2, "dihidrat"), "trihydrate": (3, "trihidrat"), "tetrahydrate": (4, "tetrahidrat"), "pentahydrate": (5, "pentahidrat"),
           "hexahydrate": (6, "hekzahidrat"), "heptahydrate": (7, "heptahidrat"), "octahydrate": (8, "oktahidrat"), "nonahydrate": (9, "nonahidrat"),
           "decahydrate": (10, "dekahidrat"), "undecahydrate": (11, "undekahidrat"), "dodecahydrate": (12, "dodekahidrat"),
           "tetradecahydrate": (14, "tetradekahidrat"), "pentadecahydrate": (15, "pentadekahidrat"), "hexadecahydrate": (16, "hekzadekahidrat"),
           "octadecahydrate": (18, "oktadekahidrat")}
NEW_CAT_HZ = {"Tl": hz("toxic", "health", "environment"), "Hg": hz("toxic", "health", "environment"), "Sb": hz("irritant", "environment"),
              "V": hz("toxic", "irritant"), "Cr": hz("irritant"), "Co": hz("health", "irritant", "environment"), "Ni": hz("health", "irritant", "environment"),
              "Pb": hz("toxic", "health", "environment"), "Cd": hz("toxic", "health", "environment"), "Be": hz("toxic", "health"), "Ba": hz("toxic", "irritant"),
              "Cu": hz("irritant", "environment"), "Ag": hz("environment"), "As": hz("toxic", "health", "environment"), "Se": hz("toxic"), "Te": hz("toxic")}
NEW_AN_HZ = {"Se": hz("toxic"), "Te": hz("toxic"), "SeO4": hz("toxic", "environment"), "SeO3": hz("toxic", "environment"), "AsO3": hz("toxic", "health", "environment"),
             "N3": hz("toxic", "explosive"), "CNO": hz("explosive", "toxic"), "PF6": hz("corrosive"), "BF4": hz("corrosive"), "SiF6": hz("toxic"),
             "S2O8": hz("oxidizer", "irritant"), "IO4": hz("oxidizer"), "ClO2": hz("oxidizer"), "O2_1": hz("oxidizer", "corrosive"),
             "MnO4_2": hz("oxidizer"), "P": hz("toxic", "waterreactive"), "NH2": hz("waterreactive", "corrosive"), "VO3": hz("toxic"), "VO4": hz("toxic")}
EL_SOLID_COLOR = {"Ni": "4CAF50", "Co": "D45A8A", "Cr": "3E7A4A", "V": "3A6AB8", "Nd": "C8A0C8", "Pr": "9AC88A", "Er": "F0B0C0", "Ho": "F0E0A0",
                  "Mn": "F0C8D0", "Tm": "B8E0B8", "Sm": "F0E8B0", "Ce": "F2EAD0", "Au": "E6C24A", "Pd": "8A5A3A", "Pt": "A8A090", "Rh": "B85A3A"}


def _formula(cat_f, cat_charge, an_f, an_charge, an_key):
    l = cat_charge * an_charge // math.gcd(cat_charge, an_charge)
    nc, na = l // cat_charge, l // an_charge
    poly_c = len(re.findall(r"[A-Z]", cat_f)) > 1 or cat_f == "Hg2"
    poly_a = len(re.findall(r"[A-Z]", an_f)) > 1 or an_key == "OH" or an_key in ("O2", "O2_1", "N3")

    def part(f, n, poly):
        return f if n == 1 else (f"({f}){n}" if poly else f"{f}{n}")

    if an_key in ("CH3COO", "HCOO", "C6H5COO", "C17H35COO", "C2H5COO") and nc == 1:
        return part(an_f, na, True) + part(cat_f, nc, poly_c), nc, na
    return part(cat_f, nc, poly_c) + part(an_f, na, poly_a), nc, na


def _pretty_ionic(ascii_):
    s = ascii_.replace("(Fe(CN)6)", "[Fe(CN)6]")
    if "[" not in s and "Fe(CN)6" in s:
        s = s.replace("Fe(CN)6", "[Fe(CN)6]")
    return pretty(s)


def _water_text(w):
    if w == 0.5:
        return "·½H₂O"
    if w == 1.5:
        return "·1½H₂O"
    return "·" + (pretty(str(int(w))) if False else str(int(w))) + "H₂O" if w != 1 else "·H₂O"


def _phase(tm, tb, mw=0):
    if tb is not None and tb < ROOM:
        return "A"
    if tm is not None:
        return "S" if tm > ROOM else "L"
    if tb is not None:
        return "S" if mw > 200 else "L"
    return None


def _num(v):
    try:
        return float(v) if v not in ("", None) else None
    except ValueError:
        return None


crc_inorganic = 0
crc_organic = 0
if _CHEM:
    inorg_rx = re.compile(r"^(?P<cat>[A-Z][a-z]+|Ammonium)(?:\((?P<ox>[IVX]+)\))? (?P<an>[a-z ]+?(?:\((?:ii|iii)\))?)(?: (?P<hyd>[a-z]+hydrate))?$")
    with open(os.path.join(_CHEM, "Misc", "Physical Constants of Inorganic Compounds.csv"), encoding="utf-8") as fh:
        for rec in csv.DictReader(fh, delimiter="\t"):
            name = rec["Chemical"].strip()
            m = inorg_rx.match(name)
            if not m:
                continue
            cat_en, ox, an_en, hyd = m.group("cat"), m.group("ox"), m.group("an").strip(), m.group("hyd")
            if an_en not in ANION_EN:
                continue
            if cat_en == "Ammonium":
                sym, cat_f, charge = "N", "NH4", 1
            else:
                sym = EN_TO_SYM.get(cat_en.lower())
                if sym is None or sym in RADIOACTIVE:
                    continue
                if ox:
                    charge = ROMAN[ox]
                elif sym in FIXED_CHARGE:
                    charge = FIXED_CHARGE[sym]
                else:
                    continue
                cat_f = sym
                if sym == "Hg" and charge == 1:
                    cat_f, charge = "Hg2", 2  # mercury(I) is the Hg₂²⁺ dimer
            waters, hyd_tr = (0, "")
            if hyd:
                if hyd not in HYDRATE:
                    continue
                waters, hyd_tr = HYDRATE[hyd]
            an_key, an_f, an_charge, an_tr, like = ANION_EN[an_en]
            cat_key = "NH4" if cat_en == "Ammonium" else ("Hg1" if cat_f == "Hg2" else EXISTING_CAT_KEY.get((sym, charge), f"{sym}{charge}"))
            if (cat_key, an_key, waters) in EXISTING_IONIC:
                continue
            ascii_, nc, na = _formula(cat_f, charge, an_f, an_charge, an_key)
            try:
                M = molar_mass(ascii_) + waters * WATER_M
            except ValueError:
                continue
            formula = _pretty_ionic(ascii_) + (_water_text(waters) if waters else "")
            nf = norm_formula(formula).replace("·", ".")
            if nf in EXISTING_FORMULA:
                continue
            EXISTING_IONIC.add((cat_key, an_key, waters))
            EXISTING_FORMULA.add(nf)
            tm, tb, rho = _num(rec["Tm"]), _num(rec["Tb"]), _num(rec["rho"])
            phase = _phase(tm, tb, M) or "S"
            if cat_en == "Ammonium":
                cat_tr = "Amonyum"
            else:
                cat_tr = ELEMENTS[sym]["tr"] + (f"({ox})" if ox else "")
            tr = f"{cat_tr} {an_tr}" + (f" {hyd_tr}" if hyd_tr else "")
            sol_key = cat_key if cat_key in CAT else cat_key
            sol = soluble(sol_key, like) if phase == "S" else phase == "L"
            if cat_key in ("Hg1", "Tl1") and like in ("Cl", "Br", "I"):
                sol = False
            h = CAT_HZ.get(cat_key, NEW_CAT_HZ.get(sym, 0)) | AN_HZ.get(like, 0) | NEW_AN_HZ.get(an_key, 0)
            if like == "OH" and sol:
                h |= HZ["corrosive"]
            color = SOLID.get((cat_key, like)) or AN_SOLID.get(an_key) or CAT_SOLID.get(cat_key) or EL_SOLID_COLOR.get(sym) or "F2F2F2"
            tint, tcol = 0.0, None
            if sol:
                tcol, tint = AN_SOLUTION.get(an_key) or CAT_SOLUTION.get(cat_key) or (None, 0.0)
            sal_ka = CAT[cat_key][5] if cat_key in CAT else 0
            ions = [species(cat_f, charge) + (f"*{nc}" if nc > 1 else ""), species(an_f, -an_charge) + (f"*{na}" if na > 1 else "")]
            kind = "StrongBase" if like == "OH" and sol else "Salt"
            row(id="c_" + rec["CAS"].replace("-", ""), en=name, tr=tr, formula=formula, aliases=[name], category="ionic", kind=kind,
                phase=phase, gas=phase == "A", soluble=sol, form="Crystals" if sol else "Powder", hazards=h, M=M,
                density=(rho / 1000.0) if rho else min(7.5, max(1.5, 1.8 + M / 110.0)), hydroxides=na if kind == "StrongBase" else 0,
                color=tcol if (sol and tcol) else color, alpha=0.8 if (sol and tcol) else 1.0, tint=tint, ions=ions if sol else [],
                cat=cat_key, catCharge=charge, catCount=nc, an=an_key, anCharge=an_charge, anCount=na, saltKa=sal_ka, saltCount=nc,
                conj="nh3" if cat_key == "NH4" else ANION_CONJ.get(like), conjCount=na, cp=800, cas=rec["CAS"],
                flags="" if rho else "d")
            crc_inorganic += 1

    # ---- organic
    formulas = {}
    with open(os.path.join(_CHEM, "Identifiers", "chemical identifiers pubchem large.tsv"), encoding="utf-8") as fh:
        for line in fh:
            parts = line.split("\t", 4)
            if len(parts) > 3:
                formulas[parts[1]] = parts[2]
    allowed_el = {"C", "H", "N", "O", "S", "P", "F", "Cl", "Br", "I", "Si", "B", "Na", "K"}
    with open(os.path.join(_CHEM, "Misc", "Physical Constants of Organic Compounds.csv"), encoding="utf-8") as fh:
        for rec in csv.DictReader(fh, delimiter="\t"):
            name = rec["Name"].strip()
            cas = rec["CAS"].strip()
            f = formulas.get(cas)
            if not f or ", " in name or len(name) > 60 or not re.fullmatch(r"[A-Za-z0-9 ,'()+\-αβγδω±\[\]]+", name):
                continue
            try:
                comp = parse_formula(f)
            except ValueError:
                continue
            if "C" not in comp or not set(comp) <= allowed_el:
                continue
            M = molar_mass(f)
            if M > 600:
                continue
            key = re.sub(r"[^a-z0-9]", "", name.lower())
            if key in EXISTING_EN:
                continue
            tm, tb, rho = _num(rec["Tm"]), _num(rec["Tb"]), _num(rec["rho"])
            phase = _phase(tm, tb, M)
            if phase is None:
                continue
            EXISTING_EN.add(key)
            clean = re.sub(r"^\((±|[RS+\-]+)\)-", "", name)
            density = rho / 1000.0 if rho else (1.0 if phase == "L" else 1.2)
            latent, bp = 0, None
            if phase == "L" and tb and tb > ROOM:
                bp = tb - 273.15
                latent = 88.0 * tb / (M / 1000.0)
            row(id="c_" + cas.replace("-", ""), en=clean, tr=tr_name(clean), formula=pretty(f), aliases=[clean, f], category="organic",
                kind="Molecular", phase=phase, gas=phase == "A", soluble=False, form="Crystals" if phase == "S" else "Powder",
                hazards=HZ["gas"] if phase == "A" else 0, M=M, density=density if phase != "A" else 1.0, bp=bp,
                color=("E8F2FA" if phase == "L" else "F4F4F2"), alpha=(0.18 if phase == "L" else 1.0), molecular=f,
                cp=2000 if phase == "L" else 1200, latent=latent, cas=cas, flags="L" + ("" if rho else "d"))
            crc_organic += 1
    print(f"CRC: {crc_inorganic} inorganic, {crc_organic} organic compounds added")
