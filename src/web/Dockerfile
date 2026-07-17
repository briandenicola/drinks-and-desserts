# ── Build stage ──────────────────────────────────────────
FROM node:26-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci --ignore-scripts
COPY . .
RUN npm run build

# ── Runtime stage (nginx on alpine) ──
FROM nginx:stable-alpine-slim AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/templates/default.conf.template

# Default API upstream (Docker Compose service name)
ENV API_HOST=whiskey-and-smokes-api
ENV API_PORT=8080

# Non-root nginx (UID 101) needs write access to render the config template
# into conf.d and to create its cache dirs at startup.
RUN chown -R 101:101 /var/cache/nginx /etc/nginx/conf.d /var/run \
    && touch /var/run/nginx.pid \
    && chown 101:101 /var/run/nginx.pid

# Run as non-root user (nginx user in alpine, UID 101)
USER 101

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
