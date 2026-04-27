# GitLab CI/CD Variables — 복붙 가이드

**Settings → CI/CD → Variables** 에 그대로 넣기 위한 레시피.

## 공통 규칙 (Variable 추가 시)

| 필드 | 값 | 이유 |
|------|----|------|
| Type | **Variable** | `File` 로 하면 `$VAR` 가 "파일 경로" 가 되어서 `echo "$VAR"` 하면 경로 문자열이 찍힘 |
| Environments | `*` 또는 `All` | 환경 제한 없이 동작 |
| **Visibility** | **Visible** (Masked X) | 여러 줄(multiline)은 Masked 를 못 씀. Masked 는 단일 라인 + 특정 charset 에서만 작동 |
| **Flags → Protect variable** | **체크** | Protected 브랜치 (`dev`) 에서만 노출. 보안 필수 |
| **Flags → Expand variable reference** | 기본값 (체크) | |
| **Description** | 적당히 적기 | |

> ⚠️ **Masked 는 켜지 말 것.** 키/known_hosts 가 multiline 이라 Masked 에서 저장이 안 되거나 잘림. "Protected" 만으로도 dev 이외에는 노출 안 됨.

---

## 변수 목록 14개

### 1. `SSH_PRIVATE_KEY`

- **Type**: Variable
- **Protected**: ✅
- **Masked**: ❌
- **Value**: 로컬 `C:\PROJECT\DOTSHELF\server\dotshelf-deploy` 파일 전체 내용 그대로 복사 (아래 형태)

```
-----BEGIN OPENSSH PRIVATE KEY-----
...내용 한 덩어리...
-----END OPENSSH PRIVATE KEY-----
```

> **주의**: 앞뒤 공백·빈 줄 포함 여부에 민감. 파일 그대로 열어서 전체 복사 → GitLab 에 붙여넣기. 끝에 빈 줄 하나는 있어도 됨.

### 2. `EC2_KNOWN_HOSTS`

- **Type**: Variable
- **Protected**: ✅
- **Masked**: ❌
- **Value**: 아래 3줄 전체 복사 (2026-04-22 기준, `3.36.72.58` = SSAFY 서버 = 새 App 서버 = deploy target)

```
3.36.72.58 ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQCoq+panXp6uNLg8FNOfmrJaUXjfINA6n1NEv765stZ4zlJJtwa/PDuSWUm8V+JiqTYDvimrJp/QmsnpjPCdvGL+oMQ46Oy7ryEUJWPiC/311yttL9DOPVg7pqrEMsdeGnhdbN75kL/9VoziirXSi1pUltKjR9iD1+RZrIdMGmnfLW0H+w/yIB/DhUGGHrNfLNcN9UXgC7uY7iZCknWUjkN9BMqJPRgUka1Azh09KcNC2Uv5M7fSnvsnK37NC9y8ahoIkR6DltjpXMIJgE/XgwYSnYbW/ymPPykodyuSYFv79XoKQC7Qa9nQFi0dVX//pfhZR2OEHx5pKdHx56zdlQ5f9fwlGltzQM3W3ilxB0rR3hfEtYZAT7I6S+ROZkgmD5CAmcnNCpZRMC1OxgMcWt2lrmVdeCFT8L21SzL7OKmCvFtq5g1Qe4c/g7u2QSlCR7PJyNGDnwGX0Bfb7psPXT3uWB0AACe+0hXUtVXMabS938NzRAjA/CAASuSLJ0gsm0=
3.36.72.58 ecdsa-sha2-nistp256 AAAAE2VjZHNhLXNoYTItbmlzdHAyNTYAAAAIbmlzdHAyNTYAAABBBGuqfnDGJxV2K1hLqsVjWtCpG/dYtdtJjQaneJEfShUwcoKQYdfMom3VmrfKWu3LCooq14rhRg+xSvdeuKWuIKg=
3.36.72.58 ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIILYbdib4qvmUkmr3zAa8XRb8ZfqMcouybVVflHE1pAi
```

> 🔄 **재생성이 필요할 때** (SSAFY sshd 호스트키 바뀐 경우 — 서버 재생성 등):
> ```
> ssh-keyscan -t ed25519,rsa,ecdsa 3.36.72.58 2>/dev/null
> ```

### 3. `EC2_HOST`

