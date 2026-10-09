"""Generates core/com.idlab.core/Runtime/Chemistry/ChemicalData.g.cs — the chemical catalogue (1000+ substances):

* ionic compounds: every sensible cation × anion pair (Turkish + English systematic names, formula, molar mass,
  water solubility from the solubility rules, colour, GHS hazards, hydrolysis data for pH),
* all 118 pure elements from desktop/IDLab.Desktop/Elements.g.cs (radioactive / synthetic ones flagged),
* molecular / organic compounds and acids from desktop/tools/data/molecular.txt,
* hidden helper ions used for carbonate/phosphate hydrolysis.

Substances already hand-tuned in SubstanceDatabase.CreateDefault are emitted as *patch* rows: they only receive
catalogue metadata (Turkish name, aliases, ionic composition, hydrolysis constants).

    python3 desktop/tools/gen_chemicals.py
"""
import json, math, os, re, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ELEMENTS_CS = os.path.join(ROOT, "desktop", "IDLab.Desktop", "Elements.g.cs")
MOLECULAR_TXT = os.path.join(ROOT, "desktop", "tools", "data", "molecular.txt")
OUT = os.path.join(ROOT, "core", "com.idlab.core", "Runtime", "Chemistry", "ChemicalData.g.cs")

# ----------------------------------------------------------------------------------------------- elements

ELEMENTS = {}
for m in re.finditer(r'new Element\((\d+), "(\w+)", "([^"]+)", ([\d.eE+-]+), ElementCategory\.(\w+), (?:[^,]+, ){4}"\w", ([^,]+), ([^,]+), ([^,]+), ([^,]+), .*?ElementPhase\.(\w+)', open(ELEMENTS_CS, encoding="utf-8").read()):
    z, sym, name, mass, cat, en_, mp, bp, dens, phase = m.groups()
    f = lambda v: float("nan") if "NaN" in v else float(v)
    ELEMENTS[sym] = dict(z=int(z), sym=sym, tr=name, mass=float(mass), cat=cat, mpK=f(mp), bpK=f(bp), density=f(dens), phase=phase)
assert len(ELEMENTS) == 118, len(ELEMENTS)
EN_NAMES = dict(H="Hydrogen", He="Helium", Li="Lithium", Be="Beryllium", B="Boron", C="Carbon", N="Nitrogen", O="Oxygen", F="Fluorine",
    Ne="Neon", Na="Sodium", Mg="Magnesium", Al="Aluminium", Si="Silicon", P="Phosphorus", S="Sulfur", Cl="Chlorine", Ar="Argon", K="Potassium",
    Ca="Calcium", Sc="Scandium", Ti="Titanium", V="Vanadium", Cr="Chromium", Mn="Manganese", Fe="Iron", Co="Cobalt", Ni="Nickel", Cu="Copper",
    Zn="Zinc", Ga="Gallium", Ge="Germanium", As="Arsenic", Se="Selenium", Br="Bromine", Kr="Krypton", Rb="Rubidium", Sr="Strontium",
    Y="Yttrium", Zr="Zirconium", Nb="Niobium", Mo="Molybdenum", Ru="Ruthenium", Rh="Rhodium", Pd="Palladium", Ag="Silver", Cd="Cadmium",
    In="Indium", Sn="Tin", Sb="Antimony", Te="Tellurium", I="Iodine", Xe="Xenon", Cs="Caesium", Ba="Barium", La="Lanthanum", Ce="Cerium",
    Pr="Praseodymium", Nd="Neodymium", Sm="Samarium", Eu="Europium", Gd="Gadolinium", Tb="Terbium", Dy="Dysprosium", Ho="Holmium",
    Er="Erbium", Tm="Thulium", Yb="Ytterbium", Lu="Lutetium", Hf="Hafnium", Ta="Tantalum", W="Tungsten", Re="Rhenium", Os="Osmium",
    Ir="Iridium", Pt="Platinum", Au="Gold", Hg="Mercury", Tl="Thallium", Pb="Lead", Bi="Bismuth", Tc="Technetium", Pm="Promethium", Po="Polonium", At="Astatine", Rn="Radon", Fr="Francium", Ra="Radium", Ac="Actinium", Th="Thorium", Pa="Protactinium", U="Uranium", Np="Neptunium", Pu="Plutonium", Am="Americium", Cm="Curium", Bk="Berkelium", Cf="Californium", Es="Einsteinium", Fm="Fermium", Md="Mendelevium", No="Nobelium", Lr="Lawrencium", Rf="Rutherfordium", Db="Dubnium", Sg="Seaborgium", Bh="Bohrium", Hs="Hassium", Mt="Meitnerium", Ds="Darmstadtium", Rg="Roentgenium", Cn="Copernicium", Nh="Nihonium", Fl="Flerovium", Mc="Moscovium", Lv="Livermorium", Ts="Tennessine", Og="Oganesson")


def parse_formula(formula):
    """'Ca(NO3)2' → {'Ca': 1, 'N': 2, 'O': 6}."""
    tokens = re.findall(r"[A-Z][a-z]?|\d+|\(|\)", formula)
    if "".join(tokens) != formula:
        raise ValueError("bad formula " + formula)
    stack = [{}]
    i = 0
    while i < len(tokens):
        t = tokens[i]
        if t == "(":
            stack.append({})
        elif t == ")":
            group = stack.pop()
            n = 1
            if i + 1 < len(tokens) and tokens[i + 1].isdigit():
                n = int(tokens[i + 1]); i += 1
            for k, v in group.items():
                stack[-1][k] = stack[-1].get(k, 0) + v * n
        elif t.isdigit():
            raise ValueError("stray number in " + formula)
        else:
            if t not in ELEMENTS:
                raise ValueError(f"unknown element {t} in {formula}")
            n = 1
            if i + 1 < len(tokens) and tokens[i + 1].isdigit():
                n = int(tokens[i + 1]); i += 1
            stack[-1][t] = stack[-1].get(t, 0) + n
        i += 1
    assert len(stack) == 1, formula
    return stack[0]


def molar_mass(formula):
    return sum(ELEMENTS[e]["mass"] * n for e, n in parse_formula(formula).items())


SUB = str.maketrans("0123456789", "₀₁₂₃₄₅₆₇₈₉")


def pretty(formula):
    return formula.translate(SUB)


def norm_formula(f):
    return f.translate(str.maketrans("₀₁₂₃₄₅₆₇₈₉", "0123456789")).replace(" ", "")


# ----------------------------------------------------------------------------------------------- hazards

HZ = dict(corrosive=1, toxic=2, flammable=4, oxidizer=8, irritant=16, environment=32, waterreactive=64, explosive=128, gas=256, health=512)


def hz(*names):
    v = 0
    for n in names:
        v |= HZ[n]
    return v


# ----------------------------------------------------------------------------------------------- ions

# key, formula, charge, Turkish, English, saltAcidKa (hydrated cation)
CATIONS = [
    ("Li", "Li", 1, "Lityum", "Lithium", 0), ("Na", "Na", 1, "Sodyum", "Sodium", 0), ("K", "K", 1, "Potasyum", "Potassium", 0),
    ("Rb", "Rb", 1, "Rubidyum", "Rubidium", 0), ("Cs", "Cs", 1, "Sezyum", "Caesium", 0), ("NH4", "NH4", 1, "Amonyum", "Ammonium", 0),
    ("Ag", "Ag", 1, "Gümüş", "Silver", 0), ("Cu1", "Cu", 1, "Bakır(I)", "Copper(I)", 0),
    ("Mg", "Mg", 2, "Magnezyum", "Magnesium", 0), ("Ca", "Ca", 2, "Kalsiyum", "Calcium", 0), ("Sr", "Sr", 2, "Stronsiyum", "Strontium", 0),
    ("Ba", "Ba", 2, "Baryum", "Barium", 0), ("Be", "Be", 2, "Berilyum", "Beryllium", 3e-7), ("Zn", "Zn", 2, "Çinko", "Zinc", 2.5e-10),
    ("Cd", "Cd", 2, "Kadmiyum", "Cadmium", 4e-10), ("Hg2", "Hg", 2, "Cıva(II)", "Mercury(II)", 2.6e-4), ("Pb2", "Pb", 2, "Kurşun(II)", "Lead(II)", 1.5e-8),
    ("Sn2", "Sn", 2, "Kalay(II)", "Tin(II)", 4e-4), ("Mn2", "Mn", 2, "Mangan(II)", "Manganese(II)", 3e-11), ("Fe2", "Fe", 2, "Demir(II)", "Iron(II)", 3e-10),
    ("Co2", "Co", 2, "Kobalt(II)", "Cobalt(II)", 1.3e-9), ("Ni2", "Ni", 2, "Nikel(II)", "Nickel(II)", 2.5e-11), ("Cu2", "Cu", 2, "Bakır(II)", "Copper(II)", 3e-8),
    ("Fe3", "Fe", 3, "Demir(III)", "Iron(III)", 6.3e-3), ("Al", "Al", 3, "Alüminyum", "Aluminium", 1.4e-5), ("Cr3", "Cr", 3, "Krom(III)", "Chromium(III)", 1.6e-4),
    ("Bi3", "Bi", 3, "Bizmut(III)", "Bismuth(III)", 1e-2), ("Sn4", "Sn", 4, "Kalay(IV)", "Tin(IV)", 1e-2),
    ("Mn4", "Mn", 4, "Mangan(IV)", "Manganese(IV)", 0), ("Pb4", "Pb", 4, "Kurşun(IV)", "Lead(IV)", 0),
]
CAT = {c[0]: c for c in CATIONS}

