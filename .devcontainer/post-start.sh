#!/bin/bash

# Runs each time the container starts

echo "$(date)    post-start start" >> ~/status

echo "$(date)    Checking Azure CLI" >> ~/status
az --version 2>/dev/null | head -1 >> ~/status

echo "$(date)    post-start complete" >> ~/status