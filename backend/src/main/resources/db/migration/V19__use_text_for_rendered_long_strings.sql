-- Bookshelf render APIs return book descriptions and reading record content as plain strings.
-- PostgreSQL OID LOB mappings can fail when existing/dev schemas use TEXT values, so keep
-- these user-facing fields as materialized TEXT columns.

CREATE OR REPLACE FUNCTION public._dotshelf_oid_lob_to_text(value oid)
RETURNS TEXT
LANGUAGE plpgsql
AS $$
BEGIN
    IF value IS NULL THEN
        RETURN NULL;
    END IF;

    BEGIN
        RETURN convert_from(lo_get(value), 'UTF8');
    EXCEPTION WHEN OTHERS THEN
        RETURN value::TEXT;
    END;
END;
$$;

DO $$
DECLARE
    books_description_type TEXT;
    reading_record_content_type TEXT;
BEGIN
    SELECT udt_name
      INTO books_description_type
      FROM information_schema.columns
     WHERE table_schema = 'public'
       AND table_name = 'books'
       AND column_name = 'description';

    IF books_description_type = 'oid' THEN
        ALTER TABLE books
            ALTER COLUMN description TYPE TEXT
            USING public._dotshelf_oid_lob_to_text(description);
    ELSE
        ALTER TABLE books
            ALTER COLUMN description TYPE TEXT
            USING description::TEXT;
    END IF;

    SELECT udt_name
      INTO reading_record_content_type
      FROM information_schema.columns
     WHERE table_schema = 'public'
       AND table_name = 'reading_records'
       AND column_name = 'content';

    IF reading_record_content_type = 'oid' THEN
        ALTER TABLE reading_records
            ALTER COLUMN content TYPE TEXT
            USING public._dotshelf_oid_lob_to_text(content);
    ELSE
        ALTER TABLE reading_records
            ALTER COLUMN content TYPE TEXT
            USING content::TEXT;
    END IF;
END;
$$;

DROP FUNCTION public._dotshelf_oid_lob_to_text(oid);
