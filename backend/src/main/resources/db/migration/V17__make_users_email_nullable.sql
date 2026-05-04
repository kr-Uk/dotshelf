-- Kakao OAuth (scope=빈 값) 흐름 지원: account_email 동의항목 미설정 상태의 사용자는 email
-- 정보 없이 가입됨. SocialUserInfo.email = null → User.email = null → DB NOT NULL 위반.
--
-- Identity 모델 결정: dotshelf는 OAuth-only 인증 (직접 로그인 / 비밀번호 재설정 미제공).
-- email은 이메일 알림 등 별도 기능에 안 쓰므로 사실상 metadata. Identity는 (provider,
-- provider_user_id) 단일 키 (user_social_accounts 테이블의 unique constraint).
--
-- 따라서 같은 email이라도 provider가 다르면 별도 user로 인정 (예: 같은 사람이 Google +
-- Kakao 둘 다 가입 → 두 user). 이를 위해:
-- 1) email NULLABLE — 카카오에서 email 못 받는 사용자도 가입 가능
-- 2) email UNIQUE 제거 — 같은 email이 다른 provider에서 들어와도 INSERT 통과
--
-- backward-compatible — 기존 데이터(모두 email 있음 + 다 unique)에 영향 없음.
ALTER TABLE users ALTER COLUMN email DROP NOT NULL;
ALTER TABLE users DROP CONSTRAINT IF EXISTS uk6dotkott2kjsp8vw4d0m25fb7;
