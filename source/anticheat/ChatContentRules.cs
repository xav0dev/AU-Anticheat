using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AUAnticheat.anticheat
{
    internal static class ChatContentRules
    {
        private static readonly Regex Invite = new(@"\b(?:discord\s*[.]\s*gg|discord(?:app)?\s*[.]\s*com\s*/\s*invite|t\s*[.]\s*me)\s*/\s*[a-z0-9]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(25));
        private static readonly Regex Recruitment = new(@"\b(?:join|come\s+to|enter|unete|uníos|entra)\b.{0,35}\b(?:my|our|mi|nuestro|another|other|new)\b.{0,20}\b(?:lobb(?:y|ies)|room|sala|partida)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(25));
        private static readonly Regex Code = new(@"\b(?:code|codigo|código)\s*[:=]\s*[a-z]{6}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(25));
        internal static string Normalize(string value)
        {
            if(value == null || value.Length > 1600) return "";
            var output = new StringBuilder();
            foreach(char c in value.Normalize(NormalizationForm.FormKC))
                if(CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.Format && !char.IsControl(c)) output.Append(c);
            return output.ToString();
        }
        public static bool IsAd(string value)
        {
            try
            {
                string text = Normalize(value);
                return Invite.IsMatch(text) || Recruitment.IsMatch(text) ||
                    (Code.IsMatch(text) && Regex.IsMatch(text, @"\b(join|come|lobby|room|sala|unete|entra)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(25)));
            }
            catch(ArgumentException) { return false; }
            catch(RegexMatchTimeoutException) { return false; }
        }
        public static bool MatchesLiteral(string value, IEnumerable<string> filters)
        {
            string text;
            try { text = Normalize(value); } catch(ArgumentException) { return false; }
            foreach(string filter in filters)
            {
                if(string.IsNullOrWhiteSpace(filter)) continue;
                int index = text.IndexOf(filter, StringComparison.OrdinalIgnoreCase);
                while(index >= 0)
                {
                    int end = index + filter.Length;
                    if((index == 0 || !char.IsLetterOrDigit(text[index-1])) && (end == text.Length || !char.IsLetterOrDigit(text[end]))) return true;
                    index = text.IndexOf(filter, index + 1, StringComparison.OrdinalIgnoreCase);
                }
            }
            return false;
        }
    }
}
