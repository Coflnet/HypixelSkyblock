#!/bin/bash
dotnet pack -c Release
dotnet nuget push bin/Release/*.nupkg --api-key $NUGET_API_KEY --source "nuget.org" --skip-duplicate
