-- 방 썸네일 이미지 URL 저장 컬럼.
-- Unity 클라가 방 배치 변경을 감지할 때마다 자동으로 캡처해서 BE 로 multipart 업로드,
-- BE 가 S3 에 저장 후 여기에 공개 URL 을 갱신한다 (`spaces/{spaceId}/thumbnail-{layoutVersion}.jpg`).
-- nullable — 신규 가입자는 아직 썸네일이 없으므로 비어 있음.
ALTER TABLE spaces
    ADD COLUMN IF NOT EXISTS thumbnail_url varchar(500) NULL;
