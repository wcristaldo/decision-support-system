# Arquitectura física (Capítulo IV de la tesis): contenedor "frontend".
# Build multi-stage: Node compila el bundle de producción de Vite/React,
# Nginx lo sirve como archivos estáticos.

FROM node:20-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 3000