# key, formula, charge, Turkish, English, polyatomic
ANIONS = [
    ("F", "F", 1, "florür", "fluoride"), ("Cl", "Cl", 1, "klorür", "chloride"), ("Br", "Br", 1, "bromür", "bromide"), ("I", "I", 1, "iyodür", "iodide"),
    ("OH", "OH", 1, "hidroksit", "hydroxide"), ("NO3", "NO3", 1, "nitrat", "nitrate"), ("NO2", "NO2", 1, "nitrit", "nitrite"),
    ("CN", "CN", 1, "siyanür", "cyanide"), ("SCN", "SCN", 1, "tiyosiyanat", "thiocyanate"), ("ClO", "ClO", 1, "hipoklorit", "hypochlorite"),
    ("ClO3", "ClO3", 1, "klorat", "chlorate"), ("ClO4", "ClO4", 1, "perklorat", "perchlorate"), ("BrO3", "BrO3", 1, "bromat", "bromate"),
    ("IO3", "IO3", 1, "iyodat", "iodate"), ("MnO4", "MnO4", 1, "permanganat", "permanganate"), ("HCO3", "HCO3", 1, "bikarbonat", "hydrogencarbonate"),
    ("HSO4", "HSO4", 1, "bisülfat", "hydrogensulfate"), ("H2PO4", "H2PO4", 1, "dihidrojen fosfat", "dihydrogenphosphate"),
    ("CH3COO", "CH3COO", 1, "asetat", "acetate"), ("HCOO", "HCOO", 1, "format", "formate"),
    ("O", "O", 2, "oksit", "oxide"), ("S", "S", 2, "sülfür", "sulfide"), ("SO4", "SO4", 2, "sülfat", "sulfate"), ("SO3", "SO3", 2, "sülfit", "sulfite"),
    ("S2O3", "S2O3", 2, "tiyosülfat", "thiosulfate"), ("CO3", "CO3", 2, "karbonat", "carbonate"), ("C2O4", "C2O4", 2, "oksalat", "oxalate"),
    ("CrO4", "CrO4", 2, "kromat", "chromate"), ("Cr2O7", "Cr2O7", 2, "dikromat", "dichromate"), ("SiO3", "SiO3", 2, "silikat", "silicate"),
    ("HPO4", "HPO4", 2, "hidrojen fosfat", "hydrogenphosphate"), ("PO4", "PO4", 3, "fosfat", "phosphate"),
    ("B4O7", "B4O7", 2, "tetraborat", "tetraborate"), ("O2", "O2", 2, "peroksit", "peroxide"), ("N", "N", 3, "nitrür", "nitride"),
    ("H", "H", 1, "hidrür", "hydride"), ("C6H5COO", "C6H5COO", 1, "benzoat", "benzoate"), ("C6H5O7", "C6H5O7", 3, "sitrat", "citrate"),
    ("C3H5O3", "C3H5O3", 1, "laktat", "lactate"), ("C17H35COO", "C17H35COO", 1, "stearat", "stearate"),
    ("FeCN6_3", "Fe(CN)6", 3, "hekzasiyanoferrat(III)", "hexacyanoferrate(III)"), ("FeCN6_4", "Fe(CN)6", 4, "hekzasiyanoferrat(II)", "hexacyanoferrate(II)"),
    ("MoO4", "MoO4", 2, "molibdat", "molybdate"), ("WO4", "WO4", 2, "tungstat", "tungstate"), ("AsO4", "AsO4", 3, "arsenat", "arsenate"),
]
AN = {a[0]: a for a in ANIONS}
TR_ALT = {"HCO3": "hidrojen karbonat", "HSO4": "hidrojen sülfat", "HCOO": "metanoat", "CH3COO": "etanoat",
          "FeCN6_3": "ferrisiyanür", "FeCN6_4": "ferrosiyanür"}
CARBOXYLATE_FIRST = {"CH3COO", "HCOO", "C6H5COO", "C17H35COO"}

ALKALI = {"Li", "Na", "K", "Rb", "Cs", "NH4"}
ALK_EARTH = {"Mg", "Ca", "Sr", "Ba"}
TRIVALENT = {"Al", "Fe3", "Cr3", "Bi3", "Sn4"}
REDUCING = {"Fe2", "Sn2", "Cu1", "Mn2"}
OXIDISING_ANIONS = {"MnO4", "Cr2O7", "CrO4", "ClO", "ClO3", "BrO3", "IO3", "NO2"}
TRANS2 = {"Zn", "Cd", "Pb2", "Mn2", "Fe2", "Co2", "Ni2", "Cu2"}


def allowed(c, a):
    """Only compounds that really exist as stable substances."""
    if c == "Mn4" or c == "Pb4":
        return a == "O"
    if a == "B4O7":
        return c in {"Li", "Na", "K", "NH4", "Ca", "Mg", "Ba"}
    if a == "O2":
        return c in {"Li", "Na", "K", "Ca", "Sr", "Ba", "Mg", "Zn"}
    if a == "N":
        return c in {"Li", "Mg", "Ca", "Sr", "Ba", "Al"}
    if a == "H":
        return c in {"Li", "Na", "K", "Ca", "Mg", "Sr", "Ba"}
    if a == "C6H5COO":
        return c in {"Li", "Na", "K", "NH4", "Ca", "Mg", "Zn", "Ag"}
    if a == "C6H5O7":
        return c in {"Li", "Na", "K", "NH4", "Ca", "Mg", "Fe3", "Zn"}
    if a == "C3H5O3":
        return c in {"Li", "Na", "K", "NH4", "Ca", "Mg", "Zn", "Fe2"}
    if a == "C17H35COO":
        return c in {"Li", "Na", "K", "Ca", "Mg", "Zn", "Al"}
    if a == "FeCN6_3":
        return c in {"Na", "K", "NH4", "Fe2", "Zn", "Ag"}
    if a == "FeCN6_4":
        return c in {"Na", "K", "NH4", "Fe3", "Cu2", "Zn", "Ca"}
    if a == "MoO4":
        return c in {"Li", "Na", "K", "NH4", "Ca", "Pb2"}
    if a == "WO4":
        return c in {"Na", "K", "Ca"}
    if a == "AsO4":
        return c in {"Na", "K", "Ca", "Pb2", "Ag", "Fe3"}
    if c in {"Mn4", "Pb4"}:
        return False
    if c == "Cu1":
        return a in {"Cl", "Br", "I", "CN", "SCN", "O", "S"}
    if c == "Sn4":
        return a in {"F", "Cl", "Br", "I", "O", "S", "SO4"}
    if c == "Bi3":
        return a in {"F", "Cl", "Br", "I", "NO3", "O", "S", "OH", "PO4"}
    if c in REDUCING and a in OXIDISING_ANIONS:
        return False
    if c == "NH4" and a in {"O", "OH"}:
        return False
    if c in {"Fe3", "Cu2"} and a == "I":
        return False  # Fe³⁺ / Cu²⁺ oxidise iodide
    if a in {"HCO3", "HSO4"}:
        return c in ALKALI
    if a == "H2PO4":
        return c in ALKALI | {"Ca", "Mg"}
    if a == "HPO4":
        return c in ALKALI | ALK_EARTH
    if a == "ClO":
        return c in ALKALI - {"NH4"} | {"Ca"}
    if a == "NO2":
        return c in ALKALI | {"Ag", "Ba", "Ca", "Sr", "Mg"}
    if a == "CN":
        return c in ALKALI | {"Ca", "Ba", "Sr", "Ag", "Zn", "Ni2", "Co2", "Hg2", "Pb2", "Cd"}
    if a == "SiO3":
        return c in ALKALI - {"NH4"} | ALK_EARTH | {"Zn", "Pb2"}
    if a == "S2O3":
        return c in ALKALI | ALK_EARTH | {"Pb2"}
    if a == "C2O4":
        return c not in {"Al", "Cr3"}
    if a == "CrO4":
        return c in ALKALI | ALK_EARTH | {"Ag", "Pb2", "Zn", "Cu2", "Ni2", "Co2", "Cd", "Hg2"}
    if a == "Cr2O7":
        return c in ALKALI | ALK_EARTH | {"Ag"}
    if a == "MnO4":
        return c in ALKALI | ALK_EARTH | {"Ag", "Zn"}
    if a in {"BrO3", "IO3", "ClO3"}:
        return c in ALKALI | ALK_EARTH | {"Ag", "Pb2", "Zn", "Cu2", "Ni2", "Co2", "Cd"}
    if a == "HCOO":
        return c in ALKALI | ALK_EARTH | {"Zn", "Cu2", "Pb2", "Ni2", "Co2", "Mn2", "Fe2", "Cd"}
    if a == "SCN":
        return c in ALKALI | ALK_EARTH | {"Ag", "Zn", "Pb2", "Hg2", "Co2", "Ni2", "Fe3", "Cd", "Mn2", "Fe2", "Cu2"}
    if c in TRIVALENT and a in {"CO3", "SO3", "CH3COO"} and c != "Al" and c != "Cr3" and c != "Fe3":
        return False
    if c in {"Al", "Fe3", "Cr3"} and a in {"CO3", "SO3"}:
        return False
    if a == "OH" and c in {"Ag", "Hg2"}:
        return False
    if a == "NO3" and c == "Sn2":
        return False
    if a == "CO3" and c in {"Hg2", "Sn2", "Be"}:
        return False
    if a == "SO3" and c in {"Hg2", "Cu2", "Be", "Sn2"}:
        return False
    if a == "PO4" and c in {"Sn2"}:
        return False
    if a == "S" and c in {"Al", "Fe3"}:
        return False
    if a == "CH3COO" and c in {"Cr3"}:
        return True
    return True


