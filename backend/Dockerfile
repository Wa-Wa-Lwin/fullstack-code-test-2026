# Build context: backend/ (see the api service in docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project files only, so the restore layer is cached until a
# package reference changes.
COPY PaymentIntegration.Api/PaymentIntegration.Api.csproj PaymentIntegration.Api/
COPY PaymentIntegration.Application/PaymentIntegration.Application.csproj PaymentIntegration.Application/
COPY PaymentIntegration.Domain/PaymentIntegration.Domain.csproj PaymentIntegration.Domain/
COPY PaymentIntegration.Infrastructure/PaymentIntegration.Infrastructure.csproj PaymentIntegration.Infrastructure/
RUN dotnet restore PaymentIntegration.Api/PaymentIntegration.Api.csproj

COPY PaymentIntegration.Api/ PaymentIntegration.Api/
COPY PaymentIntegration.Application/ PaymentIntegration.Application/
COPY PaymentIntegration.Domain/ PaymentIntegration.Domain/
COPY PaymentIntegration.Infrastructure/ PaymentIntegration.Infrastructure/
RUN dotnet publish PaymentIntegration.Api/PaymentIntegration.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "PaymentIntegration.Api.dll"]
