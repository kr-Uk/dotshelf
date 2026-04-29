-- 책의 origin 추적용 컬럼.
-- source: LOCAL = 운영 DB seed/관리자 등록, KAKAO = Kakao 검색 결과를 책장에 추가하면서 신규 생성된 책.
-- source_url: KAKAO 검색에서 받은 원본 URL (현재 클라이언트가 송신하면 채워짐, 미송신 시 NULL).
ALTER TABLE public.books
    ADD COLUMN source VARCHAR(16) NOT NULL DEFAULT 'LOCAL';

ALTER TABLE public.books
    ADD COLUMN source_url VARCHAR(500);
