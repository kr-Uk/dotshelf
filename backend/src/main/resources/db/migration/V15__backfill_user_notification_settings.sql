INSERT INTO public.user_notification_settings (
    created_at,
    updated_at,
    notify_book_added_by_others,
    notify_guestbook_reply,
    notify_new_follower,
    notify_new_guestbook,
    notify_reading_record_comment,
    notify_reading_record_reaction,
    user_id
)
SELECT
    NOW(),
    NOW(),
    true,
    true,
    true,
    true,
    true,
    true,
    u.id
FROM public.users u
WHERE NOT EXISTS (
    SELECT 1
    FROM public.user_notification_settings s
    WHERE s.user_id = u.id
);
