-- guest_migration_imports 가 어느 게스트 세션에서 넘어온 데이터인지 추적용 컬럼.
-- 현재 단계: 클라이언트가 보낸 sessionId 를 그대로 저장 (trace 목적).
-- 향후 stateful guest session 트래킹이 도입되면 여기에 외래키/검증 로직이 붙는다.
ALTER TABLE public.guest_migration_imports
    ADD COLUMN guest_session_id VARCHAR(64);

CREATE INDEX idx_guest_migration_imports_guest_session_id
    ON public.guest_migration_imports (guest_session_id);
