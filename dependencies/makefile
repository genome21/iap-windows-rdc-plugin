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
CONFIGURATION = Release

# The tag should be increased whenever one of the dependencies is changed
TAG = 11

VTNETCORE_SHA = 9e68f5561dc52edb780615b3fe133289216b3dba
VTNETCORE_URL = https://github.com/darrenstarr/VtNetCore.git
VTNETCORE_VERSION = 1.0.30.$(TAG)

# NB. The libssh2 version is defined in vcpkg-ports\libssh2\portfile.cmake,
# with dependencies determined by the vcpkg tag.
VCPKG_TAG = master
VCPKG_URL = https://github.com/microsoft/vcpkg.git
LIBSSH2_VERSION = 1.9.0.$(TAG)

# GCS bucket used for storing results
PACKAGES_BUCKET = iap-windows-rdc-plugin

default: dependencies

#------------------------------------------------------------------------------
# Common targets
#------------------------------------------------------------------------------

$(MAKEDIR)\NuGetPackages:
	@echo "========================================================"
	@echo "=== Creating nuget package repository                ===
	@echo "========================================================"
	md $(MAKEDIR)\NuGetPackages
	-nuget sources add -Name iap-desktop-dependencies -Source $(MAKEDIR)\NuGetPackages

#------------------------------------------------------------------------------
# Vtnetcore targets
#------------------------------------------------------------------------------

$(MAKEDIR)\obj\vtnetcore\VtNetCore\VtNetCore.csproj:
	@echo "========================================================"
	@echo "=== Checking out vtnetcore                           ==="
	@echo "========================================================"

	git clone $(VTNETCORE_URL) $(MAKEDIR)\obj\vtnetcore

	cd $(MAKEDIR)\obj\vtnetcore

	git checkout $(VTNETCORE_SHA)

	git config user.email "iap-desktop+build@google.com"
	git config user.name "IAP Desktop Build"
	git am $(MAKEDIR)\vtnetcore-patches\0001-Handle-OSC-sequences-with-empty-text-parameters.patch

	cd $(MAKEDIR)

$(MAKEDIR)\obj\vtnetcore\VtNetCore\bin\$(CONFIGURATION)\vtnetcore.$(VTNETCORE_VERSION).nupkg: \
		$(MAKEDIR)\obj\vtnetcore\VtNetCore\VtNetCore.csproj
	@echo "========================================================"
	@echo "=== Building vtnetcore                               ==="
	@echo "========================================================"
	cd $(MAKEDIR)\obj\vtnetcore

	nuget restore
	msbuild \
		/t:Rebuild \
		"/p:Configuration=$(CONFIGURATION);Platform=Any CPU;AssemblyName=vtnetcore;Version=$(VTNETCORE_VERSION);OutputPath=bin\$(CONFIGURATION)" \
		"$(MAKEDIR)\obj\vtnetcore\VtNetCore\VtNetCore.csproj"

	cd $(MAKEDIR)

vtnetcore: $(MAKEDIR)\obj\vtnetcore\VtNetCore\bin\$(CONFIGURATION)\vtnetcore.$(VTNETCORE_VERSION).nupkg $(MAKEDIR)\NuGetPackages
	@echo "========================================================"
	@echo "=== Publishing vtnetcore nuget package               ===
	@echo "========================================================"
	nuget add \
		$(MAKEDIR)\obj\vtnetcore\VtNetCore\bin\$(CONFIGURATION)\vtnetcore.$(VTNETCORE_VERSION).nupkg \
		-Source $(MAKEDIR)\NuGetPackages

#------------------------------------------------------------------------------
# vcpkg
#------------------------------------------------------------------------------

$(MAKEDIR)\obj\vcpkg\bootstrap-vcpkg.bat:
	@echo "========================================================"
	@echo "=== Checking out vcpkg                               ==="
	@echo "========================================================"

	git clone $(VCPKG_URL) $(MAKEDIR)\obj\vcpkg

