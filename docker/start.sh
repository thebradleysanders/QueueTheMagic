#!/bin/sh
set -e

# Generate nginx config from template — only substitute our two port variables.
# nginx's own $host, $remote_addr, etc. are left untouched.
envsubst '${APP_PORT} ${API_PORT}' \
  < /etc/nginx/templates/default.conf.template \
  > /etc/nginx/conf.d/default.conf

# Bind the API explicitly to the IPv4 loopback so nginx can always reach it.
# Using "localhost" can bind to ::1 (IPv6) while nginx connects to 127.0.0.1 (IPv4).
export ASPNETCORE_URLS="http://127.0.0.1:${API_PORT}"

exec /usr/bin/supervisord -n -c /etc/supervisor/conf.d/qtm.conf
