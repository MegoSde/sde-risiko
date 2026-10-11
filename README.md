# AlpacaMedic Risk

Første fungerende version af en undervisningsapplikation til struktureret sårbarheds- og risikoanalyse. ASP.NET Core MVC, C#, EF Core, SQLite og almindelig HTML/CSS/JavaScript. Alle ti sider har fælles navigation; redigering gemmes via JSON og `fetch()` uden genindlæsning af siden.

## Start med Podman

Kræver Podman. Kør fra repositoryets rod:

```bash
mkdir -p data
podman build -t alpacamedic-risk .
podman run --rm \
  -p 7070:7070 \
  -v ./data:/data:Z \
  alpacamedic-risk
```

Åbn **http://localhost:7070**. Applikationen lytter på `http://0.0.0.0:7070`.

Databasen ligger i `/data/alpacamedic.db` i containeren og i `./data` på værten. Mapper oprettes ved startup, EF Core-migrationer køres automatisk, og katalogdata seedes i en transaktion. Analysedata starter tomme. Genstart med samme volume bevarer data. `:Z` sørger for SELinux-label på Linux.

Containeren indeholder både webserver og SQLite; den bruger ingen eksterne services eller frontend-assets. `.containerignore` og `.dockerignore` udelukker lokale databaser, buildoutput og miljømapper.

## Lokal udvikling

Kræver .NET 10 SDK:

```bash
dotnet restore
dotnet build
dotnet run
```

Launchprofilen bruger port 7070 og `data/alpacamedic.db`. Uden launchprofil er databasepath som standard `/data/alpacamedic.db`. Det kan overskrives:

```bash
DatabasePath=./data/alpacamedic.db dotnet run --no-launch-profile
```

Port og database kan også konfigureres med miljøvariablerne `ASPNETCORE_URLS` og `DatabasePath`.

## Workflow

1. **Forretningsanalyse:** Opret processer, CIA-vurderinger og underliggende forretningsassets.
2. **Asset Mapping:** Opret systemassets og markér, hvilke systemer hvert forretningsasset afhænger af.
3. **Systemanalyse:** Se direkte konsekvens, vælg retningsbestemte adgange til andre systemer, og vurder/begrund Blast Radius.
4. **Threat Analysis:** Vælg en lokal ATT&CK-teknik, beskriv scenariet og sæt sandsynlighed.
5. **Controls:** Tilpas katalogets CIS-forslag pr. fund og gem kommentarer til til- og fravalg.
6. **Risk Analysis:** Se automatisk beregnede risici og fold grundlaget ud.
7. **Risk Matrix:** Se antal fund i 5×5-matrixen; klik for at filtrere listen.
8. **CIS Priorities:** Se alle 18 Controls sorteret efter vægtet score.
9. **Report:** Læs rapporten og vælg **Print / Save as PDF**.

Dashboardet viser nøgletal, de hyppigste valgte Controls og links til alle trin.

## Beregningsmodel og eksplicitte valg

- **Konsekvens:** `Round((2 × (C + I + A) / 3 − 1) × 2, AwayFromZero) / 2`. Beregnes på serveren og vises som readonly. CIA skal være heltal fra 1–3.
- **Direkte konsekvens:** Summen af konsekvenser for **unikke processer**, som systemets mappede forretningsassets tilhører. To assets fra samme proces dobbelttæller dermed ikke processen.
- **Blast Radius:** Manuelt input fra 1–5 i trin på 0,5. Nye systemer starter ved 1; værdier over 1 kræver en begrundelse. Adgangsrelationerne er fagligt grundlag og ændrer ikke automatisk Blast Radius.
- **Risikoscore:** `BlastRadius × Likelihood`. Hvert ATT&CK-fund er én risiko; risici har ingen separat redigerbar tabel.
- **Niveau:** Low ≤ 5; Medium > 5 og < 12; High ≥ 12 og < 20; Critical ≥ 20. Dette præciserer promptens grænser for halve scorer som 5,5 og 11,5.
- **Matrix:** Halve Blast Radius-værdier rundes op til næste hele række, fx 2,5 → 3. Den præcise score ændres ikke. Cellens farve følger række × kolonne; listen viser hver risikos faktiske niveau.
- **CIS-vægt:** Sum af risikoscorer for fund, hvor den pågældende Control er valgt (`ThreatCisControl.IsRecommended`). Fravalg gemmes som `false`, så kommentarer bevares. Andelen er vægten divideret med summen af alle Control-vægte. Et fund kan bidrage til flere Controls; vægten er ikke en målt risikoreduktion.
- Ved oprettelse af fund kopieres teknikens katalogforslag. Ændres scenarie, system eller sandsynlighed, bevares elevens Control-valg. **Skift af ATT&CK-teknik erstatter fundets valg og kommentarer med den nye tekniks forslag.**
- Sletning af en proces sletter dens forretningsassets og mappings. Sletning af et system sletter dets mappings, adgangsrelationer og fund, inklusive fundets Control-valg. UI beder om bekræftelse før sletning.

