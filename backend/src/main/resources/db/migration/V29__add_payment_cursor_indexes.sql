-- 결제 도메인 cursor paging keyset (timestamp DESC, id DESC) tie-breaker 정렬 활용을 위한 인덱스 정리.
-- MR !192 (S14P31F206-263) 의 리뷰 follow-up.
--
-- page_transactions:
--   기존 idx_page_transactions_user_id_created_at 는 (user_id, created_at DESC) 만 다룸.
--   cursor query 의 (created_at < ?) OR (created_at = ? AND id < ?) tie-breaker 가 id DESC 까지
--   인덱스 정렬을 활용하도록 (user_id, created_at DESC, id DESC) 인덱스 추가.
--   누적 데이터 (사용자당 결제 내역 N개) 가 늘어날수록 효과 ↑.
--
-- asset_purchases:
--   기존 user 단위 정렬 인덱스 부재. cursor paging 도입과 함께 추가.

CREATE INDEX IF NOT EXISTS idx_page_transactions_user_created_id
    ON public.page_transactions USING btree (user_id, created_at DESC, id DESC);

CREATE INDEX IF NOT EXISTS idx_asset_purchases_user_purchased_id
    ON public.asset_purchases USING btree (user_id, purchased_at DESC, id DESC);