def soluble(c, a):
    if a in {"O", "O2", "N", "H"}:
        return False  # oxides, peroxides, nitrides, hydrides are insoluble or react with water
    if a == "C17H35COO":
        return c in {"Na", "K"}
    if a == "C6H5COO":
        return c != "Ag"
    if a == "C3H5O3":
        return True
    if a == "C6H5O7":
        return c in ALKALI | {"Mg", "Fe3"}
    if a in {"FeCN6_3", "FeCN6_4"}:
        return c in ALKALI | {"Ca"}
    if a in {"B4O7", "MoO4", "WO4", "AsO4"}:
        return c in ALKALI
    if a == "S" and (c in ALKALI or c in ALK_EARTH):
        return True
    if c == "Li" and a in {"F", "PO4", "CO3"}:
        return a == "CO3"
    if a in {"NO3", "CH3COO", "ClO3", "ClO4", "MnO4", "HCO3", "HSO4", "H2PO4", "HCOO", "ClO"}:
        return True
    if a == "NO2":
        return c != "Ag"
    if c in ALKALI:
        return True
    if a in {"Cl", "Br", "I"}:
        if c in {"Ag", "Pb2", "Cu1"}:
            return False
        if c == "Hg2" and a == "I":
            return False
        if c == "Bi3" and a == "I":
            return False
        return True
    if a == "F":
        return c not in {"Mg", "Ca", "Sr", "Ba", "Pb2", "Al", "Cr3", "Fe3", "Bi3"}
    if a == "SO4":
        return c not in {"Ba", "Sr", "Pb2", "Ca", "Hg2"}
    if a == "OH":
        return c in {"Ba", "Sr", "Ca"}
    if a == "C2O4":
        return c == "Fe3"
    if a == "CrO4":
        return c in {"Mg", "Ca"}
    if a == "Cr2O7":
        return c != "Ag"
    if a == "S2O3":
        return c not in {"Ag", "Pb2", "Ba"}
    if a == "CN":
        return c in {"Ca", "Ba", "Sr", "Hg2"}
    if a == "SCN":
        return c not in {"Ag", "Cu1", "Hg2"}
    if a == "IO3":
        return c not in {"Ag", "Ba", "Pb2", "Hg2", "Sr", "Ca", "Bi3", "Cu2"}
    if a == "BrO3":
        return c != "Ag"
    return False  # CO3, PO4, SO3, SiO3, HPO4, S ...


# Colours ---------------------------------------------------------------------------------------------

CAT_SOLUTION = {"Cu2": ("1F6FE5", 4.0), "Ni2": ("3FBF5F", 2.5), "Co2": ("E07AA8", 2.5), "Fe3": ("D9901A", 3.0), "Fe2": ("9ED39A", 1.2),
                "Cr3": ("3E8E5C", 3.0), "Mn2": ("F2C9D0", 0.3)}
AN_SOLUTION = {"MnO4": ("7A0F8C", 400.0), "CrO4": ("F2D21E", 40.0), "Cr2O7": ("F27A1E", 30.0), "FeCN6_3": ("E0A020", 20.0), "FeCN6_4": ("F2E070", 8.0)}
CAT_SOLID = {"Cu2": "2E7DE0", "Ni2": "4CAF50", "Co2": "D45A8A", "Fe3": "B5651D", "Fe2": "A8D8A0", "Cr3": "2F6B3F", "Mn2": "F0C8D0"}
AN_SOLID = {"MnO4": "5A0A6E", "CrO4": "F2D21E", "Cr2O7": "F27A1E", "FeCN6_3": "D9401E", "FeCN6_4": "F2D86A"}
SOLID = {
    ("Cu2", "OH"): "68B4F0", ("Fe3", "OH"): "9A4A18", ("Fe2", "OH"): "6E8B5A", ("Ni2", "OH"): "7CC47F", ("Co2", "OH"): "C9708C",
    ("Mn2", "OH"): "D9C3A5", ("Cr3", "OH"): "6E8B6E", ("Ag", "Cl"): "F4F4F0", ("Ag", "Br"): "F2E8B8", ("Ag", "I"): "F2DC5A",
    ("Pb2", "I"): "F5C518", ("Hg2", "I"): "E0301E", ("Bi3", "I"): "3A2A2A", ("Pb2", "CrO4"): "F5C518", ("Ba", "CrO4"): "F2DA3A",
    ("Sr", "CrO4"): "F2DA3A", ("Ag", "CrO4"): "A8321E", ("Ag", "PO4"): "F2E05A", ("Cu2", "CO3"): "3FA87A", ("Ni2", "CO3"): "7CC47F",
    ("Co2", "CO3"): "C9709A", ("Mn2", "CO3"): "E8C0C8", ("Fe2", "CO3"): "B5A07A", ("Zn", "S"): "F0F0E8", ("Cd", "S"): "F5C518",
    ("Mn2", "S"): "E6A0A0", ("Sn2", "S"): "6B4A2A", ("Sn4", "S"): "D9B53A", ("Bi3", "S"): "3A2A1E", ("Cu2", "O"): "1E1E1E",
    ("Cu1", "O"): "B0281E", ("Fe3", "O"): "8A2F1A", ("Fe2", "O"): "1E1E1E", ("Ag", "O"): "2A2420", ("Pb2", "O"): "F2C531",
    ("Hg2", "O"): "D9401E", ("Mn2", "O"): "3E6E3E", ("Cr3", "O"): "2F6B3F", ("Ni2", "O"): "5A8A4A", ("Co2", "O"): "5A5A3A",
    ("Bi3", "O"): "F2DA6A", ("Cu2", "Cl"): "3AA58A", ("Cu2", "Br"): "2A2A2A", ("Fe3", "Cl"): "5A3A1E", ("Fe3", "Br"): "6A1E14",
    ("Fe3", "SCN"): "7A0A0A", ("Co2", "SCN"): "2A4AB8", ("Ni2", "PO4"): "7CC47F", ("Cu2", "PO4"): "4A9AD8", ("Fe3", "PO4"): "E8D8A0",
    ("Cr3", "Cl"): "6A3A8A", ("Co2", "Cl"): "D45A8A", ("Ag", "S"): "1E1E1E",
    ("Fe3", "FeCN6_4"): "1A3A9A", ("Fe2", "FeCN6_3"): "1A3A9A", ("Cu2", "FeCN6_4"): "7A3A1A", ("K", "FeCN6_4"): "F2D86A",
    ("K", "FeCN6_3"): "D9401E", ("Na", "FeCN6_3"): "D9401E", ("Ag", "AsO4"): "8A4A1E", ("Ag", "FeCN6_3"): "E0801E",
    ("Mn4", "O"): "1E1E1E", ("Pb4", "O"): "4A2A1E", ("Na", "O2"): "F2E6A0", ("Pb2", "MoO4"): "F2A03A",
}
SULFIDE_BLACK = "1E1E1E"

