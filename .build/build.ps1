[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $BranchName = 'dev',
    [bool] $IsDefaultBranch = $false,
    [string] $NugetFeedUrl,
    [string] $NugetFeedApiKey,
    [string] $SigningServiceUrl
)

$RootDir = "$PsScriptRoot\.." | Resolve-Path
$OutputDir = "$RootDir\.output\$Configuration"
$LogsDir = "$OutputDir\logs"
$NugetPackageOutputDir = "$OutputDir\nugetpackages"
$Solution = "$RootDir\ngit.sln"
# We probably don't want to publish every single nuget package ever built to our external feed.
# Let's only publish packages built from the default branch (master) by default.
$PublishNugetPackages = $env:TEAMCITY_VERSION -and $IsDefaultBranch
$NugetExe = "$PSScriptRoot\packages\Nuget.CommandLine\tools\Nuget.exe" | Resolve-Path

# Installer building routines are stored in a separate file
# . $PSScriptRoot\installer.tasks.ps1

task CreateFolders {
    New-Item $OutputDir -ItemType Directory -Force | Out-Null
    New-Item $LogsDir -ItemType Directory -Force | Out-Null
    New-Item $NugetPackageOutputDir -ItemType Directory -Force | Out-Null
}

# Synopsis: Retrieve three part version information and release notes from $RootDir\RELEASENOTES.md
# $script:Version = Major.Minor.Build.$VersionSuffix (for installer.tasks)
# $script:AssemblyVersion = $script:Version
# $script:AssemblyFileVersion = $script:Version
# $script:ReleaseNotes = read from RELEASENOTES.md
function GenerateVersionInformationFromReleaseNotesMd([int] $VersionSuffix) {
    $ReleaseNotesPath = "$RootDir\RELEASENOTES.md" | Resolve-Path
    $Notes = Read-ReleaseNotes -ReleaseNotesPath $ReleaseNotesPath
    $script:Version = [System.Version] "$($Notes.Version).$VersionSuffix"
    $script:ReleaseNotes = [string] $Notes.Content

    # Establish assembly version number
    $script:AssemblyVersion = $script:Version
    $script:AssemblyFileVersion = $script:Version
    
    TeamCity-PublishArtifact "$ReleaseNotesPath"
}

# Ensures the following are set
# $script:Version
# $script:AssemblyVersion
# $script:AssemblyFileVersion
# $script:ReleaseNotes
# $script:NugetPackageVersion = $script:Version or $script:Version-branch
task GenerateVersionInformation {
    "Retrieving version information"
    
    # For dev builds, version suffix is always 0
    $versionSuffix = 0
    if($env:BUILD_NUMBER) {
        $versionSuffix = $env:BUILD_NUMBER
    }
  
    GenerateVersionInformationFromReleaseNotesMd($versionSuffix)
    
    TeamCity-SetBuildNumber $script:Version
    
    $script:NugetPackageVersion = New-NugetPackageVersion -Version $script:Version -BranchName $BranchName -IsDefaultBranch $IsDefaultBranch
    
    "Version = $script:Version"
    "AssemblyVersion = $script:AssemblyVersion"
    "AssemblyFileVersion = $script:AssemblyFileVersion"
    "NugetPackageVersion = $script:NugetPackageVersion"
    "ReleaseNotes = $script:ReleaseNotes"
}

# Synopsis: Restore the nuget packages of the Visual Studio solution
task RestoreNugetPackages {
    exec {
        & $NugetExe restore "$Solution" -Verbosity detailed
    }
}

# Synopsis: Update the nuget packages of the Visual Studio solution
task UpdateNugetPackages RestoreNugetPackages, {
    exec {
        & $NugetExe update "$Solution" -Verbosity detailed
    }
}

# Synopsis: Update the version info in all AssemblyInfo.cs
task UpdateVersionInfo GenerateVersionInformation, {
    "Updating assembly information"

    # Ignore anything under the Testing/ folder
    @(Get-ChildItem "$RootDir" AssemblyInfo.cs -Recurse) | where { $_.FullName -notlike "$RootDir\Testing\*" } | ForEach {
        Update-AssemblyVersion $_.FullName `
            -Version $script:AssemblyVersion `
            -FileVersion $script:AssemblyFileVersion `
            -InformationalVersion $script:NuGetPackageVersion
    }
}

# Synopsis: Update the nuspec dependencies versions based on the versions of the nuget packages that are being used
task UpdateNuspecVersionInfo {
    # Find all the packages.config
    $projectFiles = Get-ChildItem "$RootDir" -Recurse -Filter "*.csproj" `
                      | ?{ $_.fullname -notmatch "\\(.build)|(packages)\\" } `
                      | Resolve-Path

    # Update dependency verions in each of our .nuspec file based on what is in our packages.config
    Resolve-Path "$RootDir\Nuspec\*.nuspec" | Update-NuspecDependenciesVersions -ProjectFilePaths $projectFiles -SpecificVersions -verbose
}

# Synopsis: A task that makes sure our initialization tasks have been run before we can do anything useful
task Init CreateFolders, RestoreNugetPackages, GenerateVersionInformation

# Synopsis: Compile the Visual Studio solution
task Compile Init, UpdateVersionInfo, {
    use 15.0 MSBuild
    try {
        exec {
            & MSBuild `
                "$Solution" `
                /maxcpucount `
                /nodereuse:false `
                /target:Build `
                /p:Configuration=$Configuration `
                /flp1:verbosity=normal`;LogFile=$LogsDir\_msbuild.log.normal.txt `
                /flp2:WarningsOnly`;LogFile=$LogsDir\_msbuild.log.warnings.txt `
                /flp3:PerformanceSummary`;NoSummary`;verbosity=quiet`;LogFile=$LogsDir\_msbuild.log.performanceSummary.txt `
                /flp4:verbosity=detailed`;LogFile=$LogsDir\_msbuild.log.detailed.txt `
                /flp5:verbosity=diag`;LogFile=$LogsDir\_msbuild.log.diag.txt `
        }
    } finally {
        TeamCity-PublishArtifact "$LogsDir\_msbuild.log.* => logs/msbuild.$Configuration.logs.zip"
    }
}

# Synopsis: Execute our unit tests
task UnitTests {
    "$RootDir\bin\net461\*.Test.dll" | Resolve-Path | ForEach-Object {
    Invoke-NUnitForAssembly `
        -AssemblyPath $_ `
        -NUnitVersion "2.6.4" `
        -FrameworkVersion "net-4.0" `
        -EnableCodeCoverage $false `
        -ExcludedCategories @('Explicit') `
    }
}

# Synopsis: Build the nuget packages.
task BuildNugetPackages Init, UpdateNuspecVersionInfo, {
    New-Item $NugetPackageOutputDir -ItemType Directory -Force | Out-Null

    $escaped=$ReleaseNotes.Replace('"','\"')
    $properties = "releaseNotes=$escaped"
    
    "$RootDir\Nuspec\*.nuspec" | Resolve-Path | ForEach-Object {
        exec {
            & $NugetExe pack $_ `
                -Version $NugetPackageVersion `
                -OutputDirectory $NugetPackageOutputDir `
                -BasePath $RootDir `
                -Properties $properties `
                -NoPackageAnalysis
        }
    }
    
    TeamCity-PublishArtifact "$NugetPackageOutputDir\*.nupkg => NugetPackages"
}

# Synopsis: Publish the nuget packages (Teamcity only)
task PublishNugetPackages -If($PublishNugetPackages) {
  assert ($NugetFeedUrl) '$NugetFeedUrl is missing. Cannot publish nuget packages'
  assert ($NugetFeedApiKey) '$NugetFeedApiKey is missing. Cannot publish nuget packages'

  Get-ChildItem $NugetPackageOutputDir -Filter "*.nupkg" | ForEach-Object {
    & $NugetExe push $_.FullName -Source $NugetFeedUrl -ApiKey $NugetFeedApiKey
  }
}

# Synopsis: Build the project.
task Build Init, Compile, UnitTests, BuildNugetPackages, PublishNugetPackages

# Synopsis: By default, Call the 'Build' task
task . Build