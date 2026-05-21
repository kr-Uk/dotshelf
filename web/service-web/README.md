# service-web

`dotshelf.digital` / `www.dotshelf.digital` 의 공개 랜딩.

- Next.js 16 App Router, Tailwind v4, shadcn 컨벤션.
- 인증/BFF 없음 (현재 단계). 추후 로그인 진입점이 생기면 BFF 라우트(`app/api/auth/*`, `app/api/proxy/[...path]`) 를 creator-web/admin-web 와 동일 패턴으로 추가.
- 컨테이너는 `output: 'standalone'` 빌드 산출물만 사용 (Dockerfile 참고).

## 로컬

```bash
npm install
npm run dev      # http://localhost:3000
```

## 배포

SSAFY app 서버에서 `docker compose up -d --build service-web` (`backend/deploy/app/docker-compose.yml`).
prod Nginx 가 Tailscale 로 `100.74.2.103:3002` 로 프록시.
