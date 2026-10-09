# Imported by gen_chemicals.py after gen_crc.py (executed in its namespace). Adds tens of thousands of further
# organic compounds from the PubChem identifier table shipped with the `chemicals` package: every compound with a
# readable common name, a molecular formula of common elements and a known melting or boiling point (so its state at
# 25 °C is known). Like the CRC organics they carry no hazard / solubility data and are flagged "L" (limited data).
# The rows are written compressed to ChemicalDataExt.g.cs (see the output section of gen_chemicals.py).
import os, re

EXT_ROWS = []
if _CHEM:
    from chemicals import Tb as _Tb, Tm as _Tm

    _used_cas = set()
    for _r in ROWS:
        _f = _r.split("|")
        if _f[41]:
            _used_cas.add(_f[41])
        if _f[0].startswith("c_"):
            _used_cas.add(_f[0][2:])
    _allowed = {"C", "H", "N", "O", "S", "P", "F", "Cl", "Br", "I", "Si", "B", "Na", "K"}
    _new_iupac = re.compile(r"anyl|anylidene|^[A-Z0-9\-]+$")
    _ok_name = re.compile(r"[a-z0-9 ,'()+\-αβγδω±]+")

    def _pick_name(cols):
        for cand in (cols[8] if len(cols) > 8 else "", cols[7]):
            c = cand.strip()
            if not c or len(c) > 40 or _new_iupac.search(c) or not _ok_name.fullmatch(c) or ", " in c:
                continue
            if re.search(r"\d{4,}", c) or c.count("(") > 2:
                continue
            return c
        return None

    _start = len(ROWS)
    _added = 0
    with open(os.path.join(_CHEM, "Identifiers", "chemical identifiers pubchem large.tsv"), encoding="utf-8") as fh:
        for line in fh:
            cols = line.rstrip("\n").split("\t")
            if len(cols) < 8:
                continue
            cas, f = cols[1], cols[2]
            if not cas or cas in _used_cas or cas.replace("-", "") in _used_cas or "+" in f or "-" in f:
                continue
            try:
                comp = parse_formula(f)
            except Exception:
                continue
            if "C" not in comp or not set(comp) <= _allowed:
                continue
            M = molar_mass(f)
            if M > 500 or M < 16:
                continue
            name = _pick_name(cols)
            if name is None:
                continue
            key = re.sub(r"[^a-z0-9]", "", name.lower())
            if key in EXISTING_EN:
                continue
            try:
                tm, tb = _Tm(cas), _Tb(cas)
            except Exception:
                tm = tb = None
            phase = _phase(tm, tb, M)
            if phase is None:
                continue
            EXISTING_EN.add(key)
            _used_cas.add(cas)
            clean = name[0].upper() + name[1:] if name[0].isalpha() else name
            latent, bp = 0, None
            if phase == "L" and tb and tb > ROOM:
                bp = tb - 273.15
                latent = 88.0 * tb / (M / 1000.0)
            row(id="p_" + cas.replace("-", ""), en=clean, tr=tr_name(clean), formula=pretty(f), aliases=[f], category="organic",
                kind="Molecular", phase=phase, gas=phase == "A", soluble=False, form="Crystals" if phase == "S" else "Powder",
                hazards=HZ["gas"] if phase == "A" else 0, M=M, density=1.0 if phase != "S" else 1.2, bp=bp,
                color=("E8F2FA" if phase == "L" else "F4F4F2"), alpha=(0.18 if phase == "L" else 1.0), molecular=f,
                cp=2000 if phase == "L" else 1200, latent=latent, cas=cas, flags="Ld")
            _added += 1
    EXT_ROWS = ROWS[_start:]
    del ROWS[_start:]
    print(f"PubChem: {_added} further organic compounds (compressed extension)")
