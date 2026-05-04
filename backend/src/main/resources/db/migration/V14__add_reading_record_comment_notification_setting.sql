ALTER TABLE public.user_notification_settings
    ADD COLUMN notify_reading_record_comment boolean NOT NULL DEFAULT true;