Grænserne ligger i `appsettings.json` og kan ændres uden at ændre beregningskoden:

```json
"RiskLevels": {
  "LowMax": 5,
  "HighMin": 12,
  "CriticalMin": 20
}
```

`GET /api/risk-levels` leverer samme grænser til brugerfladen. Bevar stigende grænser. I en container kan de overskrives med fx `-e RiskLevels__CriticalMin=22`.

Versionen har én fælles analyse uden login, separate elevprojekter eller arkitekturversioner. Blast Radius og begrundelse ligger samlet på systemasset og kan senere udvides med arkitekturversionering.

## Kataloger og kilder

Prompten indeholder T1190 som eksempel, men ingen komplet teknikliste. Første version seeder derfor et lille, tydeligt udvalg:

| ATT&CK | Teknik | Vejledende CIS-forslag |
| --- | --- | --- |
| [T1190](https://attack.mitre.org/techniques/T1190/) | Exploit Public-Facing Application | 7, 16, 18 |
| [T1078](https://attack.mitre.org/techniques/T1078/) | Valid Accounts | 5, 6, 8 |
| [T1059](https://attack.mitre.org/techniques/T1059/) | Command and Scripting Interpreter | 2, 4, 10, 13 |

Alle [18 CIS Controls](https://www.cisecurity.org/controls/cis-controls-list) er med. Beskrivelserne er korte danske undervisningsresuméer. **ATT&CK → CIS-relationerne er egne undervisningsforslag, ikke en officiel mapping fra MITRE eller CIS.** Eleven kan tilpasse dem pr. fund.

Kataloget er lokalt og fungerer uden netværk efter containerbuild. Seedkode ligger i `Data/SeedData.cs`. Seed køres kun for tomme kataloger; ændringer i seedfilen opdaterer ikke eksisterende katalogrækker. Ved udvidelser til eksisterende installationer skal katalogændringer implementeres i en migration eller en særskilt idempotent opdatering.

## Struktur

```text
Controllers/       MVC-sider, JSON-endpoints og oversættelse af valideringsfejl
Data/              DbContext, seed og EF Core-migrationer
Models/            Enkle EF-entiteter
Services/          Databaseoperationer, konsekvens, risici og CIS-prioritering
Views/             Razor-layout og separate views til alle sider
wwwroot/js/        Fælles fetch-hjælpere og JavaScript pr. side
wwwroot/css/       Minimal styling med systemfonts og CSS-variabler
tests/             HTTP- og browserintegrationstest
```

Controllerne håndterer HTTP; databaseoperationer og beregninger ligger i services. Der er ingen repository-abstraktion over EF Core. En streng Content Security Policy er allerede aktiv; JavaScript og CSS ligger i separate lokale filer.

## Migrationer

Den første migration og dens snapshot medfølger i projektet. Startup bruger `Database.MigrateAsync()`, ikke `EnsureCreated()`.

Ved modelændringer kan EF-værktøjet installeres lokalt uden globale ændringer:

```bash
dotnet tool install dotnet-ef --tool-path /tmp/alpacamedic-ef --version 10.0.0
DatabasePath=./data/alpacamedic.db /tmp/alpacamedic-ef/dotnet-ef migrations add BeskrivAendringen --output-dir Data/Migrations
dotnet build
```

Migrationen anvendes ved næste startup. Tag backup af data før ændringer i en eksisterende database.

## Verifikation

Build og containerbuild er afprøvet. HTTP-testen dækker alle sider og endpoints, samtlige 27 CIA-kombinationer, CRUD, validering, unikke processer i direkte konsekvens, halve scorer, risikogrænser, Control-valg, vægte og kaskadesletning.

Kør testen mod en **testinstans**. Den opretter egne poster og sletter dem bagefter:

```bash
python3 tests/smoke.py http://127.0.0.1:7070
```

Browserintegrationstesten har desuden afprøvet formularer, fetch, mappings, systemadgang, Controls, risikodetaljer, matrixfiltrering, rapportens printfunktion og mobilvisning. Den kræver Node.js og Playwright; disse er kun testværktøjer og indgår ikke i applikationen:

```bash
npm install --prefix /tmp/alpacamedic-ui-tools playwright
PLAYWRIGHT_BROWSERS_PATH=/tmp/alpacamedic-browsers \
  /tmp/alpacamedic-ui-tools/node_modules/.bin/playwright install chromium
PLAYWRIGHT_BROWSERS_PATH=/tmp/alpacamedic-browsers \
PLAYWRIGHT_MODULE=/tmp/alpacamedic-ui-tools/node_modules/playwright \
  node tests/browser.cjs http://127.0.0.1:7070
```

Brug en tom testdatabase til browsertesten. Den rydder sine egne poster og skriver rapport-PDF og screenshots til `/tmp/alpacamedic-*-check.*`. Persistens er også afprøvet ved at oprette data, genstarte containeren med samme volume og læse data igen.