# Hazards --------------------------------------------------------------------------------------------

CAT_HZ = {"Ba": hz("toxic", "irritant"), "Pb2": hz("toxic", "health", "environment"), "Hg2": hz("toxic", "health", "environment"),
          "Cd": hz("toxic", "health", "environment"), "Be": hz("toxic", "health"), "Cr3": hz("irritant"),
          "Ni2": hz("health", "irritant", "environment"), "Co2": hz("health", "irritant", "environment"), "Cu2": hz("irritant", "environment"),
          "Cu1": hz("irritant", "environment"), "Zn": hz("environment"), "Ag": hz("environment"), "Sn2": hz("irritant"), "Sn4": hz("corrosive"),
          "Fe3": hz("irritant"), "Fe2": hz("irritant"), "Al": hz("irritant"), "NH4": hz("irritant"), "Li": hz("irritant"), "Mn2": hz("irritant"),
          "Bi3": hz("irritant")}
AN_HZ = {"CN": hz("toxic", "environment"), "SCN": hz("irritant"), "F": hz("toxic"), "NO3": hz("oxidizer"), "NO2": hz("oxidizer", "toxic"),
         "ClO": hz("corrosive", "environment"), "ClO3": hz("oxidizer", "irritant"), "ClO4": hz("oxidizer", "irritant"), "BrO3": hz("oxidizer", "toxic"),
         "IO3": hz("oxidizer", "irritant"), "MnO4": hz("oxidizer", "environment", "irritant"), "CrO4": hz("oxidizer", "toxic", "health", "environment"),
         "Cr2O7": hz("oxidizer", "toxic", "health", "environment"), "HSO4": hz("corrosive"), "C2O4": hz("irritant"), "S": hz("environment"),
         "O2": hz("oxidizer", "corrosive"), "N": hz("waterreactive", "irritant"), "H": hz("flammable", "waterreactive"),
         "AsO4": hz("toxic", "health", "environment"), "B4O7": hz("health"), "MoO4": hz("irritant")}

# Hydrolysis: conjugate weak acid of a basic anion (hidden helper ions where the parent is not a catalogue acid).
HIDDEN = [("h_hco3", "Hydrogencarbonate ion", "HCO₃⁻", 4.7e-11), ("h_hco3_amph", "Hydrogencarbonate ion (amphiprotic)", "HCO₃⁻", 2.5e-4),
          ("h_hpo4", "Hydrogenphosphate ion", "HPO₄²⁻", 4.2e-13), ("h_h2po4", "Dihydrogenphosphate ion", "H₂PO₄⁻", 6.2e-8),
          ("h_hso3", "Hydrogensulfite ion", "HSO₃⁻", 6.3e-8), ("h_hc2o4", "Hydrogenoxalate ion", "HC₂O₄⁻", 6.4e-5),
          ("h_hs", "Hydrogensulfide ion", "HS⁻", 1.3e-13), ("h_hsio3", "Hydrogensilicate ion", "HSiO₃⁻", 1.6e-12)]
ANION_CONJ = {"CO3": "h_hco3", "HCO3": "h_hco3_amph", "PO4": "h_hpo4", "HPO4": "h_h2po4", "SO3": "h_hso3", "C2O4": "h_hc2o4",
              "S": "h_hs", "SiO3": "h_hsio3"}  # F, CN, NO2, ClO, CH3COO, HCOO → catalogue acids (filled in below)
ANION_ACID_KA = {"HSO4": 1.2e-2, "H2PO4": 6.2e-8}

DENSITY = {"NaCl": 2.16, "KCl": 1.98, "CaCO3": 2.71, "CaO": 3.34, "Ca(OH)2": 2.21, "MgO": 3.58, "Al2O3": 3.95, "Fe2O3": 5.24, "CuO": 6.31,
           "ZnO": 5.61, "PbO": 9.53, "Na2CO3": 2.54, "K2CO3": 2.43, "BaSO4": 4.5, "CaSO4": 2.96, "AgBr": 6.47, "AgI": 5.68, "PbI2": 6.16,
           "NaBr": 3.21, "KBr": 2.74, "NaI": 3.67, "K2Cr2O7": 2.68, "K2CrO4": 2.73, "Na2S2O3": 1.67, "CuCl2": 3.39, "NiSO4": 4.01,
           "CoCl2": 3.36, "MgCO3": 2.96, "BaCl2": 3.86, "Pb(NO3)2": 4.53, "Al2(SO4)3": 2.67, "NaF": 2.56, "CaF2": 3.18, "Mg(OH)2": 2.34,
           "Na3PO4": 2.54, "Ca3(PO4)2": 3.14, "LiCl": 2.07, "SrCl2": 3.05, "ZnS": 4.09, "Cu2O": 6.0, "Ag2O": 7.14, "HgO": 11.14,
           "MnO": 5.37, "Cr2O3": 5.22, "NiO": 6.67, "SnO2": 6.95, "Bi2O3": 8.9, "BaO": 5.72, "SrO": 4.7, "Li2O": 2.01, "Na2O": 2.27,
           "K2O": 2.35, "Na2SO3": 2.63, "Na2SiO3": 2.61, "KSCN": 1.89, "KIO3": 3.89, "KClO3": 2.32, "NaClO4": 2.52}

ALIASES = {
    ("Na", "B4O7"): ["Boraks"], ("Na", "C17H35COO"): ["Sabun", "Sodyum sabunu"], ("K", "C17H35COO"): ["Arap sabunu", "Potasyum sabunu"],
    ("Ca", "C17H35COO"): ["Sabun kiri (kireçli su)"], ("Fe3", "FeCN6_4"): ["Prusya mavisi", "Berlin mavisi"],
    ("Fe2", "FeCN6_3"): ["Turnbull mavisi"], ("K", "FeCN6_4"): ["Sarı kan tuzu"], ("K", "FeCN6_3"): ["Kırmızı kan tuzu"],
    ("Mn4", "O"): ["Mangan dioksit", "Pirolüzit"], ("Na", "C6H5COO"): ["E211", "Koruyucu"], ("Na", "C6H5O7"): ["Trisodyum sitrat"],
    ("Ca", "C3H5O3"): [], ("Na", "O2"): [], ("Ca", "WO4"): ["Şelit"], ("Pb2", "MoO4"): ["Vulfenit"], ("Ca", "H"): [], ("Mg", "N"): [],
    ("Na", "Cl"): ["Sofra tuzu", "Kaya tuzu", "Tuz"], ("Na", "HCO3"): ["Karbonat", "Kabartma tozu", "Sodyum hidrojen karbonat"],
    ("Ca", "CO3"): ["Kireç taşı", "Mermer", "Tebeşir"], ("Ca", "O"): ["Sönmemiş kireç"], ("Ca", "OH"): ["Sönmüş kireç", "Kireç suyu"],
    ("Na", "OH"): ["Sud kostik", "Kostik soda"], ("K", "OH"): ["Potas kostik"], ("Na", "CO3"): ["Çamaşır sodası", "Soda"],
    ("Cu2", "SO4"): ["Göztaşı"], ("K", "NO3"): ["Güherçile"], ("NH4", "Cl"): ["Nişadır"], ("Mg", "SO4"): ["İngiliz tuzu", "Epsom tuzu"],
    ("Na", "SO4"): ["Glauber tuzu"], ("Na", "ClO"): ["Çamaşır suyu"], ("Ca", "SO4"): ["Alçı", "Jips"], ("Ag", "NO3"): ["Cehennem taşı"],
    ("Fe3", "O"): ["Pas", "Hematit"], ("Al", "O"): ["Alümina", "Korundum"], ("K", "MnO4"): ["Permanganat"], ("Na", "S2O3"): ["Hipo"],
    ("Ca", "F"): ["Florit"], ("Ba", "SO4"): ["Barit"], ("Pb2", "S"): ["Galen"], ("Zn", "S"): ["Sfalerit"], ("Hg2", "S"): ["Zinober"],
    ("Na", "NO3"): ["Şili güherçilesi"], ("K", "CO3"): ["Potas"], ("Cu2", "O"): ["Bakır oksit"], ("Ca", "ClO"): ["Kireç kaymağı"],
    ("Ti", "O"): [], ("Na", "CH3COO"): ["Sodyum etanoat"], ("NH4", "NO3"): ["Amonyum güherçilesi"], ("K", "Cr2O7"): ["Bikromat"],
    ("Na", "SiO3"): ["Su camı"], ("Na", "PO4"): ["TSP"], ("Pb2", "I"): ["Altın yağmuru (çökelek)"],
}

