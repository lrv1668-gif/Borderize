# syntax=docker/dockerfile:1

# Build stage: restore + compile the solution.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only project/solution files first so `restore` is its own cached layer
# (re-used across builds whenever no .csproj/.slnx changed).
COPY Borderize.slnx Borderize.csproj ./
COPY Borderize.Tests/Borderize.Tests.csproj Borderize.Tests/
RUN dotnet restore Borderize.slnx

# Copy the rest of the sources and build.
COPY . .
RUN dotnet build Borderize.slnx -c Release --no-restore

# Test stage: run the xUnit suite. A failing test fails `docker build`, gating CI.
FROM build AS test
RUN dotnet test Borderize.slnx -c Release --no-restore --verbosity normal
