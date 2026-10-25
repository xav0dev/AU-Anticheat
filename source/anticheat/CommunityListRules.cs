using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AUAnticheat.anticheat
{
    internal static class CommunityListRules
    {
        public sealed class Entry { public string Code, Status, Reason; public DateTimeOffset Expires; }
        public static bool GoogleDownload(Uri uri) => uri != null && uri.IsAbsoluteUri && uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
            (uri.Host == "docs.google.com" || uri.Host.EndsWith(".googleusercontent.com", StringComparison.OrdinalIgnoreCase));
        public static bool ReportingUrl(string value) => Uri.TryCreate(value,UriKind.Absolute,out var uri) && uri.Scheme == "https" && uri.IsDefaultPort &&
            uri.UserInfo.Length == 0 && uri.Host == "script.google.com" && uri.AbsolutePath.StartsWith("/macros/s/",StringComparison.Ordinal) && uri.AbsolutePath.EndsWith("/exec",StringComparison.Ordinal) && uri.Query.Length == 0;
        public static bool ReportReplyUrl(Uri uri) => uri != null && uri.IsAbsoluteUri && uri.Scheme=="https" && uri.IsDefaultPort && uri.UserInfo.Length==0 && uri.Host=="script.googleusercontent.com";
        public static bool ReportAccepted(string json)
        {
            if(json==null || json.Length>4096) return false;
            try { using var doc=System.Text.Json.JsonDocument.Parse(json); return doc.RootElement.ValueKind==System.Text.Json.JsonValueKind.Object && doc.RootElement.TryGetProperty("accepted",out var accepted) && accepted.ValueKind==System.Text.Json.JsonValueKind.True; }
            catch(System.Text.Json.JsonException) { return false; }
        }
        public static Dictionary<string,Entry> Parse(string csv, DateTimeOffset now)
        {
            if(csv == null || csv.Length > 262144) throw new FormatException("List is too large.");
            var rows = Csv(csv);
            if(rows.Count == 0 || rows[0].Count != 4 || rows[0][0].Trim('\uFEFF',' ') != "friend_code" || rows[0][1] != "status" || rows[0][2] != "reason" || rows[0][3] != "expires_utc")
                throw new FormatException("Expected friend_code,status,reason,expires_utc headers.");
            var result = new Dictionary<string,Entry>(StringComparer.Ordinal);
            var duplicate = new HashSet<string>(StringComparer.Ordinal);
            for(int i=1;i<rows.Count;i++)
            {
                var row=rows[i]; if(row.Count == 1 && row[0].Length == 0) continue;
                if(row.Count != 4) throw new FormatException("Invalid CSV row.");
                string code=FriendCodeExceptions.Normalize(row[0]);
                if(code == null) continue;
                if(result.ContainsKey(code) || duplicate.Contains(code)) { result.Remove(code); duplicate.Add(code); continue; }
                if(row[1] != "ban" && row[1] != "watch") continue;
                DateTimeOffset expiry=DateTimeOffset.MaxValue;
                if(row[3].Length > 0 && (!row[3].EndsWith("Z",StringComparison.Ordinal) || !DateTimeOffset.TryParse(row[3],CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out expiry))) continue;
                if(expiry <= now) continue;
                result[code]=new Entry { Code=code, Status=row[1], Reason=PublicText.Clean(row[2],160), Expires=expiry };
            }
            return result;
        }
        private static List<List<string>> Csv(string input)
        {
            var rows=new List<List<string>>(); var row=new List<string>(); var cell=new StringBuilder(); bool quoted=false,closed=false;
            for(int i=0;i<input.Length;i++)
            {
                char c=input[i];
                if(quoted)
                {
                    if(c=='"') { if(i+1<input.Length && input[i+1]=='"') { cell.Append('"'); i++; } else { quoted=false; closed=true; } }
                    else cell.Append(c);
                }
                else if(c=='"' && cell.Length==0 && !closed) quoted=true;
                else if(c==',' || c=='\r' || c=='\n')
                {
                    row.Add(cell.ToString()); cell.Clear(); closed=false;
                    if(row.Count>4) throw new FormatException("Too many CSV columns.");
                    if(c!=',') { rows.Add(row); row=new(); if(c=='\r' && i+1<input.Length && input[i+1]=='\n') i++; }
                }
                else { if(closed || c=='"') throw new FormatException("Invalid quoted cell."); cell.Append(c); }
                if(cell.Length>4096 || rows.Count>2001) throw new FormatException("List limit exceeded.");
            }
            if(quoted) throw new FormatException("Unclosed CSV cell.");
            if(cell.Length>0 || row.Count>0 || closed) { row.Add(cell.ToString()); rows.Add(row); }
            return rows;
        }
    }
}
