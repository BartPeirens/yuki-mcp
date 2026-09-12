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

1. Pak de zip uit op een vaste locatie, bv. `C:\Tools\YukiMcp\YukiMcp.exe`.
2. Open (of maak) het Claude Desktop configuratiebestand `claude_desktop_config.json`:
   - Windows: `%APPDATA%\Claude\claude_desktop_config.json`
   - macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`
3. Voeg een entry toe onder `mcpServers` (pas het pad en de API-key aan):

   ```json
   {
     "mcpServers": {
       "yuki": {
         "command": "C:\\Tools\\YukiMcp\\YukiMcp.exe",
         "args": ["--api-key", "JOUW_YUKI_API_KEY"]
       }
     }
   }
   ```

4. Herstart Claude Desktop. De Yuki-tools verschijnen dan in het "hamer"-icoon (MCP tools) van een
   nieuwe chat.

De API-key wordt **enkel als opstartargument** meegegeven — er zit geen `.env`-bestand of
losstaande configuratie bij, en de key komt nooit in een tool-aanroep terecht.

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
# -> dist/win-x64/YukiMcp.exe

# Publiceren + inpakken in een deelbare zip (met deze README en de LICENSE erbij):
./scripts/package-zip.ps1 -Version 0.1.0
# -> dist/YukiMcp-0.1.0-win-x64.zip
```

## Status

Dit project is een werkende eerste versie: de server, alle 13 Yuki-webservices en 96 tools zijn
opgezet en end-to-end getest (opstarten, tools/resources oplijsten, een tool aanroepen) — maar nog
**niet functioneel getest tegen een echte Yuki-administratie**. Zie `plan.md` voor de volledige
architectuur, de roadmap en gekende aandachtspunten (met name rond de tools die data wijzigen).

## Licentie

Zie [LICENSE](LICENSE).
