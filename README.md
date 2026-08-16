# Domestic Financial Planning

Domestic Financial Planning is een ASP.NET Razor Pages applicatie om terugkerende huishoudelijke kosten te plannen, betalingen op te volgen en verschillen tussen verwachte en effectieve uitgaven te verklaren.

De app is vooral bedoeld voor gezinnen of kleine huishoudens die maandelijks een vaste provisie apart zetten voor jaarlijkse, kwartaal- en maandelijkse kosten zoals energie, verzekeringen, belastingen, abonnementen, water, telecom en kredietkaartuitgaven.

## Wat kan de app?

### Dashboard

Het dashboard geeft een snel beeld van de actuele financiële planning:

- verwachte jaarlijkse kost;
- vaste of berekende maandelijkse provisie;
- reeds geprovisioneerd bedrag;
- reeds betaalde kosten;
- recente maandkaarten voor huidige maand, vorige maand en de maand daarvoor;
- vermoedelijk betaalde maar nog niet geïmporteerde automatische betalingen;
- vermoedelijke provisiebetaling;
- laatste gekende rekeningbalans.

Omdat bankafschriften vaak pas later beschikbaar zijn, toont het dashboard bewust meerdere recente maanden. Zo blijven vertragingen in CODA-bestanden, kredietkaartafrekeningen en energiefacturen zichtbaar.

### Recurrente betalingen

Recurrente betalingen vormen de basis van de planning. Per betaling kan je onder meer instellen:

- omschrijving;
- bedragstype:
  - vast bedrag;
  - variabel verwacht bedrag;
  - maandprofiel voor seizoensgebonden kosten;
- bedrag per betaling of maandbedragen;
- verwachte betalingsdag;
- periodiciteit;
- aangepaste betalingsmaanden;
- betalingsvertraging;
- betaalmethode;
- geldigheidsperiode;
- herkenningswoorden voor reconciliatie.

De app houdt rekening met `Geldig van` en `Geldig tot`. Een abonnement dat stopt, blijft dus historisch correct zichtbaar in de periode waarin het geldig was, maar telt niet meer mee in latere planningen.

Voor betalingen die opnieuw starten, kan een bestaande template gekopieerd worden naar een nieuwe geldigheidsperiode. Overlappende templates met dezelfde omschrijving en betaalmethode worden geblokkeerd om dubbele planning te vermijden.

### Betalingsoverzicht

Het betalingsoverzicht toont alle templates met compacte Nederlandse labels, zoals:

- `11de vd maand`;
- `17de in feb, mei, jul, nov`;
- `1ste apr`;
- `Domiciliëring`, `Kredietkaart`, `Overschrijving`.

De volgorde kan via een aparte volgorde-modus worden aangepast met slepen of met omhoog/omlaag-knoppen. Die volgorde wordt gebruikt voor een consistente presentatie en vorige/volgende navigatie bij het bewerken.

### Imports

De app ondersteunt import van:

- CODA-bankafschriften;
- Europabank Visa PDF-afschriften.

Imports zijn gekoppeld aan geregistreerde bankrekeningen of kredietkaarten. Dubbele imports worden gedetecteerd op basis van bronreferenties.

Lokale importbestanden zoals CODA, PDF, CSV en Excel-bestanden horen niet in Git en worden door `.gitignore` uitgesloten.

### Reconciliatie

In de reconciliatie kan je transacties mappen naar:

- recurrente betaling;
- kredietkaartafrekening;
- geplande of extra provisie;
- interne overschrijving;
- extra kost.

Kredietkaartafrekeningen tellen niet mee als kost, omdat de onderliggende kaarttransacties al de echte kosten bevatten. Zo wordt dubbele telling vermeden.

De zoekfunctie gebruikt zowel transactiebeschrijving als de omschrijving en keywords van gemapte recurrente betalingen.

### Manueel afpunten

Voor gebruikers die geen bankafschriften of kredietkaart-PDF's willen importeren, is er een manuele afpuntpagina.

Daar kunnen verwachte betalingen per maand handmatig als betaald worden aangeduid, met:

- betaaldatum;
- bedrag;
- betaalwijze;
- maandelijkse provisie;
- kredietkaartafrekening;
- huidig of maandultimo banksaldo.

### Rapporten

De app bevat meerdere rapporten om planning en realiteit te vergelijken:

- Maandplan versus betaald;
- Maandelijkse kostenafwijking;
- Provisieprognose;
- Overzicht geplande posten;
- Provisie recurrente betalingen.

