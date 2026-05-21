package com.dotshelf.global.common;

/**
 * visibility_scope / guestbook_visibility_scope 컬럼의 정식 값과 정규화 헬퍼.
 *
 * <p>레거시 별칭(FOLLOWERS, FOLLOWERS_ONLY)은 MUTUAL(맞팔)로 정규화한다.
 * FE가 아직 구 값을 전송하므로 엔티티 write 시점에 정식 값으로 수렴시키고,
 * 읽기 판정도 normalize를 거쳐 구·신 값을 함께 수용한다.
 */
public final class VisibilityScope {

    public static final String PUBLIC = "PUBLIC";
    public static final String MUTUAL = "MUTUAL";
    public static final String PRIVATE = "PRIVATE";

    private VisibilityScope() {
    }

    /** null/빈값 → PUBLIC, FOLLOWERS·FOLLOWERS_ONLY → MUTUAL, 그 외는 대문자화해 그대로. */
    public static String normalize(String raw) {
        if (raw == null || raw.isBlank()) {
            return PUBLIC;
        }
        String value = raw.trim().toUpperCase();
        if (value.equals("FOLLOWERS") || value.equals("FOLLOWERS_ONLY")) {
            return MUTUAL;
        }
        return value;
    }

    public static boolean isPublic(String raw) {
        return PUBLIC.equals(normalize(raw));
    }

    public static boolean isMutual(String raw) {
        return MUTUAL.equals(normalize(raw));
    }
}
