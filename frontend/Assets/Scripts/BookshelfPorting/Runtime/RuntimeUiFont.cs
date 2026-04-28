using UnityEngine;

namespace BookshelfPorting.Runtime
{
    internal static class RuntimeUiFont
    {
        private const string ResourcePath = "Fonts/Pretendard-Regular";
        private const string KoreanGlyphProbe = "가나다라마바사아자차카타파하책장검색추가편집저장취소";
        private static readonly string[] OsFontCandidates =
        {
            "Pretendard",
            "Noto Sans KR",
            "Noto Sans CJK KR",
            "Malgun Gothic",
            "맑은 고딕",
            "Apple SD Gothic Neo",
            "Arial Unicode MS"
        };

        private static Font cachedFont;

        public static Font Resolve()
        {
            if (cachedFont != null)
            {
                return cachedFont;
            }

            var font = Resources.Load<Font>(ResourcePath);
            if (!CanRenderKorean(font, FontStyle.Normal))
            {
                font = ResolveOsKoreanFont();
            }

            cachedFont = CanRenderKorean(font, FontStyle.Normal)
                ? font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return cachedFont;
        }

        public static FontStyle ResolveStyle(Font font, FontStyle preferredStyle)
        {
            if (preferredStyle == FontStyle.Normal || CanRenderKorean(font, preferredStyle))
            {
                return preferredStyle;
            }

            return FontStyle.Normal;
        }

        private static Font ResolveOsKoreanFont()
        {
            for (var i = 0; i < OsFontCandidates.Length; i++)
            {
                var fontName = OsFontCandidates[i];
                if (string.IsNullOrWhiteSpace(fontName))
                {
                    continue;
                }

                var font = Font.CreateDynamicFontFromOSFont(fontName, 16);
                if (CanRenderKorean(font, FontStyle.Normal))
                {
                    return font;
                }
            }

            return null;
        }

        private static bool CanRenderKorean(Font font, FontStyle style)
        {
            if (font == null)
            {
                return false;
            }

            font.RequestCharactersInTexture(KoreanGlyphProbe, 32, style);
            return font.GetCharacterInfo('한', out _, 32, style) &&
                   font.GetCharacterInfo('책', out _, 32, style);
        }
    }
}
