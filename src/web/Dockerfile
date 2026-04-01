# ── Build stage ──────────────────────────────────────────
FROM node:22-alpine AS build
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

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
