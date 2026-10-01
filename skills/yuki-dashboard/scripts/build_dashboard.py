#!/usr/bin/env python3
"""
Bouwt het datablok voor het Yuki-dashboard uit ruwe Yuki MCP-resultaten en zet het in de template.

Gebruik:
  python build_dashboard.py --raw <map> --admin-name "<naam uit yuki_general_companies>" \
      --today JJJJ-MM-DD --template <dashboard-template.html> --out <dashboard.html>

De map <raw> bevat:
  scheme.json            resultaat van yuki_accounting_info_get_gl_account_scheme
  start_balance.json     resultaat van yuki_accounting_info_get_start_balance_by_gl_account (bookyear = huidig jaar - 1)
  <rekening>_<x>.json    resultaten van yuki_accounting_info_get_transaction_details, één bestand per call,
                         bestandsnaam begint met de rekeningcode (bv. 400000_a.json, 550000_2024.json)

Elk bestand mag de ruwe JSON-lijst zijn of de opgeslagen vorm [{"text": "<json>"}].
Het script rekent volledig deterministisch: dezelfde invoer geeft altijd hetzelfde datablok.
"""
import argparse, calendar, datetime as dt, glob, json, os, re, sys
from collections import defaultdict

# ---------- vaste regels (niet aanpassen zonder de skill te herzien) ----------
SUB_KLANTEN, SUB_LEVERANCIERS, SUB_BANK = 1, 2, 49
SUB_BTW_VERKOOP = {63, 95, 64, 65}
SUB_BTW_AANKOOP = {72}
ROW_LIMIT = 500
KEYWORDS = [  # volgorde telt; eerste treffer wint
    ("loon_dividend", ["LOON", "DIVIDEND", "BEZOLDIGING"]),
    ("btw", ["BTW", "TVA"]),
    ("belastingen", ["VOORHEFFING", "BELASTING", "SPF FINANCES", "AGPR", "FOD FINANCIEN", "SERVICE PUBLIC"]),
    ("sociaal_verzekering", ["SOCIAAL VERZEKERINGSFONDS", "XERIUS", "ACERTA", "LIANTIS", "PARTENA", "GROUP S", "INSURANCE"]),
    ("tax_shelter", ["TAX SHELTER"]),
]
CREDIT_REF = re.compile(r"creditnota voor factuur\s+(\S+)", re.I)


def load(path):
    with open(path, encoding="utf-8") as f:
        d = json.load(f)
    if isinstance(d, list) and d and isinstance(d[0], dict) and "text" in d[0] and set(d[0].keys()) <= {"text", "type"}:
        d = json.loads("".join(x["text"] for x in d))
    return d


def cents(x):
    return int(round(float(x) * 100))


def month_of(s):
    return s[:7]


