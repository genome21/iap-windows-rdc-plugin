#
# Copyright 2019 Google LLC
#
# Licensed to the Apache Software Foundation (ASF) under one
# or more contributor license agreements.  See the NOTICE file
# distributed with this work for additional information
# regarding copyright ownership.  The ASF licenses this file
# to you under the Apache License, Version 2.0 (the
# "License"); you may not use this file except in compliance
# with the License.  You may obtain a copy of the License at
# 
#   http://www.apache.org/licenses/LICENSE-2.0
# 
# Unless required by applicable law or agreed to in writing,
# software distributed under the License is distributed on an
# "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
# KIND, either express or implied.  See the License for the
# specific language governing permissions and limitations
# under the License.
#

$ErrorActionPreference = "stop"

# Use TLS 1.2 for all downloads.
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

${env:__BUILD_ENV_INITIALIZED} = "1"

#------------------------------------------------------------------------------
# Remove Cygwin, Msys from PATH
#------------------------------------------------------------------------------

$env:Path = ($env:Path -split ";" `
  | Where-Object {!($_ -like "*msys*")} `
  | Where-Object {!($_ -like "*cygwin*")}) -join ";"

#------------------------------------------------------------------------------
# Find MSBuild and add to PATH
#------------------------------------------------------------------------------

if ((Get-Command "msbuild.exe" -ErrorAction SilentlyContinue) -eq $null)
{
    $MsBuildCandidates = `
        "${Env:ProgramFiles}\Microsoft Visual Studio\*\*\MSBuild\*\bin\msbuild.exe",
        "${Env:ProgramFiles(x86)}\Microsoft Visual Studio\*\*\MSBuild\*\bin\msbuild.exe",
        "c:\VS\MSBuild\Current\Bin\"

    $Msbuild = $MsBuildCandidates | Resolve-Path  -ErrorAction Ignore | Select-Object -ExpandProperty Path -First 1
    if ($Msbuild)
    {
        $MsbuildDir = (Split-Path $Msbuild -Parent)
        $env:Path += ";$MsbuildDir"
    }
    else
    {
        Write-Host "Could not find msbuild" -ForegroundColor Red
        exit 1
    }
}

$env:MSBUILD = $Msbuild

#------------------------------------------------------------------------------
# Find nmake and add to PATH
#------------------------------------------------------------------------------

$Nmake = (Get-Command "nmake.exe" -ErrorAction SilentlyContinue).Source
if ($Nmake -eq $null)
{
    $NmakeCandidates = `
        "${Env:ProgramFiles}\Microsoft Visual Studio\*\*\VC\Tools\MSVC\*\bin\Hostx86\*\nmake.exe",
        "${Env:ProgramFiles(x86)}\Microsoft Visual Studio\*\*\VC\Tools\MSVC\*\bin\Hostx86\*\nmake.exe",
        "c:\VS\VC\Tools\MSVC\*\bin\Hostx86\*\nmake.exe"
    $Nmake = $NmakeCandidates | Resolve-Path  -ErrorAction Ignore | Select-Object -ExpandProperty Path -First 1
    if ($Nmake)
    {
        $NmakeDir = (Split-Path $NMake -Parent)
        $env:Path += ";$NmakeDir"
    }
    else
    {
        Write-Host "Could not find nmake" -ForegroundColor Red
        exit 1
    }
}

#------------------------------------------------------------------------------
# Find cmake and add to PATH
#------------------------------------------------------------------------------

