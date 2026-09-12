# Plan: Yuki MCP-server

## Doel

Een MCP-server (stdio) in C#/.NET die de Yuki accounting-webservices
(https://documenter.getpostman.com/view/12207912/UVCBB51L) ontsluit als MCP-tools, zodat een
assistent (bv. Claude Desktop) rechtstreeks met een Yuki-domein/administratie kan werken: opzoeken,
rapporteren, en (met de nodige voorzichtigheid) data aanmaken/wijzigen.

De server wordt als één enkele, self-contained .exe gedistribueerd (geen .NET-installatie nodig bij
de ontvanger), met een README als handleiding, in een zip.

## Belangrijke architectuurbeslissing: Yuki is een SOAP-API, geen REST-API

De Postman-documentatie ziet er als een lijst "endpoints" uit, maar elke aanroep is in werkelijkheid
een SOAP 1.1-operatie op één van een dertiental `.asmx`-webservices onder
`https://api.yukiworks.be/ws/` (namespace `http://www.theyukicompany.com/`). Er is dus geen vaste
lijst van REST-paden; de echte contractdefinitie staat in elke service's WSDL
(`<Service>.asmx?WSDL`).

**We genereren de clients daarom rechtstreeks uit de live WSDL met `dotnet-svcutil`** (niet met
handgeschreven XML-envelopes), bv.:

```
dotnet-svcutil "https://api.yukiworks.be/ws/Accounting.asmx?WSDL" \
  --outputDir Yuki/Accounting --outputFile AccountingClient.cs \
  --namespace "*,YukiMcp.Yuki.Accounting" --projectFile YukiMcp.csproj
```

Dit is voor alle 13 services gedaan; zie `src/YukiMcp/Yuki/<Service>/<Service>Client.cs`
(gegenereerd, maar geen `.WithToolsFromAssembly()`-magie erin — gewoon meebewerkbare code) en het
bijhorende `dotnet-svcutil.params.json` per service (nodig om later met `dotnet-svcutil -u` te
kunnen verversen, bv. als Yuki de WSDL wijzigt).

**Belangrijke ontdekking**: de *live* WSDL bevat behoorlijk wat operaties die niet in de publieke
Postman-documentatie staan (zie "Onder de motorkap" verderop) én mist er één die wél gedocumenteerd
staat (`Projects.asmx / UpdateProject`, zie Open vragen). De WSDL is dus leidend, niet de
Postman-collectie — die gebruiken we enkel nog voor de beschrijvingsteksten.

## Architectuur

- **Taal/runtime**: C#, .NET 10 (huidige LTS-lijn op het moment van schrijven; SDK's 6/8/9/10
  stonden allemaal al lokaal geïnstalleerd, gekozen voor de nieuwste).
- **MCP SDK**: officiële `ModelContextProtocol`-package (2.2.0) + `Microsoft.Extensions.Hosting`
  (generic host), stdio-transport (`WithStdioServerTransport()`). Alle logging gaat naar stderr
  (`LogToStandardErrorThreshold = Trace`) zodat stdout uitsluitend JSON-RPC bevat.
- **Yuki-clients**: 13 gegenereerde WCF-clients (zie hierboven), telkens SOAP 1.1
  (`<Service>SoapClient.EndpointConfiguration.<Service>Soap`), transient geregistreerd in DI
  (`Yuki/ServiceCollectionExtensions.cs`) — transient omdat een WCF-kanaal na een fout "Faulted"
  kan raken en dan niet herbruikt mag worden.
- **Sessie-/authenticatiebeheer** (`Yuki/YukiSessionManager.cs`): roept `Authenticate(accessKey)`
  aan (bestaat identiek op elke service; hier via de Sales-client, net als in Postman's GENERAL
  voorbeelden), cachet de sessionID (geldig ~24u, we verversen na 23u), en herprobeert een aanroep
  precies één keer met een verse sessie als de call een `FaultException` geeft. **Tools krijgen
  nooit een sessionID-parameter** — dat wordt altijd injecteerd.
