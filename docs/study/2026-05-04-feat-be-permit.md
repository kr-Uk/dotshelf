# 관리자 운영 콘솔 공지/모더레이션 개선

- **브랜치**: `feat/be/permit`
- **범위**: `9c164933..HEAD`
- **기간**: 2026-05-04 ~ 2026-05-04
- **관련 커밋**: `origin/dev..HEAD` 범위의 관리자 운영 콘솔 변경 커밋

## TL;DR

관리자 운영 콘솔에서 공지사항을 발행만 하던 흐름에 수정/삭제 기능을 추가했다.
백엔드에는 `PUT/DELETE /v1/admin/operations/announcements/{announcementId}`를 열고, 프론트에는 기존 공지 다이얼로그를 등록/수정 겸용으로 바꾸었다.
공지 생성/수정/삭제 감사 로그에는 제목만이 아니라 공지 전체 스냅샷을 남긴다.
모더레이션 큐에는 상태별 필터를 추가해 `PENDING`, `IN_REVIEW`, `RESOLVED`, `REJECTED` 상태를 화면에서 바로 걸러볼 수 있게 했다.
admin-web의 MSW mock도 실제 API 요청 파라미터와 같은 방식으로 동작하도록 보강했다.

## 무엇을 했나

### 1. 공지사항 수정/삭제

- 사용자 관점 효과: 운영자가 잘못 발행한 공지의 제목, 본문, 대상, 기간, 긴급 여부를 다시 고치거나 삭제할 수 있다.
- 시스템 관점 효과: 공지 변경과 삭제가 `AdminService` 트랜잭션 안에서 처리되고, `ANNOUNCEMENT_UPDATE`, `ANNOUNCEMENT_DELETE` 감사 로그에는 변경 전/후 전체 스냅샷이 남는다.
- 주요 파일: `AdminController.java`, `AdminService.java`, `AdminAnnouncement.java`, `AdminDtos.java`

### 2. 공지 다이얼로그 재사용

- 사용자 관점 효과: 새 공지와 기존 공지 수정이 같은 폼 경험을 공유한다.
- 시스템 관점 효과: `AnnouncementDialog`가 `initialValue`를 받으면 수정 모드로 열리고, 없으면 생성 모드로 초기화된다.
- 주요 파일: `operations/page.tsx`, `announcement-dialog.tsx`, `api.ts`, `types.ts`

### 3. 모더레이션 큐 상태 필터

- 사용자 관점 효과: 모더레이터가 전체 큐에서 상태별로 `미처리`, `검토 중`, `조치 완료`, `기각` 신고만 빠르게 확인할 수 있다.
- 시스템 관점 효과: 프론트 상태값과 백엔드 저장 상태값을 명시적으로 매핑해 `/reports?status=PENDING&targetType=...&limit=50` 형태로 요청한다.
- 주요 파일: `moderation/page.tsx`, `handlers.ts`

## 왜 했나

기존 운영 관리 화면은 공지 발행 기능만 있고, 발행된 공지를 수정하거나 삭제할 수 있는 액션이 없었다. 운영 중에는 점검 시간, 공지 본문, 긴급 여부가 바뀌는 일이 자연스럽게 발생하므로 발행 이후 관리 기능이 필요하다.

모더레이션 큐도 대상 타입별 탭은 있었지만 상태 필터가 없어서 처리된 신고와 미처리 신고가 섞여 보였다. 실제 운영 업무에서는 "지금 봐야 하는 미처리 큐"와 "검토 중인 건"을 분리해서 보는 것이 중요하므로 상태 필터를 추가했다.

## 어떻게 구현했나

### 공지 수정/삭제 API

엔티티에는 setter를 열지 않고 상태 변경 메서드만 추가했다.

```java
public void update(String title, String body, String audience, Instant startsAt,
                   Instant endsAt, boolean isUrgent) {
    this.title = title;
    this.body = body;
    this.audience = audience;
    this.startsAt = startsAt;
    this.endsAt = endsAt;
    this.isUrgent = isUrgent;
}
```

서비스는 `requireAdminUser(userId)`로 관리자 여부를 먼저 확인한 뒤 공지를 조회한다. 수정은 전체 갱신 의미가 명확하도록 `PUT`으로 받고 엔티티 메서드로 처리한다. 삭제는 `adminAnnouncementRepository.delete(announcement)`로 물리 삭제하되, 삭제 전 전체 스냅샷을 감사 로그에 저장한다. 생성/수정/삭제 모두 감사 로그에 공지 제목, 본문, 대상, 기간, 긴급 여부, 작성자 id를 포함한다.

