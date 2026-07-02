FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/HelpdeskTicketManagement.Domain/HelpdeskTicketManagement.Domain.csproj", "src/HelpdeskTicketManagement.Domain/"]
COPY ["src/HelpdeskTicketManagement.Application/HelpdeskTicketManagement.Application.csproj", "src/HelpdeskTicketManagement.Application/"]
COPY ["src/HelpdeskTicketManagement.Infrastructure/HelpdeskTicketManagement.Infrastructure.csproj", "src/HelpdeskTicketManagement.Infrastructure/"]
COPY ["src/HelpdeskTicketManagement.Api/HelpdeskTicketManagement.Api.csproj", "src/HelpdeskTicketManagement.Api/"]
RUN dotnet restore "src/HelpdeskTicketManagement.Api/HelpdeskTicketManagement.Api.csproj"
COPY . .
RUN dotnet publish "src/HelpdeskTicketManagement.Api/HelpdeskTicketManagement.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HelpdeskTicketManagement.Api.dll"]