- **Resultaatformattering** (`Yuki/YukiResult.cs`): Yuki's WSDL geeft voor de meeste operaties een
  ongetypeerd `XmlNode` terug (netjes geformatteerd als XML-tekst); getypeerde DTO's/arrays worden
  generiek naar geïndenteerde JSON geserialiseerd (`IncludeFields = true`, want de gegenereerde
  DTO's gebruiken public fields, geen properties); scalars (string/decimal/bool/int/DateTime) gaan
  er ongewijzigd doorheen.
- **Enums & binaire data**: SOAP-enums (sorteervolgordes, zoekopties, ...) worden als `string`
  tool-parameter blootgesteld (met de geldige waarden in de `[Description]`) en via
  `YukiEnumHelper.Parse<T>` naar het echte enum-type omgezet; `byte[]`-parameters (documentupload)
  zijn een base64-`string` tool-parameter; XML-inputparameters (bv. `ProcessJournal`,
  `UpdateContact`) zijn een raw-XML-`string`, geparsed via `YukiXml.Parse`.
- **Config** (`Configuration/YukiServerOptions.cs`): `--api-key <key>` (verplicht, ook als
  `YUKI_API_KEY` env var) en `--base-url <url>` (optioneel, default
  `https://api.yukiworks.be/ws/`).

## "Algemene" operaties: één keer, niet 13 keer

Elke van de 13 services blijkt **dezelfde 11 sessie-/domeinoperaties** te exposeren
(`Authenticate`, `AuthenticateClient`, `AuthenticateByUserName`, `Domains`, `Companies`,
`AdministrationID`, `Administrations`, `AdministrationsWithInternalCustomerCode`,
`GetCurrentDomain`, `Language`, `SupportedLanguages`) — een quirk van hoe deze .asmx-services zijn
opgebouwd. Die exposen we niet 13×:

- De drie `Authenticate*`-varianten zijn **puur intern** (sessiebeheer) — geen tool, want een
  assistent mag niet met losse/andere credentials inloggen dan de geconfigureerde API-key.
- De overige 8 zijn nuttige, read-only opzoekingen en staan **één keer** in
  `Tools/GeneralTools.cs` als `yuki_general_*`, gebonden aan de Sales-client (geen andere reden dan
  dat Postman's eigen GENERAL-voorbeelden toevallig Sales.asmx gebruiken).

## Tool-conventie

- Naam: `yuki_<service>_<operatie>` in snake_case (bv. `yuki_accounting_gl_account_balance`,
  `yuki_archive_search_documents`), of `yuki_general_*` voor de gedeelde operaties.
- MCP-annotaties: read-only operaties krijgen `ReadOnly/Idempotent = true, Destructive = false`;
  operaties die beginnen met `Update/Process/Import/Upload/Create/Add/Delete/Insert/Enable/Disable`
  (plus `LyantheRecognitionEngine`) worden als **write** behandeld: `ReadOnly = false,
  Destructive = true, Idempotent = false`. Alle tools krijgen `OpenWorld = true` (roepen een
  externe, niet-gesandboxte API aan).
- Beschrijving: hergebruikt de tekst uit de Postman-documentatie waar die bestaat; operaties die
  niet in Postman staan (zie hieronder) krijgen een gegenereerde beschrijving die dat vermeldt.

## Onder de motorkap: verschillen met de publieke documentatie

Ontdekt door de live WSDL te generen in plaats van de Postman-collectie te volgen:

- **Extra, ongedocumenteerde operaties** die wél gewoon werken (voor zover getest = compileert;
  nog niet functioneel getest met een echte API-key): `Accounting` heeft o.a.
  `OutstandingCreditorItemsByDateOutstanding`, `OutstandingDebtorItemsByDateOutstanding`,
  `OutstandingDebtorItemsWithLanguage`; `Sales` heeft `ProcessRecognizedSalesInvoices`;
  `AccountingInfo` heeft `GetFinancialYearModifiedDate`, `GetContactDefaultValues`; `Archive` heeft
  `DocumentDownloadUrl`, `UploadDocument`; `Domains` heeft een hele reeks accountant-/reseller-
  niveau operaties (`GetDomainName`, `CreateDomain`, `CreateTrialDomain`, `GetDomainUsers`,
  `AddDomainUser`, `LyantheRecognitionEngine`) die vermoedelijk een ander soort API-key
  (reseller/accountant) vereisen dan een gewone Administratie-key.
- **Projects.asmx exposeert in de live WSDL geen enkele projectspecifieke operatie** — geen
  `UpdateProject`, hoewel de Postman-documentatie die wél beschrijft. Enkel de 11 algemene
  operaties zitten er nog op. Zie "Open vragen" hieronder.
- Eén parameter wordt vandaag bewust **niet** blootgesteld:
  `AccountingInfo.GetTransactions(..., TransactionSearch[] searchValues)` — een array van een
  filterobject. De tool geeft er `null` voor door (= geen extra filter); een latere versie kan dit
  alsnog flatten naar tool-parameters zodra er een concrete usecase voor is.

## Distributie & installatie

- `dotnet publish` met `SelfContained=true` + `PublishSingleFile=true` (zie
  `src/YukiMcp/YukiMcp.csproj`) → één .exe per platform, geen losse .dll's of runtime-installatie
  nodig. `RuntimeIdentifiers` staat open voor win-x64/osx-x64/osx-arm64/linux-x64; enkel win-x64 is
  effectief getest (dat is waar Claude Desktop hier draait).
- `scripts/publish.ps1` (publiceert naar `dist/<rid>/`) en `scripts/package-zip.ps1` (publiceert +
  zipt de exe samen met README.md en LICENSE naar `dist/YukiMcp-<versie>-<rid>.zip`) — dat is de
  zip die je aan iemand anders geeft.
- Geen aparte installer (MSI/Inno Setup) gebouwd: voor een MCP-stdio-server volstaat "unzip, wijs
  Claude Desktop naar de .exe" ruimschoots, dus dat is bewust niet gebouwd. Zie Fase 6 als dat ooit
  toch gewenst is (bv. om Windows SmartScreen-meldingen op een ongesigneerde exe te vermijden).
- Claude Desktop-config: zie README.md voor het exacte `claude_desktop_config.json`-fragment
  (`command` → pad naar de exe, `args` → `["--api-key", "<key>"]`).

## Fasering

### Fase 0 — Repo & code-skeleton — ✅ afgerond
Git-repo, `.gitignore`, `src/YukiMcp`-project (net10.0), ModelContextProtocol + Hosting-wiring,
stdio-transport, `yuki_server_status`-tool en `help://yuki-mcp`-resource, lege scaffolding voor
Prompts/Apps (zie Fase 4/5), publish/zip-scripts. Single-file publish getest.

### Fase 1 — SOAP-clients & sessiebeheer — ✅ afgerond
Alle 13 `.asmx`-services gegenereerd met `dotnet-svcutil` (zie hierboven), NuGet-pakketten
opgeschoond (enkel `System.ServiceModel.Http`/`.Security` behouden — Duplex/NetTcp/Federation
weggelaten, worden nergens gebruikt), transitieve kwetsbaarheid in
`System.Security.Cryptography.Pkcs` 6.0.1 opgelost door een directe `10.0.12`-referentie.
`YukiSessionManager` werkend (authenticatie, caching, retry-on-fault). `YukiResult`/`YukiXml`/
`YukiEnumHelper` als generieke helpers voor resultaatformattering en parameterconversie.

### Fase 2 — Tools per endpoint — ✅ afgerond (eerste versie)
96 tools gegenereerd en getest (`tools/list` + een paar `tools/call` end-to-end tegen de
gepubliceerde exe, met een nep-API-key — dus transport/schema geverifieerd, **niet** de echte Yuki-
antwoorden): 8 `yuki_general_*` + 87 servicespecifieke tools + `yuki_server_status`. Alle
servicebestanden staan in `src/YukiMcp/Tools/*.cs`. Zie "Onder de motorkap" voor bekende hiaten.

**Nog te doen, met een echte API-key:**
- Functioneel testen per service (vooral de write-tools: `ProcessJournal`, `ProcessSalesInvoices`,
  `ProcessRecognizedSalesInvoices`, `UpdateContact`, `ImportStatement`,
  `ImportSingleStatementLine(ProjectLine)`, `UploadDocument(WithData/WithAttachment)`,
  `UpdateDomainFunctions`) — dit zijn de risicovolste, want ze wijzigen echte boekhouddata.
  Bij voorkeur eerst tegen een testadministratie.
  - `ProcessJournal`/`ProcessSalesInvoices`/`ProcessRecognizedSalesInvoices`/`UpdateContact` nemen
    ruwe XML als tool-parameter (moet voldoen aan Yuki's XSD — zie
    `yuki_sales_sales_invoice_schema_path` voor het schema-pad); dat is voor een taalmodel een
    minder natuurlijke interface dan losse velden. Overwegen om voor de meest gebruikte gevallen
    (bv. één sales-invoice, één journaalregel) een tweede, "vriendelijke" tool te schrijven die de
    XML zelf opbouwt uit losse parameters.
  - Uitzoeken of de foutrespons van Yuki bij een verlopen/ongeldige sessie werkelijk een
    `FaultException` is (waar `YukiSessionManager.ExecuteAsync` op vangt) — dit is nu een aanname,
    niet geverifieerd tegen een echte 24u-verlopen sessie.
- `AccountingInfo.GetTransactions`: beslissen of/hoe `searchValues` (array van `TransactionSearch`)
  alsnog bruikbaar gemaakt wordt.
- De ongedocumenteerde `Domains`-operaties (`CreateDomain`, `AddDomainUser`, ...) uitproberen: het
  is niet duidelidk of de standaard Administratie-API-key daar toegang toe geeft, dan wel of dit
  een reseller/accountant-key vereist. Mogelijk moeten deze tools default verborgen/uitgeschakeld
  worden voor de meeste gebruikers.
- Overwegen: paginatie/grote resultaten (bv. `Archive.Documents`, `SearchDocuments`) kunnen zeer
  lange tool-antwoorden geven; evalueren of er een max-resultaatgrootte of samenvatting nodig is.

### Fase 3 — Help-resource, inhoud verder verfijnen
`help://yuki-mcp` (`Resources/HelpResource.cs`) geeft nu een correcte samenvatting van hoe de
server werkt en welke toolgroepen bestaan. Eventueel uit te breiden met concrete voorbeeldaanroepen
zodra er met een echte administratie getest is.

### Fase 4 — Prompts (later, nog geen inhoud)
Scaffolding staat klaar (`src/YukiMcp/Prompts/README.md`); `Program.cs` heeft een uitgecommentte
`.WithPromptsFromAssembly()`-regel klaarstaan. Nog geen concrete prompts (bv. "maak een
maandafsluitingsrapport", "zoek alle openstaande facturen ouder dan 30 dagen") — pas zinvol na
functionele tests van de onderliggende tools.

### Fase 5 — MCP "apps" (later, nog geen inhoud)
Scaffolding staat klaar (`src/YukiMcp/Apps/README.md`). Dit is nog geen stabiel, geversioneerd
onderdeel van de `ModelContextProtocol` C#-SDK op dit moment — pas oppikken zodra de SDK dit
ondersteunt.

### Fase 6 — Distributie verder verfijnen (optioneel, later)
- Eventueel een echte installer (Inno Setup/MSI) als de single-file exe in de praktijk toch
  hinder ondervindt (bv. Windows SmartScreen-waarschuwing op een ongesigneerde exe).
- Code signing overwegen als dit vaker dan incidenteel gedeeld wordt.
- Automatische versienummering (nu hard `1.0.0.0` via de standaard .NET-default) en een
  `--version`-vlag.
- Eventueel trimming/AOT overwegen om de ~41MB exe kleiner te maken — nog niet gedaan omdat WCF's
  reflectie-gebaseerde serialisatie niet zomaar trimbaar is; vereist onderzoek.

## Open vragen / te bevestigen met een echte Yuki-omgeving

1. **Projects.asmx**: de live WSDL exposeert geen projectspecifieke operatie, terwijl Postman
   `UpdateProject` documenteert. Mogelijke verklaringen: vereist een hoger bundelniveau
   ("Medium"), is een oudere/andere WSDL-versie, of is intussen verwijderd. Te verifiëren met een
   domein dat de Projects-functionaliteit heeft.
2. Werkt `--base-url` echt voor een test-/sandboxomgeving van Yuki, of is er maar één (productie)
   endpoint? Nog niet bevestigd — de optie bestaat wel al.
3. Sessie-verloopgedrag (zie Fase 2) — welke fout geeft Yuki precies terug, en is één retry
   voldoende?
4. Rechten van een gewone Administratie-API-key op de ongedocumenteerde `Domains`-operaties.

## Referenties

- Publieke API-documentatie (Postman): https://documenter.getpostman.com/view/12207912/UVCBB51L
- Live WSDL's: `https://api.yukiworks.be/ws/<Service>.asmx?WSDL` voor elk van: Accounting,
  AccountingInfo, Archive, Backoffice, ChangeDigest, Contact, Domains, FiscalTable, Integration,
  Pettycash, Projects, Sales, Vat.
- MCP C# SDK: https://github.com/modelcontextprotocol/csharp-sdk (NuGet: `ModelContextProtocol`)
