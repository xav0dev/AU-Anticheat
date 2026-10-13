global using AmongUs.GameOptions;
namespace HarmonyLib { public class HarmonyPatch : Attribute { public HarmonyPatch(Type t,string s){} } }
namespace UnityEngine { public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} } public static class Time { public static float realtimeSinceStartup; } public static class Application { public static string version="test-build"; } }
namespace InnerNet
{
    public enum QuickChatModes { FreeChatOrQuickChat, QuickChatOnly }
    public class InnerNetClient {
        public enum GameStates { Joined, Started }
        public bool AmHost=true; public int HostId=1,GameId=42,ClientId=1; public GameStates GameState;
        public Dictionary<int,ClientData> Clients=new(); public List<(int Id,bool Ban)> Removals=new();
        public ClientData FindClientById(int id)=>Clients.TryGetValue(id,out var c)?c:null;
        public void KickPlayer(int id,bool ban)=>Removals.Add((id,ban));
        public void SendLateRejection(int id,int reason){}
    }
    public class ClientData { public int Id; public PlayerControl Character; public uint PlayerLevel; public bool IsBeingCreated,IsReady; public string FriendCode=""; }
}
namespace AmongUs.GameOptions { public enum RoleTypes { Crewmate, Impostor, Shapeshifter, Phantom, ImpostorGhost, SpiritGuide } }
public static class RoleManager { public static void SetRole(PlayerControl player,RoleTypes role){} public static bool IsGhostRole(RoleTypes r)=>r is RoleTypes.ImpostorGhost or RoleTypes.SpiritGuide; public static bool IsImpostorRole(RoleTypes r)=>r is RoleTypes.Impostor or RoleTypes.Shapeshifter or RoleTypes.Phantom or RoleTypes.ImpostorGhost; }
public class PlayerData { public string FriendCode="",PlayerName="Player"; public bool Disconnected,IsDead,IsIncomplete; public uint PlayerLevel; public RoleTypes RoleType; public RoleTypes? RoleWhenAlive; }
public class PlayerControl
{
    public static List<PlayerControl> AllPlayerControls=new(); public static PlayerControl LocalPlayer;
    public int OwnerId; public uint NetId; public bool roleAssigned=true; public PlayerData Data=new();
    public UnityEngine.Vector2 Position; public UnityEngine.Vector2 GetTruePosition()=>Position;
    public void SetLevel(uint level){Data.PlayerLevel=level;}
    public static bool RejectChat, RenderChatDuringSend;
    public static List<string> Sent=new(); public bool RpcSendChat(string text) { if(RejectChat)return false; Sent.Add(text); if(RenderChatDuringSend) HudManager.Instance.Chat.AddChat(this,text); return true; }
    public void MurderPlayer(PlayerControl p,MurderResultFlags f){}
}
[Flags] public enum MurderResultFlags { Failed=0,Succeeded=1 }
public enum GameOverReason { CrewmatesByVote=0, CrewmatesByTask=1, ImpostorsByKill=3 }
public class EndGameResult { public GameOverReason GameOverReason; }
public class AmongUsClient : InnerNet.InnerNetClient
{
    public static AmongUsClient Instance=new();
    public bool BanAllowed=true; public bool CanBan()=>BanAllowed;
    public int GetClientIdFromCharacter(PlayerControl player)=>Clients.Values.FirstOrDefault(c=>c.Character==player)?.Id??-1;
    public void OnPlayerJoined(InnerNet.ClientData data){} public void OnPlayerLeft(InnerNet.ClientData data){} public void OnGameEnd(EndGameResult e){}
    public void OnDisconnected(){}
}
public class LobbyBehaviour
{
    public static LobbyBehaviour Instance=new(); public int Id=1, Requests;
    public int GetInstanceID()=>Id; public void FixedUpdate(){} public void HandleLobbyTimerExtensionRequest(int a,bool b,int c,int d,int e){}
    public void RpcExtendLobbyTimer()=>Requests++;
    public void LobbyTimerExtended(){}
}
public class GameManager { public static GameManager Instance=new(); public bool GameHasStarted; public TestLogic LogicOptions=new(); }
public class GameOptionsManager { public static GameOptionsManager Instance=new(); public TestOptions CurrentGameOptions=new(),GameHostOptions=new(); }
public class TestOptions
{
 public int Version {get;set;}=12; public int MaxPlayers {get;set;}=15; public int MapId {get;set;} public int GameMode {get;set;}
 public int KillCooldown {get;set;}=30; public bool Invalid {get;set;} public bool AreInvalid(int count)=>Invalid;
}
public class TestFactory
{
 public byte[] ToBytes(TestOptions options,bool april)=>System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(options);
 public TestOptions FromBytes(byte[] bytes)=>System.Text.Json.JsonSerializer.Deserialize<TestOptions>(bytes);
}
public class TestLogic { public TestFactory gameOptionsFactory=new(); public TestOptions Applied; public int Syncs; public void SetGameOptions(TestOptions options)=>Applied=options; public void SyncOptions()=>Syncs++; }
public class GameStartManager { public static GameStartManager Instance=new(); public enum StartingStates { NotStarting,Countdown } public StartingStates startState; }
public class GameData { public static void OnMeetingStart(){} }
namespace Il2CppInterop.Runtime.InteropTypes.Arrays { public class Il2CppStructArray<T> { readonly T[] v; public Il2CppStructArray(T[] values){v=values;} public int Length=>v.Length; public T this[int i]=>v[i]; } }
public class MeetingHud { public static MeetingHud Instance; public struct VoterState { public byte VoterId,VotedForId; } public void VotingComplete(){} }
public class HudManager { public static HudManager Instance=new(); public ChatController Chat=new(); }
public class ChatController
{
    public static float MAX_CHAT_SEND_RATE=3f;
    private float lastMessage=-100;
    // Model the game's timer advancing between frames, not a perpetually-ready stub.
    public float timeSinceLastMessage { get=>UnityEngine.Time.realtimeSinceStartup-lastMessage; set=>lastMessage=UnityEngine.Time.realtimeSinceStartup-value; }
    public int Rendered;
    public void AddChat(PlayerControl player,string text){if((bool)typeof(AUAnticheat.anticheat.LobbyChat).GetNestedType("ObserveChatRender",System.Reflection.BindingFlags.NonPublic)!.GetMethod("Prefix",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.Invoke(null,new object[]{player,text})!) Rendered++;}
}
namespace AmongUs.Data
{
    public static class DataManager { public static TestSettings Settings=new(); }
    public class TestSettings { public TestMultiplayer Multiplayer=new(); }
    public class TestMultiplayer { public InnerNet.QuickChatModes ChatMode; }
}
namespace AUAnticheat
{
    public static class AUAnticheatPlugin { public static bool ProtectionsAttached=true; public static Logger Log=new(); public static Notifications notifications=new(); }
    public class Logger { public void LogWarning(string s){} }
    public class Notifications { public List<(string Title,string Body)> Messages=new(); public void Send(string t,string s)=>Messages.Add((t,s)); }
}
namespace AUAnticheat.anticheat
{
    public static class PacketPreflight { public static bool Block(string s)=>false; }
    public static class Anticheat { public static int Grace; public static void GrantMeetingTransitionGrace(double s)=>Grace++; public static void GrantMovementGrace(PlayerControl p,double s)=>Grace++; }
}
