# Builds either .NET service: docker build --build-arg PROJECT=Crawler.Api|Crawler.Worker .
ARG PROJECT=Crawler.Api

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT
WORKDIR /src

# Restore first, from project files only, so the layer is cached until dependencies change.
COPY global.json Directory.Build.props Directory.Build.targets Directory.Packages.props ./
COPY src/Crawler.Domain/Crawler.Domain.csproj src/Crawler.Domain/
COPY src/Crawler.Contracts/Crawler.Contracts.csproj src/Crawler.Contracts/
COPY src/Crawler.Infrastructure/Crawler.Infrastructure.csproj src/Crawler.Infrastructure/
COPY src/${PROJECT}/${PROJECT}.csproj src/${PROJECT}/
RUN dotnet restore src/${PROJECT}/${PROJECT}.csproj

COPY src/ src/
RUN dotnet publish src/${PROJECT}/${PROJECT}.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
ARG PROJECT
# curl is only used by the container health check.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
# Optional extra CAs for TLS-intercepting proxies (see certs/README.md); no-op when the folder has no *.crt.
COPY certs/ /usr/local/share/ca-certificates/extra/
RUN update-ca-certificates
WORKDIR /app
COPY --from=build /app .
ENV APP_DLL=${PROJECT}.dll
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]