EXISTING = {  # (cation, anion) → hand-tuned id in SubstanceDatabase.CreateDefault
    ("Na", "OH"): "naoh", ("K", "OH"): "koh", ("Na", "Cl"): "nacl", ("K", "Cl"): "kcl", ("Na", "NO3"): "nano3", ("K", "NO3"): "kno3",
    ("Na", "SO4"): "na2so4", ("K", "SO4"): "k2so4", ("Na", "CH3COO"): "sodium_acetate", ("K", "CH3COO"): "potassium_acetate",
    ("NH4", "Cl"): "nh4cl", ("NH4", "NO3"): "nh4no3", ("NH4", "SO4"): "nh4_2so4", ("Mg", "Cl"): "mgcl2", ("Zn", "Cl"): "zncl2",
    ("Fe2", "Cl"): "fecl2", ("Ca", "Cl"): "cacl2", ("Mg", "SO4"): "mgso4", ("Zn", "SO4"): "znso4", ("Fe2", "SO4"): "feso4",
    ("Cu2", "NO3"): "cu_no3_2", ("Cu2", "SO4"): "cuso4", ("Fe3", "Cl"): "fecl3", ("K", "MnO4"): "kmno4", ("Ag", "NO3"): "agno3",
    ("Ag", "Cl"): "agcl", ("Cu2", "OH"): "cu_oh_2", ("Fe3", "OH"): "fe_oh_3", ("Na", "HCO3"): "nahco3", ("Ca", "CO3"): "caco3",
    ("K", "I"): "ki", ("Na", "ClO"): "naocl",
}
EXISTING_ELEMENTS = {"Mg": "mg", "Zn": "zn", "Fe": "fe", "Cu": "cu", "Ag": "ag", "Na": "sodium"}
EXISTING_MOLECULAR = {"H2O": "water", "C2H5OH": "ethanol", "HCl": "hcl", "H2SO4": "h2so4", "HNO3": "hno3", "CH3COOH": "acetic_acid",
                      "NH3": "nh3", "C12H22O11": "sucrose", "H2O2": "h2o2", "C20H14O4": "phenolphthalein"}


def species(formula, charge):
    sign = "+" if charge > 0 else "-"
    n = abs(charge)
    return f"{formula}{sign}" if n == 1 else f"{formula}^{n}{sign}"


def gcd(a, b):
    while b:
        a, b = b, a % b
    return a


def ionic_formula(c, a):
    ck, cf, cq = CAT[c][0], CAT[c][1], CAT[c][2]
    ak, af, aq = AN[a][0], AN[a][1], AN[a][2]
    l = cq * aq // gcd(cq, aq)
    nc, na = l // cq, l // aq
    poly_c = len(re.findall(r"[A-Z]", cf)) > 1
    poly_a = len(re.findall(r"[A-Z]", af)) > 1 or ak in {"OH"}

    def part(f, n, poly):
        if n == 1:
            return f
        return f"({f}){n}" if poly else f"{f}{n}"

    poly_a = poly_a or len(re.findall(r"[A-Z]", af)) > 1
    if a in CARBOXYLATE_FIRST:  # carboxylates: anion written first (CH₃COONa, (CH₃COO)₂Ca)
        ascii_ = part(af, na, True) + part(cf, nc, poly_c)
    else:
        ascii_ = part(cf, nc, poly_c) + part(af, na, poly_a)
    return ascii_, nc, na


def display_ionic(ascii_):
    """Hexacyanoferrates get square brackets: K₄[Fe(CN)₆], Fe₄[Fe(CN)₆]₃."""
    s = ascii_.replace("(Fe(CN)6)", "[Fe(CN)6]")
    if "[" not in s:
        s = s.replace("Fe(CN)6", "[Fe(CN)6]", 1) if s.count("Fe") == 1 or not s.startswith("Fe") else s.replace("Fe(CN)6", "[Fe(CN)6]")
    return pretty(s)


def fmt(x):
    if x == 0:
        return ""
    return repr(float(x)) if abs(x) < 1e-3 or abs(x) >= 1e6 else f"{x:.6g}"


ROWS = []
USED_IDS = set()


def row(**k):
    fields = [
        k["id"], "1" if k.get("patch") else "0", k.get("en", ""), k.get("tr", ""), k.get("formula", ""), ";".join(k.get("aliases", [])),
        k.get("category", ""), k.get("kind", "Molecular"), k.get("phase", "S"), "1" if k.get("gas") else "0", "1" if k.get("soluble") else "0",
        k.get("form", "Powder"), str(k.get("hazards", 0)), fmt(k.get("M", 0)), fmt(k.get("density", 0)),
        "" if k.get("bp") is None else fmt(k["bp"]) if k["bp"] != 0 else "0", str(k.get("protons", 0) or ""), str(k.get("hydroxides", 0) or ""),
        fmt(k.get("ka", 0)), fmt(k.get("kb", 0)), k.get("conj", "") or "", str(k.get("conjCount", "") or ""),
        k.get("cat", "") or "", str(k.get("catCharge", "") or ""), str(k.get("catCount", "") or ""),
        k.get("an", "") or "", str(k.get("anCharge", "") or ""), str(k.get("anCount", "") or ""),
        k.get("element", "") or "", fmt(k.get("saltKa", 0)), str(k.get("saltCount", "") or ""),
        k.get("color", "") or "", fmt(k.get("alpha", 0)), fmt(k.get("tint", 0)), " ".join(k.get("ions", [])), k.get("molecular", "") or "",
        "1" if k.get("hidden") else "0", "1" if k.get("complex") else "0", fmt(k.get("cp", 0)), fmt(k.get("latent", 0)),
        k.get("flags", ""), k.get("cas", ""),
    ]
    assert len(fields) == 42
    for f in fields:
        assert "|" not in f and "\n" not in f, fields
    if not k.get("patch"):
        assert k["id"] not in USED_IDS, k["id"]
    USED_IDS.add(k["id"])
    ROWS.append("|".join(fields))


# ----------------------------------------------------------------------------------------------- molecular + acids

HZ_LETTER = dict(C="corrosive", T="toxic", F="flammable", O="oxidizer", I="irritant", E="environment", H="health", X="explosive", W="waterreactive")


def load_molecular():
    out = []
    for line in open(MOLECULAR_TXT, encoding="utf-8"):
        line = line.rstrip("\n")
        if not line or line.startswith("#"):
            continue
        f = line.split("|")
        assert len(f) == 15, line
        formula, tr, en, aliases, phase, dens, bp, sol, kind, k, protons, anion, hzs, color, category = f
        d = dict(formula=formula, tr=tr, en=en, aliases=[a for a in aliases.split(";") if a],
                 phase={"g": "gas", "l": "liquid", "s": "solid"}[phase], soluble=sol == "1",
                 kind={"m": "molecular", "sa": "strong_acid", "wa": "weak_acid", "wb": "weak_base"}[kind],
                 hazards=[HZ_LETTER[c] for c in hzs], category=category)
        if dens:
            d["density"] = float(dens)
        if bp:
            d["bp"] = float(bp)
        if k:
            d["ka" if kind == "wa" else "kb"] = float(k)
        if protons:
            d["protons"] = int(protons)
        if anion:
            d["anion"] = anion
        if color:
            d["color"] = color
        out.append(d)
    return out


mol = load_molecular()
ACID_FOR_ANION = {}
seen_formula = {}


def mol_id(formula):
    base = "m_" + re.sub(r"[^a-z0-9]", "", formula.lower())
    i, cand = 1, base
    while cand in USED_IDS:
        i += 1
        cand = f"{base}_{i}"
    return cand


KIND = {"molecular": "Molecular", "strong_acid": "StrongAcid", "weak_acid": "WeakAcid", "weak_base": "WeakBase"}

# hidden helper ions first
for hid, en, f, ka in HIDDEN:
    row(id=hid, en=en, tr=en, formula=f, category="hidden", kind="WeakAcid", phase="A", soluble=True, M=60, protons=1, ka=ka, hidden=True)

