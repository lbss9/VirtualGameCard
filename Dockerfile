FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY VirtualGameCard.slnx ./
COPY VirtualGameCard.Api/VirtualGameCard.Api.csproj VirtualGameCard.Api/
COPY VirtualGameCard.Application/VirtualGameCard.Application.csproj VirtualGameCard.Application/
COPY VirtualGameCard.Domain/VirtualGameCard.Domain.csproj VirtualGameCard.Domain/
COPY VirtualGameCard.Infrastructure/VirtualGameCard.Infrastructure.csproj VirtualGameCard.Infrastructure/
RUN dotnet restore VirtualGameCard.Api/VirtualGameCard.Api.csproj

COPY . .
RUN dotnet publish VirtualGameCard.Api/VirtualGameCard.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Infisical CLI: permite que o container busque seus proprios segredos ao subir.
# Se as variaveis INFISICAL_* nao estiverem setadas, o app roda normal (portatil).
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl bash ca-certificates \
    && curl -1sLf 'https://artifacts-cli.infisical.com/setup.deb.sh' | bash \
    && apt-get install -y infisical \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish .
COPY docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh
RUN chmod +x /usr/local/bin/docker-entrypoint.sh

ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
