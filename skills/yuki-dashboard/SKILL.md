---
name: "yuki-dashboard"
description: Bouwt of vernieuwt het vaste financiële dashboard (verkoop, uitgaven, btw, netto over per maand en per jaar, banksaldo) uit een Belgische Yuki-administratie via de Yuki MCP, altijd met exact dezelfde layout en berekening. Gebruik deze skill wanneer de gebruiker vraagt om het Yuki-dashboard te maken, bij te werken, te vernieuwen of te publiceren, om de wekelijkse (geplande) dashboard-update uit te voeren, of om "netto over", banksaldo of verkoop/uitgaven per maand uit Yuki te tonen, ook als het woord dashboard niet valt.
---

# Yuki-dashboard

Deze skill maakt telkens **hetzelfde** dashboard. Consistentie komt uit drie vaste onderdelen; verander er geen enkele:

1. `assets/dashboard-template.html`: de vaste pagina (layout, grafiek, tabel, berekening van totalen en netto over). Nooit herschrijven, nooit restylen.
2. `scripts/build_dashboard.py`: rekent alle cijfers deterministisch uit de ruwe Yuki-data en vult het datablok in de template.
3. De vaste lijst Yuki-calls hieronder.

Jij (Claude) doet alleen: de calls uitvoeren, de ruwe resultaten opslaan, het script draaien, de controles nalopen en publiceren. **Reken zelf niets uit en vul het datablok nooit met de hand in.** Zo is de output elke week identiek van vorm.

De rekenregels staan voor naslag in `references/rekenregels.md`. Lees die alleen als het script een waarschuwing geeft of de gebruiker vraagt hoe iets berekend is.

## Werkwijze

Werkmap: maak een lege map `raw/` (bv. `/home/claude/raw/`). Elke tool-uitkomst wordt daar een bestand.

**Een resultaat opslaan:** als de omgeving het resultaat zelf in een bestand heeft gezet (melding "Tool result too large … stored at …"), kopieer dat bestand. Anders schrijf je de teruggegeven JSON letterlijk weg. Bestandsnamen volgen het patroon in de tabel; alleen het begin (rekeningcode + `_`) is belangrijk. Overlappende periodes zijn geen probleem: het script ontdubbelt op transactie-id.

### Stap 1: administratie
- `yuki_general_companies` → ID en naam. Bij meerdere administraties: vraag welke (bij een geplande taak: gebruik de administratie die in de taakopdracht staat). De **naam** gaat ongewijzigd naar `--admin-name`.

### Stap 2: rekeningschema
- `yuki_accounting_info_get_gl_account_scheme` (administrationID) → opslaan als `raw/scheme.json`.
- Bepaal uit dit schema (alleen `isEnabled: true`) de rekeningen per rol:

| Rol | subtype |
|---|---|
| klanten | 1 |
| leveranciers | 2 |
| btw verkoop | 63, 95, 64, 65 |
| btw aankoop | 72 |
| bank | 49 |

### Stap 3: transacties
Periode: **1 januari van (huidig jaar − 2) t/m de laatste dag van de huidige maand**. Tool: `yuki_accounting_info_get_transaction_details` met `financialMode = 0`. De tool geeft hoogstens 500 regels terug; gebruik daarom deze opdeling:

| Rekening(en) | Opdeling | Bestandsnaam |
|---|---|---|
| klanten | één call over de hele periode | `raw/<code>_a.json` |
| elke btw-verkooprekening | één call over de hele periode | `raw/<code>_a.json` |
| leveranciers | per halfjaar | `raw/<code>_<jaar>h1.json`, `…h2.json` |
| btw aankoop | per kalenderjaar | `raw/<code>_<jaar>.json` |
| elke bankrekening | per kalenderjaar | `raw/<code>_<jaar>.json` |

Geeft een call precies 500 regels terug, haal die periode dan opnieuw op in kleinere stukken (bv. per kwartaal).

### Stap 4: banksaldo-anker
- `yuki_accounting_info_get_start_balance_by_gl_account` met `bookyear = huidig jaar − 1`, `financialMode = 0` → `raw/start_balance.json`.
  Let op: deze call is een jaar verschoven. `bookyear = huidig jaar − 1` geeft het saldo op **1 januari van het huidige jaar**. Dat is precies wat het script verwacht.
- `yuki_accounting_gl_account_balance` (transactionDate = vandaag) → huidig saldo van de bankrekeningen, voor de controle in stap 6.

### Stap 5: script draaien
```bash
python <skill>/scripts/build_dashboard.py --raw raw --admin-name "<naam>" --today <JJJJ-MM-DD> \
  --template <skill>/assets/dashboard-template.html --out /mnt/user-data/outputs/dashboard.html
```

### Stap 6: controles
Het script drukt een rapport af. Controleer:
- Geen `WAARSCHUWING`-regels, of elke waarschuwing is verklaard of opgelost (bv. 500 regels → kleiner ophalen en opnieuw draaien).
- "laatste maand" banksaldo = het huidige banksaldo uit `yuki_accounting_gl_account_balance` (als er nadien geen bewegingen meer waren).
- Optioneel: "Controle verkoop excl. btw" van vorige maand ≈ `yuki_accounting_net_revenue` voor die maand. Kleine verschillen kunnen alleen door de creditnotaregel komen.

Klopt een controle niet, publiceer dan niet. Meld het verschil aan de gebruiker.

### Stap 7: publiceren
- Bestaat het dashboard al (de gebruiker of de taakopdracht geeft een claude.ai-artifactlink), publiceer dan met de Artifact-tool naar **die url**, zodat de link gelijk blijft.
- Anders: publiceer als nieuw artifact en geef de link aan de gebruiker, met de tip om die link in de wekelijkse taak op te nemen.
- Favicon `💶`, titel "Resultaat per maand".

Sluit af met een korte samenvatting: periode, netto over van de laatste volledige maand, netto over per jaar en eventuele opmerkingen uit de controles. Maak geen nieuwe analyses tenzij erom gevraagd wordt.

## Wekelijkse taak
Taakopdracht die je de gebruiker kunt voorstellen:
> Voer de skill yuki-dashboard uit voor administratie "<naam>" en publiceer naar <artifactlink>.

## Wat je niet doet
- De template aanpassen, kleuren wijzigen of secties toevoegen tijdens een update. Wijzigingen aan het dashboard horen in een nieuwe versie van deze skill.
- Cijfers zelf berekenen, afronden of corrigeren in het datablok.
- Andere rekeningen of trefwoorden gebruiken dan in het script staan. Wil de gebruiker iets anders, pas dan de skill aan (script + rekenregels) en zeg dat expliciet.

## Beperking
Werkt voor Belgische Yuki-administraties (rekeningschema met subtypes zoals hierboven). Bij een Nederlandse administratie vindt het schema andere rekeningen; meld dat en stop.
