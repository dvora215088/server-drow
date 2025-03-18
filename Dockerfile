# שלב בסיסי עם ASP.NET Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

# שימוש בפורט שמוקצה על ידי Render
ENV ASPNETCORE_URLS=http://+:$PORT

# שלב הבנייה
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["serverDrow.csproj", "./"]
RUN dotnet restore "serverDrow.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "serverDrow.csproj" -c $configuration -o /app/build

# שלב הפרסום
FROM build AS publish
ARG configuration=Release
RUN dotnet publish "serverDrow.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

# שלב ההרצה הסופי
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "serverDrow.dll"]
