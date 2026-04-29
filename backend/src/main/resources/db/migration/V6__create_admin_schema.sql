-- admin 도메인 데이터 격리용 스키마.
-- public 스키마(사용자 도메인)와 분리해 운영자 전용 테이블(audit_log, moderation_*, admin_users 등)을 둔다.
-- 같은 dotshelf DB / 같은 datasource 에 들어가므로 한 트랜잭션·한 connection pool 그대로 활용 가능.
-- 실제 admin 테이블은 도메인 코드 작성 시 V7+ 에서 추가 (Spring entity 에 @Table(schema = "admin") 명시).

CREATE SCHEMA IF NOT EXISTS admin;

COMMENT ON SCHEMA admin IS 'Admin/operator-only tables (audit logs, moderation queue, admin user accounts). Accessed by /v1/admin/* controllers only.';
