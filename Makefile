SOLUTION = Gelyn.slnx
MAIN_PROJECT = ./src/Gelyn
THEMES = ./plugins/gelyn/skills/bootstrap/themes
EXAMPLE = ./example
DOCS = ./docs

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

# The site to build
project ?= docs

SITE_CONFIG = ./$(project)/gelyn.json

setup:
	# local tools, and the git hooks one of them installs
	dotnet tool restore
	dotnet tool run husky install

	# restore project packages
	dotnet restore $(SOLUTION)

	# the stylesheets and scripts the example and doc sites build with
	$(MAKE) themes

	# enforced git settings
	git config --local commit.gpgsign true
	git config --local tag.gpgsign true
	git config --local fetch.prune true

themes:
	cp -f $(THEMES)/classic.css $(EXAMPLE)/
	cp -f $(THEMES)/modern.css $(DOCS)/
	cp -f $(THEMES)/modern.js $(DOCS)/

build:
	dotnet build $(SOLUTION) --configuration Release --no-restore

test: build
	dotnet test $(SOLUTION) --configuration Release --no-build

publish:
	dotnet publish $(MAIN_PROJECT) --runtime $(RID)

gelyn: publish
	if [ "$(serve)" = "true" ]; then \
		$(GELYN) serve --config $(SITE_CONFIG) --launch-browser $(REFRESH); \
	else \
		$(GELYN) build --config $(SITE_CONFIG); \
	fi

lint:
	dotnet format $(SOLUTION)

clean:
	dotnet clean
	dotnet clean --configuration Release
	@clear

.PHONY: setup themes build test publish gelyn lint clean

$(V).SILENT:
