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

Databasen ligger i `/data/alpacamedic.db` i containeren og i `./data` på værten. Mapper oprettes ved startup, EF Core-migrationer køres automatisk, og katalogdata seedes i en transaktion. En helt ny database får AlpacaMedic-demoens processer, fem Business Assets, 13 System Assets og relationer. Eksisterende analyser tilføjes ikke demo-data. Genstart med samme volume bevarer data. `:Z` sørger for SELinux-label på Linux.

Containeren indeholder både webserver og SQLite; den bruger ingen eksterne services eller frontend-assets. `.containerignore` og `.dockerignore` udelukker lokale databaser, buildoutput og miljømapper.

## Lokal udvikling

På Ubuntu 22.04, 24.04, 25.10 eller 26.04 kan afhængigheder og det lokale ATT&CK-katalog installeres med:

```bash
./install.sh
dotnet run
```

Scriptet bruger `sudo` til apt-installation, installerer .NET 10 SDK (inklusive ASP.NET Core-runtime), hvis det mangler, og henter den fastlagte Enterprise ATT&CK-version **19.2** fra MITRE til `Data/Seed/enterprise-attack.json`. På Ubuntu 22.04 tilføjes `ppa:dotnet/backports`; de øvrige versioner bruger Ubuntus pakkekilder. Se [Microsofts Ubuntu-installationsvejledning](https://learn.microsoft.com/dotnet/core/install/linux-ubuntu-install).

Downloaden valideres som en STIX-bundle med aktive techniques, før den erstatter seed-filen. Scriptet kan køres igen og ændrer ikke SQLite-databasen. Et allerede importeret katalog genimporteres ikke ved startup. Ved Podman-brug skal image rebuildes efter download for at inkludere filen.

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
2. **Asset Model:** Opret systemassets og typede relationer fra Business Assets til systemer og mellem systemer. Systemerne kan foldes ud, og nye systemer kan oprettes direkte fra en relation.
3. **Attack Surface:** Markér eksisterende systemassets, vælg eksponering og begrund første adgang.
4. **Threat Analysis:** Opret Attack Components fra Attack Surface og forbind dem via udgående Asset Model-relationer. Hvert asset + technique vurderes én gang.
5. **Controls:** Tilpas CIS-forslag pr. komponent og gem kommentarer til til- og fravalg.
6. **Risk Analysis:** Se automatisk beregnede scenarier fra initiale komponenter med paths, nåede Business Assets og beregningsgrundlag.
7. **Risk Matrix:** Se scenarier fordelt på Scenario Likelihood × Consequence Index, og klik på en celle for at filtrere.
8. **CIS Priorities:** Se antal komponenter med valgte Controls; vægtet CIS-prioritering er endnu ikke implementeret.
9. **Report:** Læs processer, systemassets, Attack Components og forbindelser og vælg **Print / Save as PDF**.

Dashboardet viser nøgletal, de hyppigste valgte Controls og links til alle trin.

## Asset Model

`BusinessAsset` er fælles på tværs af processer. `BusinessProcessAssets` forbinder et asset med alle dets forretningsprocesser. Asset Model viser ét kort pr. asset, de tilknyttede processer og deres konsekvenser. Navnet kan ændres med **Omdøb**, og ændringen gælder både tags, relationer og rapporter. Konsekvens vises fra processerne og lagres ikke på asset eller relation.

`GET /api/business-assets` returnerer `businessProcessIds` for hvert asset. `POST /api/business-assets` med `name` og `businessProcessId` genbruger et eksisterende asset og tilføjer procestilknytningen; navne sammenlignes uden forskel på store/små bogstaver og indledende/afsluttende mellemrum. `PUT /api/business-assets/{id}` omdøber det fælles asset og bevarer procestilknytningerne. Et andet assets navn afvises med HTTP 400, og beskrivelsen bevares, hvis den udelades fra PUT.

Krydset på et tag bruger `DELETE /api/business-assets/{assetId}/processes/{processId}` og fjerner kun tilknytningen til den proces. Asset og systemrelationer bevares, også hvis ingen processer længere bruger det. **Slet asset** i Asset Model sletter hele asset'et med alle tilknytninger og systemrelationer.

`BusinessAssetSystemRelation` forbinder et Business Asset med et System Asset. Typer: `StoredIn`, `ProcessedBy`, `UsedBy`, `ProtectedBy`, `DependsOn`, `RepresentedBy`, `Other`.

`SystemAssetRelation` forbinder et kildesystem med et målsystem. Typer: `HostedOn`, `LocatedIn`, `ConnectsTo`, `AuthenticatesVia`, `AccessedVia`, `AdministeredFrom`, `Uses`, `DependsOn`, `MemberOf`, `ProtectedBy`, `RoutesThrough`, `Other`. Relationer vises som `SOURCE —RelationType→ TARGET`. Et par assets kan have flere relationstyper; en identisk kombination af kilde, mål og type afvises med HTTP 400. Systemrelationer er retningsbestemte og tillader ikke samme kilde og mål.

Systemtyper: `Data`, `Application`, `Service`, `Host`, `Network`, `Endpoint`, `Identity`, `CloudService`, `Person`, `External`, `Other`. Typer lagres som validerede strenge, som i den eksisterende kodebase. `Zone`-feltet er fjernet. Netværk som DMZ og OfficeNetwork er selvstændige System Assets af typen `Network`; placering modelleres med `LocatedIn`-relationer.

Relationerne har JSON-endpoints med GET/POST og PUT/DELETE på `/{id}`:

```text
/api/business-asset-relations
/api/system-asset-relations
```

GET kan filtreres med henholdsvis `?businessAssetId=12` og `?sourceSystemAssetId=7`. Oprettelse af en Business-relation tager `businessAssetId`, `systemAssetId`, `relationType`, `description`. En systemrelation tager `sourceSystemAssetId`, `targetSystemAssetId`, `relationType`, `description`. POST returnerer HTTP 201, PUT 200 og DELETE 204. Ukendte assets, ugyldige typer og dubletter afvises; sletning af assets fjerner deres relationer.

Siden ligger på `/asset-model`; det gamle `/asset-mapping` omdirigerer hertil. Det tidligere `/api/asset-mappings` er fjernet. Systemanalyse og `/api/system-access` er fjernet. Threat Analysis bruger nu systemrelationernes retning til mulige næste assets. Relationer følges kun, når brugeren opretter en forbindelse; der er ingen automatisk propagation eller grafvisualisering.

## Beregningsmodel og eksplicitte valg

- **Konsekvens:** `Round((2 × (C + I + A) / 3 − 1) × 2, AwayFromZero) / 2`. Beregnes på serveren og vises som readonly. CIA skal være heltal fra 1–3.
- **Likelihood:** Heltal fra 1–5 på en Attack Component; vurderingen er fælles for alle indgående forbindelser.
- **Controls:** Nye komponenter får teknikens undervisningsforslag. Genbrug af komponenten ændrer hverken vurderingen eller dens Control-valg. Fravalg og kommentarer bevares.
- **Risiko og impact:** Manuel Blast Radius er fjernet. Scenarier beregnes fra initiale komponenters Attack Connections og de nåede systemers Business Asset-relationer. Der gemmes ingen beregnede risikoværdier i databasen.
- Sletning af en komponent fjerner dens forbindelser og Control-valg, men bevarer andre komponenter. Sletning af en forbindelse bevarer begge vurderinger. Sletning af en systemrelation fjerner de forbindelser, som bruger den. En anvendt relations endpoints kan ikke ændres, før dens Attack Connections er fjernet.

Versionen har én fælles analyse uden login eller separate elevprojekter.

## Kataloger og kilder

Det lokale Enterprise ATT&CK-katalog importeres fra STIX-filen ved første startup på en tom teknik-tabel. Disse tre teknikker har desuden lokale undervisningsforslag til CIS Controls:

| ATT&CK | Teknik | Vejledende CIS-forslag |
| --- | --- | --- |
| [T1190](https://attack.mitre.org/techniques/T1190/) | Exploit Public-Facing Application | 7, 16, 18 |
| [T1078](https://attack.mitre.org/techniques/T1078/) | Valid Accounts | 5, 6, 8 |
| [T1059](https://attack.mitre.org/techniques/T1059/) | Command and Scripting Interpreter | 2, 4, 10, 13 |

Alle [18 CIS Controls](https://www.cisecurity.org/controls/cis-controls-list) er med. Beskrivelserne er korte danske undervisningsresuméer. **ATT&CK → CIS-relationerne er egne undervisningsforslag, ikke en officiel mapping fra MITRE eller CIS.** Eleven kan tilpasse dem pr. fund.

Kataloget er lokalt og fungerer uden netværk efter containerbuild. ATT&CK-import ligger i `Data/AttackImporter.cs`; CIS-seeding og undervisningsforslag ligger i `Data/SeedData.cs`. Ændringer i STIX-filen opdaterer ikke allerede importerede techniques.

## Struktur

```text
Controllers/       MVC-sider, JSON-endpoints og oversættelse af valideringsfejl
Data/              DbContext, seed og EF Core-migrationer
Models/            Enkle EF-entiteter
Services/          Databaseoperationer, konsekvens, komponenter og Control-optælling
Views/             Razor-layout og separate views til alle sider
wwwroot/js/        Fælles fetch-hjælpere og JavaScript pr. side
wwwroot/css/       Minimal styling med systemfonts og CSS-variabler
tests/             HTTP- og browserintegrationstest
```

Controllerne håndterer HTTP; databaseoperationer og beregninger ligger i services. Der er ingen repository-abstraktion over EF Core. En streng Content Security Policy er allerede aktiv; JavaScript og CSS ligger i separate lokale filer.

## Migrationer

Migrationerne og deres snapshot medfølger i projektet. `AssetModel` opretter de to relationstabeller, kopierer gamle `AssetMappings` med samme ID og `RelationType = DependsOn` og fjerner derefter den gamle tabel. `Server` bliver `Host`, `Cloud` bliver `CloudService`, og `PersonAccount` bliver `Identity`; øvrige asset-data, manuelle adgange og trusler bevares. Nedgradering til den gamle model kan kun bevare ét business-link pr. par og mister relationstyper, beskrivelser og systemrelationer. `SharedBusinessAssets` kopierer de eksisterende procestilknytninger til den nye join-tabel. Ved startup samler `BusinessAssetConsolidation` dubletter efter trimmet navn og Unicode-casing i seed-transaktionen. Alle processer og systemrelationer bevares, identiske relationer samles, og forskellige beskrivelser samles på hver sin linje. Det ældste asset-ID bevares, og en unik navnenøgle beskytter mod nye dubletter. Schemaets navnenøgle er kun null for gamle rækker indtil denne engangsopgradering. Den delte model understøtter ikke nedgradering til én proces pr. asset; gendan en tidligere databasebackup ved tilbagerulning. `RemoveSystemAssetZone` omsætter gamle Zone-værdier til Network-assets og `LocatedIn`-relationer, genbruger eksisterende netværk og relationer og fjerner derefter kolonnen. Tomme værdier og selvrelationer springes over. Startup bruger `Database.MigrateAsync()`, ikke `EnsureCreated()`.

Ved modelændringer kan EF-værktøjet installeres lokalt uden globale ændringer:

```bash
dotnet tool install dotnet-ef --tool-path /tmp/alpacamedic-ef --version 10.0.0
DatabasePath=./data/alpacamedic.db /tmp/alpacamedic-ef/dotnet-ef migrations add BeskrivAendringen --output-dir Data/Migrations
dotnet build
```

Migrationen anvendes ved næste startup. Tag backup af data før ændringer i en eksisterende database.

## Verifikation

HTTP-testen dækker sider, Asset Model-endpoints, samtlige 27 CIA-kombinationer, CRUD, validering, kaskadesletning. Attack Component-tests dækker startpunkter, retning, genbrug, indgående forbindelser, Controls-migration og genstart.

Kør testen mod en **testinstans**. Den opretter egne poster og sletter dem bagefter:

```bash
python3 tests/smoke.py http://127.0.0.1:7070
```

Browserintegrationstesten afprøver Asset Model med oprettelse, redigering og sletning af begge slags relationer, systemoprettelse i relationen, fetch, de øvrige siders indlæsning, rapportens printfunktion og mobilvisning. Den kræver Node.js og Playwright; disse er kun testværktøjer og indgår ikke i applikationen:

```bash
npm install --prefix /tmp/alpacamedic-ui-tools playwright
PLAYWRIGHT_BROWSERS_PATH=/tmp/alpacamedic-browsers \
  /tmp/alpacamedic-ui-tools/node_modules/.bin/playwright install chromium
PLAYWRIGHT_BROWSERS_PATH=/tmp/alpacamedic-browsers \
PLAYWRIGHT_MODULE=/tmp/alpacamedic-ui-tools/node_modules/playwright \
  node tests/browser.cjs http://127.0.0.1:7070
```

Brug en separat testdatabase til browsertesten. Demo-data uden trusler kan være til stede. Den rydder sine egne poster og skriver rapport-PDF og screenshots til `/tmp/alpacamedic-*-check.*`. Persistens er også afprøvet ved at oprette data, genstarte containeren med samme volume og læse data igen.

Migration fra de tidligere modeller, sammenlægning af dubletter med danske navne, omdøbning, bevaring af processer og relationer, SQLite-persistens efter genstart og idempotent demo-seed testes med midlertidige databaser:

```bash
dotnet build
EF_TOOL=/tmp/alpacamedic-ef/dotnet-ef python3 tests/storage.py
```

Denne test kræver EF-værktøjet fra migrationsafsnittet og starter selv lokale testservere. Den berører ikke `./data`.

## Attack Surface

`/attack-surface` viser eksisterende System Assets med manuel markering, eksponering og begrundelse. Ændringer gemmes med fetch; ugemte værdier farves, og fejl kan forsøges gemt igen. Oversigten viser de gemte markeringer fordelt på eksponering. Filtrér på type, direkte LocatedIn-relationer til Network-assets eller kun valgte assets. Placering arves ikke gennem andre relationer.

`GET /api/attack-surface?systemAssetId=7`, `POST /api/attack-surface`, `PUT /api/attack-surface/{id}` og `DELETE /api/attack-surface/{id}` håndterer markeringerne. POST opdaterer en eksisterende post for samme systemasset. En unik nøgle sikrer én post pr. asset; sletning af et systemasset sletter også posten. Fjernes markeringen, bevares eksponering og begrundelse til senere brug. Migrationen tilføjer kun den nye tabel; eksisterende assets markeres ikke automatisk. Valgte Attack Surface-assets bruges som startpunkter i Threat Analysis. Ændring af en markering sletter ikke eksisterende komponenter.

Browsertest af Attack Surface:

```bash
PLAYWRIGHT_MODULE=/path/to/playwright node tests/attack-surface.cjs http://127.0.0.1:7081
```

## Lokalt MITRE Enterprise ATT&CK-katalog

`Data/Seed/enterprise-attack.json` er den lokale STIX-bundle og kopieres med ved build/publish, også til containeren. Efter EF-migrationer importerer startup aktive Enterprise `attack-pattern`-objekter, hvis `AttackTechniques` er tom. Revoked/deprecated techniques udelades. Techniques, tactics, mange-til-mange-relationer, subtechnique/parent-ID og semikolon-separerede platforms gemmes i én SQLite-transaktion. En efterfølgende startup logger antal eksisterende techniques og åbner ikke JSON-filen.

En eksisterende database med det tidligere undervisningskatalog beholder dette katalog og alle fund; den fyldes ikke automatisk op, fordi importen kun kører på en tom tabel. En ny database får hele kataloget. CIS-forslagene for T1190, T1078 og T1059 kobles via Technique ID, ikke importerens interne database-ID'er.

Threat Analysis søger via `GET /api/attack-techniques?search=` på Technique ID, navn og tactic (navn eller short name), case-insensitive, med højst 25 resultater. `GET /api/attack-techniques/{id}` henter en enkelt valgt teknik, også til redigering af eksisterende fund. Eleven skal vælge fra søgeresultaterne; fritekst kan ikke gemmes som teknik. Runtime bruger udelukkende SQLite. Der er ingen live MITRE-integration.

Efter `dotnet build` kontrolleres import, søgning og genstart med:

```bash
python3 tests/attack-catalog.py
```

Autocomplete og redigering kan testes med `PLAYWRIGHT_MODULE=/path/to/playwright node tests/attack-catalog.cjs http://127.0.0.1:7081` mod en testserver med hele kataloget.

## Attack Components og forbindelser

Threat Analysis bruger `AttackComponent` (SystemAssetId, AttackTechniqueId, Likelihood, Comment) med en unik asset/technique-kombination. `AttackConnection` forbinder From/To-komponenter via en eksisterende SystemAssetRelation. En forbindelse skal følge relationens Source → Target, og samme forbindelse gemmes kun én gang. Flere indgående forbindelser er tilladt.

- `GET/POST /api/attack-components`: vis komponenter eller opret/genbrug et Attack Surface-startpunkt.
- `PUT/DELETE /api/attack-components/{id}`: opdatér likelihood/kommentar eller slet komponenten.
- `GET /api/attack-components/{id}/next-assets`: direkte udgående relationer og deres target-assets.
- `POST /api/attack-components/{id}/next`: vælg `systemAssetRelationId`, `attackTechniqueId`, `likelihood`, `comment`. Opret/genbrug komponenten og opret forbindelsen atomisk. En eksisterende vurdering overskrives ikke.
- `GET /api/attack-connections?fromAttackComponentId=1&toAttackComponentId=2`: filtrér forbindelser.
- `POST /api/attack-connections` og `DELETE /api/attack-connections/{id}`: forbind eksisterende komponenter eller fjern forbindelsen.
- `GET /api/component-controls` og `PUT /api/component-controls/{componentId}/{controlId}`: Control-valg for komponenten.

Migrationen `AttackComponents` omsætter gamle SystemThreats til komponenter. Dubletter samles med det ældste ID, højeste likelihood og alle scenariekommentarer; Control-valg og kommentarer samles også. Den fjerner SystemAccess, SystemThreats, ThreatControls og SystemAssets' manuelle Blast Radius-felter. Gamle manuelle adgange omsættes ikke til Attack Connections. Tilbagerulning kræver en tidligere backup. Attack Surface, Asset Model og MITRE-kataloget bevares.

```bash
python3 tests/attack-components.py
PLAYWRIGHT_MODULE=/path/to/playwright node tests/attack-components.cjs http://127.0.0.1:7081
```

## Beregnet Risk Analysis og Risk Matrix

`RiskAnalysisService` læser en konsistent SQLite-snapshot og beregner den aktuelle analyse. `AttackPathService` finder alle afsluttede paths via rettede Attack Connections: til en slutkomponent eller til en cycle, der stoppes før et gentaget komponentbesøg. Delte komponenter kan indgå i flere paths. Et startpunkt uden forbindelser har ét path med sin egen likelihood. Korte prefixes tæller ikke som separate paths. Cycles vises eksplicit i detaljerne. Kun systemer i den oprettede attack-graf påvirker scenariet; en Asset Model-relation uden en Attack Connection udvider ikke grafen.

Et scenarie er en komponent med `IsInitial = true` på et aktuelt valgt Attack Surface-asset. Oprettelse/genbrug via startformularen markerer komponenten som initial; en downstream-komponent på et eksponeret asset er ikke automatisk et scenarie. Migrationen `InitialAttackComponents` markerer eksisterende komponenter på valgte Attack Surface-assets som initiale, fordi den gamle model ikke gemte deres oprindelse. Hver asset/technique-kombination giver højst ét scenarie. Et eksponeret asset uden en startkomponent har endnu intet scenarie.

- `PathLikelihood = MIN` af komponenternes likelihood på et afsluttet path.
- `ScenarioLikelihood = MAX` af alle afsluttede path-likelihoods.
- Et Business Asset tælles én gang, selv ved flere systemlinks eller paths. Dets konsekvens er **summen af konsekvenser fra dets distinct tilknyttede Business Processes**. Processernes CIA-konsekvens beregnes fortsat i Business Analysis.
- `AccumulatedConsequence = SUM` af de distinct nåede assets' konsekvenser.
- `MaximumConsequence = SUM` af alle distinct Business Assets' konsekvenser, også ikke-nåede assets.
- `ImpactRatio = AccumulatedConsequence / MaximumConsequence`.
- Consequence Index: 0–20 % → 1, >20–40 % → 2, >40–60 % → 3, >60–80 % → 4, >80–100 % → 5.
- Assets uden procestilknytning har konsekvens 0. Hvis samlet konsekvens er 0 (herunder ingen assets), sættes ratio til 0 og index til 1 uden division.
- `RiskScore = ConsequenceIndex × ScenarioLikelihood` (1–25).

Niveauerne konfigureres samlet under `RiskLevels` i `appsettings.json`: `LowMax=5`, `HighMin=12`, `CriticalMin=20`. Serverens `RiskLevels` bruges af beregningen og leveres til matrixens farver via `GET /api/risk-levels`. Standardniveauer: Low 1–5, Medium 6–11, High 12–19, Critical 20–25.

`GET /api/risks` returnerer scenarier og alle beregningsdetaljer; `GET /api/risks/{initialComponentId}` returnerer ét scenarie. Siderne er read-only. Klik på en række viser paths og mellemregninger uden genindlæsning. Matrixen placerer hvert scenarie én gang: X = Scenario Likelihood, Y = Consequence Index. Klik på en celle viser dens scenarier; **Opdatér analyse** genberegner fra den aktuelle model uden page reload.

```bash
python3 tests/risk-analysis.py
PLAYWRIGHT_MODULE=/path/to/playwright node tests/risk-analysis.cjs http://127.0.0.1:7081
```

API-testen kontrollerer initiale scenarier og migration, forgreninger, cycles, MIN/MAX, asset- og process-deduplikering, summerede proceskonsekvenser, nulkonsekvens, genstart samt alle 25 index/likelihood-kombinationer og impact-grænser. Browsertesten kontrollerer beregningsdetaljer, alle matrixcellers antal og filtrering, modelændringer uden page reload og mobilvisning.