핵심 포인트:
- Controller는 DTO 검증과 Service 호출만 담당한다.
- Service가 트랜잭션 경계를 가진다.
- 엔티티 직접 setter 없이 `update()` 메서드로 변경 의도를 드러낸다.

### 공지 다이얼로그의 생성/수정 겸용화

`AnnouncementDialog`에 `initialValue`를 추가해 기존 공지를 수정할 때 같은 폼에 값을 주입한다. 날짜 입력은 HTML `date` input이 기대하는 `YYYY-MM-DD` 형태로 변환하되, 기존 값이 `null`이면 빈 입력으로 보존한다.

```ts
function dateInputValue(source: string | null | undefined): string {
  if (!source) return "";
  if (/^\d{4}-\d{2}-\d{2}$/u.test(source)) return source;
  const date = new Date(source);
  return Number.isNaN(date.getTime()) ? "" : date.toISOString().slice(0, 10);
}
```

핵심 포인트:
- 폼 컴포넌트는 생성/수정의 UI 중복을 줄인다.
- 제출 처리는 부모 페이지가 `POST` 또는 `PUT`을 선택한다.
- 기존 공지의 `startsAt`/`endsAt`가 `null`이면 빈 날짜 input으로 보존하고, 제출 시에도 `null`로 다시 보낸다.
- 실패 시 toast를 띄우고 에러를 다시 던져 다이얼로그가 닫히지 않도록 했다.

### 모더레이션 상태 필터 매핑

프론트의 화면 상태값은 `open`, `in_review`, `resolved`, `dismissed`이고, 백엔드 저장값은 `PENDING`, `IN_REVIEW`, `RESOLVED`, `REJECTED`다. 이 차이를 상수로 분리했다.

```ts
const REPORT_STATUS_QUERY: Record<ReportStatus, string> = {
  open: "PENDING",
  in_review: "IN_REVIEW",
  resolved: "RESOLVED",
  dismissed: "REJECTED",
};
```

상태 필터와 대상 타입 탭은 쿼리 키에도 포함했다.

```ts
const { data, isLoading } = useQuery({
  queryKey: ["reports", tab, statusFilter],
  queryFn: () => api<ReportItem[]>(reportPath),
});
```

핵심 포인트:
- 필터 변경 시 선택 항목과 일괄 선택을 초기화한다.
- 액션 처리 후 `["reports"]` 쿼리를 invalidate해서 목록을 갱신한다.
- 백엔드 필터와 클라이언트 필터를 둘 다 둬 mock/실서버 양쪽에서 동일하게 보이도록 했다.

## 배운 것

- **관리자 도구의 CRUD도 감사 로그가 중요하다**: 일반 사용자 기능보다 오히려 운영 액션은 누가 무엇을 바꿨는지 추적 가능해야 한다.
- **UI 상태값과 서버 도메인 값은 다를 수 있다**: 화면에 맞는 이름과 DB 상태값을 억지로 통일하기보다 명시적인 매핑 상수를 두는 편이 안전하다.
- **재사용 폼은 초기화 타이밍이 중요하다**: 다이얼로그가 열릴 때마다 `initialValue`를 기준으로 상태를 재설정해야 이전 수정 값이 다음 생성 폼에 남지 않는다.
- **MSW mock은 단순 응답보다 계약 검증 역할도 한다**: 실제 API 쿼리 파라미터와 같은 방식으로 필터링하면 프론트 변경을 로컬에서 더 정확히 검증할 수 있다.

## 더 공부할 것

- [ ] 관리자 공지 삭제를 물리 삭제로 둘지, `deletedAt` 기반 soft delete로 바꿀지 판단 기준
- [ ] 공지 수정 API를 `PUT` 전체 갱신으로 유지할지, 향후 nullable 필드 기반 `PATCH`를 별도로 추가할지의 trade-off
- [ ] 모더레이션 상태 전이 모델을 enum으로 고정할지 문자열로 유지할지
- [ ] React Query에서 리스트 액션 후 invalidate와 optimistic update 중 어떤 상황에 무엇이 더 적합한지

## 참고

- 관련 커밋: `origin/dev..HEAD` 범위의 관리자 운영 콘솔 변경 커밋
- 검증: `backend ./gradlew.bat test`, `web/admin-web npm.cmd run lint`, `web/admin-web npm.cmd run build`
