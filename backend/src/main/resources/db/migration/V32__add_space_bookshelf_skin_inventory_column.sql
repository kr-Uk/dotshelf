ALTER TABLE spaces
    ADD COLUMN IF NOT EXISTS current_bookshelf_skin_inventory_id bigint NULL;
