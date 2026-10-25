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
        private static readonly Regex Links = new(@"(?:https?\s*:\s*/\s*/|www\s*[.]\s*|\b[a-z0-9](?:[a-z0-9-]{0,62})\s*(?:[.]|\[dot\]|\(dot\))\s*(?:com|net|org|gg|io|me|co|xyz|app|dev|live|tv|uk|es)\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(25));
        private static readonly Regex Promotion = new(@"\b(?:s\s*n\s*s|s\s*&\s*s|shift\s*(?:and|&)\s*seek|expert|modded?|hosted?|hosting|join|lobby|lobbies|room|sala|codigo|código|code|unete|únete)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(25));
        private static readonly Regex LobbyCodes = new(@"(?<![a-z])([a-z]{6})(?![a-z])|(?<![a-z])([a-z](?:[ .\-][a-z]){5})(?![a-z])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(25));
        private static readonly HashSet<string> OrdinaryWords = new(StringComparer.OrdinalIgnoreCase) { "expert","please","people","player","anyone","really","little","before","inside","around","should","thanks","report","killed","killer","murder","orange","yellow","maroon","banana","purple","friend","public","modded","normal","lobbys","server","button","voting","joined","called","cannot","always","guyswe","region","europe","police","cheats" };
        internal static string Normalize(string value)
        {
            if(value == null || value.Length > 1600) return "";
            var output = new StringBuilder();
            foreach(char c in value.Normalize(NormalizationForm.FormKC))
                if(CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.Format)
                { if(char.IsWhiteSpace(c)) output.Append(' '); else if(!char.IsControl(c)) output.Append(c); }
            // Match rendered text even when advertisers insert rich-text tags.
            return Regex.Replace(output.ToString(), @"<[^>]{0,120}>", "", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(25));
        }
        public static bool IsAd(string value)
        {
            try
            {
                string text = Normalize(value);
                if(Links.IsMatch(text) || Invite.IsMatch(text) || Recruitment.IsMatch(text) || Code.IsMatch(text)) return true;
                bool context = Promotion.IsMatch(text);
                foreach(Match match in LobbyCodes.Matches(text))
                {
                    string token=Regex.Replace(match.Value,@"[ .\-]","");
                    if(OrdinaryWords.Contains(token)) continue;
                    // Bare six-letter codes and short mode + code advertisements.
                    // Lowercase ordinary conversation is not treated as a bare code.
                    if(token==token.ToUpperInvariant() && (context || text.Trim()==match.Value)) return true;
                    if(context && text.Split(' ',StringSplitOptions.RemoveEmptyEntries).Length<=4 && !Regex.IsMatch(text,@"[?!]")) return true;
                }
                return false;
            }
            catch(ArgumentException) { return false; }
            catch(RegexMatchTimeoutException) { return false; }
        }
        public static bool MatchesLiteral(string value, IEnumerable<string> filters)
        {
            string text;
            try { text = Normalize(value); } catch(ArgumentException) { return false; } catch(RegexMatchTimeoutException) { return false; }
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
