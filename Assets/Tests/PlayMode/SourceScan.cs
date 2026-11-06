using System.Text;

namespace ChronoSavior.Tests.PlayMode
{
    /// <summary>
    /// 소스를 훑는 규칙 검사(`WorldLookupTests`·`StateOwnershipTests`·`CreationOwnershipTests`·`OriginalFidelityTests.DefinedApis`)가 같이 쓰는 전처리 —
    /// **주석(`//`·`/* */`)과 문자열·문자 리터럴을 지운 코드만** 남긴다(줄 바꿈은 그대로). 그래야 설명 글이나 문자열 속 낱말을 세지 않고,
    /// 호출을 여러 줄에 나눠 써도 정규식이 코드 전체에서 찾는다(2026-11-07 최종 점검 — 예전엔 검사마다 '줄 전체가 주석인 줄'만 건너뛰었다).
    /// **C# 구문 분석기는 아니다 — 한계**: 보간 문자열의 `{ }` 안 코드는 문자열로 보고 지운다(그 안의 호출은 검사 밖), `#if`는 해석하지 않아
    /// 꺼진 분기도 코드로 본다, raw 문자열(`"""…"""`, C# 11)은 다루지 않는다(Unity 6의 C# 9엔 없다). 검사가 막으려는 건 평범한 호출이라 이 정도로 둔다.
    /// </summary>
    static class SourceScan
    {
        public static string CodeOnly(string source)
        {
            var sb = new StringBuilder(source.Length);
            int i = 0, n = source.Length;
            while (i < n)
            {
                char c = source[i];
                char next = i + 1 < n ? source[i + 1] : '\0';
                if (c == '/' && next == '/') { while (i < n && source[i] != '\n') i++; continue; }
                if (c == '/' && next == '*')
                {
                    i += 2;
                    while (i < n && !(source[i] == '*' && i + 1 < n && source[i + 1] == '/')) { if (source[i] == '\n') sb.Append('\n'); i++; }
                    i += 2;
                    continue;
                }
                bool verbatim = (c == '@' && next == '"') || (c == '@' && next == '$') || (c == '$' && next == '@');
                if (verbatim)
                {
                    while (i < n && source[i] != '"') i++;
                    i++;
                    while (i < n)
                    {
                        if (source[i] == '"') { if (i + 1 < n && source[i + 1] == '"') { i += 2; continue; } i++; break; }
                        if (source[i] == '\n') sb.Append('\n');
                        i++;
                    }
                    sb.Append("\"\"");
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    i++;
                    while (i < n && source[i] != quote && source[i] != '\n') { if (source[i] == '\\') i++; i++; }
                    i++;
                    sb.Append(quote).Append(quote);
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }
    }
}
