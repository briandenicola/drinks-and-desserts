#!/bin/bash

# Runs at Codespace/devcontainer creation

echo "$(date)    post-create start" >> ~/status

# Install Task runner
sh -c "$(curl --location https://taskfile.dev/install.sh)" -- -d -b /usr/local/bin

# Restore .NET dependencies
dotnet restore src/WhiskeyAndSmokes.sln

# Install Vue frontend dependencies
cd src/web && npm ci && cd ../..

echo "$(date)    post-create complete" >> ~/status
