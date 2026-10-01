# Rekenregels (naslag)

Alles hieronder is geïmplementeerd in `scripts/build_dashboard.py`. Dit document legt uit wat het script doet; pas het script aan als een regel verandert, nooit alleen dit document.

## Periode
Van 1 januari van (huidig jaar − 2) t/m de huidige maand. De grafiek toont standaard de laatste 12 maanden tot en met vorige maand; de tabel toont alle maanden; de jaarkaarten tonen de laatste 3 kalenderjaren.

## Verkoop (klanten + btw verkoop)
1. Alleen regels met `documentType` die begint met `TRMSales invoice`.
2. Per `documentID`: factuurbedrag incl. btw = som op de klantenrekening; btw = som op de btw-verkooprekeningen × −1.
3. Maand = datum van de regel op de klantenrekening (de btw-regel valt soms een dag later).
4. Excl. btw = incl. btw − btw.
5. Creditnota's (negatief bedrag) en hun vervangende factuur tellen in de maand van de gecorrigeerde factuur:
   - gecorrigeerde factuur = nummer na "Creditnota voor factuur …" in de omschrijving, anders de meest recente eerdere factuur van dezelfde klant met hetzelfde bedrag incl. btw;
   - vervangende factuur = factuur van dezelfde klant met hetzelfde bedrag incl. btw, gedateerd tussen de gecorrigeerde factuur en de creditnota.

## Aankoop (leveranciers + btw aankoop)
1. Alleen regels met `documentType` die begint met `TRMPurchase invoice`.
2. Per `documentID`: incl. btw = som op leveranciersrekening × −1; btw = som op btw-aankooprekening.
3. Maand = datum van de regel op de leveranciersrekening.

## Bankbewegingen (bankrekeningen)
Elke bankregel krijgt één categorie, in deze volgorde:
1. **klanten**: positief bedrag met op dezelfde datum een betaling van hetzelfde bedrag op de klantenrekening.
2. **leveranciers**: negatief bedrag met op dezelfde datum een betaling van hetzelfde bedrag op de leveranciersrekening.
3. Trefwoorden in de omschrijving:
   - loon_dividend: LOON, DIVIDEND, BEZOLDIGING
   - btw (niet in netto): BTW, TVA
   - belastingen: VOORHEFFING, BELASTING, SPF FINANCES, AGPR, FOD FINANCIEN, SERVICE PUBLIC
   - sociaal_verzekering: SOCIAAL VERZEKERINGSFONDS, XERIUS, ACERTA, LIANTIS, PARTENA, GROUP S, INSURANCE
   - tax_shelter: TAX SHELTER
4. Al de rest: **leveranciers** (overige betalingen en terugbetalingen).

Uitgaven per categorie = bedrag × −1 (een terugbetaling geeft dus een negatieve uitgave).

`overige_betalingen` = (leveranciers × −1) − aankoopfacturen incl. btw van dezelfde maand. Negatief = facturen geboekt maar pas later betaald.

## Banksaldo
Anker = saldo op 1 januari van het huidige jaar (start balance met bookyear = huidig jaar − 1). Huidig jaar: vooruit tellen met de maandbewegingen. Eerdere jaren: terugrekenen.

## In de pagina
- Uitgaven excl. btw = aankoopfacturen + loon_dividend + belastingen + sociaal_verzekering + tax_shelter + overige_betalingen
- Netto over = verkoop excl. btw − uitgaven excl. btw
- Jaar: som van de maanden; banksaldo einde jaar = saldo van de laatste maand van dat jaar (huidig jaar: "nu").

## Datablok-schema
```json
{"administratie":"<naam>","bijgewerkt_op":"JJJJ-MM-DD","maanden":[
 {"maand":"JJJJ-MM","verkoop_excl":0.00,"verkoop_btw":0.00,"aankoopfacturen_excl":0.00,"uitgaven_btw":0.00,
  "loon_dividend":0.00,"belastingen":0.00,"sociaal_verzekering":0.00,"tax_shelter":0.00,"overige_betalingen":0.00,"banksaldo":0.00}]}
```
