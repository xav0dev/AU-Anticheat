using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AUAnticheat.anticheat
{
    internal static class CommunityServices
    {
        public const string ListUrl="https://docs.google.com/spreadsheets/d/1X6mvEPqhUeBf-mEluQJ4S0BbBkwVGVMIeYUNCCTIny0/export?format=csv&gid=0";
        public static bool Enabled;
        public static FilterResponse Response=FilterResponse.Ban;
        public const string DefaultReportingEndpoint="https://script.google.com/macros/s/AKfycbxYliNd_CdWw9VUlujbdgB9Z3l8enMxWAqUO5pYFYP6DHzF-MI5Jwk2x2M1qGbsbkzz/exec";
        public static string ReportingEndpoint=DefaultReportingEndpoint;
        public static volatile string Status="Online list off. Enabling downloads the public Google Sheet; Google receives the connection IP.";
        public static volatile string ReportStatus="No report submitted.";
        private static readonly object sync=new();
        private static Dictionary<string,CommunityListRules.Entry> entries=new();
        private static DateTimeOffset loaded, nextFetch, nextReport;
        private static int fetching, reporting;
        private static readonly Dictionary<int,uint> noticed=new();
        private static string room="";
        public static int Count { get { lock(sync) return entries.Count; } }
        public static bool CanEnforce(string code,FilterResponse response)
        {
            lock(sync) return Enabled && Response==response && response!=FilterResponse.HideOrAlert &&
                DateTimeOffset.UtcNow-loaded<=TimeSpan.FromHours(24) && entries.TryGetValue(code,out var entry) &&
                entry.Status=="ban" && entry.Expires>DateTimeOffset.UtcNow;
        }
        public static void Refresh()
        {
            if(!Enabled || DateTimeOffset.UtcNow<nextFetch || Interlocked.CompareExchange(ref fetching,1,0)!=0) return;
            nextFetch=DateTimeOffset.UtcNow.AddMinutes(15);
            _=Fetch();
        }
        private static async Task Fetch()
        {
            try
            {
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
                using var http=new HttpClient(new HttpClientHandler { AllowAutoRedirect=false,UseCookies=false });
                Uri uri=new(ListUrl); string csv=null;
                for(int redirects=0;redirects<4;redirects++)
                {
                    if(!CommunityListRules.GoogleDownload(uri)) throw new IOException("Unexpected download host");
                    using var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false);
                    if((int)response.StatusCode>=300 && (int)response.StatusCode<400)
                    { uri=new Uri(uri,response.Headers.Location ?? throw new IOException()); continue; }
                    response.EnsureSuccessStatusCode();
                    if(response.Content.Headers.ContentLength>262144) throw new IOException();
                    await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                    using var buffer=new MemoryStream(); byte[] chunk=new byte[8192]; int count;
                    while((count=await stream.ReadAsync(chunk.AsMemory(),timeout.Token).ConfigureAwait(false))>0)
                    { if(buffer.Length+count>262144) throw new IOException(); buffer.Write(chunk,0,count); }
                    csv=Encoding.UTF8.GetString(buffer.ToArray()); break;
                }
                var parsed=CommunityListRules.Parse(csv,DateTimeOffset.UtcNow);
                lock(sync) { if(Enabled) { entries=parsed; loaded=DateTimeOffset.UtcNow; } }
                Status=$"Downloaded {parsed.Count} active list entries. Listed status is a community claim, not verified cheating.";
            }
            catch(Exception) { Status="List download/validation failed. No new entries applied. Snapshots older than 24h are never enforced."; }
            finally { Interlocked.Exchange(ref fetching,0); }
        }
        public static void Tick()
        {
            if(!Enabled) { lock(sync) entries=new(); noticed.Clear(); return; }
            Refresh();
            string current=$"{AmongUsClient.Instance?.GameId}:{AmongUsClient.Instance?.HostId}";
            if(current!=room) { room=current; noticed.Clear(); }
            if(!LobbyChat.WaitingLobby || DateTimeOffset.UtcNow-loaded>TimeSpan.FromHours(24)) return;
            foreach(var player in PlayerControl.AllPlayerControls)
            {
                if(player?.Data==null || player==PlayerControl.LocalPlayer || player.Data.Disconnected) continue;
                string code=ChatModeration.BoundCode(player); if(code==null) continue;
                CommunityListRules.Entry entry; lock(sync) entries.TryGetValue(code,out entry);
                if(entry==null || entry.Expires<=DateTimeOffset.UtcNow) continue;
                if(!noticed.TryGetValue(player.OwnerId,out uint net) || net!=player.NetId)
                {
                    if(noticed.Count>=64) noticed.Clear(); noticed[player.OwnerId]=player.NetId;
                    AUAnticheatPlugin.notifications.Send("Community list",$"{PublicText.Name(player.Data.PlayerName)} matches a {entry.Status} entry: {entry.Reason}. Game-reported friend codes are not verified identities.");
                }
                if(entry.Status=="ban") HostModeration.RequestAction(player,Response,"optional shared-list policy");
            }
        }
        public static void SubmitReviewedReport(string code)
        {
            code=FriendCodeExceptions.Normalize(code);
            if(code==null || !CommunityListRules.ReportingUrl(ReportingEndpoint)) { ReportStatus="Configure the Reporting service URL in Lobby → Local moderation and chat filters first."; return; }
            if(DateTimeOffset.UtcNow<nextReport || Interlocked.CompareExchange(ref reporting,1,0)!=0) { ReportStatus="Wait at least 60 seconds between submissions."; return; }
            nextReport=DateTimeOffset.UtcNow.AddSeconds(60);
            _=SendReport(ReportingEndpoint,code);
        }
        private static async Task SendReport(string endpoint,string code)
        {
            try
            {
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
                using var http=new HttpClient(new HttpClientHandler { AllowAutoRedirect=false,UseCookies=false });
                string body=JsonSerializer.Serialize(new { friend_code=code, reason="User-requested review of suspected cheating; sender attribution unverified", version=AUAnticheatPlugin.DisplayVersion });
                using var request=new HttpRequestMessage(HttpMethod.Post,endpoint) { Content=new StringContent(body,Encoding.UTF8,"application/json") };
                using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false);
                // A content-service redirect is fetched as GET, never by repeating
                // the report POST. Refuse all non-Google or extra redirects.
                if((int)response.StatusCode>=300 && (int)response.StatusCode<400)
                {
                    var uri=response.Headers.Location;
                    if(!CommunityListRules.ReportReplyUrl(uri)) throw new IOException();
                    using var reply=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false);
                    await ReadReceipt(reply,timeout.Token).ConfigureAwait(false);
                }
                else await ReadReceipt(response,timeout.Token).ConfigureAwait(false);
            }
            catch(Exception) { ReportStatus="Report delivery unconfirmed. No automatic retry or ban. Check the review service before resubmitting."; }
            finally { Interlocked.Exchange(ref reporting,0); }
        }
        private static async Task ReadReceipt(HttpResponseMessage reply,CancellationToken token)
        {
            reply.EnsureSuccessStatusCode();
            if(reply.Content.Headers.ContentLength>4096) throw new IOException();
            await using var stream=await reply.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var data=new MemoryStream(); byte[] bytes=new byte[1024];int read;
            while((read=await stream.ReadAsync(bytes.AsMemory(),token).ConfigureAwait(false))>0)
            { if(data.Length+read>4096) throw new IOException();data.Write(bytes,0,read); }
            ReportStatus=CommunityListRules.ReportAccepted(Encoding.UTF8.GetString(data.ToArray())) ?
                "Service accepted the report into its review inbox. No ban was added by this client." :
                "Service did not confirm acceptance. Check the private review inbox; no automatic retry or ban.";
        }
    }
}
