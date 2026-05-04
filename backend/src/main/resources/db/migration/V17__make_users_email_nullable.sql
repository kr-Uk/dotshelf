-- Kakao OAuth (scope=빈 값) 흐름 지원: account_email 동의항목 미설정 상태의 사용자는 email
-- 정보 없이 가입됨. SocialUserInfo.email = null → User.email = null → DB NOT NULL 위반.
--
-- Fix: users.email NULLABLE 변경. unique constraint는 유지 (email 있으면 중복 X).
-- backward-compatible — 기존 데이터(모두 email 있음)에 영향 없음.
--
-- 향후 Kakao 콘솔 account_email 동의항목 활성화 + 검수 통과 시 scope 추가 → email 받기
-- 가능. 다만 그 시점에도 미가입 사용자는 그대로 nullable 허용 (사용자가 카카오에서
-- email 제공 거부할 수 있음).
ALTER TABLE users ALTER COLUMN email DROP NOT NULL;
