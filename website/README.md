# financialplanning.pware.be

Deze map bevat de statische introductiepagina en update-metadata voor `https://financialplanning.pware.be/`.

## Publiceren

Upload de inhoud van deze map naar de webroot van de site:

```text
index.html
updates/latest.json
downloads/
```

Plaats de Windows installer in:

```text
downloads/DomesticFinancialPlanning-Setup-1.0.3.0.exe
```

Plaats de optionele serverversie in:

```text
downloads/DomesticFinancialPlanning-Server-1.0.3.0.zip
```

De map `downloads/` is in Git genegeerd, zodat grote installers en ZIP-bestanden niet per ongeluk in de repository terechtkomen.

Publiceer ook:

```text
downloads/checksums.txt
```

Dat bestand bevat SHA-256 checksums voor de downloads. Genereer het opnieuw na elke release:

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

## Windows installer maken

Voor de desktopversie:

```powershell
.\tools\package-desktop-inno.ps1
```

Output:

```text
<deploy-root>\Installer\DomesticFinancialPlanning-Setup-1.0.3.0.exe
```

Upload dit bestand naar:

```text
downloads/DomesticFinancialPlanning-Setup-1.0.3.0.exe
```

## Server ZIP maken

Voor de serverversie:

```powershell
.\tools\package-server-zip.ps1
```

Output:

```text
<deploy-root>\Packages\DomesticFinancialPlanning-Server-1.0.3.0.zip
```

Upload dit bestand naar:

```text
downloads/DomesticFinancialPlanning-Server-1.0.3.0.zip
```

Het serverpackage-script:

- publiceert met `PublishProfile=Standard`;
- schrijft naar `<deploy-root>\Server`;
- weigert te packagen als echte secrets worden gevonden;
- sluit `secrets.json`, `appsettings.Local.json`, `appsettings.Development.json`, `appsettings.Desktop.json`, `desktop.mode`, `.pdb` en `.map` uit;
- laat `secrets.template.json` wel toe als documentatie voor configuratie.

Controleer voor upload eventueel zelf nog:

```powershell
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::OpenRead("<deploy-root>\Packages\DomesticFinancialPlanning-Server-1.0.3.0.zip").Entries |
  Where-Object { $_.FullName -match "secret|appsettings\.(Development|Desktop|Local)|desktop\.mode" } |
  Select-Object FullName
```

Daarbij mag alleen `secrets.template.json` zichtbaar zijn.

## Update-check

De app kan later `updates/latest.json` ophalen en `version` vergelijken met de eigen assembly-versie. Bij een nieuwere versie kan de app `downloadUrl` openen.

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