Rapporten houden rekening met:

- tenant;
- geldigheidsperiode van recurrente betalingen;
- betalingsvertraging;
- maandprofielen;
- gemapte rapporteringsmaand;
- uitsluiting van kredietkaartafrekeningen als kost;
- vermoedelijk betaalde maar nog niet geregistreerde automatische betalingen.

Het rapport Maandelijkse kostenafwijking is bedoeld om snel te verklaren waarom een bepaalde maand afwijkt van de verwachting.

### Multi-tenant en beheer

De applicatie ondersteunt meerdere tenants. Dat is vooral nuttig wanneer dezelfde installatie meerdere huishoudens of administratieve omgevingen bevat.

Beschikbare beheermogelijkheden:

- tenants aanmaken en beheren;
- gebruikers toevoegen aan tenants;
- tenantrollen beheren;
- globale admins beheren;
- gebruikers deactiveren/reactiveren;
- wachtwoorden resetten;
- tenant-specifieke weergavenaam en avatar instellen;
- tenant-specifieke provisie-instellingen beheren.

### Single user / desktop mode

Naast de klassieke multi-tenant servermodus heeft de app een single-user desktopmodus. Die modus gebruikt dezelfde applicatie, maar draait lokaal met SQLite en verbergt tenant- en globaal-beheerkeuzes die voor één lokale gebruiker niet nodig zijn.

Desktopmodus wordt geactiveerd met:

```json
"Application": {
  "Mode": "SingleUserDesktop"
}
```

De standaard blijft:

```json
"Application": {
  "Mode": "MultiTenant"
}
```

In `SingleUserDesktop` blijft het bestaande datamodel intern tenant-aware, maar de app maakt automatisch één lokaal huishouden aan en presenteert zich als lokale single-user toepassing.

Bij een lege database stuurt desktopmodus de gebruiker automatisch naar de eerste setup:

```text
/Account/DesktopSetup
```

Die setup maakt een lokale beheerder en een lokaal huishouden aan, kent de nodige rechten toe en meldt de gebruiker meteen aan. Daarna is gewone zelfregistratie in desktopmodus uitgeschakeld.

Voor lokaal testen is er een aparte launch profile:

```powershell
dotnet run --project FinancialPlanningApp.Web --launch-profile desktop
```

Die gebruikt `ASPNETCORE_ENVIRONMENT=Desktop` en laadt `appsettings.Desktop.json`, waarin `Application:Mode` automatisch op `SingleUserDesktop` staat.

Voor een Windows desktop-publish is er een apart publish-profiel:

```powershell
dotnet publish .\FinancialPlanningApp.Web\FinancialPlanningApp.Web.csproj /p:PublishProfile=Desktop
```

Dat profiel publiceert self-contained naar:

```text
<deploy-root>\Desktop
```

Standaard gebruiken de scripts en publish-profielen een sibling-map `DEPLOY\FinancialPlanning` naast de map waarin deze repository staat. Je kan dit overschrijven met de environment variable `FINANCIAL_PLANNING_DEPLOY_ROOT` of met de MSBuild-property `FinancialPlanningDeployRoot`.

De desktop-publish schrijft een `desktop.mode` markerbestand mee. Daardoor start de gepubliceerde `.exe` automatisch in desktopmodus met SQLite, zonder dat de gebruiker zelf environment variables moet instellen. Het bestaande `Standard.pubxml` profiel blijft bedoeld voor serverhosting.

De desktopversie gebruikt een single-instance guard. Als de app al draait en de gebruiker de `.exe` opnieuw start, wordt de bestaande lokale app opnieuw geopend in de browser en sluit het tweede proces meteen af.

Via `Data en backup` in het gebruikersmenu kan de gebruiker zien waar de lokale SQLite-database staat en een backup maken.

Desktopmodus controleert optioneel op een nieuwe versie via `updates/latest.json`. Wanneer een nieuwere versie beschikbaar is, toont de app een updatebanner en, vanaf versies die deze functie bevatten, één keer per sessie een update-modal.

### Windows Installer

Voor Windows kan van de desktop-publish een Inno Setup installer worden gemaakt. Installeer eerst Inno Setup 7 of 6. Via `winget` kan dat bijvoorbeeld met:

```powershell
winget install JRSoftware.InnoSetup
```

Daarna kan de installer worden gebouwd met:

```powershell
.\tools\package-desktop-inno.ps1
```

Het script voert eerst de desktop-publish uit, controleert dat er geen lokale secrets in de publish-output zitten, en compileert daarna:

```text
<deploy-root>\Installer\DomesticFinancialPlanning-Setup-1.0.4.0.exe
```

De installer installeert per gebruiker onder `%LOCALAPPDATA%\Programs\Domestic Financial Planning`, maakt een startmenu-snelkoppeling, en laat de SQLite-data bewust staan onder `%LOCALAPPDATA%\DomesticFinancialPlanning`.

### Mailinstellingen

Mailinstellingen zijn globaal opgeslagen in de database. Ze kunnen gebruikt worden voor:

- wachtwoord vergeten;
- testmails;
- uitnodigingen of tijdelijke wachtwoorden zodra die workflow wordt gebruikt.

Ondersteunde opties zijn onder meer provider-API's en Custom SMTP. Gmail met app password kan via Custom SMTP worden gebruikt.

## Technische stack

- ASP.NET Razor Pages;
- .NET 10;
- Dapper;
- MySQL/MariaDB;
- SQLite voor desktopmodus;
- Razor Pages UI met Bootstrap;
- SQL-migraties in `FinancialPlanningApp.Web/Database/Migrations`;
- cookie-authenticatie;
- Serilog console logging;
- Dockerfile en Docker Compose.

## Repository-indeling

```text
.
├── FinancialPlanningApp.Web/        # Razor Pages webapp
├── installer/                       # Inno Setup script voor Windows desktop installer
├── tools/                           # Packaging, checksum en website publish scripts
├── website/                         # Statische download/update website
├── LICENSE                          # GNU AGPL v3.0-or-later
├── Dockerfile
├── docker-compose.yml
├── FinancialPlanningApp.slnx        # Solution op repo-root
├── .env.example
├── .gitignore
└── README.md
```

De repository-root bevat bewust ook Docker-, packaging-, website-, environment- en solutionbestanden. Daarom staat Git best op de rootmap, niet enkel in `FinancialPlanningApp.Web`.

## Licentie

Domestic Financial Planning is beschikbaar onder de GNU Affero General Public License v3.0 of later (`AGPL-3.0-or-later`).

Zie [LICENSE](LICENSE) voor de volledige licentietekst.

Copyright (C) 2026 Paul Peeters / PWARE.

