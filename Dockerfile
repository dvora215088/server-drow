FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5283

ENV ASPNETCORE_URLS=http://+:5283

USER app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["serverDrow.csproj", "./"]
RUN dotnet restore "serverDrow.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "serverDrow.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "serverDrow.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "serverDrow.dll"]
