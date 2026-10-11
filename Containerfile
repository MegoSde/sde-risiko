FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY AlpacaMedic.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:7070
ENV DatabasePath=/data/alpacamedic.db
RUN mkdir -p /data
EXPOSE 7070
VOLUME /data
ENTRYPOINT ["dotnet", "AlpacaMedic.dll"]
