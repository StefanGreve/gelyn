SOLUTION = Gelyn.slnx
MAIN_PROJECT = ./src/Gelyn

# A 32-bit make sees x86 and finds the host architecture in PROCESSOR_ARCHITEW6432.
ifeq ($(OS),Windows_NT)
PLATFORM = win
ifneq ($(filter ARM64,$(PROCESSOR_ARCHITECTURE) $(PROCESSOR_ARCHITEW6432)),)
ARCHITECTURE = arm64
else
ARCHITECTURE = x64
endif
else
UNAME_S = $(shell uname -s)
UNAME_M = $(shell uname -m)

ifeq ($(UNAME_S),Darwin)
PLATFORM = osx
else ifeq ($(UNAME_S),Linux)
PLATFORM = linux
else
PLATFORM = win
endif

ifneq ($(filter $(UNAME_M),arm64 aarch64),)
ARCHITECTURE = arm64
else
ARCHITECTURE = x64
endif
endif

# Without SHELL, make on Windows runs recipes with any sh.exe on PATH, such as Git's.
ifeq ($(PLATFORM),win)
SHELL := cmd.exe
BINARY = gelyn.exe
CLEAR = cls
NULL = nul
DELETE_PACKAGES = if exist artifacts\nupkg del /q artifacts\nupkg\Gelyn*.nupkg
else
BINARY = gelyn
CLEAR = clear
NULL = /dev/null
DELETE_PACKAGES = rm -f artifacts/nupkg/Gelyn*.nupkg
endif

# Native AOT cannot cross-compile between operating systems, so the host decides the runtime.
# Override it as `make publish RID=linux-x64` to pick another of the identifiers Gelyn.csproj targets.
RID ?= $(PLATFORM)-$(ARCHITECTURE)

GELYN = $(MAIN_PROJECT)/bin/Release/net10.0/$(RID)/publish/$(BINARY)

ifeq ($(PLATFORM),win)
GELYN := $(subst /,\,$(GELYN))
endif

# Absent rather than zero, which the serve command rejects as a reload loop.
REFRESH = $(if $(refresh),--refresh $(refresh))

# The site to build
project ?= docs

SITE_CONFIG = ./$(project)/gelyn.json

# Succeeds whether or not the tool is installed.
UNINSTALL_TOOL = dotnet tool uninstall --global Gelyn >$(NULL) 2>&1 || exit 0

setup:
	: local tools, and the git hooks one of them installs
	dotnet tool restore
	dotnet tool run husky install

	: restore project packages
	dotnet restore $(SOLUTION)

	: enforced git settings
	git config --local commit.gpgsign true
	git config --local tag.gpgsign true
	git config --local fetch.prune true

build:
	dotnet build $(SOLUTION) --configuration Release --no-restore

test: build
	dotnet test $(SOLUTION) --configuration Release --no-build

publish:
	dotnet publish $(MAIN_PROJECT) --runtime $(RID)

install:
	: the pointer package and the one for RID, with no older version beside them for --prerelease to prefer
	$(DELETE_PACKAGES)
	dotnet pack $(MAIN_PROJECT)
	dotnet pack $(MAIN_PROJECT) --runtime $(RID)

	: an installed copy of the same version would count as up to date, so it goes first
	$(UNINSTALL_TOOL)
	dotnet tool install --global Gelyn --prerelease

gelyn: publish
ifeq ($(serve),true)
	$(GELYN) serve --config $(SITE_CONFIG) --launch-browser $(REFRESH)
else
	$(GELYN) build --config $(SITE_CONFIG)
endif

lint:
	dotnet format $(SOLUTION)

clean:
	dotnet clean
	dotnet clean --configuration Release
	$(UNINSTALL_TOOL)
	@$(CLEAR)

.PHONY: setup build test publish install gelyn lint clean

$(V).SILENT:
