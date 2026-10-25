# Builds and runs only the Web API. The MAUI client needs platform
# workloads and isn't part of the hosted demo.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY LMS/CLI.LMS.Core/CLI.LMS.Core.csproj LMS/CLI.LMS.Core/
COPY LMS/CLI.LMS.Api/CLI.LMS.Api.csproj LMS/CLI.LMS.Api/
RUN dotnet restore LMS/CLI.LMS.Api/CLI.LMS.Api.csproj
COPY LMS/CLI.LMS.Core/ LMS/CLI.LMS.Core/
COPY LMS/CLI.LMS.Api/ LMS/CLI.LMS.Api/
RUN dotnet publish LMS/CLI.LMS.Api/CLI.LMS.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
# The aspnet image runs as the non-root "app" user, so give it the data dir.
RUN mkdir -p /app/data && chown app /app/data
USER app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Database__Path=/app/data/lms-api.db
EXPOSE 8080
ENTRYPOINT ["dotnet", "CLI.LMS.Api.dll"]
