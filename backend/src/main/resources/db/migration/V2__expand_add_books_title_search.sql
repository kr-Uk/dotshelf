ALTER TABLE public.books
    ADD COLUMN title_search character varying(255);

UPDATE public.books
SET title_search = lower(regexp_replace(title, '[[:space:]]+', '', 'g'))
WHERE title IS NOT NULL
  AND title_search IS NULL;
