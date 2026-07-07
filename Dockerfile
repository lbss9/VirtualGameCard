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

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "VirtualGameCard.Api.dll"]
