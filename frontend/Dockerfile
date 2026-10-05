# Contenedor "frontend" (arquitectura física del Capítulo IV).
# Build multi-stage: Node compila el bundle de producción de Vite/React y Nginx lo sirve por HTTPS,
# además de reenviar /api/* al contenedor de la API.

FROM node:20-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine AS runtime
RUN apk add --no-cache openssl
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY certificado-autofirmado.sh /docker-entrypoint.d/40-certificado-autofirmado.sh
RUN chmod +x /docker-entrypoint.d/40-certificado-autofirmado.sh

EXPOSE 80 443
