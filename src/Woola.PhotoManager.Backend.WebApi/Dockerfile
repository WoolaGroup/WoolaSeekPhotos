FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY . .
RUN dotnet restore src/Woola.PhotoManager.Backend.WebApi/Woola.PhotoManager.Backend.WebApi.csproj
RUN dotnet publish src/Woola.PhotoManager.Backend.WebApi/Woola.PhotoManager.Backend.WebApi.csproj -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .

EXPOSE 5150
ENV ASPNETCORE_URLS=http://+:5150
ENV WoolaPhotos__DatabasePath=/data/photos.db
ENV WoolaPhotos__ThumbnailDirectory=/data/thumbnails

VOLUME ["/data"]

ENTRYPOINT ["dotnet", "Woola.PhotoManager.Backend.WebApi.dll"]