for c in mol:
    formula = c["formula"]
    parse_formula(formula)
    key = (formula, c["tr"].lower())
    if key in seen_formula:
        continue
    seen_formula[key] = True
    kind = KIND[c["kind"]]
    hazards = hz(*[h for h in c.get("hazards", []) if h in HZ])
    phase = {"gas": "A", "liquid": "L", "solid": "S"}[c["phase"]]
    if c["phase"] == "gas":
        hazards |= HZ["gas"]
    is_acid = kind in ("StrongAcid", "WeakAcid")
    anion = c.get("anion") if is_acid else None
    if anion and anion not in AN:
        anion = None
    M = molar_mass(formula)
    common = dict(en=c["en"], tr=c["tr"], formula=pretty(formula), aliases=c.get("aliases", []), category=c.get("category", "organic"))
    if anion:
        protons = AN[anion][2]
        common.update(cat="H", catCharge=1, catCount=protons, an=anion, anCharge=AN[anion][2], anCount=1)
    if formula in EXISTING_MOLECULAR and EXISTING_MOLECULAR[formula] not in USED_IDS:
        existing = EXISTING_MOLECULAR[formula]
        row(id=existing, patch=True, **common)
        if anion and kind == "WeakAcid":
            ACID_FOR_ANION.setdefault(anion, existing)
        continue
    sid = mol_id(formula)
    tb = c.get("bp")
    latent = 0
    if phase == "L" and tb is not None and tb > 25:
        latent = 88.0 * (tb + 273.15) / (M / 1000.0)  # Trouton's rule, J/kg
    color = (c.get("color") or "").lstrip("#").upper() or None
    alpha = 0.6 if phase == "L" else 1.0
    if color is None:
        color, alpha = ("E8F2FA", 0.18) if phase in ("L", "A") else ("F4F4F2", 1.0)
    protons = c.get("protons", 1 if is_acid else 0)
    ions = []
    if is_acid and anion:
        ions = [f"H3O+*{protons}" if protons > 1 else "H3O+", species(AN[anion][1], -AN[anion][2])]
    elif kind == "StrongAcid":
        ions = [f"H3O+*{protons}" if protons > 1 else "H3O+"]
    row(id=sid, kind=kind, phase=phase, gas=c["phase"] == "gas", soluble=c.get("soluble", False),
        form="Crystals" if phase == "S" else "Powder", hazards=hazards, M=M, density=c.get("density", 0) if phase != "A" else 1.0,
        bp=tb if phase == "L" and latent > 0 else None, protons=protons if is_acid else 0, hydroxides=1 if kind == "WeakBase" else 0,
        ka=c.get("ka", 0) if kind == "WeakAcid" else 0, kb=c.get("kb", 0) if kind == "WeakBase" else 0,
        color=color, alpha=alpha, tint=40.0 if c.get("category") == "indicator" and c.get("soluble") else 0,
        ions=ions, molecular=formula if not ions or kind == "WeakAcid" else "", cp=2000 if phase == "L" else 1200, latent=latent, **common)
    if anion and kind == "WeakAcid":
        ACID_FOR_ANION.setdefault(anion, sid)

for an_key in ("F", "CN", "NO2", "ClO", "CH3COO", "HCOO"):
    if an_key in ACID_FOR_ANION:
        ANION_CONJ[an_key] = ACID_FOR_ANION[an_key]
ANION_CONJ.setdefault("CH3COO", "acetic_acid")
for formula_, an_key in (("C6H5COOH", "C6H5COO"), ("C7H6O2", "C6H5COO"), ("H3BO3", "B4O7"), ("C6H8O7", "C6H5O7"), ("C3H6O3", "C3H5O3")):
    for r in ROWS:
        f_ = r.split("|")
        if norm_formula(f_[4]) == formula_ and f_[7] == "WeakAcid":
            ANION_CONJ.setdefault(an_key, f_[0])

row(id="universal_indicator", patch=True, tr="Üniversal indikatör", aliases=["pH kâğıdı çözeltisi", "Evrensel indikatör"], category="indicator")

# ----------------------------------------------------------------------------------------------- ionic compounds

ionic_count = 0
for c in CATIONS:
    for a in ANIONS:
        ck, ak = c[0], a[0]
        if not allowed(ck, ak):
            continue
        ascii_, nc, na = ionic_formula(ck, ak)
        sol = soluble(ck, ak)
        aq = AN[ak][2]
        name_tr = f"{c[3]} {a[3]}"
        name_en = f"{c[4]} {a[4]}"
        aliases = list(ALIASES.get((ck, ak), []))
        if ak in TR_ALT:
            aliases.append(f"{c[3]} {TR_ALT[ak]}")
        # hazards
        h = CAT_HZ.get(ck, 0) | AN_HZ.get(ak, 0)
        if ck == "Ba" and not sol:
            h &= ~HZ["toxic"]  # BaSO4 is used as an X-ray contrast agent
        if ak == "OH" and sol:
            h |= HZ["corrosive"]
        if ak == "O" and ck in ALKALI | ALK_EARTH:
            h |= HZ["corrosive"]
        if ak in {"CO3", "SiO3", "PO4"} and ck in ALKALI:
            h |= HZ["irritant"]
        if ck == "Ag" and ak == "NO3":
            h |= HZ["corrosive"]
        if ck == "Fe3" and ak == "Cl":
            h |= HZ["corrosive"]
        kind = "StrongBase" if ak == "OH" and sol else "Salt"
        # colour
        complex_ = ck == "Fe3" and ak == "SCN"
        tint, color, alpha = 0.0, None, 1.0
        if sol:
            if ak in AN_SOLUTION:
                color, tint = AN_SOLUTION[ak]
            elif ck in CAT_SOLUTION:
                color, tint = CAT_SOLUTION[ck]
            if complex_:
                color, tint = "8B0A0A", 150.0
        solid_color = SOLID.get((ck, ak)) or (SULFIDE_BLACK if ak == "S" and ck not in ALKALI | ALK_EARTH else None) \
            or AN_SOLID.get(ak) or (CAT_SOLID.get(ck) if sol or ak not in {"OH", "CO3", "PO4"} else None) or "F2F2F2"
        if ak == "S" and ck in ALKALI | ALK_EARTH:
            solid_color = "F2E6A0"
        if sol and color is not None:
            color_out, alpha = color, 0.8
        else:
            color_out, alpha = solid_color, 1.0
        M = molar_mass(ascii_)
        conj = ANION_CONJ.get(ak)
        if ck == "NH4":
            conj = "nh3"
        salt_ka = c[5] if c[5] > 0 else ANION_ACID_KA.get(ak, 0)
        salt_count = nc if c[5] > 0 else na
        ions = [species(c[1], c[2]) + (f"*{nc}" if nc > 1 else ""), species(a[1], -aq) + (f"*{na}" if na > 1 else "")]
        meta = dict(en=name_en, tr=name_tr, aliases=aliases, category="ionic", cat=ck, catCharge=c[2], catCount=nc, an=ak,
                    anCharge=aq, anCount=na, saltKa=salt_ka, saltCount=salt_count, conj=conj, conjCount=na if conj and ck != "NH4" else nc if conj else 0)
        if (ck, ak) in EXISTING:
            row(id=EXISTING[(ck, ak)], patch=True, **meta)
        else:
            sid = "i_" + (ck + "_" + ak).lower()
            row(id=sid, formula=display_ionic(ascii_), kind=kind, phase="S", soluble=sol,
                form="Crystals" if sol else "Powder", hazards=h, M=M, density=DENSITY.get(ascii_, min(7.5, max(1.5, 1.8 + M / 110.0))),
                hydroxides=na if kind == "StrongBase" else 0, color=color_out, alpha=alpha, tint=tint, ions=ions if sol else [],
                complex=complex_, cp=800, **meta)
        ionic_count += 1

# ----------------------------------------------------------------------------------------------- hydrates and special solids

