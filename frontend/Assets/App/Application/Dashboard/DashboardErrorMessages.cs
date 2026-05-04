namespace App.Application.Dashboard
{
    /// <summary>
    /// 대시보드에서 사용하는 에러 코드 → 사용자 노출 메시지 중앙 lookup.
    /// MR #59 revbot 의견 반영 — Presentation 레이어가 코드별 분기를 직접 들고 있던 걸 분리.
    /// 새 코드는 backend ErrorCode.java와 UseCase 클라이언트 검증 코드 양쪽을 모두 포함.
    /// </summary>
    public static class DashboardErrorMessages
    {
        public static string Format(DashboardApiError error)
        {
            if (error == null) return "알 수 없는 오류가 발생했습니다.";
            switch (error.Code)
            {
                // ==== Backend ErrorCode (사용자 앱 v1.0 갱신) ====
                case "G001": return "방명록을 찾을 수 없습니다.";
                case "G002": return "답장을 찾을 수 없습니다.";
                case "G003": return "답장에 대한 권한이 없습니다.";
                case "G004": return "이미 답장이 작성된 방명록입니다.";
                case "G005": return "공간 주인이 허용하지 않은 포스트잇 스타일입니다.";
                case "S003": return "공간을 찾을 수 없습니다.";
                case "S004": return "본인 공간엔 방명록을 작성할 수 없습니다.";
                case "S005": return "차단된 사용자의 공간엔 접근할 수 없습니다.";
                case "F001": return "팔로우 관계를 찾을 수 없습니다.";
                case "F002": return "차단된 사용자와 관련된 요청은 허용되지 않습니다.";
                case "U001": return "사용자를 찾을 수 없습니다.";
                case "U003": return "이미 사용 중인 닉네임입니다.";
                case "A001": return "로그인이 필요합니다.";
                case "A002": return "권한이 없습니다.";
                case "A003": return "세션이 만료됐습니다. 다시 로그인하세요.";
                case "C001": return "잘못된 입력 값입니다.";
                // ==== Client UseCase 검증 코드 ====
                case "GUESTBOOK_CONTENT_BLANK": return "방명록 내용을 입력하세요.";
                case "GUESTBOOK_CONTENT_TOO_LONG": return "방명록은 30자 이하로 작성하세요.";
                case "GUESTBOOK_POSTIT_REQUIRED": return "포스트잇 스타일을 선택하세요.";
                case "GUESTBOOK_REPLY_CONTENT_BLANK": return "답장 내용을 입력하세요.";
                case "GUESTBOOK_REPLY_CONTENT_TOO_LONG": return "답장은 30자 이하로 작성하세요.";
                case "GUESTBOOK_REPLY_POSTIT_REQUIRED": return "답장 포스트잇 스타일을 선택하세요.";
                case "INTRODUCTION_TOO_LONG": return "한 줄 소개는 255자 이하로 입력하세요.";
                case "REPORT_REASON_REQUIRED": return "신고 사유를 선택하세요.";
                // ApiSpaceSettingsRepository.ApplyThemeWithMode short-circuits
                // KEEP_BLOCKERS_ONLY with this code; backend also returns it
                // when the mode is invoked server-side. Single mapping so
                // callers see the same Korean string either way.
                case "THEME_MODE_NOT_IMPLEMENTED": return "이 모드는 아직 지원되지 않습니다.";
                default: return string.IsNullOrEmpty(error.Message) ? "오류가 발생했습니다." : error.Message;
            }
        }
    }
}
