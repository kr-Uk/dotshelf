# 멀티스테이지: 의존성 -> 빌드 -> 런타임. next.config.ts 의 output: 'standalone' 산출물만 복사.
# SSAFY 서버는 amd64. backend Dockerfile 과 동일하게 timezone Asia/Seoul 고정.

FROM node:22-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json* ./
RUN if [ -f package-lock.json ]; then npm ci; else npm install; fi

FROM node:22-alpine AS builder
WORKDIR /app
ENV NEXT_TELEMETRY_DISABLED=1
COPY --from=deps /app/node_modules ./node_modules
COPY . .
RUN npm run build

FROM node:22-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV PORT=3000
ENV HOSTNAME=0.0.0.0
ENV TZ=Asia/Seoul

RUN apk add --no-cache curl tzdata \
    && cp /usr/share/zoneinfo/Asia/Seoul /etc/localtime \
    && echo "Asia/Seoul" > /etc/timezone \
    && addgroup --system --gid 1001 nodejs \
    && adduser --system --uid 1001 nextjs

# standalone 출력은 server.js + 최소 node_modules 만 포함. public/.next/static 은 별도 복사.
COPY --from=builder --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /app/.next/static ./.next/static
COPY --from=builder --chown=nextjs:nodejs /app/public ./public

USER nextjs

EXPOSE 3000

HEALTHCHECK --interval=20s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -fsS http://localhost:3000/ || exit 1

CMD ["node", "server.js"]