HYDRATES = [  # anhydrous formula, waters, Turkish, English, aliases, colour, cation, anion
    ("CuSO4", 5, "Bakır(II) sülfat pentahidrat", "Copper(II) sulfate pentahydrate", ["Göztaşı", "Mavi vitriol"], "1F6FE5", "Cu2", "SO4"),
    ("MgSO4", 7, "Magnezyum sülfat heptahidrat", "Magnesium sulfate heptahydrate", ["İngiliz tuzu", "Epsom tuzu"], "F2F2F2", "Mg", "SO4"),
    ("Na2SO4", 10, "Sodyum sülfat dekahidrat", "Sodium sulfate decahydrate", ["Glauber tuzu"], "F2F2F2", "Na", "SO4"),
    ("Na2CO3", 10, "Sodyum karbonat dekahidrat", "Sodium carbonate decahydrate", ["Çamaşır sodası (kristal)"], "F2F2F2", "Na", "CO3"),
    ("CaSO4", 2, "Kalsiyum sülfat dihidrat", "Calcium sulfate dihydrate", ["Alçı taşı", "Jips"], "F2F2EE", "Ca", "SO4"),
    ("FeSO4", 7, "Demir(II) sülfat heptahidrat", "Iron(II) sulfate heptahydrate", ["Yeşil vitriol", "Karaboya"], "9ED39A", "Fe2", "SO4"),
    ("ZnSO4", 7, "Çinko sülfat heptahidrat", "Zinc sulfate heptahydrate", ["Beyaz vitriol"], "F2F2F2", "Zn", "SO4"),
    ("CoCl2", 6, "Kobalt(II) klorür hekzahidrat", "Cobalt(II) chloride hexahydrate", ["Nem göstergesi"], "C8406A", "Co2", "Cl"),
    ("NiSO4", 6, "Nikel(II) sülfat hekzahidrat", "Nickel(II) sulfate hexahydrate", [], "3FBF5F", "Ni2", "SO4"),
    ("Na2B4O7", 10, "Sodyum tetraborat dekahidrat", "Sodium tetraborate decahydrate", ["Boraks"], "F2F2F2", "Na", "B4O7"),
    ("Na2S2O3", 5, "Sodyum tiyosülfat pentahidrat", "Sodium thiosulfate pentahydrate", ["Hipo"], "F2F2F2", "Na", "S2O3"),
    ("CuCl2", 2, "Bakır(II) klorür dihidrat", "Copper(II) chloride dihydrate", [], "3AA58A", "Cu2", "Cl"),
    ("FeCl3", 6, "Demir(III) klorür hekzahidrat", "Iron(III) chloride hexahydrate", [], "C88A1A", "Fe3", "Cl"),
    ("Al2(SO4)3", 18, "Alüminyum sülfat oktadekahidrat", "Aluminium sulfate octadecahydrate", [], "F2F2F2", "Al", "SO4"),
    ("BaCl2", 2, "Baryum klorür dihidrat", "Barium chloride dihydrate", [], "F2F2F2", "Ba", "Cl"),
    ("CaCl2", 6, "Kalsiyum klorür hekzahidrat", "Calcium chloride hexahydrate", [], "F2F2F2", "Ca", "Cl"),
    ("MgCl2", 6, "Magnezyum klorür hekzahidrat", "Magnesium chloride hexahydrate", [], "F2F2F2", "Mg", "Cl"),
    ("Na2HPO4", 12, "Disodyum hidrojen fosfat dodekahidrat", "Disodium hydrogenphosphate dodecahydrate", [], "F2F2F2", "Na", "HPO4"),
    ("Pb(CH3COO)2", 3, "Kurşun(II) asetat trihidrat", "Lead(II) acetate trihydrate", ["Kurşun şekeri"], "F2F2F2", "Pb2", "CH3COO"),
    ("CH3COONa", 3, "Sodyum asetat trihidrat", "Sodium acetate trihydrate", ["Sıcak buz"], "F2F2F2", "Na", "CH3COO"),
]
for anhydrous, waters, tr, en, aliases, color, ck, ak in HYDRATES:
    if not allowed(ck, ak):
        continue
    base = (ck, ak)
    M = molar_mass(anhydrous) + waters * molar_mass("H2O")
    ascii_, nc, na = ionic_formula(ck, ak)
    sol = soluble(ck, ak)
    h = CAT_HZ.get(ck, 0) | AN_HZ.get(ak, 0)
    ions = [species(CAT[ck][1], CAT[ck][2]) + (f"*{nc}" if nc > 1 else ""), species(AN[ak][1], -AN[ak][2]) + (f"*{na}" if na > 1 else "")]
    tint, tcol = (AN_SOLUTION.get(ak) or CAT_SOLUTION.get(ck) or (None, 0.0))
    row(id="i_" + (ck + "_" + ak).lower() + f"_{waters}h2o", en=en, tr=tr, formula=pretty(anhydrous) + "·" + (str(waters) if waters > 1 else "") + "H₂O",
        aliases=aliases, category="ionic", kind="Salt", phase="S", soluble=sol, form="Crystals", hazards=h, M=M, density=1.9,
        color=color, alpha=1.0, tint=tcol, ions=ions if sol else [], cat=ck, catCharge=CAT[ck][2], catCount=nc, an=ak, anCharge=AN[ak][2], anCount=na,
        saltKa=CAT[ck][5], saltCount=nc, conj="nh3" if ck == "NH4" else ANION_CONJ.get(ak), conjCount=na, cp=1200)
    ionic_count += 1

SPECIAL = [  # formula, Turkish, English, aliases, colour, hazards, soluble
    ("Fe3O4", "Demir(II,III) oksit", "Iron(II,III) oxide", ["Manyetit", "Magnetit"], "1E1E1E", 0, False),
    ("Pb3O4", "Kurşun(II,IV) oksit", "Lead(II,IV) oxide", ["Sülüğen", "Minyum"], "D9401E", hz("toxic", "health", "environment"), False),
    ("KAl(SO4)2", "Potasyum alüminyum sülfat", "Potassium aluminium sulfate", ["Şap", "Potasyum şapı"], "F2F2F2", hz("irritant"), True),
    ("(NH4)2Fe(SO4)2", "Amonyum demir(II) sülfat", "Ammonium iron(II) sulfate", ["Mohr tuzu"], "B8D8A8", hz("irritant"), True),
    ("Ca5(PO4)3OH", "Hidroksiapatit", "Hydroxyapatite", ["Diş minesi", "Kemik minerali"], "F2F0E8", 0, False),
    ("Ca5(PO4)3F", "Florapatit", "Fluorapatite", [], "F2F0E8", 0, False),
    ("CaMg(CO3)2", "Dolomit", "Dolomite", ["Kalsiyum magnezyum karbonat"], "EDE8DD", 0, False),
    ("Cu2(OH)2CO3", "Bakır(II) karbonat hidroksit", "Basic copper carbonate", ["Malakit", "Bakır pası"], "3FA87A", hz("irritant", "environment"), False),
    ("Na2SiF6", "Sodyum hekzaflorosilikat", "Sodium hexafluorosilicate", [], "F2F2F2", hz("toxic"), False),
    ("NaAlO2", "Sodyum alüminat", "Sodium aluminate", [], "F2F2F2", hz("corrosive"), True),
]
for formula, tr, en, aliases, color, h, sol in SPECIAL:
    row(id="x_" + re.sub(r"[^a-z0-9]", "", formula.lower()), en=en, tr=tr, formula=pretty(formula).replace("(NH₄)₂Fe(SO₄)₂", "(NH₄)₂Fe(SO₄)₂"),
        aliases=aliases, category="ionic", kind="Salt", phase="S", soluble=sol, form="Crystals" if sol else "Powder", hazards=h,
        M=molar_mass(formula), density=3.0, color=color, alpha=1.0, cp=800)
    ionic_count += 1

# ----------------------------------------------------------------------------------------------- elements

DIATOMIC = {"H", "N", "O", "F", "Cl", "Br", "I"}
EL_HZ = {"Li": hz("flammable", "waterreactive", "corrosive"), "Na": hz("flammable", "waterreactive", "corrosive"),
         "K": hz("flammable", "waterreactive", "corrosive"), "Rb": hz("flammable", "waterreactive", "corrosive"),
         "Cs": hz("flammable", "waterreactive", "corrosive"), "Ca": hz("waterreactive"), "Sr": hz("waterreactive"), "Ba": hz("waterreactive", "irritant"),
         "Mg": hz("flammable"), "Al": hz("flammable"), "Zn": hz("environment"), "P": hz("flammable", "toxic", "environment"), "S": hz("irritant"),
         "Hg": hz("toxic", "health", "environment"), "Pb": hz("toxic", "health", "environment"), "Cd": hz("toxic", "health", "environment"),
         "As": hz("toxic", "environment"), "Be": hz("toxic", "health"), "Tl": hz("toxic", "health"), "Ni": hz("health", "irritant"),
         "Co": hz("health", "irritant"), "Se": hz("toxic"), "Sb": hz("irritant", "environment"), "Br": hz("corrosive", "toxic", "environment"),
         "I": hz("irritant", "environment"), "Cl": hz("toxic", "oxidizer", "environment"), "F": hz("toxic", "corrosive", "oxidizer"),
         "O": hz("oxidizer"), "H": hz("flammable"), "Cr": 0, "Ba_": 0}
EL_COLOR = {"Cu": "C8703A", "Au": "E6B422", "S": "F2E03A", "I": "3A2A4A", "Br": "8A2A0A", "C": "2A2A2A", "P": "B03A2A", "Si": "6A6E78",
            "Bi": "C8B8C8", "Hg": "C8CCD2", "Se": "5A2A2A", "B": "4A3A2A", "As": "7A7A7A", "Ca": "D8D8D0", "Pb": "6E7278", "Mg": "C9CCD2",
            "Zn": "AEB4BE", "Fe": "55585E", "Ag": "D8DADF", "Cs": "E8D8A0", "Na": "BFBFB8", "Ti": "8A8E96"}
