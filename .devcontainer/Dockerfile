FROM mcr.microsoft.com/devcontainers/base:2-noble

SHELL ["/bin/bash", "-o", "pipefail", "-c"]

# clang links the Native AOT binary.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends clang make \
    && rm -rf /var/lib/apt/lists/*

# Copied alone, so that this layer rebuilds exactly when the SDK pin moves.
COPY global.json /tmp/global.json

RUN curl -fsSL https://dot.net/v1/dotnet-install.sh \
    | bash /dev/stdin --jsonfile /tmp/global.json --install-dir /usr/share/dotnet --no-path \
    && ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet \
    && rm /tmp/global.json

ENV DOTNET_ROOT=/usr/share/dotnet
ENV PATH="${PATH}:/home/vscode/.dotnet/tools"