Broncode: [github.com/paulpeeters/domestic-financial-planning](https://github.com/paulpeeters/domestic-financial-planning)

## Vereisten

Voor lokale ontwikkeling:

- .NET 10 SDK;
- MySQL of MariaDB voor server/multi-tenant ontwikkeling;
- SQLite wordt meegeleverd via NuGet voor desktopmodus;
- optioneel Docker Desktop;
- een IDE zoals Visual Studio, Rider of VS Code.

## Configuratie per modus

### Desktopmodus

Voor normaal desktopgebruik is geen `secrets.json`, MySQL-server of mailconfiguratie nodig. De Windows installer en het desktop launch profile gebruiken `appsettings.Desktop.json`:

```json
"Application": {
  "Mode": "SingleUserDesktop"
},
"Database": {
  "Provider": "Sqlite",
  "ConnectionString": "Data Source=%LOCALAPPDATA%\\DomesticFinancialPlanning\\app.db"
}
```

De lokale SQLite-database wordt bij de gebruiker onder `%LOCALAPPDATA%\DomesticFinancialPlanning` bewaard. Bij een lege database opent de app automatisch de desktop setup.

De SQLite-migratie is een geconsolideerd schema voor nieuwe lokale desktopdatabases. Het is dus geen conversiepad van bestaande MySQL-data naar SQLite.

### Server / multi-tenant modus

Voor serverhosting of gewone multi-tenant development gebruikt de app standaard MySQL/MariaDB. `appsettings.json` en `appsettings.Development.json` bevatten placeholders, bijvoorbeeld:

```json
"Database": {
  "Provider": "MySql",
  "ConnectionString": "Server=@{DB_HOST};Port=@{DB_PORT};Database=@{DB_NAME};User ID=@{DB_USER};Password=@{DB_PASSWORD};Allow User Variables=true;"
}
```

De echte waarden komen uit:

1. `FinancialPlanningApp.Web/secrets.json`; of
2. environment variables met dezelfde namen.

Maak lokaal een secrets-bestand:

```powershell
Copy-Item FinancialPlanningApp.Web\secrets.template.json FinancialPlanningApp.Web\secrets.json
```

Vul daarna de waarden in:

```json
{
  "DB_HOST": "",
  "DB_PORT": "3306",
  "DB_NAME": "",
  "DB_USER": "",
  "DB_PASSWORD": ""
}
```

`secrets.json` staat in `.gitignore` en mag niet gecommit worden.

Het serverpublish-profiel `Standard.pubxml` neemt `secrets.json` expliciet mee naar de private serverpublish-output. De publieke desktop installer en publieke server ZIP sluiten echte secrets bewust uit.

## Lokaal starten met bestaande serverdatabase

```powershell
dotnet run --project FinancialPlanningApp.Web
```

Bij startup voert de app automatisch ontbrekende SQL-migraties uit vanuit:

```text
FinancialPlanningApp.Web/Database/Migrations
```

## Starten met Docker Compose

Maak eerst een `.env` bestand op basis van `.env.example`:

```powershell
Copy-Item .env.example .env
```

Vul daarna de databasewaarden in:

```env
DB_NAME=
DB_USER=
DB_PASSWORD=
DB_ROOT_PASSWORD=
```

Start vervolgens de volledige stack:

```powershell
docker compose up --build
```

Standaard is de app dan beschikbaar op:

```text
http://localhost:8080
```

## Eerste stappen in de app

In server/multi-tenant modus:

1. Maak een eerste gebruiker aan.
2. Maak of selecteer een tenant.
3. Stel eventueel de maandelijkse provisiedag en het vaste provisiebedrag in.
4. Voeg recurrente betalingen toe.
5. Importeer CODA- of kredietkaartafschriften, of gebruik manueel afpunten.
6. Reconcileer transacties naar recurrente betalingen, provisies of extra kosten.
7. Gebruik dashboard en rapporten om afwijkingen te verklaren.

In desktopmodus start een lege database automatisch met `/Account/DesktopSetup`. Die eerste setup maakt de lokale beheerder en het lokale huishouden aan en meldt de gebruiker meteen aan.

## Voorbeeld van recurrente betalingen

### Maandelijks abonnement

- Bedragstype: vast bedrag;
- Periodiciteit: maandelijks;
- Verwachte betalingsdag: bijvoorbeeld 10;
- Betaalmethode: kredietkaart of domiciliëring.

### Jaarlijkse verzekering

- Bedragstype: vast bedrag;
- Periodiciteit: jaarlijks;
- Verwachte maand en dag instellen;
- Bedrag per betaling is het jaarbedrag.

### Water met voorschotten

Voor een jaarbudget met betalingen in februari, mei, juli en november:

- Periodiciteit: jaarbudget met aangepaste maanden;
- Betalingsmaanden: februari, mei, juli, november;
- Bedrag per betaling: jaarbudget;
- De app verdeelt het jaarbudget over de gekozen maanden.

### Energie met seizoensprofiel

Voor energieverbruik dat per maand sterk wisselt:

- Bedragstype: maandprofiel;
- Vul per maand het verwachte bedrag in;
- Gebruik betalingsvertraging wanneer de betaling pas later gebeurt dan de verbruiksmaand.

## Build controleren

Voor een buildcontrole zonder apphost-lock:

```powershell
dotnet build .\FinancialPlanningApp.Web\FinancialPlanningApp.Web.csproj /p:UseAppHost=false -o .\artifacts\build-check
```

## Git hygiene

De `.gitignore` sluit onder meer uit:

- lokale secrets;
- buildoutput;
- IDE-bestanden;
- CODA-bestanden;
- kredietkaartafschriften;
- CSV/Excel/PDF-importbestanden;
- tijdelijke artifacts.

Controleer voor publicatie altijd:

```powershell
git status --short
git grep -n "password\|secret\|DB_PASSWORD"
```

Let op: placeholders zoals `@{DB_PASSWORD}` zijn verwacht in server/multi-tenant configuratie. Echte waarden horen alleen lokaal in `secrets.json`, in environment variables of in een private deployomgeving. Desktopmodus heeft normaal geen `secrets.json` nodig.

## Status

De huidige versie is `1.0.4.0`.

De app is functioneel voor lokaal of privaat gebruik. Er is een multi-tenant servermodus met MySQL/MariaDB en een Windows desktopmodus met SQLite, eerste setup, backupmogelijkheid, update-check en Inno Setup installer.

Voor brede publieke distributie blijven vooral praktische randzaken belangrijk, zoals code-signing/reputatie van de installer, verdere documentatie en extra testdekking op verschillende machines.

