# syntax=docker/dockerfile:1.7

FROM node:24-alpine AS dependencies
WORKDIR /app
COPY AqualLifeStyle/9.4.2/aqua-frontend/package.json AqualLifeStyle/9.4.2/aqua-frontend/package-lock.json ./
RUN npm ci

FROM node:24-alpine AS build
WORKDIR /app
ENV NEXT_TELEMETRY_DISABLED=1
ARG NEXT_PUBLIC_ABP_API_URL
ARG NEXT_PUBLIC_DEFAULT_TENANT_NAME=Default
ARG NEXT_PUBLIC_MONITORING_ENDPOINT
ENV NEXT_PUBLIC_ABP_API_URL=$NEXT_PUBLIC_ABP_API_URL \
    NEXT_PUBLIC_DEFAULT_TENANT_NAME=$NEXT_PUBLIC_DEFAULT_TENANT_NAME \
    NEXT_PUBLIC_MONITORING_ENDPOINT=$NEXT_PUBLIC_MONITORING_ENDPOINT
COPY --from=dependencies /app/node_modules ./node_modules
COPY AqualLifeStyle/9.4.2/aqua-frontend/ ./
RUN npm run build

FROM node:24-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    HOSTNAME=0.0.0.0 \
    PORT=3000

COPY --from=build --chown=node:node /app/public ./public
COPY --from=build --chown=node:node /app/.next/standalone ./
COPY --from=build --chown=node:node /app/.next/static ./.next/static

USER node
EXPOSE 3000

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD ["node", "-e", "fetch('http://127.0.0.1:3000/').then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))"]

CMD ["node", "server.js"]
