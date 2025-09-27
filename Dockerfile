# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
LABEL stage=build

WORKDIR /build

# Copy source
COPY . .

# Tools & DB repo
RUN apt-get update \
 && apt-get install -y unzip git sed \
 && git config --global http.sslVerify false \
 && git clone https://github.com/OpenDAoC/OpenDAoC-Database.git /tmp/opendaoc-db \
 && rm -rf /var/lib/apt/lists/*

# Combine SQL files
WORKDIR /tmp/opendaoc-db/opendaoc-db-core
RUN cat *.sql > combined.sql

# Back to build root
WORKDIR /build

# Copy serverconfig example -> working config
RUN cp /build/CoreServer/config/serverconfig.example.xml /build/CoreServer/config/serverconfig.xml

# Build
RUN dotnet build DOLLinux.sln -c Release

# ---- final ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final
LABEL stage=final

# Runtime deps:
# - icu-libs: globalization
# - su-exec: drop privileges
# - shadow: usermod/groupdel for safe UID/GID/group tweaks
RUN apk add --no-cache icu-libs su-exec shadow

WORKDIR /app

# App binaries
COPY --from=build /build/Release /app

# Combined DB SQL
COPY --from=build /tmp/opendaoc-db/opendaoc-db-core/combined.sql /tmp/opendaoc-db/combined.sql

# Entrypoint (make sure your repo has the updated safe entrypoint.sh)
COPY --from=build /build/entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

ENTRYPOINT ["/bin/sh", "/app/entrypoint.sh"]