- **Type**: Variable
- **Protected**: ✅
- **Masked**: ❌ (IP 라 Masked 걸어도 되긴 하지만 불필요)
- **Value**:

```
3.36.72.58
```

> SSAFY 서버 (`k14f206.p.ssafy.io`) 의 Public IP. GitLab Runner → SSH → SSAFY 서버로 JAR 전달 후 `docker compose up -d --build`. **prod 가 아닌 SSAFY 서버가 deploy target** 인 이유: 2026-04-22 에 app/DB 서버를 분리하면서 Spring Boot 컨테이너를 SSAFY 로 이전했기 때문. 자세한 배경은 [2026-04-22-tailscale-app-db-split.md](./2026-04-22-tailscale-app-db-split.md) 참조.

### 4. `EC2_USER`

- **Type**: Variable
- **Protected**: ✅
- **Masked**: ❌
- **Value**:

```
ubuntu
```

### 5. `EC2_DEPLOY_PATH`

- **Type**: Variable
- **Protected**: ✅
- **Masked**: ❌
- **Value**:

```
/opt/dotshelf
```

### 6. `GOOGLE_SERVER_CLIENT_ID`

- **Type**: Variable
- **Protected**: ✅
- **Masked**: ❌
- **Value**: Google Cloud Console 의 Web OAuth Client ID

> BE 가 Google authorization code 를 token endpoint 에서 교환할 때 사용하는 server client id.

### 7. `GOOGLE_CLIENT_ID`

- **Type**: Variable
- **Protected**: ✅
- **Masked**: ❌
- **Value**: Google Cloud Console 의 Web OAuth Client ID

> `GOOGLE_SERVER_CLIENT_ID` 와 같은 값을 넣어도 된다. `application.yml` fallback 과 문서 가독성을 위해 둘 다 명시한다.

### 8. `GOOGLE_CLIENT_SECRET`

- **Type**: Variable
- **Protected**: ✅
- **Masked**: ✅
- **Value**: Google Web OAuth Client Secret

> Google authorization code 교환 시 필요하다. 절대 git에 커밋하지 않는다.

### 9. `GOOGLE_CLIENT_IDS`

- **Type**: Variable
- **Protected**: ✅
- **Masked**: ❌
- **Value**: BE 가 허용할 Google OAuth Client ID 전체 목록

```
{web-client-id},{android-client-id},{desktop-client-id},{ios-client-id}
```

> Google `id_token` 의 `aud` 검증에 사용한다. Unity 플랫폼별 client id 를 모두 포함해야 한다.

### 10. `KAKAO_REST_API_KEY`

- **Type**: Variable
- **Protected**: ✅
- **Masked**: 가능하면 ✅
- **Value**: Kakao Developers 의 REST API 키

> Desktop REST OAuth 에서 authorization code 를 access token 으로 교환할 때 사용한다.

### 11. `KAKAO_CLIENT_SECRET`

- **Type**: Variable
- **Protected**: ✅
- **Masked**: ✅
- **Value**: Kakao Developers 의 카카오 로그인 client secret

> 카카오 로그인 client secret 기능이 활성화되어 있으면 token API 호출에 필수다. 절대 git에 커밋하지 않는다.

### 12. `KAKAO_REDIRECT_URI`

- **Type**: Variable
- **Protected**: ✅
- **Masked**: ❌
- **Value**:

```
http://localhost:18080/oauth/kakao/callback
```

> BE가 브라우저 callback을 직접 받는 URI가 아니다. Desktop Unity 앱이 authorization code를 수신할 때 사용한 `redirect_uri`와 token exchange 시 BE가 카카오에 보내는 `redirect_uri`가 정확히 같아야 한다.
> Kakao Developers 의 카카오 로그인 리다이렉트 URI에도 같은 값을 등록해야 한다.

### 13. `APPLE_CLIENT_ID`

- **Type**: Variable
- **Protected**: ✅
- **Masked**: ❌
- **Value**: Apple Developer 의 iOS Bundle ID 또는 Web Services ID

```
com.jihocompany.dotshelf
```

> 단일 Apple `identityToken` audience 검증 기본값이다. iOS 앱에서 받은 토큰만 검증하면 Bundle ID 를 넣고, Web/Android/Desktop 용 Services ID 를 쓰면 해당 Services ID 를 넣는다.

### 14. `APPLE_CLIENT_IDS`

