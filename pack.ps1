# -SkipTests sert aux CI des applications, qui empaquettent Mue sans rejouer ses tests :
# le test réel du Restart Manager dépend du poste.
param([string]$Feed = 'C:\Users\Sylvain\nuget-local', [switch]$SkipTests)

$ErrorActionPreference = 'Stop'
$Feed = [IO.Path]::GetFullPath($Feed)
$version = ([xml](Get-Content "$PSScriptRoot/Directory.Build.props" -Raw)).Project.PropertyGroup.Version
$projects = @('Mue', 'Mue.Velopack', 'Mue.Wpf')
foreach ($project in $projects) {
    if (Test-Path (Join-Path $Feed "$project.$version.nupkg")) {
        throw "Le paquet $project $version existe déjà dans $Feed. Augmenter la version avant de publier."
    }
}

Push-Location $PSScriptRoot
try {
    dotnet build Mue.sln -m:4
    if ($LASTEXITCODE -ne 0) { throw 'Échec de la compilation de Mue.' }
    if (-not $SkipTests) {
        dotnet test Mue.sln -m:4
        if ($LASTEXITCODE -ne 0) { throw 'Échec des tests de Mue.' }
    }
    New-Item -ItemType Directory -Force $Feed | Out-Null
    foreach ($project in $projects) {
        dotnet pack "src/$project/$project.csproj" -c Release -m:4 -o $Feed
        if ($LASTEXITCODE -ne 0) { throw "Échec de la création du paquet $project." }
    }
}
finally { Pop-Location }