EL_FORM = {"Mg": "Ribbon", "Cu": "Turnings", "Zn": "Granules", "Sn": "Granules", "Pb": "Granules", "Li": "Chunks", "Na": "Chunks", "K": "Chunks",
           "Rb": "Chunks", "Cs": "Chunks", "Ca": "Chunks", "Sr": "Chunks", "Ba": "Chunks", "I": "Crystals", "S": "Powder", "C": "Powder",
           "Fe": "Powder", "Al": "Powder", "Si": "Chunks", "Bi": "Chunks"}
EL_ALIASES = {
    ("Na", "B4O7"): ["Boraks"], ("Na", "C17H35COO"): ["Sabun", "Sodyum sabunu"], ("K", "C17H35COO"): ["Arap sabunu", "Potasyum sabunu"],
    ("Ca", "C17H35COO"): ["Sabun kiri (kireçli su)"], ("Fe3", "FeCN6_4"): ["Prusya mavisi", "Berlin mavisi"],
    ("Fe2", "FeCN6_3"): ["Turnbull mavisi"], ("K", "FeCN6_4"): ["Sarı kan tuzu"], ("K", "FeCN6_3"): ["Kırmızı kan tuzu"],
    ("Mn4", "O"): ["Mangan dioksit", "Pirolüzit"], ("Na", "C6H5COO"): ["E211", "Koruyucu"], ("Na", "C6H5O7"): ["Trisodyum sitrat"],
    ("Ca", "C3H5O3"): [], ("Na", "O2"): [], ("Ca", "WO4"): ["Şelit"], ("Pb2", "MoO4"): ["Vulfenit"], ("Ca", "H"): [], ("Mg", "N"): [],"C": ["Grafit", "Kömür"], "P": ["Kırmızı fosfor"], "S": ["Kükürt tozu"], "Hg": ["Civa"], "Al": ["Alüminyum tozu", "Alüminyum folyo"],
              "Fe": ["Demir tozu", "Demir talaşı"], "Cu": ["Bakır talaşı", "Bakır tel"], "Mg": ["Magnezyum şerit"], "Zn": ["Çinko granül"]}
METAL_CATS = {"AlkaliMetal", "AlkalineEarth", "Transition", "PostTransition", "Lanthanide", "Actinide"}
element_count = 0
for sym, e in sorted(ELEMENTS.items(), key=lambda kv: kv[1]["z"]):
    # Radioactive elements (Tc, Pm and Z ≥ 84) are listed too, flagged "R"; those made only a few atoms at a time
    # (Z ≥ 100) are also flagged "X". The app shows them but does not put them on the bench (except U and Th).
    radioactive = e["z"] > 83 or e["z"] in (43, 61)
    flags = ("R" if radioactive else "") + ("X" if e["z"] >= 100 else "")
    formula = sym + ("2" if sym in DIATOMIC else "")
    names = dict(en=EN_NAMES[sym], tr=e["tr"], aliases=EL_ALIASES.get(sym, []) + [sym], category="element", element=sym)
    if sym in EXISTING_ELEMENTS:
        row(id=EXISTING_ELEMENTS[sym], patch=True, **names)
        element_count += 1
        continue
    phase = {"Gas": "A", "Liquid": "L"}.get(e["phase"], "S")
    gas = e["phase"] == "Gas"
    h = EL_HZ.get(sym, 0) | (HZ["gas"] if gas else 0) | (hz("toxic", "health") if radioactive else 0)
    kind = "Metal" if e["cat"] in METAL_CATS else "Molecular"
    if e["cat"] == "AlkaliMetal":
        kind = "ReactiveMetal"
    M = e["mass"] * (2 if sym in DIATOMIC else 1)
    dens = e["density"] if not math.isnan(e["density"]) and not gas else 0
    color = EL_COLOR.get(sym, "B8BCC4" if kind != "Molecular" else "D8D8D8")
    latent = 0
    bp = None
    if phase == "L":
        bp = e["bpK"] - 273.15
        latent = 88.0 * e["bpK"] / (M / 1000.0)
    row(id="e_" + sym.lower(), formula=pretty(formula), kind=kind, phase=phase, gas=gas, soluble=False, form=EL_FORM.get(sym, "Granules"),
        hazards=h, M=M, density=dens, bp=bp, color=color, alpha=0.9 if phase == "L" else 1.0, molecular=sym, cp=500, latent=latent,
        flags=flags + ("" if dens else "d"), **names)
    element_count += 1

exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "gen_crc.py"), encoding="utf-8").read())
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "gen_pubchem.py"), encoding="utf-8").read())

# ----------------------------------------------------------------------------------------------- CAS numbers

# Rows written by hand (and the ionic/element tables) get their CAS number from the `chemicals` identifier table,
# looked up by English name and accepted only when the molar mass agrees.
if _CHEM is not None:
    from chemicals.identifiers import CAS_from_any, search_chemical
    _cas_added = 0
    for _i, _r in enumerate(ROWS):
        _f = _r.split("|")
        if _f[41] or _f[36] == "1" or not _f[2]:
            continue
        _m = _f[13]
        if not _m and not _f[4] and _f[1] == "1":
            _m = None  # hand-tuned substance (name checked by hand): trust the name lookup
        elif not _m:
            try:
                _m = molar_mass(_f[4].translate(str.maketrans("₀₁₂₃₄₅₆₇₈₉", "0123456789")))
            except Exception:
                continue
        try:
            _cas = CAS_from_any(_f[2])
            _meta = search_chemical(_cas)
        except Exception:
            continue
        if _meta is None or (_m is not None and abs(float(_meta.MW) - float(_m)) > 0.6):
            continue
        _f[41] = _cas
        ROWS[_i] = "|".join(_f)
        _cas_added += 1
    print(f"CAS numbers added to {_cas_added} rows")

# ----------------------------------------------------------------------------------------------- output

visible = len([r for r in ROWS if r.split("|")[36] == "0"])
new_rows = len([r for r in ROWS if r.split("|")[1] == "0" and r.split("|")[36] == "0"])
with open(OUT, "w", encoding="utf-8") as f:
    f.write("// <auto-generated> by desktop/tools/gen_chemicals.py. Do not edit by hand. </auto-generated>\n")
    f.write("namespace IDLab.Core.Chemistry\n{\n")
    f.write("    /// <summary>Chemical catalogue rows: id|patch|en|tr|formula|aliases|category|kind|phase|gas|soluble|form|hazards|M|density|bp|"
            "protons|hydroxides|ka|kb|conj|conjCount|cat|catCharge|catCount|an|anCharge|anCount|element|saltKa|saltCount|color|alpha|tint|ions|"
            "molecular|hidden|complex|cp|latent|flags|cas</summary>\n")
    f.write("    internal static class ChemicalData\n    {\n")
    f.write(f"        public const int Count = {visible};\n\n")
    f.write("        public const string Rows = @\"\n")
    for r in ROWS:
        f.write(r.replace('"', '""') + "\n")
    f.write("\";\n    }\n}\n")
print(f"ionic {ionic_count}, elements {element_count}, molecular {len(mol)}, rows {len(ROWS)}, catalogue entries {visible} (new {new_rows})")

# The PubChem extension is large: it is stored deflate-compressed and base64-encoded in string chunks
# (ChemicalDataExt.g.cs), which keeps the assembly's string heap small; ChemicalLibrary inflates it at start-up.
import base64, zlib
_raw = "\n".join(EXT_ROWS).encode("utf-8")
_co = zlib.compressobj(9, zlib.DEFLATED, -15)
_packed = base64.b64encode(_co.compress(_raw) + _co.flush()).decode("ascii")
_chunks = [_packed[i:i + 60000] for i in range(0, len(_packed), 60000)]
with open(OUT.replace("ChemicalData.g.cs", "ChemicalDataExt.g.cs"), "w", encoding="utf-8") as f:
    f.write("// <auto-generated> by desktop/tools/gen_chemicals.py (gen_pubchem.py). Do not edit by hand. </auto-generated>\n")
    f.write("namespace IDLab.Core.Chemistry\n{\n")
    f.write("    /// <summary>Further catalogue rows (same format as ChemicalData.Rows), raw-deflate compressed and base64 encoded.</summary>\n")
    f.write("    internal static class ChemicalDataExt\n    {\n")
    f.write(f"        public const int Count = {len(EXT_ROWS)};\n\n")
    f.write("        public static readonly string[] Chunks =\n        {\n")
    for c in _chunks:
        f.write(f'            "{c}",\n')
    f.write("        };\n    }\n}\n")
print(f"extension rows {len(EXT_ROWS)}: {len(_raw) // 1024} KB raw, {len(_packed) // 1024} KB packed")
