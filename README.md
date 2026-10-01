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

## Skill: wekelijks Yuki-dashboard

In [skills/yuki-dashboard/](skills/yuki-dashboard/) zit een generieke Claude-skill die op basis van
deze MCP-server altijd hetzelfde financiële dashboard maakt (verkoop, uitgaven, btw, netto over per
maand en per jaar, banksaldo) en als artifact publiceert. Je kan er ook een wekelijkse geplande
taak van maken, zodat het dashboard zichzelf ververst.

> **Belangrijk:** de MCP-server draait lokaal op je pc. Claude Desktop heeft dus je pc nodig
> om de Yuki-tools te gebruiken, en een geplande taak draait enkel als die pc aan staat en wakker
> is.

### 1. De skill toevoegen in Claude Desktop

1. Maak een zip van de skill-map, zodat `yuki-dashboard/SKILL.md` in de zip staat:

   ```powershell
   Compress-Archive -Path skills\yuki-dashboard -DestinationPath yuki-dashboard.zip
   ```

2. Open in Claude Desktop **Settings → Capabilities** (of **Customize → Skills**) en zorg dat
   *code execution / file creation* aan staat (de skill draait een Python-script).
3. Kies bij **Skills** voor **Upload skill** en selecteer `yuki-dashboard.zip`.
4. Zet de skill aan. Test met een nieuwe chat, bv.: *"Maak het Yuki-dashboard voor administratie
   `<naam van je administratie>`"*.

### 2. Een artifact-url krijgen

De skill publiceert het dashboard als artifact en wil bij elke update **dezelfde link**
hergebruiken. Die link maak je zo:

1. Laat de skill het dashboard één keer uitvoeren (zie de testchat hierboven). Omdat er nog geen
   link is, publiceert Claude een nieuw artifact.
2. Open het artifact in Claude en kopieer de url uit de adresbalk of via het deel-/kopieer-menu
   van het artifact. Ze ziet eruit als `https://claude.ai/artifact/<id>`.
3. Bewaar die link: die gebruik je in de geplande taak, zodat elke update dezelfde pagina
   vernieuwt in plaats van telkens een nieuw artifact te maken.

### 3. De wekelijkse taak plannen

1. Open in Claude Desktop **Scheduled tasks** (zijbalk) en kies **New task**. Je kan Claude de
   taak ook laten aanmaken door in een chat gewoon te typen dat je het dashboard elke week wilt
   bijwerken.
2. Vul in:
   - **Name**: bv. `Weekly Yuki Dashboard update`
   - **Instructions**:

     ```
     Voer de skill yuki-dashboard uit voor administratie "<naam van je administratie>" en
     publiceer naar <jouw artifact-url>.
     ```

   - **Frequency**: `Weekly`, een dag en uur naar keuze (bv. maandag 10:00)
   - **Permissions**: `Automatically approve`, zodat de taak de Yuki-tools kan gebruiken zonder
     telkens om toestemming te vragen. Het dashboard leest enkel gegevens uit Yuki.
   - **Require this computer**: **aan**. De lokale Yuki-MCP draait enkel op je pc, dus de taak kan
     alleen lopen terwijl die pc aanstaat en wakker is.
3. Klik **Save**.

Kies het uur op een moment waarop je pc normaal aanstaat, anders loopt de update niet.

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
