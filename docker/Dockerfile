ARG VERSION=main

# ── Stage 1: Download source from GitHub ─────────────────────────────────────
FROM alpine:3.20 AS source
ARG VERSION
RUN apk add --no-cache curl \
 && mkdir /src \
 && if echo "$VERSION" | grep -qE '^v[0-9]'; then \
      URL="https://github.com/thebradleysanders/QueueTheMagic/archive/refs/tags/${VERSION}.tar.gz"; \
    else \
      URL="https://github.com/thebradleysanders/QueueTheMagic/archive/refs/heads/${VERSION}.tar.gz"; \
    fi \
 && curl -fsSL "$URL" | tar -xz --strip-components=1 -C /src

# ── Stage 2: Build Vue frontend ───────────────────────────────────────────────
FROM node:22-alpine AS web-build
WORKDIR /web
COPY --from=source /src/web/package*.json ./
RUN npm ci
COPY --from=source /src/web ./
RUN npm run build

# ── Stage 3: Build .NET API ───────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS api-build
WORKDIR /src
COPY --from=source /src/api/QueueTheMagic.csproj ./
RUN dotnet restore
COPY --from=source /src/api ./
RUN dotnet publish -c Release -o /app/publish --no-restore

# ── Stage 4: Runtime image ────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0
RUN apt-get update \
 && apt-get install -y --no-install-recommends nginx supervisor curl gettext-base \
 && rm -rf /var/lib/apt/lists/* \
 && rm -f /etc/nginx/sites-enabled/default

COPY --from=api-build /app/publish /app/api
COPY --from=web-build /web/dist /usr/share/nginx/html
COPY --from=source /src/docker/nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=source /src/docker/supervisord.conf /etc/supervisor/conf.d/qtm.conf
COPY --from=source /src/docker/start.sh /start.sh
RUN chmod +x /start.sh

RUN mkdir -p /app/data

# Defaults that users should never need to change
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/xlights.db"
ENV Jwt__Issuer=QueueTheMagic

# Port defaults — override via docker-compose or docker run -e
ENV APP_PORT=80
ENV API_PORT=5288

EXPOSE ${APP_PORT}

CMD ["/start.sh"]
