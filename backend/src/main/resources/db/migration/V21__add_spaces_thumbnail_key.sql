ALTER TABLE spaces
    ADD COLUMN IF NOT EXISTS thumbnail_key varchar(500) NULL;
