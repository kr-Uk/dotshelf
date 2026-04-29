# Dashboard 화면 — Unity 세팅 가이드 (P0c)

## 빠르게 켜보기 (5분)

1. **새 Scene 만들기** (또는 기존 MainScene에 임시로 얹기)
2. 빈 GameObject 생성 → 이름 `DashboardScreen`
3. `UI Toolkit > UI Document` 컴포넌트 추가
4. 같은 GameObject에 **`DashboardScreenController`** 스크립트 추가
5. `DashboardScreenController` Inspector에서 다음 4개 필드 채우기:

| 필드 | 드롭할 에셋 | 위치 |
|---|---|---|
| `View Asset` | `DashboardView.uxml` | `Assets/App/Features/Dashboard/Presentation/` |
| `Stylesheet` | `DashboardView.uss` | 같은 폴더 |
| `Post It Catalog` | (선택) `PostItStyleCatalogAsset` 인스턴스 | 직접 만들어야 함 — 아래 참고 |
| `Panel Settings` (UI Document) | 기존 `NotebookPanelSettings.asset` 재사용 OK | `Assets/App/Features/Notebook/Presentation/` |

6. `View Mode` 토글로 `Mine` (본인 공간) / `Visitor` (타인 공간) 선택
7. Play 모드 진입 → Mock 데이터로 화면 렌더링 확인

## PostIt Catalog 만들기 (P0c는 미사용, P2 들어가면 필요)

1. Project 창 우클릭 → `Create > Dotshelf > PostIt Style Catalog`
2. 생성된 `PostItStyleCatalog.asset` 선택
3. Entries 배열에 항목 추가, 각 항목마다:
   - `Asset Id`: 백엔드 인벤토리 ID (Mock에선 1~5 사용)
   - `Display Name`: "노란 메모지", "체크무늬" 등
   - `Preview Sprite`: 포스트잇 텍스처 sprite

## 현재 P0c에서 동작하는 것

✅ 프로필 헤더 (닉네임·한 줄 소개·팔로잉/팔로워 수)
✅ 통계 (좋아요·책·독후감 수)
✅ 좋아요 토글 (낙관적 업데이트 + 디바운스)
✅ 방명록 3x5 그리드 + 페이지 슬라이드 (◀ ▶)
✅ 본인/타인 모드 분기 (편집 vs 방명록 작성 버튼 토글)
✅ 방명록 셀 클릭 시 콘솔 로그 (실제 모달은 P2)

## 미구현 (다음 단계)

- 책/독후감 캐러셀 데이터 바인딩 (현재 빈 상태)
- 방명록 작성·답장·신고 모달 (P2)
- 대시보드 편집 모달 (P2)
- 팔로워/팔로잉 모달 (P1)
- 보유 책 / 독후감 풀 목록 모달 (P3)
- Mock → Api 토글 SceneInstaller (Bootstrap 통합)

## 알려진 한계

- 방명록 카운트는 현재 페이지 아이템 수만 표시 (백엔드 totalCount 필드 합류 후 갱신)
- 프로필 사진 / 책 표지 / 포스트잇 이미지 미연결 (placeholder 회색 박스)
