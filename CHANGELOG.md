# Changelog

Wat er in elke gepakte release zit (`dist/YukiMcp-vN-<runtime>.zip`, gebouwd via
`scripts/package-zip.ps1` — zie README.md "Zelf bouwen"). Versienummering is simpelweg v1, v2, ...
(telkens +1 per nieuwe build), geen SemVer — dit volgt de distributie, niet de functionaliteit van
de Yuki-tools zelf.

`scripts/package-zip.ps1` weigert te bouwen als de nieuwste sectie hieronder niet overeenkomt met
de volgende versie: voeg dus eerst een `## vN - <datum>`-sectie toe voor je een nieuwe zip maakt.

## v1 - 2026-09-12
- Eerste gepakte release: `YukiMcp.exe` (win-x64), README.md, LICENSE, `.env`-template.
- API-key wordt gelezen uit een `.env`-bestand naast de exe (`APIKEY=...`) in plaats van een
  verplicht opstartargument in `claude_desktop_config.json` — zie README.md "Installatie".
- 13 Yuki-webservices, 96 tools; nog niet functioneel getest tegen een echte Yuki-administratie
  (zie README.md "Status").
