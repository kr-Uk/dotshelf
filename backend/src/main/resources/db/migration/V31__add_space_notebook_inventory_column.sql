ALTER TABLE spaces
    ADD COLUMN IF NOT EXISTS current_notebook_inventory_id bigint NULL;
