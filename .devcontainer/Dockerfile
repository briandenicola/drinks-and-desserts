ARG VARIANT=10.0
FROM mcr.microsoft.com/devcontainers/dotnet:1-${VARIANT}

ARG NODE_VERSION="22"
RUN if [ "${NODE_VERSION}" != "none" ]; then su vscode -c ". /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