$Cmake = (Get-Command "cmake.exe" -ErrorAction SilentlyContinue).Source
if ($Cmake -eq $null)
{
    $CmakeCandidates = `
        "${Env:ProgramFiles}\Microsoft Visual Studio\*\*\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe",
        "${Env:ProgramFiles(x86)}\Microsoft Visual Studio\*\*\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe",
        "c:\VS\VC\Tools\MSVC\*\bin\Hostx86\*\nmake.exe"
    $Cmake = $CmakeCandidates | Resolve-Path  -ErrorAction Ignore | Select-Object -ExpandProperty Path -First 1
    if ($Cmake)
    {
        $CmakeDir = (Split-Path $CMake -Parent)
        $env:Path += ";$CmakeDir"
    }
    else
    {
        Write-Host "Could not find cmake" -ForegroundColor Red

        #
        # cmake isn't needed for all targets, so allow build to proceed.
        #
    }
}

$env:CMAKE = $Cmake

#------------------------------------------------------------------------------
# Find nuget and add to PATH
#------------------------------------------------------------------------------

$Nuget = (Get-Command "nuget.exe" -ErrorAction SilentlyContinue).Source
if ($Nuget -eq $null) 
{
	$NugetDownloadUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"

	New-Item -ItemType Directory -Force "${PSScriptRoot}\.tools" | Out-Null
	$Nuget = "${PSScriptRoot}\.tools\nuget.exe"
	(New-Object System.Net.WebClient).DownloadFile($NugetDownloadUrl, $Nuget)
	
	$env:Path += ";${PSScriptRoot}\.tools"
}

$env:NUGET = $Nuget

#------------------------------------------------------------------------------
# Restore packages and make them available in the environment
#------------------------------------------------------------------------------

if ((Test-Path "*.sln") -and !$args.Contains("clean"))
{
    #
    # Generate a nuget.config that prioritizes packages from the local feed.
    #
    if (Test-Path "${env:KOKORO_GFILE_DIR}\NuGetPackages")
    {
        $LocalFeed = "${env:KOKORO_GFILE_DIR}\NuGetPackages"
    }
    else 
    {
        $LocalFeed = (Resolve-Path "${PSScriptRoot}\..\dependencies\NuGetPackages").Path
    }
 
    $NuGetConfig = @"
<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <packageSources>
    <clear />
    <add key='nuget.org' value='https://api.nuget.org/v3/index.json' protocolVersion='3' />
    <add key='dependencies' value='{0}' />
  </packageSources>
</configuration>
"@ -f $LocalFeed | Out-File -Encoding ASCII ${PSScriptRoot}\NuGet.config

    #
    # Restore packages for solution.
    #
	& $Nmake restore
	if ($LastExitCode -ne 0)
	{
		exit $LastExitCode
	}

    $PackageReferences = ` 
        Get-ChildItem -Recurse -Include "*.csproj" `
            | % { [xml](Get-Content $_) | Select-Xml "//PackageReference" | Select-Object -ExpandProperty Node } `
            | sort -Property Include -Unique
        
	#
	# Add all tools to PATH.
	#
    $ToolsDirectories = $PackageReferences | % { "$($env:USERPROFILE)\.nuget\packages\$($_.Include)\$($_.Version)\tools" }
	$env:Path += ";" + ($ToolsDirectories -join ";")

	#
	# Add environment variables indicating package versions, for example
	# $env:Google_Apis_Auth = 1.2.3
	#
    $PackageReferences `
        | Where-Object { $_.Include -ne $null } `
        | ForEach-Object { New-Item -Name $_.Include.Replace(".", "_") -value $_.Version -ItemType Variable -Path Env: -Force }
}

Write-Host "PATH: ${Env:PATH}" -ForegroundColor Yellow

#------------------------------------------------------------------------------
# Find Google Cloud credentials and project (for tests)
#------------------------------------------------------------------------------

if (Test-Path "${env:KOKORO_GFILE_DIR}\iapdesktop-kokoro.json")
{
	if (!$Env:GOOGLE_APPLICATION_CREDENTIALS)
	{
		$Env:GOOGLE_APPLICATION_CREDENTIALS = "${env:KOKORO_GFILE_DIR}\iapdesktop-kokoro.json"
	}

    & gcloud auth activate-service-account --key-file=$Env:GOOGLE_APPLICATION_CREDENTIALS | Out-Default
    
	Write-Host "Google Cloud credentials: ${Env:GOOGLE_APPLICATION_CREDENTIALS}" -ForegroundColor Yellow
}

if (Test-Path "${env:KOKORO_GFILE_DIR}\test-configuration.json")
{
	if (!$Env:IAPDESKTOP_CONFIGURATION)
	{
		$Env:IAPDESKTOP_CONFIGURATION = "${env:KOKORO_GFILE_DIR}\test-configuration.json"
	}

	Write-Host "Test configuration: ${Env:IAPDESKTOP_CONFIGURATION}" -ForegroundColor Yellow
}

#------------------------------------------------------------------------------
# Run nmake.
#------------------------------------------------------------------------------

& $Nmake $args

if ($LastExitCode -ne 0)
{
    exit $LastExitCode
}
