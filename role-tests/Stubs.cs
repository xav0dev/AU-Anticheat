namespace HarmonyLib
{
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type t,string name){} }
    public class HarmonyPriority : Attribute { public HarmonyPriority(int n){} }
    public static class Priority { public const int First=800; }
}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    public class Il2CppStructArray<T> { readonly T[] values; public Il2CppStructArray(T[] v) { values=v; } public int Length=>values.Length; public T this[int i]=>values[i]; }
}
namespace UnityEngine
{
    public static class Time { public static float realtimeSinceStartup; }
    public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} public static float Distance(Vector2 a,Vector2 b)=>MathF.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)); }
    public class Transform { public Vector2 position; }
}
namespace Hazel
{
    public class MessageReader
    {
        public byte[] Buffer=Array.Empty<byte>(); public int Position, Offset, Length;
        public int BytesRemaining=>Length-Position;
        public ushort ReadUInt16(){var v=BitConverter.ToUInt16(Buffer,Offset+Position);Position+=2;return v;}
    }
}
namespace AmongUs.GameOptions { public enum RoleTypes : ushort { Crewmate=0, Engineer=3, GuardianAngel=4, CrewmateGhost=6, ImpostorGhost=7, SpiritGuide=21 } }
public static class RoleManager { public static bool IsGhostRole(AmongUs.GameOptions.RoleTypes r)=>r is AmongUs.GameOptions.RoleTypes.GuardianAngel or AmongUs.GameOptions.RoleTypes.CrewmateGhost or AmongUs.GameOptions.RoleTypes.ImpostorGhost or AmongUs.GameOptions.RoleTypes.SpiritGuide; }
public class SocialMediumFeedSystem
{
    public List<object> PossibleSpriteList=new(){new(),new(),new()}; public int Id=1;
    public int GetInstanceID()=>Id;
    public void HandleSpiritGuideMessage(Hazel.MessageReader r){}
    public void AddImageToFeed(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> a){}
}
public enum RpcCalls { SnapTo=21, SpiritGuideMessage=67 }
public class PlayerData { public bool IsDead; public AmongUs.GameOptions.RoleTypes RoleType; public object Role=new(); }
public class PlayerPhysics { public bool DoingCustomAnimation; }
public class PlayerControl
{
    public bool roleAssigned=true, inVent,onLadder,inMovingPlat;
    public PlayerData Data=new(); public PlayerPhysics MyPhysics=new(); public UnityEngine.Transform transform=new();
    public void Die(bool assignGhostRole){} public void Exiled(){} public void CoSetRole(AmongUs.GameOptions.RoleTypes role,bool canOverride){}
}
public class AmongUsClient { public static AmongUsClient Instance=new(); public bool AmHost=true; }
public class GameManager { public static GameManager Instance=new(); public bool GameHasStarted=true; }
public class ShipStatus { public static ShipStatus Instance=new(); }
public class LobbyBehaviour { public static LobbyBehaviour Instance; }
public class MeetingHud { public static MeetingHud Instance; }
public class CustomNetworkTransform {}
public static class NetHelpers { public static UnityEngine.Vector2 ReadVector2(Hazel.MessageReader r)=>new(unchecked((short)r.ReadUInt16()),unchecked((short)r.ReadUInt16())); }
namespace AUAnticheat
{
    public static class Utilities { public static bool ServerAuthority; public static bool IsAnticheatPresent()=>ServerAuthority; }
    public static class AUAnticheatPlugin { public static LogStub Log=new(); public static NoticeStub notifications=new(); }
    public class LogStub { public void LogWarning(string s){} }
    public class NoticeStub { public void Send(string title,string body){} }
}
namespace AUAnticheat.anticheat
{
    public static class LobbyChat { public static void ProtectionNotice(){} }
    public abstract class RpcCheck { public abstract bool Validate(PlayerControl p,Hazel.MessageReader r); public abstract RpcCalls GetId(); public virtual Type GetExpectedNetObject()=>typeof(PlayerControl); }
    public static class Anticheat
    {
        public static bool RoundGrace,MoveGrace,GhostGrace,MeetingGrace;
        public static bool IsRoundStartGrace()=>RoundGrace;
        public static bool HasMovementGrace(PlayerControl p)=>MoveGrace;
        public static bool HasGhostRoleGrace(PlayerControl p)=>GhostGrace;
        public static bool HasMeetingTransitionGrace()=>MeetingGrace;
        public static void GrantMovementGrace(PlayerControl p,double s=2)=>MoveGrace=true;
        public static void GrantGhostRoleGrace(PlayerControl p,double s=8)=>GhostGrace=true;
        public static void Reset(){RoundGrace=MoveGrace=GhostGrace=MeetingGrace=false;}
        // Deliberately no Flag/Punish API: new adapters must never attribute an unknown sender.
    }
}
