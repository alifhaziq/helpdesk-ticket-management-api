FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/HelpDeskPro.Domain/HelpDeskPro.Domain.csproj", "src/HelpDeskPro.Domain/"]
COPY ["src/HelpDeskPro.Application/HelpDeskPro.Application.csproj", "src/HelpDeskPro.Application/"]
COPY ["src/HelpDeskPro.Infrastructure/HelpDeskPro.Infrastructure.csproj", "src/HelpDeskPro.Infrastructure/"]
COPY ["src/HelpDeskPro.Api/HelpDeskPro.Api.csproj", "src/HelpDeskPro.Api/"]
RUN dotnet restore "src/HelpDeskPro.Api/HelpDeskPro.Api.csproj"
COPY . .
RUN dotnet publish "src/HelpDeskPro.Api/HelpDeskPro.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HelpDeskPro.Api.dll"]
