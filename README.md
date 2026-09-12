# Yuki MCP-server

Een MCP-server (Model Context Protocol, via stdio) die de [Yuki](https://yuki.be) accounting-
webservices ontsluit als tools voor Claude Desktop (of een andere MCP-client). Hiermee kan een
assistent rechtstreeks gegevens uit je Yuki-administratie opzoeken (facturen, boekingen, contacten,
documenten, BTW, ...) en, met de nodige voorzichtigheid, ook aanmaken of wijzigen.

De server wordt als één zelfstandige `.exe` gedistribueerd — er is geen .NET-installatie nodig op
de machine waar hij draait.

## Wat je nodig hebt

- Claude Desktop (of een andere MCP-client die stdio-servers ondersteunt).
- Een Yuki **webservice access key** (Domain- of Administratie-API-key). Deze vraag je op via je
  Yuki-domein (Instellingen → Webservices/API) of bij je Yuki-accountant/-beheerder.
- Windows (x64) — de meegeleverde `.exe` is gebouwd voor `win-x64`. (Het project ondersteunt ook
  macOS en Linux als build-target, zie "Zelf bouwen" hieronder, maar dat is niet getest.)

## Installatie

1. Pak de zip uit op een vaste locatie, bv. `C:\Tools\YukiMcp\`. Naast `YukiMcp.exe` zit een
   `.env`-bestand.
2. Open dat `.env`-bestand en vul je echte API-key in:

   ```
   APIKEY=JOUW_YUKI_API_KEY
   ```

3. Open (of maak) het Claude Desktop configuratiebestand `claude_desktop_config.json`:
   - Windows: `%APPDATA%\Claude\claude_desktop_config.json`
   - macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`
4. Voeg een entry toe onder `mcpServers` (enkel het pad naar de exe, geen API-key nodig):

   ```json
   {
     "mcpServers": {
       "yuki": {
         "command": "C:\\Tools\\YukiMcp\\YukiMcp.exe"
       }
     }
   }
   ```

5. Herstart Claude Desktop. De Yuki-tools verschijnen dan in het "hamer"-icoon (MCP tools) van een
   nieuwe chat.

De server leest de API-key uit het `.env`-bestand in dezelfde map als de exe (`APIKEY=...`) — zo
hoef je hem nooit in `claude_desktop_config.json` te zetten. Wie dat liever heeft, kan de key nog
steeds als opstartargument meegeven (`"args": ["--api-key", "JOUW_YUKI_API_KEY"]`) of via de
`YUKI_API_KEY`-omgevingsvariabele; die twee hebben voorrang op `.env`. De key komt nooit in een
tool-aanroep terecht.

## Wat kan de server?

- **Tools**: één tool per Yuki-webservice-operatie (`yuki_<service>_<operatie>`, bv.
  `yuki_accounting_gl_account_balance`, `yuki_archive_search_documents`,
  `yuki_contact_search_contacts`), plus een kleine groep `yuki_general_*`-tools voor
  domein-/administratie-opzoekingen (welke administraties/domeinen heb ik toegang toe, ...).
  Sommige tools **wijzigen data** in Yuki (documenten uploaden, journaalposten boeken, contacten
  bijwerken, ...) — behandel die met dezelfde voorzichtigheid als een rechtstreekse Yuki-API-call.
  Zie `plan.md` in de broncode voor de volledige lijst per Yuki-webservice.
- **Resource**: `help://yuki-mcp` geeft een korte uitleg over authenticatie en de toolindeling —
  vraag je assistent gerust om die op te vragen als je twijfelt hoe iets werkt.
- **Prompts / apps**: nog niet geïmplementeerd; de basis staat klaar voor later (zie `plan.md`).

Er is geen aparte sessie- of inlogstap nodig: de server authenticeert zelf bij Yuki met de
opgegeven API-key en ververst de sessie automatisch.

## Zelf bouwen

Vereist: [.NET SDK 10](https://dotnet.microsoft.com/download) of nieuwer.

```powershell
dotnet build                                   # compileren
dotnet run --project src/YukiMcp -- --api-key <key>   # lokaal draaien

# Publiceren als één zelfstandige exe (standaard win-x64):
./scripts/publish.ps1
# -> build/YukiMcp.exe

# Publiceren + inpakken in een deelbare zip (met deze README, de LICENSE, CHANGELOG.md en een
# .env-template met een placeholder-key erbij). Versienummer (v1, v2, ...) wordt automatisch
# +1 t.o.v. het VERSION-bestand - voeg eerst een "## vN - <datum>"-sectie toe aan CHANGELOG.md,
# anders weigert het script te bouwen:
./scripts/package-zip.ps1
# -> dist/YukiMcp-vN-win-x64.zip
```

Zie [CHANGELOG.md](CHANGELOG.md) voor wat er in elke release zit.

## Testen met MCP Inspector

[MCP Inspector](https://github.com/modelcontextprotocol/inspector) is Anthropics browser-tool om
een MCP-server rechtstreeks te testen (tools/resources oplijsten, een tool aanroepen, de ruwe
request/response bekijken) zonder Claude Desktop erbij nodig te hebben — handig tijdens
ontwikkeling of om een build te controleren voor je hem aan Claude Desktop koppelt. Vereist
[Node.js](https://nodejs.org/).

1. Zorg dat er ergens een `YukiMcp.exe` staat met een `.env`-bestand (met een geldige `APIKEY`, zie
   "Installatie" hierboven) in **dezelfde map** — bv. na `./scripts/publish.ps1` in `build/`, of na
   `dotnet build` in `src/YukiMcp/bin/Debug/net10.0/win-x64/`.
2. Start Inspector met dat pad als command:

   ```powershell
   npx @modelcontextprotocol/inspector C:\Tools\YukiMcp\YukiMcp.exe
   ```

   Dit opent Inspector in de browser (het exacte adres, standaard iets als
   `http://localhost:6274`, staat in de terminal-output) met **Transport Type: STDIO** en
   **Command** al ingevuld.
3. Voeg je de server liever manueel toe in de Inspector-UI (of in een andere MCP-client met
   dezelfde STDIO/Command/Arguments-velden)? Vul dan:
   - **Command**: het volledige, absolute pad naar een `YukiMcp.exe` die ook echt bestaat op die
     locatie. Een foutmelding als `'...\YukiMcp.exe' is not recognized as an internal or external
     command` betekent meestal dat het pad niet naar een bestaand bestand wijst (bv. omdat de exe
     nog in `build/` of `bin/...` staat in plaats van in de map die je hebt ingevuld).
   - **Arguments**: leeg laten — de key komt uit het `.env`-bestand naast de exe. (Wil je expliciet
     met een opstartargument testen, vul dan `--api-key JOUW_KEY` in.)
   - **Environment Variables** (indien de UI dat veld heeft): optioneel, als alternatief voor
     `.env`, `YUKI_API_KEY` = je key.
4. Klik **Connect**, open het "Tools"-tabblad — daar staan de ~96 `yuki_*`-tools — kies er één, vul
   de parameters in en klik **Run Tool** om de respons te bekijken.

## Status

Dit project is een werkende eerste versie: de server, alle 13 Yuki-webservices en 96 tools zijn
opgezet en end-to-end getest (opstarten, tools/resources oplijsten, een tool aanroepen) — maar nog
**niet functioneel getest tegen een echte Yuki-administratie**. Zie `plan.md` voor de volledige
architectuur, de roadmap en gekende aandachtspunten (met name rond de tools die data wijzigen).

## Licentie

Zie [LICENSE](LICENSE).
