#requires -Version 7.0
param([string]$SqlServer = 'localhost\sqlexpress')

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$runId = [Guid]::NewGuid().ToString('N')
$databaseName = 'BolagomVerify_' + $runId
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) $databaseName
$apiOutput = Join-Path $tempRoot 'api'
$consoleOutput = Join-Path $tempRoot 'console'
$databaseCreated = $false
$apiProcess = $null
$checks = 0

function Invoke-Sql([string]$Database, [string]$Query) {
    $output = & sqlcmd -S $SqlServer -E -C -d $Database -l 5 -b -h -1 -W -Q $Query 2>&1
    if ($LASTEXITCODE -ne 0) { throw "SQL misslyckades: $output" }
    return ($output -join "`n").Trim()
}

function Assert-That([bool]$Condition, [string]$Description) {
    if (-not $Condition) { throw "Underkänt: $Description. Senaste API-svar: $script:lastApiResponse" }
    $script:checks++
}

function Send-Api([string]$Method, [string]$Path, $Body = $null) {
    $parameters = @{
        Uri = $script:baseUrl + $Path
        Method = $Method
        SkipHttpErrorCheck = $true
        TimeoutSec = 10
    }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json; charset=utf-8'
        $parameters.Body = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Compress))
    }
    $response = Invoke-WebRequest @parameters
    $content = if ($response.Content -is [byte[]]) {
        [Text.Encoding]::UTF8.GetString($response.Content)
    } else { $response.Content }
    $script:lastApiResponse = "HTTP $($response.StatusCode): $content"
    $json = if ($content) { $content | ConvertFrom-Json } else { $null }
    return @{ Status = [int]$response.StatusCode; Body = $json }
}

function Start-Dotnet([string]$Assembly, [string[]]$Arguments = @()) {
    $startInfo = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.StandardOutputEncoding = [Text.Encoding]::UTF8
    $startInfo.WorkingDirectory = $repoRoot
    $startInfo.ArgumentList.Add($Assembly)
    foreach ($argument in $Arguments) { $startInfo.ArgumentList.Add($argument) }
    $startInfo.Environment['ConnectionStrings__DefaultConnection'] = "Server=$SqlServer;Database=$databaseName;Integrated Security=True;Encrypt=False;"
    $startInfo.Environment['Logging__EventLog__LogLevel__Default'] = 'None'
    $startInfo.Environment['ASPNETCORE_ENVIRONMENT'] = 'Production'
    $startInfo.Environment['BOLAGOM_API_URL'] = $script:baseUrl
    return [Diagnostics.Process]::Start($startInfo)
}

function Run-Console([string[]]$Lines) {
    $process = Start-Dotnet (Join-Path $consoleOutput 'BoLagom.ConsolApp.dll')
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    try {
        foreach ($line in $Lines) { $process.StandardInput.WriteLine($line) }
        $process.StandardInput.Close()
        if (-not $process.WaitForExit(15000)) {
            throw 'Konsolflödet avslutades inte inom 15 sekunder.'
        }
        $output = $stdout.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw $stderr.GetAwaiter().GetResult() }
        return $output
    } finally {
        if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
}

