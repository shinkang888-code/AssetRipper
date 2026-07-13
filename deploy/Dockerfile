# Self-host AssetRipper's web GUI (includes PMKE encrypted-bundle support).
#
# Build:  docker build -f deploy/Dockerfile -t assetripper-web .
# Run:    docker run --rm -p 8080:8080 assetripper-web
# Open:   http://localhost:8080
#
# NOTE: AssetRipper's web GUI is a single-user tool that loads an entire game into
# server memory and has no authentication. Only expose it on a trusted network or
# behind an authenticating reverse proxy. AR_BIND_HOST=0.0.0.0 makes it reachable
# from outside the container (the app binds 127.0.0.1 by default).

# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
# Framework-dependent publish (PublishAot is disabled here so the image builds without
# the native AOT toolchain; the shipped desktop build still uses AOT).
RUN dotnet publish Source/AssetRipper.GUI.Free/AssetRipper.GUI.Free.csproj \
    -c Release -o /app \
    -p:PublishAot=false -p:PublishSingleFile=false --self-contained false

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Expose the server outside the container and pick a fixed port.
ENV AR_BIND_HOST=0.0.0.0
EXPOSE 8080

# --headless disables the automatic browser launch (there is no desktop in a container).
ENTRYPOINT ["dotnet", "AssetRipper.GUI.Free.dll", "--headless", "--port", "8080"]
