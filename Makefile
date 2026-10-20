MAIN_PROJECT = ./src/Gelyn
THEMES = ./plugins/gelyn/skills/bootstrap/themes
EXAMPLE = ./example
DOCS_CONFIG = ./docs/gelyn.json

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

ifeq ($(PLATFORM),win)
BINARY = gelyn.exe
else
BINARY = gelyn
endif

# Native AOT cannot cross-compile between operating systems, so the host decides the runtime.
# Override it as `make publish RID=linux-x64` to pick another of the identifiers Gelyn.csproj targets.
RID ?= $(PLATFORM)-$(ARCHITECTURE)

GELYN = $(MAIN_PROJECT)/bin/Release/net10.0/$(RID)/publish/$(BINARY)

# Absent rather than zero, which the serve command rejects as a reload loop.
REFRESH = $(if $(refresh),--refresh $(refresh))

setup:
	# local tools, and the git hooks one of them installs
	dotnet tool restore
	dotnet tool run husky install

	# the stylesheet the example site builds with, which is git-ignored
	cp -f $(THEMES)/classic.css $(EXAMPLE)/

build:
	dotnet build $(MAIN_PROJECT) --configuration Release --no-restore

publish:
	dotnet publish $(MAIN_PROJECT) --runtime $(RID)

docs: publish
	if [ "$(serve)" = "true" ]; then \
		$(GELYN) serve --config $(DOCS_CONFIG) --launch-browser $(REFRESH); \
	else \
		$(GELYN) build --config $(DOCS_CONFIG); \
	fi

lint:
	dotnet format

clean:
	dotnet clean --configuration Release
	@clear

.PHONY: setup build publish docs lint clean

$(V).SILENT:
