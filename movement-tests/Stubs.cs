namespace HarmonyLib
{
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type t, string n) { } }
    public class HarmonyPriority : Attribute { public HarmonyPriority(int n) { } }
    public static class Priority { public const int First = 800; }
}
namespace UnityEngine
{
    public struct Vector2
    {
        public float x,y; public Vector2(float x,float y) { this.x=x; this.y=y; }
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x-b.x,a.y-b.y);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x+b.x,a.y+b.y);
        public static float Distance(Vector2 a, Vector2 b) => MathF.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));
    }
    public class Transform { public Vector2 position; }
    public enum RigidbodyType2D { Static, Dynamic }
    public class Rigidbody2D { public RigidbodyType2D bodyType; }
    public class Component
    {
        public int Id=1; public int GetInstanceID()=>Id;
        public Collider2D[] Colliders={ new(),new() };
        public T[] GetComponentsInChildren<T>()=>Colliders.Cast<T>().ToArray();
    }
    public class GameObject { public int layer; }
    public struct Bounds
    {
        public Vector2 min,max;
        public void Encapsulate(Bounds other) { min=new(Math.Min(min.x,other.min.x),Math.Min(min.y,other.min.y)); max=new(Math.Max(max.x,other.max.x),Math.Max(max.y,other.max.y)); }
    }
    public class Collider2D { public bool enabled=true, isTrigger; public Rigidbody2D attachedRigidbody; public GameObject gameObject=new(); public Bounds bounds=new(){min=new(-5,-5),max=new(5,5)}; }
    public struct RaycastHit2D { public Collider2D collider; public float fraction, distance; }
    public static class Physics2D
    {
        public static int Casts;
        public static bool ThrowCast;
        private static readonly Collider2D wall = new();
        public static Collider2D[] OverlapCircleAll(Vector2 p,float r,int mask) => Math.Abs(p.x)<0.1f ? new[]{wall} : Array.Empty<Collider2D>();
        public static RaycastHit2D[] LinecastAll(Vector2 a,Vector2 b,int mask)
        {
            Casts++;
            if(ThrowCast) throw new InvalidOperationException("scene geometry unavailable");
            return a.x*b.x < 0 ? new[]{ new RaycastHit2D { collider=wall, fraction=0.5f, distance=Vector2.Distance(a,b)/2 } } : Array.Empty<RaycastHit2D>();
        }
    }
    public static class Time { public static float realtimeSinceStartup; }
}
namespace InnerNet { public class InnerNetClient { public enum GameStates { Joined, Started } } }
namespace Hazel
{
    public class MessageReader
    {
        public byte[] Buffer; public int Position;
        public ushort ReadUInt16() { ushort v=BitConverter.ToUInt16(Buffer,Position); Position+=2; return v; }
    }
}
public class AmongUsClient { public static AmongUsClient Instance=new(); public int GameId=1,HostId=1; public InnerNet.InnerNetClient.GameStates GameState; }
public class LobbyBehaviour : UnityEngine.Component { public static LobbyBehaviour Instance=new(); public void FixedUpdate() { } }
public class ShipStatus : UnityEngine.Component { public static ShipStatus Instance; }
public class GameManager { public static GameManager Instance=new(); public bool GameHasStarted; }
public class PlayerData { public bool IsDead,Disconnected; public string PlayerName="Target"; }
public class PlayerPhysics { public bool DoingCustomAnimation; }
public class PlayerControl
{
    public static List<PlayerControl> AllPlayerControls=new(); public uint NetId=1; public int OwnerId=1;
    public bool inVent,onLadder,inMovingPlat; public PlayerData Data=new(); public PlayerPhysics MyPhysics=new();
    public CustomNetworkTransform NetTransform; public UnityEngine.Transform transform=new();
    public UnityEngine.Vector2 GetTruePosition()=>transform.position;
}
// Deliberately no SnapTo/RpcSnapTo: compiling the production observer proves it
// cannot request ordinary-walking corrections through these APIs.
public class CustomNetworkTransform { public PlayerControl myPlayer; public void HandleRpc(byte id,Hazel.MessageReader reader) { } }
public static class Constants { public const int ShipOnlyMask=1; }
public enum RpcCalls { SnapTo=21 }
public static class NetHelpers { public static UnityEngine.Vector2 ReadVector2(Hazel.MessageReader r)=>new(unchecked((short)r.ReadUInt16())/100f,unchecked((short)r.ReadUInt16())/100f); }
namespace AUAnticheat
{
    public static class AUAnticheatPlugin { public static bool ProtectionsAttached=true; public static TestLog Log=new(); public static Notices notifications=new(); }
    public class TestLog { public void LogWarning(string text) { } }
    public class Notices { public int Count; public void Send(string a,string b) { Count++; } }
}
namespace AUAnticheat.anticheat { public static class PacketPreflight { public static bool Block(string text)=>false; } }
namespace AUAnticheat.anticheat
{
 public static class Anticheat { public static bool Enabled=true; }
 public static class MovementContext { public static bool Stable=true; public static bool StableLivingPlayer(PlayerControl player)=>Stable && !player.Data.IsDead; }
}
