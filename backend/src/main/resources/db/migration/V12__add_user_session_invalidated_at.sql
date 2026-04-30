ALTER TABLE users
    ADD COLUMN IF NOT EXISTS session_invalidated_at timestamp(6) without time zone;