def r2(c):
    return round(c / 100.0, 2)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--raw", required=True)
    ap.add_argument("--admin-name", required=True)
    ap.add_argument("--today", required=True)
    ap.add_argument("--template", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--data-out")
    a = ap.parse_args()

    today = dt.date.fromisoformat(a.today)
    first_month = f"{today.year - 2}-01"
    last_month = f"{today.year}-{today.month:02d}"
    months = []
    y, m = today.year - 2, 1
    while f"{y}-{m:02d}" <= last_month:
        months.append(f"{y}-{m:02d}")
        m += 1
        if m == 13:
            y, m = y + 1, 1
    warnings = []

    # ---------- rekeningschema ----------
    scheme = load(os.path.join(a.raw, "scheme.json"))
    roles = defaultdict(set)
    for acc in scheme:
        if not acc.get("isEnabled"):
            continue
        st, code = acc.get("subtype"), str(acc.get("code"))
        if st == SUB_KLANTEN: roles["klanten"].add(code)
        elif st == SUB_LEVERANCIERS: roles["leveranciers"].add(code)
        elif st == SUB_BANK: roles["bank"].add(code)
        elif st in SUB_BTW_VERKOOP: roles["btw_verkoop"].add(code)
        elif st in SUB_BTW_AANKOOP: roles["btw_aankoop"].add(code)

    # ---------- transacties laden, ontdubbelen op id ----------
    tx = defaultdict(dict)
    for p in sorted(glob.glob(os.path.join(a.raw, "*.json"))):
        name = os.path.basename(p)
        mcode = re.match(r"^(\d{4,})_", name)
        if not mcode:
            continue
        rows = load(p)
        if len(rows) >= ROW_LIMIT:
            warnings.append(f"{name}: {len(rows)} regels (limiet {ROW_LIMIT}); controleer of de periode volledig is of haal kleiner op.")
        for r in rows:
            tx[mcode.group(1)][r.get("id") or f"{name}:{len(tx[mcode.group(1)])}"] = r
    def lines(role):
        out = []
        for code in sorted(roles[role]):
            out.extend(tx.get(code, {}).values())
        return out
    for role in ("klanten", "leveranciers", "bank"):
        for code in roles[role]:
            if code not in tx and role != "bank":
                warnings.append(f"Geen transacties gevonden voor {role}-rekening {code}.")
    banks_with_data = [c for c in roles["bank"] if c in tx]

    # ---------- verkoop ----------
    sdoc = {}
    for r in lines("klanten"):
        if not str(r.get("documentType") or "").startswith("TRMSales invoice"):
            continue
        d = sdoc.setdefault(r["documentID"], dict(gross=0, vat=0, date=r["transactionDate"][:10],
                                                  contact=r.get("contactID"), ref=r.get("documentReference"),
                                                  desc=r.get("description") or ""))
        d["gross"] += cents(r["transactionAmount"])
        d["date"] = min(d["date"], r["transactionDate"][:10])
    for r in lines("btw_verkoop"):
        if str(r.get("documentType") or "").startswith("TRMSales invoice") and r.get("documentID") in sdoc:
            sdoc[r["documentID"]]["vat"] += -cents(r["transactionAmount"])
    for d in sdoc.values():
        d["month"] = month_of(d["date"])
    # creditnota's + vervangende facturen naar de maand van de gecorrigeerde factuur
    by_ref = {d["ref"]: k for k, d in sdoc.items() if d["ref"]}
    for k, cn in sorted(sdoc.items(), key=lambda kv: kv[1]["date"]):
        if cn["gross"] >= 0:
            continue
        orig = None
        mref = CREDIT_REF.search(cn["desc"])
        if mref and mref.group(1) in by_ref:
            orig = by_ref[mref.group(1)]
        else:
            cands = [(d["date"], kk) for kk, d in sdoc.items()
                     if kk != k and d["contact"] == cn["contact"] and d["gross"] == -cn["gross"] and d["date"] <= cn["date"]]
            if cands:
                orig = max(cands)[1]
        if not orig:
            continue
        o = sdoc[orig]
        cn["month"] = o["month"]
        for kk, d in sdoc.items():
            if kk in (k, orig) or d["contact"] != o["contact"] or d["gross"] != o["gross"]:
                continue
            if o["date"] <= d["date"] <= cn["date"]:
                d["month"] = o["month"]
                break
    sales = defaultdict(lambda: [0, 0])
    for d in sdoc.values():
        sales[d["month"]][0] += d["gross"] - d["vat"]
        sales[d["month"]][1] += d["vat"]

    # ---------- aankoop ----------
    pdoc = {}
    for r in lines("leveranciers"):
        if not str(r.get("documentType") or "").startswith("TRMPurchase invoice"):
            continue
        d = pdoc.setdefault(r["documentID"], dict(gross=0, vat=0, date=r["transactionDate"][:10]))
        d["gross"] += -cents(r["transactionAmount"])
        d["date"] = min(d["date"], r["transactionDate"][:10])
    for r in lines("btw_aankoop"):
        if str(r.get("documentType") or "").startswith("TRMPurchase invoice") and r.get("documentID") in pdoc:
            pdoc[r["documentID"]]["vat"] += cents(r["transactionAmount"])
    purch = defaultdict(lambda: [0, 0])
    for d in pdoc.values():
        mo = month_of(d["date"])
        purch[mo][0] += d["gross"] - d["vat"]
        purch[mo][1] += d["vat"]

    # ---------- bank indelen ----------
    def payment_keys(role):
        keys = defaultdict(int)
        for r in lines(role):
            if not r.get("documentType"):
                keys[(r["transactionDate"][:10], -cents(r["transactionAmount"]))] += 1
        return keys
    kpay, lpay = payment_keys("klanten"), payment_keys("leveranciers")
    cat = defaultdict(lambda: defaultdict(int))
    move = defaultdict(int)
    for r in sorted(lines("bank"), key=lambda r: (r["transactionDate"], str(r.get("id")))):
        mo, c, key = month_of(r["transactionDate"]), cents(r["transactionAmount"]), (r["transactionDate"][:10], cents(r["transactionAmount"]))
        move[mo] += c
        u = (r.get("description") or "").upper()
        if c > 0 and kpay.get(key, 0) > 0:
            kpay[key] -= 1; k = "klanten"
        elif c < 0 and lpay.get(key, 0) > 0:
            lpay[key] -= 1; k = "leveranciers"
        else:
            k = next((name for name, words in KEYWORDS if any(w in u for w in words)), None)
            if k is None:
                k = "leveranciers"  # overige betalingen en terugbetalingen
        cat[mo][k] += c

    # ---------- banksaldo ----------
    sb = load(os.path.join(a.raw, "start_balance.json"))
    anchor = sum(cents(x["startBalance"]) for x in sb if str(x.get("accountID")) in roles["bank"])
    jan = f"{today.year}-01"
    bal = {}
    b = anchor
    for mo in [x for x in months if x >= jan]:
        b += move.get(mo, 0); bal[mo] = b
    b = anchor
    prev = [x for x in months if x < jan]
    for i in range(len(prev) - 1, -1, -1):
        bal[prev[i]] = b
        b -= move.get(prev[i], 0)
    bank_months = {month_of(r["transactionDate"]) for r in lines("bank")}
    for mo in months[:-1]:
        if mo not in bank_months:
            warnings.append(f"Geen bankbewegingen gevonden in {mo}; controleer of de bankdata volledig is.")

    # ---------- datablok ----------
    out = []
    for mo in months:
        cm = cat.get(mo, {})
        lev_paid = -cm.get("leveranciers", 0)
        pg = purch[mo][0] + purch[mo][1]
        out.append({
            "maand": mo,
            "verkoop_excl": r2(sales[mo][0]), "verkoop_btw": r2(sales[mo][1]),
            "aankoopfacturen_excl": r2(purch[mo][0]), "uitgaven_btw": r2(purch[mo][1]),
            "loon_dividend": r2(-cm.get("loon_dividend", 0)),
            "belastingen": r2(-cm.get("belastingen", 0)),
            "sociaal_verzekering": r2(-cm.get("sociaal_verzekering", 0)),
            "tax_shelter": r2(-cm.get("tax_shelter", 0)),
            "overige_betalingen": r2(lev_paid - pg) if mo in bank_months else 0.0,
            "banksaldo": r2(bal[mo]),
        })
    data = {"administratie": a.admin_name, "bijgewerkt_op": today.isoformat(), "maanden": out}

    keys = ["maand", "verkoop_excl", "verkoop_btw", "aankoopfacturen_excl", "uitgaven_btw", "loon_dividend",
            "belastingen", "sociaal_verzekering", "tax_shelter", "overige_betalingen", "banksaldo"]
    rows = ",\n".join("    {" + ",".join(
        (f'"{k}":"{r[k]}"' if k == "maand" else f'"{k}":{r[k]:.2f}') for k in keys) + "}" for r in out)
    block = ('{\n  "administratie": ' + json.dumps(a.admin_name, ensure_ascii=False) +
             ',\n  "bijgewerkt_op": "' + today.isoformat() + '",\n  "maanden": [\n' + rows + "\n  ]\n}")

    with open(a.template, encoding="utf-8") as f:
        html = f.read()
    pat = re.compile(r'(<script type="application/json" id="dashboard-data">\n).*?(\n</script>)', re.S)
    if not pat.search(html):
        sys.exit("Template bevat geen datablok (script#dashboard-data).")
    html = pat.sub(lambda mm: mm.group(1) + block + mm.group(2), html)
    with open(a.out, "w", encoding="utf-8") as f:
        f.write(html)
    if a.data_out:
        with open(a.data_out, "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=1)

    # ---------- rapport ----------
    print(f"Administratie: {a.admin_name}   periode: {first_month} t/m {last_month}")
    print("Rekeningen:", {k: sorted(v) for k, v in roles.items()})
    print(f"Banksaldo 1 januari {today.year}: {r2(anchor):.2f}   laatste maand: {r2(bal[last_month]):.2f}")
    prev_m = months[-2]
    print(f"Controle verkoop excl. btw {prev_m}: {r2(sales[prev_m][0]):.2f} (vergelijk met yuki_accounting_net_revenue)")
    for w in warnings:
        print("WAARSCHUWING:", w)
    print(f"Geschreven: {a.out}")


if __name__ == "__main__":
    main()