- **Type**: Variable
- **Protected**: ✅
- **Masked**: ❌
- **Value**: BE 가 허용할 Apple Client ID 전체 목록

```
com.jihocompany.dotshelf,com.jihocompany.dotshelf.signin
```

> Apple `identityToken` 의 `aud` 검증에 사용한다. iOS Bundle ID 와 Android/Desktop/Web 에서 사용하는 Services ID 가 다르면 모두 포함해야 한다.

---

## Blue-Green 도입으로 추가된 변수 3개 (2026-04-23)

Blue-Green 배포는 prod 서버에서도 Nginx 설정을 rsync 하고 `nginx -s reload` 를 수행하므로 CI 가 **prod 서버에도 SSH** 해야 한다. 따라서 prod 접속용 변수 3개와 `EC2_KNOWN_HOSTS` 확장이 필요하다.

### 6. `PROD_HOST`

- **Type**: Variable, Protected ✅, Masked ❌
- **Value**:

```
172.17.0.1
```

> **왜 public IP 가 아니고 docker bridge IP 인가** — GitLab runner 는 **prod 호스트 안에서** 돌고 있다. CI 잡은 prod 위의 Docker 컨테이너 안에서 실행되는데, 그 컨테이너가 **자기 호스트의 public IP(`3.38.240.110`)** 로 SSH 를 걸면 EC2 의 hairpin NAT 제한 때문에 TCP 연결이 타임아웃한다. 해결책은 **Docker 기본 bridge(`172.17.0.1`)** 로 접속하는 것이다. 이 IP 는 호스트의 sshd(`0.0.0.0:22`) 로 바로 라우팅된다. 이 이슈는 2026-04-23 실전에서 확인됐다.

### 7. `PROD_USER`

- **Type**: Variable, Protected ✅, Masked ❌
- **Value**: `ubuntu`

### 8. `PROD_DEPLOY_PATH`

- **Type**: Variable, Protected ✅, Masked ❌
- **Value**: `/opt/dotshelf`

### `EC2_KNOWN_HOSTS` 확장 (기존 변수 수정)

기존 app(`3.36.72.58`) 3줄 + prod public IP(`3.38.240.110`) 3줄 + **docker bridge(`172.17.0.1`) 3줄** = **총 9줄**.

`172.17.0.1` 과 `3.38.240.110` 은 물리적으로 같은 서버이므로 호스트 키도 같다. IP 부분만 `172.17.0.1` 로 바꿔 복사한다.

획득 방법 (로컬에서):
```bash
# app
ssh-keyscan -t ed25519,rsa,ecdsa 3.36.72.58 2>/dev/null
# prod public
ssh-keyscan -t ed25519,rsa,ecdsa 3.38.240.110 2>/dev/null
# prod docker bridge (위 결과 IP 만 172.17.0.1 로 바꾸면 됨)
ssh-keyscan -t ed25519,rsa,ecdsa 3.38.240.110 2>/dev/null | sed 's/^3\.38\.240\.110/172.17.0.1/'
```

최종 9줄 형태:
```
3.36.72.58 ssh-rsa AAAA...                    # app
3.36.72.58 ecdsa-sha2-nistp256 AAAA...        # app
3.36.72.58 ssh-ed25519 AAAA...                # app
3.38.240.110 ssh-rsa AAAA...                  # prod public (참고용)
3.38.240.110 ecdsa-sha2-nistp256 AAAA...      # prod public
3.38.240.110 ssh-ed25519 AAAA...              # prod public
172.17.0.1 ssh-rsa AAAA...                    # prod docker bridge (← 실제 사용)
172.17.0.1 ecdsa-sha2-nistp256 AAAA...
172.17.0.1 ssh-ed25519 AAAA...
```

> prod public IP 3줄은 `PROD_HOST=172.17.0.1` 쓰는 한 실제로 필요 없지만, 사람이 관리 SSH 로 접속하거나 러너가 다른 호스트로 이사할 때 대비해 보관해둔다.

### SSH 키 공유 여부 확인

`SSH_PRIVATE_KEY` 에 해당하는 공개키가 **prod 서버의 `ubuntu` 사용자 `~/.ssh/authorized_keys`** 에도 등록되어 있어야 한다 (docker bridge 로 들어와도 `ubuntu` 계정에 붙으므로 동일).

