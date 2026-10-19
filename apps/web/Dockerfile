FROM node:24-alpine AS build
WORKDIR /app

COPY apps/web/package.json apps/web/package-lock.json ./apps/web/
COPY packages/api-client ./packages/api-client

WORKDIR /app/apps/web
RUN npm ci --no-audit --no-fund --install-links

COPY apps/web ./
RUN npm run build

FROM node:24-alpine AS runtime
WORKDIR /app/apps/web

ENV NODE_ENV=production
ENV PORT=3000
ENV NEXT_TELEMETRY_DISABLED=1

COPY --from=build /app/apps/web/.next ./.next
COPY --from=build /app/apps/web/node_modules ./node_modules
COPY --from=build /app/apps/web/package.json ./
COPY --from=build /app/packages/api-client /app/packages/api-client

EXPOSE 3000

CMD ["npx", "next", "start", "-p", "3000"]
