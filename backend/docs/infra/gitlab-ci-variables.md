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

## 변수 목록 5개

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
- **Value**: 아래 3줄 전체 복사 (2026-04-21 기준, 172.17.0.1 = Runner 컨테이너가 바라보는 EC2 호스트)

```
172.17.0.1 ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIC81Y3Wye2obGWDr+dnCE+6uARxxP92hcETakFu+ouED
172.17.0.1 ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQDDtdCoZ3VOEgOLBr9rc+FelDIA5th0zDJj6JZ/YLeBgRLJZyJls4trRQ5WJAbBUfK1t5+B/72weFYgCKQbtSSJsMIHbNG0EpP4XOOzqRSK+7nLznfC59J0eB3mgtClUJPaFg0aVIUJynrGExnZbVqdgOZFTbKys8oTH205F2p/DTZibk6gShpyHBxHt7XYmmnL5b4dFny8Km/kLIfpvUr3GJ/a/nWkR76aoLrxJSvSQ6BHYFgpk15cTUhVtWF+YiYlg1pbwEygeP7o6H4olmDrYt89QOMEfZpxkJaXQBYErDxJ60kMuF4yXNY6W8awEbv2oFDP5/1pHX3wwpHIXUhMwXGhCRZ6Eh/dap4AMYds6JAPwFQBgQD154Mgm+IUyBhQQzz+yLYZKKg6WKaLmNFLO+DiVKFCPtwiO1boJLfzMpMNpyeUOqI6k55Y5nyHTbB8fH+vuTJim0qq771NiESZDamh1Kk1dB3PxKGdbT1hQX/6DrPIE0QlYJijt+5GA0k=
172.17.0.1 ecdsa-sha2-nistp256 AAAAE2VjZHNhLXNoYTItbmlzdHAyNTYAAAAIbmlzdHAyNTYAAABBBCGXkVCcSrJut0cYLgA5EauG8x0ZIGvl1Cm8JLu/y+R0pwdSTJ9sSVgLBLhlXtlWe5ZBat7gXESx1kKJRgjRf10=
```

> 🔄 **재생성이 필요할 때** (EC2 sshd 호스트키 바뀐 경우):
> ```
> ssh -i C:/PROJECT/DOTSHELF/server/f206_jihocompany.pem ubuntu@3.38.240.110 "ssh-keyscan -t ed25519,rsa,ecdsa 172.17.0.1 2>/dev/null"
> ```

### 3. `EC2_HOST`

- **Type**: Variable
- **Protected**: ✅
- **Masked**: ❌ (IP 라 Masked 걸어도 되긴 하지만 불필요)
- **Value**:

```
172.17.0.1
```

> Runner 가 **EC2 내부** Docker 컨테이너로 돌기 때문에 "같은 머신의 Docker 브리지 게이트웨이" 주소를 쓴다. 외부 퍼블릭 IP `3.38.240.110` 은 AWS hairpin NAT 제약으로 자기 자신에게 접속이 안 됨.

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

---

## 저장 후 확인 체크리스트

- [ ] 5개 전부 **Protected ✅** 인지 (Variables 목록 상단에서 자물쇠 아이콘 확인)
- [ ] Masked 는 전부 **꺼져 있는지**
- [ ] `EC2_KNOWN_HOSTS` 의 Type 이 **`Variable`** (File 아님) 인지
- [ ] `dev` 브랜치가 **Protected Branches** 에 등록돼 있는지 (Settings → Repository → Protected branches)

## 트러블슈팅

| 증상 | 원인 | 해결 |
|------|------|------|
| `Host key verification failed` | `EC2_KNOWN_HOSTS` 가 잘림/공백만 저장됨 | 이 문서 그대로 재복사. Masked 해제 확인 |
| `Permission denied (publickey)` | `SSH_PRIVATE_KEY` 가 EC2 `authorized_keys` 에 없음 | `dotshelf-deploy.pub` 내용을 EC2 ubuntu 사용자 `~/.ssh/authorized_keys` 에 추가 |
| `connect to host ... Operation timed out` | `EC2_HOST` 가 아직 `3.38.240.110` 로 되어 있음 | `172.17.0.1` 로 수정 |
| 파이프라인이 변수를 못 읽음 | Protected 변수인데 브랜치가 Protected 아님 | `Settings → Repository → Protected branches` 에서 `dev` 추가 |

## 변경 이력

- 2026-04-21 · 최초 작성 (Runner 를 EC2 로 옮기며 `EC2_HOST`·`EC2_KNOWN_HOSTS` 를 `172.17.0.1` 기준으로 갱신)