```bash
ssh dotshelf "cat >> ~/.ssh/authorized_keys" < ~/.ssh/gitlab-ci-dotshelf-deploy.pub
```

---

## 저장 후 확인 체크리스트

- [ ] 기본 변수 14개와 Blue-Green 추가 변수 3개가 전부 **Protected ✅** 인지 (Variables 목록 상단에서 자물쇠 아이콘 확인)
- [ ] multiline 변수(`SSH_PRIVATE_KEY`, `EC2_KNOWN_HOSTS`)는 Masked 가 꺼져 있는지
- [ ] 단일 라인 secret(`GOOGLE_CLIENT_SECRET`, `KAKAO_CLIENT_SECRET`)은 Masked 가 켜져 있는지
- [ ] `APPLE_CLIENT_ID`, `APPLE_CLIENT_IDS` 는 secret 이 아니므로 Masked 없이 값 전체가 보이는지
- [ ] `EC2_KNOWN_HOSTS` 의 Type 이 **`Variable`** (File 아님) 이고 호스트 키 **9줄** (app 3 + prod public 3 + docker bridge 3) 모두 포함인지
- [ ] `PROD_HOST` 가 **`172.17.0.1`** (docker bridge) 로 설정되어 있는지 — prod public IP 쓰면 hairpin NAT 로 SSH 타임아웃
- [ ] `SSH_PRIVATE_KEY` 대응 공개키가 **app + prod 양쪽 `authorized_keys`** 에 있는지
- [ ] `dev` 브랜치가 **Protected Branches** 에 등록돼 있는지 (Settings → Repository → Protected branches)

## 트러블슈팅

| 증상 | 원인 | 해결 |
|------|------|------|
| `Host key verification failed` | `EC2_KNOWN_HOSTS` 가 잘림/공백만 저장됨 | 이 문서 그대로 재복사. Masked 해제 확인 |
| `Permission denied (publickey)` | `SSH_PRIVATE_KEY` 에 해당하는 공개키가 SSAFY 서버 `authorized_keys` 에 없음 | `gitlab-ci-dotshelf-deploy` 공개키를 SSAFY ubuntu 사용자 `~/.ssh/authorized_keys` 에 추가 |
| `connect to host ... Operation timed out` | `EC2_HOST` 가 옛 값(`172.17.0.1`, `3.38.240.110` 등) 으로 남아있음 | `3.36.72.58` (SSAFY Public IP) 로 수정 |
| 파이프라인이 변수를 못 읽음 | Protected 변수인데 브랜치가 Protected 아님 | `Settings → Repository → Protected branches` 에서 `dev` 추가 |
| deploy 중 `docker compose ... no such service: api` | prod (`3.38.240.110`) 로 잘못 들어갔을 때 발생 | `EC2_HOST` 가 정말 `3.36.72.58` 인지 재확인 (prod 에는 더 이상 api 서비스 없음) |

## 변경 이력

- 2026-04-21 · 최초 작성 (Runner 를 EC2 로 옮기며 `EC2_HOST`·`EC2_KNOWN_HOSTS` 를 `172.17.0.1` 기준으로 갱신)
- 2026-04-22 · **App 서버를 SSAFY 로 분리** 하면서 `EC2_HOST` 를 `3.36.72.58`, `EC2_KNOWN_HOSTS` 를 SSAFY 서버 호스트키로 교체. 이제 deploy target 은 "다른 호스트" 이므로 hairpin NAT 이슈 해당 없음 → Docker bridge(`172.17.0.1`) 불필요
- 2026-04-23 · **Blue-Green 배포 도입** 으로 prod 접속용 변수 3개 (`PROD_HOST`, `PROD_USER`, `PROD_DEPLOY_PATH`) 추가. `SSH_PRIVATE_KEY` 대응 공개키를 prod 서버 `authorized_keys` 에도 등록 필요
- 2026-04-23 (hotfix) · 첫 Blue-Green 배포 시 `PROD_HOST=3.38.240.110` (prod public IP) 에서 **hairpin NAT 타임아웃** 발생. `PROD_HOST` 를 **`172.17.0.1`** (docker bridge) 로 교체하고 `EC2_KNOWN_HOSTS` 에 bridge IP 호스트 키 3줄 추가. 총 9줄 구성
- 2026-04-27 · Apple Sign in 검증용 `APPLE_CLIENT_ID`, `APPLE_CLIENT_IDS` 변수 추가
