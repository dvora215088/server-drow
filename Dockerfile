# שלב בסיס - Runtime Environment
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

# הגדרת URL שהאפליקציה מאזינה עליו
ENV ASPNETCORE_URLS=http://+:8080

# שלב בנייה - SDK Environment
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# העתקת קובץ ה-proj כדי להשתמש ב-caching
COPY ["serverDrow.csproj", "./"]
RUN dotnet restore "serverDrow.csproj"

# העתקת שאר הקוד ובנייה
COPY . .
RUN dotnet build "serverDrow.csproj" -c Release -o /app/build

# שלב פרסום
FROM build AS publish
RUN dotnet publish "serverDrow.csproj" -c Release -o /app/publish /p:UseAppHost=false

# שלב הרצה - העתקת קבצים מהפרסום
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# בדיקת בריאות - מוודא שהשרת זמין
HEALTHCHECK --interval=30s --timeout=10s --retries=3 CMD curl --fail http://localhost:8080 || exit 1

# נקודת הכניסה
ENTRYPOINT ["dotnet", "serverDrow.dll"]