try {
    New-Item -ItemType Directory -Path $tempRoot | Out-Null
    & dotnet build (Join-Path $repoRoot 'BoLagom.Api/BoLagom.Api.csproj') --no-restore -o $apiOutput
    if ($LASTEXITCODE -ne 0) { throw 'API-bygget misslyckades.' }
    & dotnet build (Join-Path $repoRoot 'BoLagom.ConsolApp/BoLagom.ConsolApp.csproj') --no-restore -o $consoleOutput
    if ($LASTEXITCODE -ne 0) { throw 'Konsolbygget misslyckades.' }

    Invoke-Sql master "CREATE DATABASE [$databaseName];" | Out-Null
    $databaseCreated = $true
    Invoke-Sql $databaseName @'
CREATE TABLE dbo.Properties (
    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name varchar(50) NOT NULL,
    Floors int NULL,
    Address varchar(100) NOT NULL,
    PortCode varchar(20) NULL
);
CREATE SEQUENCE dbo.ApartmentIdSequence AS int START WITH 1 INCREMENT BY 1;
CREATE TABLE dbo.Apartments (
    Id int NOT NULL CONSTRAINT DF_Apartments_Id DEFAULT (NEXT VALUE FOR dbo.ApartmentIdSequence),
    ApartmentNumber varchar(5) NOT NULL,
    LivingArea decimal(4,1) NOT NULL,
    MontlyRent int NOT NULL,
    PropertyId int NOT NULL,
    CONSTRAINT PK_Apartments PRIMARY KEY (Id),
    CONSTRAINT UQ_ApartmentNumber_DiffProperty UNIQUE (PropertyId, ApartmentNumber),
    CONSTRAINT FK_Apartments_Properties FOREIGN KEY (PropertyId) REFERENCES dbo.Properties(Id),
    CHECK (LivingArea > 0),
    CHECK (MontlyRent >= 0)
);
'@ | Out-Null

    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()
    $script:baseUrl = "http://127.0.0.1:$port"
    $apiProcess = Start-Dotnet (Join-Path $apiOutput 'BoLagom.Api.dll') @('--urls', $script:baseUrl)
    $apiStdout = $apiProcess.StandardOutput.ReadToEndAsync()
    $apiStderr = $apiProcess.StandardError.ReadToEndAsync()
    $ready = $false
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        if ($apiProcess.HasExited) { throw 'API:t avslutades vid start.' }
        try { $null = Send-Api GET '/api/properties'; $ready = $true; break } catch { Start-Sleep -Milliseconds 100 }
    }
    if (-not $ready) { throw 'API:t startade inte i tid.' }

    $output = Run-Console @('1', '1', '', '0', '0')
    Assert-That ($output.Contains('Inga fastigheter hittades.')) 'Tom fastighetslista'
    Assert-That ($output.Contains('1. Fastighet') -and $output.Contains('4. Uppdatera fastighet')) 'Huvudmeny och undermeny'

    $propertyData = @{ name = 'Verifiering A'; address = 'Testgatan 1'; floors = '3'; portCode = '1234' }
    $response = Send-Api POST '/api/properties' $propertyData
    Assert-That ($response.Status -eq 201 -and $response.Body.id -gt 0) 'Skapa fastighet'
    $firstId = $response.Body.id
    $propertyData.name = 'Verifiering B'
    $response = Send-Api POST '/api/properties' $propertyData
    Assert-That ($response.Status -eq 201) 'Skapa andra fastigheten'
    $secondId = $response.Body.id
    $path = "/api/properties/$firstId/apartments"
    $valid = @{ apartmentNumber = '00101'; livingArea = 65.5; monthlyRent = 0 }

    $response = Send-Api GET $path
    Assert-That ($response.Status -eq 200 -and $script:lastApiResponse -eq 'HTTP 200: []') 'Tom lägenhetslista'
    $output = Run-Console @('1', '1', "$firstId", '0', '0', '0', '0')
    Assert-That ($output.Contains('Inga lägenheter finns i fastigheten ännu.') -and
        $output.Contains('1. Skapa lägenhet')) 'Tom lägenhetslista och skapa-val visas i fastighetsmenyn'
    $response = Send-Api GET '/api/properties/2147483647/apartments'
    Assert-That ($response.Status -eq 404) 'Lista lägenheter för saknad fastighet'
    foreach ($id in @(0, -1)) {
        $response = Send-Api GET "/api/properties/$id/apartments"
        Assert-That ($response.Status -eq 400 -and $response.Body.errors.PropertyId) 'Lista lägenheter med ogiltigt fastighets-ID'
    }

    $response = Send-Api POST $path @{}
    Assert-That ($response.Status -eq 400 -and @($response.Body.errors.PSObject.Properties).Count -eq 3) 'Saknade fält ger separata fel'
    $cases = @(
        @{ Field = 'apartmentNumber'; Key = 'ApartmentNumber'; Value = $null },
        @{ Field = 'apartmentNumber'; Key = 'ApartmentNumber'; Value = '' },
        @{ Field = 'apartmentNumber'; Key = 'ApartmentNumber'; Value = '   ' },
        @{ Field = 'apartmentNumber'; Key = 'ApartmentNumber'; Value = '123456' },
        @{ Field = 'livingArea'; Key = 'LivingArea'; Value = $null },
        @{ Field = 'livingArea'; Key = 'LivingArea'; Value = 0 },
        @{ Field = 'livingArea'; Key = 'LivingArea'; Value = -1 },
        @{ Field = 'livingArea'; Key = 'LivingArea'; Value = 1000 },
        @{ Field = 'livingArea'; Key = 'LivingArea'; Value = 65.55 },
        @{ Field = 'monthlyRent'; Key = 'MonthlyRent'; Value = $null },
        @{ Field = 'monthlyRent'; Key = 'MonthlyRent'; Value = -1 }
    )
    foreach ($case in $cases) {
        $body = $valid.Clone()
        $body[$case.Field] = $case.Value
        $response = Send-Api POST $path $body
        $keys = @($response.Body.errors.PSObject.Properties.Name)
        Assert-That ($response.Status -eq 400 -and $keys.Count -eq 1 -and $keys[0] -eq $case.Key) "Validering av $($case.Field): $($case.Value)"
    }
    foreach ($id in @(0, -1)) {
        $response = Send-Api POST "/api/properties/$id/apartments" $valid
        Assert-That ($response.Status -eq 400 -and $response.Body.errors.PropertyId) 'Ogiltigt fastighets-ID'
    }
    $body = $valid.Clone(); $body.monthlyRent = 1.5
    $response = Send-Api POST $path $body
    Assert-That ($response.Status -eq 400) 'Hyra måste vara heltal'
    $response = Send-Api POST '/api/properties/2147483647/apartments' $valid
    Assert-That ($response.Status -eq 404) 'Fastigheten saknas'

    $body = $valid.Clone(); $body.apartmentNumber = ' 00101 '
    $response = Send-Api POST $path $body
    Assert-That ($response.Status -eq 201 -and $response.Body.id -gt 0) 'Skapa lägenhet'
    Assert-That ($response.Body.apartmentNumber -eq '00101' -and $response.Body.propertyId -eq $firstId) 'Trimning, inledande nollor och koppling till fastighet'
    Assert-That ($response.Body.monthlyRent -eq 0 -and $response.Body.livingArea -eq 65.5) 'Hyra noll och korrekt decimal'
    $createdApartmentId = $response.Body.id
    $response = Send-Api GET $path
    $apartments = @($response.Body)
    Assert-That ($response.Status -eq 200 -and $apartments.Count -eq 1 -and
        $apartments[0].id -eq $createdApartmentId -and $apartments[0].apartmentNumber -eq '00101' -and
        $apartments[0].livingArea -eq 65.5 -and $apartments[0].propertyId -eq $firstId) 'Hämta sparad lägenhet med alla uppgifter'
    $saved = Invoke-Sql $databaseName 'SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.Apartments WHERE ApartmentNumber=''00101'' AND LivingArea=65.5 AND MontlyRent=0;'
    Assert-That ($saved -eq '1') 'Värdena sparas i databasens MontlyRent-kolumn'
    $response = Send-Api POST $path $valid
    Assert-That ($response.Status -eq 409) 'Dubblett i samma fastighet'
    $otherApartment = $valid.Clone(); $otherApartment.monthlyRent = 4500
    $response = Send-Api POST "/api/properties/$secondId/apartments" $otherApartment
    Assert-That ($response.Status -eq 201) 'Samma nummer tillåts i en annan fastighet'
    $response = Send-Api GET "/api/properties/$secondId/apartments"
    $apartments = @($response.Body)
    Assert-That ($response.Status -eq 200 -and $apartments.Count -eq 1 -and
        $apartments[0].propertyId -eq $secondId -and $apartments[0].monthlyRent -eq 4500) 'Lägenhetslistan visar bara rätt fastighet och mappar MontlyRent'
    $response = Send-Api GET $path
    Assert-That (@($response.Body).Count -eq 1 -and $response.Body.monthlyRent -eq 0) 'Andra fastighetens lägenhet påverkar inte den första listan'
    foreach ($area in @(0.1, 999.9)) {
        $body = $valid.Clone(); $body.apartmentNumber = "B$($checks)"; $body.livingArea = $area
        $response = Send-Api POST $path $body
        Assert-That ($response.Status -eq 201) "Giltig gräns för boyta: $area"
    }
    $body = $valid.Clone(); $body.apartmentNumber = 'MAX'; $body.monthlyRent = [int]::MaxValue
    $response = Send-Api POST $path $body
    Assert-That ($response.Status -eq 201) 'Högsta tillåtna heltalshyra'

    $propertyData.name = 'Verifiering ändrad'
    $response = Send-Api PUT "/api/properties/$firstId" $propertyData
    Assert-That ($response.Status -eq 204) 'Uppdatera fastighet'
    $response = Send-Api DELETE "/api/properties/$firstId"
    Assert-That ($response.Status -eq 409) 'Fastighet med lägenheter kan inte raderas'
    $response = Send-Api GET '/api/properties'
    Assert-That (@($response.Body).Count -eq 2) 'Fastigheten finns kvar efter nekad radering'
    $propertyData.name = 'Tom fastighet'
    $response = Send-Api POST '/api/properties' $propertyData
    $emptyId = $response.Body.id
    $response = Send-Api DELETE "/api/properties/$emptyId"
    Assert-That ($response.Status -eq 204) 'Tom fastighet kan raderas'
    $response = Send-Api DELETE "/api/properties/$emptyId"
    Assert-That ($response.Status -eq 404) 'Radera redan raderad fastighet'

    $output = Run-Console @('1', '9', '', '1', 'abc', '-1', '99999', "$firstId", '1',
        '123456', '00402', '0', '65,55', '1 000', '42,5', '-1', '0', '', '0', '0', '0', '0')
    Assert-That ($output.Contains('Du valde ett ogiltigt menyval.')) 'Ogiltigt menyval'
    Assert-That ($output.Contains('Välj ett fastighets-ID som finns i listan.')) 'Ogiltiga fastighetsval'
    Assert-That ($output.Contains('Fastighet: Verifiering ändrad') -and $output.Contains('Adress: Testgatan 1')) 'Vald fastighets namn och adress'
    Assert-That ($output.Contains('Lägenheten har skapats med ID')) 'Skapa lägenhet från konsolmenyn'
    Assert-That ($output.Contains('Befintliga lägenheter:') -and $output.Contains('Lägenhet 00101') -and
        $output.Contains('Boyta: 65,5 m²') -and $output.Contains('Hyra: 0 kr/månad')) 'Befintliga lägenheter visas med nummer, boyta och hyra'
    Assert-That ($output.IndexOf('Lägenhet 00402') -gt $output.IndexOf('Lägenheten har skapats med ID')) 'Lägenhetslistan uppdateras automatiskt efter skapande'
    $saved = Invoke-Sql $databaseName "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.Apartments WHERE ApartmentNumber='00402' AND LivingArea=42.5 AND MontlyRent=0 AND PropertyId=$firstId;"
    Assert-That ($saved -eq '1') 'Decimal med komma och validerad konsolinmatning'
    $output = Run-Console @('1', '1', "$firstId", '1', '00402', '42.5', '0', '', '0', '0', '0', '0')
    Assert-That ($output.Contains('Lägenhetsnumret finns redan i den valda fastigheten.')) 'Konflikt visas i konsolen och decimalpunkt accepteras'
    $output = Run-Console @('1', '3', "$firstId", '', '0', '0')
    Assert-That ($output.Contains('Fastigheten kan inte raderas eftersom den har lägenheter.')) 'Raderingskonflikt visas i konsolen'
    $output = Run-Console @('1', '2', ('x' * 101), 'Testgatan 1', '3', '1234', '', '0', '0')
    Assert-That ($output.Contains('Name must be no more than 100 characters.')) 'Fältvisa API-fel visas i konsolen'

    Invoke-Sql $databaseName "ALTER TABLE dbo.Apartments ADD CONSTRAINT CK_VerificationFailure CHECK (ApartmentNumber <> 'ERR');" | Out-Null
    $body = $valid.Clone(); $body.apartmentNumber = 'ERR'
    $response = Send-Api POST $path $body
    Assert-That ($response.Status -eq 500) 'Oväntat databasfel blir 500, inte 404'

    Invoke-Sql $databaseName "EXEC sp_rename 'dbo.Apartments', 'ApartmentsVerificationHidden';" | Out-Null
    $response = Send-Api GET $path
    Assert-That ($response.Status -eq 500) 'Databasfel vid hämtning av lägenheter ger 500'
    $output = Run-Console @('1', '1', "$firstId", '0', '0', '0', '0')
    Assert-That ($output.Contains('Lägenheterna kunde inte hämtas.') -and
        -not $output.Contains('Inga lägenheter finns i fastigheten ännu.')) 'Hämtningsfel visas utan att påstå att lägenhetslistan är tom'

    Write-Output "$checks kontroller godkända. Endast den separata testdatabasen $databaseName användes."
} finally {
    if ($null -ne $apiProcess) {
        if (-not $apiProcess.HasExited) { $apiProcess.Kill($true); $apiProcess.WaitForExit() }
        $apiProcess.Dispose()
    }
    if ($databaseCreated) {
        if ($databaseName -notmatch '^BolagomVerify_[a-f0-9]{32}$') { throw 'Oväntat databasnamn vid städning.' }
        Invoke-Sql master "ALTER DATABASE [$databaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$databaseName];" | Out-Null
    }
    if (Test-Path -LiteralPath $tempRoot) {
        $resolved = (Resolve-Path -LiteralPath $tempRoot).Path
        $expected = Join-Path ([IO.Path]::GetTempPath()) $databaseName
        if ($resolved -ne $expected) { throw 'Oväntad temporär sökväg vid städning.' }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
