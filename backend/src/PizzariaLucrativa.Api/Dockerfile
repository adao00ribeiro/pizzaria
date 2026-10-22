# ---- build ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore em camada separada (cache de dependências).
COPY src/PizzariaLucrativa.Domain/PizzariaLucrativa.Domain.csproj src/PizzariaLucrativa.Domain/
COPY src/PizzariaLucrativa.Application/PizzariaLucrativa.Application.csproj src/PizzariaLucrativa.Application/
COPY src/PizzariaLucrativa.Infrastructure/PizzariaLucrativa.Infrastructure.csproj src/PizzariaLucrativa.Infrastructure/
COPY src/PizzariaLucrativa.Api/PizzariaLucrativa.Api.csproj src/PizzariaLucrativa.Api/
RUN dotnet restore src/PizzariaLucrativa.Api/PizzariaLucrativa.Api.csproj

COPY src/ src/
RUN dotnet publish src/PizzariaLucrativa.Api/PizzariaLucrativa.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- runtime -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

ENTRYPOINT ["dotnet", "PizzariaLucrativa.Api.dll"]
