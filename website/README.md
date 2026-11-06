# financialplanning.pware.be

Deze map bevat de statische introductiepagina voor `https://financialplanning.pware.be/`. De Windows-desktopknop verwijst naar Microsoft Store-product `9MXRZ150MSCJ`; de website host geen desktopinstaller meer.

## Publiceren

Upload de inhoud van deze map naar de webroot van de site:

```text
index.html
privacy/index.html
updates/latest.json
downloads/
```

Plaats de optionele serverversie in:

```text
downloads/DomesticFinancialPlanning-Server-<version>.zip
```

De map `downloads/` is in Git genegeerd, zodat grote installers en ZIP-bestanden niet per ongeluk in de repository terechtkomen.

Publiceer ook:

```text
downloads/checksums.txt
```

Dat bestand bevat de SHA-256 checksum voor de optionele serverdownload. Genereer het opnieuw na elke release:

```powershell
.\tools\write-release-checksums.ps1
```

## Volledige website publish

Maak eerst lokaal een stagingmap zonder upload:

```powershell
.\tools\publish-website.ps1 -SkipBuild -SkipUpload
```

Daarna staat alles wat naar de website moet in:

```text
artifacts/website-upload
```

Als SSH/SFTP beschikbaar is, kan dezelfde stap ook uploaden:

```powershell
.\tools\publish-website.ps1 -UploadMode Sftp -RemoteHost financialplanning-web -RemotePath .
```

Met een aparte host/user, andere poort of sleutel:

```powershell
.\tools\publish-website.ps1 -UploadMode Sftp -RemoteHost example.org -RemoteUser webuser -RemotePath . -Port 22 -IdentityFile <path-to-private-key>
```

Gebruik `-DryRun` om eerst de SFTP-uploadstap te tonen zonder upload:

```powershell
.\tools\publish-website.ps1 -SkipBuild -UploadMode Sftp -RemoteHost financialplanning-web -RemotePath . -DryRun
```

## DEPLOY-map scripts

Je kan optioneel wrapper-scripts buiten de repository bewaren, bijvoorbeeld in je eigen DEPLOY-map:

```text
<deploy-root>\deploy_financialplanning_website.cmd
<deploy-root>\deploy_financialplanning_website.ps1
```

Gebruik op Windows normaal de `.cmd`. Die:

- vraagt of packages opnieuw gebouwd moeten worden;
- maakt/staget de website via `tools/publish-website.ps1`;
- uploadt daarna rechtstreeks via SFTP;
- vraagt SFTP-host/alias, optionele gebruiker, optionele private key en remote path interactief.

Zolang de echte SSH/SFTP gegevens van `financialplanning.pware.be` nog niet bekend zijn, kan je alleen lokaal stagen met:

```powershell
.\tools\publish-website.ps1 -SkipBuild -SkipUpload
```

## Microsoft Store-package maken

Maak het x64 Store-indienpakket met:

```powershell
.\tools\package-desktop-store.ps1
```

Output:

```text
artifacts\store\DomesticFinancialPlanning-Store-<version>-x64.msixupload
```

Upload dit bestand in Partner Center bij het gereserveerde product en publiceer de website pas nadat de Store-listing beschikbaar is:

```text
https://apps.microsoft.com/detail/9MXRZ150MSCJ
```

## Server ZIP maken

Voor de serverversie:

```powershell
.\tools\package-server-zip.ps1
```

Output:

```text
<deploy-root>\Packages\DomesticFinancialPlanning-Server-<version>.zip
```

Upload dit bestand naar:

```text
downloads/DomesticFinancialPlanning-Server-<version>.zip
```

Het serverpackage-script:

- publiceert met `PublishProfile=Standard`;
- schrijft naar `<deploy-root>\Server`;
- weigert te packagen als echte secrets worden gevonden;
- sluit `secrets.json`, `appsettings.Local.json`, `appsettings.Development.json`, `appsettings.Desktop.json`, `desktop.mode`, `.pdb` en `.map` uit;
- laat `secrets.template.json` wel toe als documentatie voor configuratie.

`publish-website.ps1` leest `<version>` uit `FinancialPlanningApp.Web.csproj` en zet tijdens het stagen zowel de serverdownloadlink als de zichtbare SHA-256-checksum automatisch goed. Een expliciet opgegeven afwijkende `-Version` wordt geweigerd.

Controleer voor upload eventueel zelf nog:

```powershell
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::OpenRead("<deploy-root>\Packages\DomesticFinancialPlanning-Server-<version>.zip").Entries |
  Where-Object { $_.FullName -match "secret|appsettings\.(Development|Desktop|Local)|desktop\.mode" } |
  Select-Object FullName
```

Daarbij mag alleen `secrets.template.json` zichtbaar zijn.

## Update-check

De Store-variant schakelt de eigen updatechecker uit; Microsoft Store verzorgt updates. `updates/latest.json` blijft alleen bestaan voor oudere Inno-installaties en verwijst die gebruikers naar de Store-productpagina. Houd `version` gelijk aan de actuele Store-versie, zodat elke oudere EXE-installatie de overstapmelding kan tonen.

Werk bij een nieuwe release minstens deze velden bij:

- `version`
- `releaseDate`
- `downloadUrl`
- `releaseNotesUrl`

## Licentie

De website vermeldt de projectlicentie:

```text
GNU Affero General Public License v3.0 of later
```

en linkt naar:

```text
https://github.com/paulpeeters/domestic-financial-planning
```