$(MAKEDIR)\obj\vcpkg\vcpkg.exe: $(MAKEDIR)\obj\vcpkg\bootstrap-vcpkg.bat
	@echo "========================================================"
	@echo "=== Building vcpkg                                   ==="
	@echo "========================================================"
	cd $(MAKEDIR)\obj\vcpkg
	git checkout $(VCPKG_TAG)
	cd $(MAKEDIR)
	
	$(MAKEDIR)\obj\vcpkg\bootstrap-vcpkg.bat

#------------------------------------------------------------------------------
# libssh2
#------------------------------------------------------------------------------

$(MAKEDIR)\obj\vcpkg\installed\libssh2-x86-windows-mixed\bin\libssh2.dll: $(MAKEDIR)\obj\vcpkg\vcpkg.exe
	@echo "========================================================"
	@echo "=== Building libssh2                                 ==="
	@echo "========================================================"
	$(MAKEDIR)\obj\vcpkg\vcpkg.exe install libssh2 \
		--triplet libssh2-x86-windows-mixed \
		--overlay-ports=$(MAKEDIR)\vcpkg-ports \
		--overlay-triplets=$(MAKEDIR)\vcpkg-triplets

$(MAKEDIR)\obj\vcpkg\installed\libssh2-x86-windows-mixed\bin\libssh2.$(LIBSSH2_VERSION).nupkg: \
		$(MAKEDIR)\obj\vcpkg\installed\libssh2-x86-windows-mixed\bin\libssh2.dll
	@echo "========================================================"
	@echo "=== Building libssh2 nuget package                   ==="
	@echo "========================================================"
	nuget pack -OutputDirectory $(MAKEDIR)\obj\vcpkg\installed\libssh2-x86-windows-mixed\bin\ <<libssh2.nuspec
<?xml version="1.0"?>
<package>
  <metadata>
    <id>libssh2</id>
    <version>$(LIBSSH2_VERSION)</version>
    <authors>https://www.libssh2.org/</authors>
    <owners>https://www.libssh2.org/</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>libssh2 is a client-side C library implementing the SSH2 protocol</description>
	<tags>Native, native</tags>
  </metadata>
  <files>
	<!-- pretend the library is platform-neutral -->
    <file src="obj\vcpkg\installed\libssh2-x86-windows-mixed\bin\libssh2.dll" target="build" />
    <file src="obj\vcpkg\installed\libssh2-x86-windows-mixed\bin\libssh2.dll" target="build\net46" />
    <file src="libssh2.targets" target="build" />
  </files>
</package>
<<NOKEEP

libssh2: $(MAKEDIR)\obj\vcpkg\installed\libssh2-x86-windows-mixed\bin\libssh2.$(LIBSSH2_VERSION).nupkg $(MAKEDIR)\NuGetPackages
	@echo "========================================================"
	@echo "=== Publishing libssh2 nuget package                 ===
	@echo "========================================================"
	nuget add \
		$(MAKEDIR)\obj\vcpkg\installed\libssh2-x86-windows-mixed\bin\libssh2.$(LIBSSH2_VERSION).nupkg \
		-Source $(MAKEDIR)\NuGetPackages

clean-libssh2: $(MAKEDIR)\obj\vcpkg\vcpkg.exe
	@echo "========================================================"
	@echo "=== Cleaning libssh2                                 ==="
	@echo "========================================================"
	$(MAKEDIR)\obj\vcpkg\vcpkg.exe remove libssh2 \
		--triplet libssh2-x86-windows-mixed \
		--overlay-ports=$(MAKEDIR)\vcpkg-ports \
		--overlay-triplets=$(MAKEDIR)\vcpkg-triplets
	rd /S /Q $(LOCALAPPDATA)\vcpkg

#------------------------------------------------------------------------------
# Main targets
#------------------------------------------------------------------------------

dependencies: vtnetcore libssh2
!if ( "$(KOKORO_BUILD_NUMBER)" != "" ) 
    gsutil cp -r NuGetPackages/* gs://$(PACKAGES_BUCKET)/NuGetPackages/
!endif

clean:
	rd /S /Q obj
	rd /S /Q NuGetPackages
