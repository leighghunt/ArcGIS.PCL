#!/bin/bash

mono .nuget/NuGet.exe update -self

mono .nuget/NuGet.exe install FAKE -OutputDirectory packages -ExcludeVersion

mono .nuget/NuGet.exe install xunit.runners -pre -OutputDirectory packages/FAKE -ExcludeVersion

mono packages/FAKE/tools/FAKE.exe build.fsx RunTestsMono

