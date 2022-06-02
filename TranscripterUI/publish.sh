#!/bin/bash

dotnet publish -c  release -r "$1" --self-contained true
