---
name: post-merge-cleanup
description: Use this skill whenever a feature/fix branch's MR has been merged on the remote (GitLab) and the user wants to clean up locally — switch to the dev integration branch, pull the latest, and delete the now-merged local branch. Trigger when the user says "리모트 머지했으니 로컬 브랜치 없애고 dev sync", "머지 끝났으니 정리", "MR 머지됐어 브랜치 정리해", "post-merge cleanup", "dev 싱크 맞춰", "머지된 브랜치 지워" or any variant signalling a post-merge local-branch tidy-up. The skill verifies the branch was actually merged before deleting (safe `git branch -d`, never blind `-D`), syncs `dev` with `origin/dev`, and reports what was removed and how far dev advanced.
---

# post-merge-cleanup

피처/픽스 브랜치의 MR이 리모트(GitLab)에서 `dev`로 머지된 뒤, 로컬을 정리하는 워크플로를 일관되게 수행한다: `dev`로 전환 → `origin/dev` pull → 머지 완료된 로컬 작업 브랜치 삭제 → 결과 보고.

## 왜 필요한가

- dotshelf는 `feat/unity/*` · `fix/unity/*` 작업 브랜치에서 MR을 만들어 `dev`로 머지한다 (`frontend/docs/unity-git-convention.md`). 머지 후 로컬에 죽은 브랜치가 쌓이면, 다음 작업을 stale 브랜치 위에서 분기하는 사고가 난다.
- `git pull` 없이 다음 브랜치를 따면 머지된 동료 작업이 빠진 상태로 시작 → 불필요한 충돌이 누적된다.
- 브랜치 삭제를 `-D`(강제)로 하면 머지 안 된 작업도 조용히 사라진다. 머지 검증 후 `-d`(안전) 삭제가 규약.

## 언제 발동하나

다음 중 하나라도 해당하면 이 스킬을 적용한다.

- 사용자가 "리모트 머지했으니 로컬 브랜치 없애고 dev sync", "머지 끝났으니 정리", "MR 머지됐어 브랜치 정리", "dev 싱크 맞춰", "post-merge cleanup", "머지된 브랜치 지워" 등으로 머지 후 정리를 요청할 때
- MR 생성·머지 흐름 직후 사용자가 "정리하자" 신호를 줄 때

머지가 아직 안 된 상태에서의 정리 요청에는 적용하지 않는다 — 먼저 머지 여부를 확인한다.

## 절차

1. **현재 상태 확인** — `git branch --show-current` 로 정리 대상 브랜치명을 기억하고, `git status -s` 로 미커밋 변경이 있는지 본다.
   - 미커밋 변경이 있으면 멈추고 사용자에게 알린다 (`git checkout` 시 변경이 따라오거나 실패).
2. **dev로 전환** — `git checkout dev`.
3. **최신 동기화** — `git pull origin dev`. 몇 커밋 갱신됐는지 출력에서 확인.
4. **머지 검증 후 삭제** — `git branch -d <feature-branch>`.
   - `-d` 는 머지 안 된 브랜치면 거부한다 (안전장치). **절대 `-D` 로 우회하지 말 것** — 사용자가 명시적으로 "머지 안 됐지만 버려"라고 한 경우만 예외.
   - `-d` 가 "not fully merged" 로 거부하면: 리모트에서 squash-merge 됐을 가능성. `git log origin/dev --oneline | grep <branch>` 또는 MR 상태(`glab mr view`)로 실제 머지 여부를 확인하고, 머지가 확인되면 사용자 컨펌을 받은 뒤에만 `-D`.
5. **결과 보고** — 삭제된 브랜치명 + dev가 몇 커밋 전진했는지 한 줄로 요약.

## 주의

- 통합 브랜치는 `dev` 고정 (dotshelf 규약). `master` 머지/정리는 별도 절차이며 이 스킬 범위 밖이다.
- 원격 브랜치 삭제(`git push origin --delete`)는 보통 MR 의 "remove source branch" 옵션이 처리한다. 로컬에서 별도로 원격 브랜치를 지우지 않는다 — 필요 시 사용자에게 먼저 확인.
- 이 스킬은 read-mostly + `-d` 안전 삭제만 수행한다. 파괴적 작업(`-D`, `reset --hard`)은 사용자 컨펌 없이 실행하지 않는다.
