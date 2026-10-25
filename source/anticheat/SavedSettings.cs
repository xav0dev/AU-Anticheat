using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using UnityEngine;
using AUAnticheat.ui;
using AUAnticheat.features;

namespace AUAnticheat.anticheat
{
 internal static class SavedSettings
 {
  private sealed class Setting { public Func<string> Read; public Action<string> Apply; public string Default; }
  private static readonly Dictionary<string,Setting> Items=new();
  private static string path, last;
  private static float next;
  public static string Status { get; private set; }="Settings not initialized.";
  private static string Text<T>(T value)=>Convert.ToString(value,CultureInfo.InvariantCulture);
  private static void Bind<T>(string key,Func<T> read,Action<T> write,T fallback)
  {
   Items[key]=new Setting { Read=()=>Text(read()),Default=Text(fallback),Apply=text=>
   {
    object value=typeof(T).IsEnum?Enum.Parse(typeof(T),text):Convert.ChangeType(text,typeof(T),CultureInfo.InvariantCulture);
    if(typeof(T).IsEnum&&!Enum.IsDefined(typeof(T),value)) return;
    if(value is float f&&(float.IsInfinity(f)||float.IsNaN(f))) return;
    write((T)value);
   }};
  }
  public static void Initialize(string directory)
  {
   Items.Clear(); path=Path.Combine(directory,"settings.json"); next=0;
   Bind("Detection.Enabled",()=>Anticheat.Enabled,v=>Anticheat.Enabled=v,true);
   Bind("Detection.Alerts",()=>Anticheat.SendNotification,v=>Anticheat.SendNotification=v,true);
   Bind("Detection.Block",()=>Anticheat.DiscardInvalidMessages,v=>Anticheat.DiscardInvalidMessages=v,true);
   Bind("Detection.Platforms",()=>Anticheat.CheckSpoofedPlatforms,v=>Anticheat.CheckSpoofedPlatforms=v,true);
   Bind("Detection.Behavior",()=>Anticheat.EnhancedMenuBehaviorChecks,v=>Anticheat.EnhancedMenuBehaviorChecks=v,true);
   Bind("Detection.Proximity",()=>ProximityMonitor.Enabled,v=>ProximityMonitor.Enabled=v,false);
   Bind("Detection.ReferencedOwnerPunishment",()=>Anticheat.UseReferencedOwnerPunishment,v=>Anticheat.UseReferencedOwnerPunishment=v,true);
   Bind("Detection.Response",()=>Anticheat.Punishment,v=>Anticheat.Punishment=v,Anticheat.Punishments.PersistentBan);
   Bind("Detection.Threshold",()=>Anticheat.EnforcementThreshold,v=>Anticheat.EnforcementThreshold=Math.Clamp(v,50,200),100);
   Bind("Detection.AlertSeconds",()=>Anticheat.NotificationDuration,v=>Anticheat.NotificationDuration=Math.Clamp(v,1,60),10f);
   foreach(var pair in Anticheat.RpcHandlers) { var item=pair.Value; Bind("RPC."+pair.Key,()=>item.Enabled,v=>item.Enabled=v,true); }
   foreach(var pair in Anticheat.GameDataHandlers) { var item=pair.Value; Bind("GameData."+pair.Key,()=>item.Enabled,v=>item.Enabled=v,true); }
   Bind("Guard.Removal",()=>Protections.ProtectAgainstNonHostKickExploit,v=>Protections.ProtectAgainstNonHostKickExploit=v,true);
   Bind("Guard.LocalRemovalRequests",()=>RemovalRequestGuard.Enabled,v=>RemovalRequestGuard.Enabled=v,true);
   Bind("Guard.VoteKick",()=>Protections.FilterVoteKickReplays,v=>Protections.FilterVoteKickReplays=v,true);
   Bind("Guard.Teleport",()=>Protections.BlockUnexpectedLocalTeleport.Enabled,v=>Protections.BlockUnexpectedLocalTeleport.Enabled=v,true);
   Bind("Guard.Meetings",()=>MeetingProtection.Enabled,v=>MeetingProtection.Enabled=v,true);
   Bind("Guard.MeetingSpam",()=>MeetingProtection.SpamChecks,v=>MeetingProtection.SpamChecks=v,true);
   Bind("Guard.Influencer",()=>InfluencerProtection.Enabled,v=>InfluencerProtection.Enabled=v,true);
   Bind("Guard.LobbyWalls",()=>LobbyMovementGuard.Enabled,v=>LobbyMovementGuard.Enabled=v,true);
   Bind("Guard.Encryption",()=>Protections.ForceDtls.Enabled,v=>Protections.ForceDtls.Enabled=v,true);
   Bind("Guard.Preflight",()=>Protections.PreflightPackets,v=>Protections.PreflightPackets=v,true);
   Bind("Guard.LargeMessages",()=>Protections.BlockLargeGameMessages,v=>Protections.BlockLargeGameMessages=v,true);
   Bind("Guard.MessageTypes",()=>Protections.BlockInvalidGameDataMessages,v=>Protections.BlockInvalidGameDataMessages=v,true);
   Bind("Guard.Numbers",()=>Protections.HardenedReadPackedUInt.Enabled,v=>Protections.HardenedReadPackedUInt.Enabled=v,true);
   Bind("Guard.VotingMemory",()=>Protections.MemoryAllocationOverload.Enabled,v=>Protections.MemoryAllocationOverload.Enabled=v,true);
   Bind("Lobby.ShareDetections",()=>LobbyChat.ShareAlerts,v=>LobbyChat.ShareAlerts=v,true);
   Bind("Moderation.PersistentBans",()=>ModerationStore.EnforceBans,v=>ModerationStore.EnforceBans=v,true);
   Bind("Moderation.ChatResponse",()=>HostModeration.ChatResponse,v=>HostModeration.ChatResponse=v,FilterResponse.HideOrAlert);
   Bind("Moderation.NameResponse",()=>HostModeration.NameResponse,v=>HostModeration.NameResponse=v,FilterResponse.HideOrAlert);
   Bind("Moderation.BotNames",()=>HostModeration.BotNameFilter,v=>HostModeration.BotNameFilter=v,true);
   Bind("Moderation.DefaultNames",()=>HostModeration.DefaultNameFilter,v=>HostModeration.DefaultNameFilter=v,true);
   Bind("Moderation.BotNameResponse",()=>HostModeration.BotNameResponse,v=>HostModeration.BotNameResponse=v,FilterResponse.Ban);
   Bind("Moderation.DefaultNameResponse",()=>HostModeration.DefaultNameResponse,v=>HostModeration.DefaultNameResponse=v,FilterResponse.Ban);
   Bind("Moderation.AdResponse",()=>HostModeration.AdResponse,v=>HostModeration.AdResponse=v,FilterResponse.HideOrAlert);
   Bind("Moderation.AfkResponse",()=>HostModeration.AfkResponse,v=>HostModeration.AfkResponse=v,FilterResponse.HideOrAlert);
   Bind("Moderation.ProximityResponse",()=>HostModeration.ProximityResponse,v=>HostModeration.ProximityResponse=v,FilterResponse.HideOrAlert);
   Bind("Self.RestoreOutfitColor",()=>Wardrobe.RestorePreferredColor,v=>Wardrobe.RestorePreferredColor=v,true);
   Bind("Self.PreferredColor",()=>Wardrobe.PreferredColor,v=>Wardrobe.PreferredColor=Math.Clamp(v,-1,17),-1);
   Bind("Lobby.JoinFlood",()=>HostLobbyAutomation.JoinFloodProtection,v=>HostLobbyAutomation.JoinFloodProtection=v,true);
   Bind("Lobby.AutoLock",()=>HostLobbyAutomation.AutoLock,v=>HostLobbyAutomation.AutoLock=v,true);
   Bind("Lobby.JoinLimit",()=>HostLobbyAutomation.JoinLimit,v=>HostLobbyAutomation.JoinLimit=Math.Clamp(v,4,30),8);
   Bind("Lobby.JoinWindow",()=>HostLobbyAutomation.JoinWindowSeconds,v=>HostLobbyAutomation.JoinWindowSeconds=Math.Clamp(v,5,60),10);
   Bind("Lobby.AutoStart",()=>HostLobbyAutomation.AutoStart,v=>HostLobbyAutomation.AutoStart=v,false);
   Bind("Lobby.StartPlayers",()=>HostLobbyAutomation.StartPlayers,v=>HostLobbyAutomation.StartPlayers=v==0?0:Math.Clamp(v,4,15),0);
   Bind("Lobby.StartDelay",()=>HostLobbyAutomation.StartDelaySeconds,v=>HostLobbyAutomation.StartDelaySeconds=Math.Clamp(v,5,120),10);
   Bind("Lobby.AutoReturn",()=>HostLobbyAutomation.AutoReturn,v=>HostLobbyAutomation.AutoReturn=v,false);
   Bind("Community.Enabled",()=>CommunityServices.Enabled,v=>CommunityServices.Enabled=v,false);
   Bind("Community.Response",()=>CommunityServices.Response,v=>CommunityServices.Response=v,FilterResponse.Ban);
   Bind("Community.ReportingEndpoint",()=>CommunityServices.ReportingEndpoint,v=>CommunityServices.ReportingEndpoint=CommunityListRules.ReportingUrl(v)?v:"",CommunityServices.DefaultReportingEndpoint);
   // Do not persist connection-mode switching: selecting a modded server mode is
   // deliberate per launch. Preserve the editable values, not an active lobby mode.
   foreach(var option in CustomLobbySettings.Options)
    { var item=option; Bind("CustomDraft."+item.Name,()=>item.Value,v=>item.Value=float.IsFinite(v)?Math.Clamp(v,item.Min,item.Max):item.DefaultValue,item.DefaultValue); }
   foreach(var option in LobbyGameplayRules.Options)
    { var item=option; Bind("GameplayDraft."+item.Key,()=>item.Value,v=>item.Value=float.IsFinite(v)?Math.Clamp(v,item.Min,item.Max):item.Default,item.Default); }
   foreach(string task in Enum.GetNames(typeof(TaskTypes)))
    { string key=task; Bind("GameplayTaskDisabled."+key,()=>LobbyGameplayRules.DisabledTasks.Contains(key),v=> { if(v) LobbyGameplayRules.DisabledTasks.Add(key); else LobbyGameplayRules.DisabledTasks.Remove(key); },false); }
   Bind("Chat.HideAds",()=>ChatModeration.Ads,v=>ChatModeration.Ads=v,true);
   Bind("Chat.HideSpam",()=>ChatModeration.Spam,v=>ChatModeration.Spam=v,true);
   Bind("Chat.WordFilter",()=>ChatModeration.WordFilter,v=>ChatModeration.WordFilter=v,false);
   Bind("Chat.NameFilter",()=>ChatModeration.NameFilter,v=>ChatModeration.NameFilter=v,false);
   Bind("Moderation.AfkAlerts",()=>ChatModeration.AfkAlerts,v=>ChatModeration.AfkAlerts=v,false);
   Bind("Moderation.AfkSeconds",()=>ChatModeration.AfkSeconds,v=>ChatModeration.AfkSeconds=Math.Clamp(v,60,600),120);
   Bind("Lobby.Reports",()=>RoundReports.Enabled,v=>RoundReports.Enabled=v,true);
   Bind("Lobby.Extensions",()=>LobbyExtension.Enabled,v=>LobbyExtension.Enabled=v,true);
   Bind("Lobby.LevelRule",()=>LobbyLevelPolicy.Enabled,v=>LobbyLevelPolicy.Enabled=v,false);
   Bind("Lobby.MinimumLevel",()=>LobbyLevelPolicy.MinimumLevel,v=>LobbyLevelPolicy.MinimumLevel=Math.Max(1u,v),10u);
   Bind("Lobby.Rules",()=>LobbyRules.Enabled,v=>LobbyRules.Enabled=v,false);
   Bind("Lobby.RulesDelay",()=>LobbyRules.DelaySeconds,v=>LobbyRules.DelaySeconds=v,10);
   Bind("UI.Scale",()=>MainUI.scale,v=>MainUI.scale=Math.Clamp(v,.75f,1.5f),1f);
   Bind("UI.MenuKey",()=>MainUI.MenuKey,v=> { if(v==KeyCode.Insert || v==KeyCode.Home || (v>=KeyCode.F1 && v<=KeyCode.F12)) MainUI.MenuKey=v; },KeyCode.Insert);
   Bind("UI.Opacity",()=>Styles.menuOpacity,v=>Styles.menuOpacity=Math.Clamp(v,.35f,1f),.75f);
   Bind("UI.X",()=>MainUI.windowPosition.x,v=>MainUI.windowPosition=new Vector2(Math.Clamp(v,0,100000),MainUI.windowPosition.y),90f);
   Bind("UI.Y",()=>MainUI.windowPosition.y,v=>MainUI.windowPosition=new Vector2(MainUI.windowPosition.x,Math.Clamp(v,0,100000)),70f);
   Bind("Alerts.HidePopups",()=>AUAnticheatPlugin.notifications.DisableNotifications,v=>AUAnticheatPlugin.notifications.DisableNotifications=v,false);
   Bind("Alerts.Technical",()=>AUAnticheatPlugin.notifications.ShowTechnicalDetails,v=>AUAnticheatPlugin.notifications.ShowTechnicalDetails=v,false);
   try
   {
    Directory.CreateDirectory(directory);
    if(File.Exists(path))
    {
     if(new FileInfo(path).Length>65536) throw new IOException("Settings file too large");
     var values=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(path));
     if(values==null) throw new FormatException();
     foreach(var pair in values) if(Items.TryGetValue(pair.Key,out var setting))
      try { setting.Apply(pair.Value); } catch(Exception) { /* Bad entries leave that setting at its existing default. */ }
     // Preserve the old combined Ban + save toggle as the new single choice.
     if(values.TryGetValue("Moderation.SaveCheaterBans",out var save) && bool.TryParse(save,out bool persist) && persist && Anticheat.Punishment==Anticheat.Punishments.Ban)
      Anticheat.Punishment=Anticheat.Punishments.PersistentBan;
    }
    last=Snapshot(); Status="Settings save automatically on change.";
   }
   catch(Exception) { last=Snapshot(); Status="Could not load settings. Existing values kept; check settings.json."; }
  }
  private static string Snapshot()
  {
   var values=new Dictionary<string,string>(); foreach(var item in Items) values[item.Key]=item.Value.Read();
   return JsonSerializer.Serialize(values,new JsonSerializerOptions { WriteIndented=true });
  }
  public static void Tick()
  {
   if(path==null||Time.realtimeSinceStartup<next) return;
   next=Time.realtimeSinceStartup+1; Save();
  }
  public static void Save()
  {
   if(path==null) return;
   string data=Snapshot(); if(data==last&&File.Exists(path)) return;
   try { File.WriteAllText(path+".tmp",data); File.Move(path+".tmp",path,true); last=data; Status="Settings saved automatically."; }
   catch(Exception error) { Status=$"Settings could not be saved ({error.GetType().Name}). Check folder permissions."; }
  }
  public static void ResetDefaults()
  {
   foreach(var item in Items.Values) item.Apply(item.Default);
   FriendCodeExceptions.Clear();
   Styles.primaryColor=Styles.UIColors.Cardinal;
   Save();
  }
 }
}
