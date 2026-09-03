FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY WebApplication1/WebApplication1.csproj WebApplication1/
RUN dotnet restore WebApplication1/WebApplication1.csproj

COPY WebApplication1/ WebApplication1/
RUN dotnet publish WebApplication1/WebApplication1.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:$PORT
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5000

ENTRYPOINT ["dotnet", "WebApplication1.dll"]
