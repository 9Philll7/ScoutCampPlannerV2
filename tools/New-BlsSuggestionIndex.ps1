param(
    [Parameter(Mandatory = $true)]
    [string]$CsvPath,

    [string]$OutputPath = "src/backend/ScoutCampPlanner.Api/reference-data/bls-4.0-suggestions.json"
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $CsvPath -PathType Leaf)) {
    throw "BLS-CSV nicht gefunden: $CsvPath"
}

$rows = @(Import-Csv -LiteralPath $CsvPath -Delimiter ';' -Encoding UTF8)
if ($rows.Count -eq 0) { throw 'Die BLS-CSV enthält keine Datensätze.' }
$columns = @($rows[0].PSObject.Properties.Name)

function Resolve-Column([string]$prefix) {
    $matches = @($columns | Where-Object {
        $_ -like "$prefix *" -and $_ -notlike '* Datenherkunft' -and $_ -notlike '* Referenz'
    })
    if ($matches.Count -ne 1) { throw "BLS-Spalte '$prefix' ist nicht eindeutig vorhanden." }
    return $matches[0]
}

function Convert-DecimalOrNull($value) {
    $text = ([string]$value).Trim()
    if ($text -eq '' -or $text -eq '-' -or $text.StartsWith('<')) { return $null }
    [decimal]$number = 0
    if (-not [decimal]::TryParse($text, [Globalization.NumberStyles]::Number,
        [Globalization.CultureInfo]::GetCultureInfo('de-DE'), [ref]$number)) {
        return $null
    }
    return $number
}

$energy = Resolve-Column 'ENERCJ'
$fat = Resolve-Column 'FAT'
$saturatedFat = Resolve-Column 'FASAT'
$carbohydrate = Resolve-Column 'CHO'
$sugars = Resolve-Column 'SUGAR'
$protein = Resolve-Column 'PROT625'
$salt = Resolve-Column 'NACL'
$fiber = Resolve-Column 'FIBT'
$substanceColumns = [ordered]@{
    LACTOSE = Resolve-Column 'LACS'
    FRUCTOSE = Resolve-Column 'FRUS'
    SORBITOL = Resolve-Column 'SORTL'
    MANNITOL = Resolve-Column 'MANTL'
    XYLITOL = Resolve-Column 'XYLTL'
}

$entries = foreach ($row in $rows) {
    $code = ([string]$row.'BLS Code').Trim()
    $name = ([string]$row.'Lebensmittelbezeichnung').Trim()
    if ($code -eq '' -or $name -eq '') { continue }

    $substances = [ordered]@{}
    foreach ($item in $substanceColumns.GetEnumerator()) {
        $value = Convert-DecimalOrNull $row.($item.Value)
        if ($null -ne $value) { $substances[$item.Key] = $value }
    }

    [ordered]@{
        code = $code
        name = $name
        nutrition = [ordered]@{
            energyKilojoules = Convert-DecimalOrNull $row.$energy
            fatGrams = Convert-DecimalOrNull $row.$fat
            saturatedFatGrams = Convert-DecimalOrNull $row.$saturatedFat
            carbohydrateGrams = Convert-DecimalOrNull $row.$carbohydrate
            sugarsGrams = Convert-DecimalOrNull $row.$sugars
            proteinGrams = Convert-DecimalOrNull $row.$protein
            saltGrams = Convert-DecimalOrNull $row.$salt
            fiberGrams = Convert-DecimalOrNull $row.$fiber
        }
        substances = $substances
    }
}

$document = [ordered]@{
    schemaVersion = '1.0'
    source = 'Bundeslebensmittelschlüssel (BLS), Version 4.0, Max Rubner-Institut'
    license = 'CC BY 4.0'
    entries = @($entries)
}

$target = [IO.Path]::GetFullPath($OutputPath)
$directory = [IO.Path]::GetDirectoryName($target)
[IO.Directory]::CreateDirectory($directory) | Out-Null
$json = $document | ConvertTo-Json -Depth 6 -Compress
[IO.File]::WriteAllText($target, $json, [Text.UTF8Encoding]::new($false))
Write-Host "BLS-Vorschlagsindex erstellt: $target ($($entries.Count) Einträge)"
