// Managed stand-ins only: exercise the real adapter's offsets and error paths
// without starting IL2CPP or sending packets to another player.
public enum RpcCalls { CompleteTask = 1 }
public class VoteBanSystem { public void AddVote(int a,int b){} public void CmdAddVote(int id){} }
namespace HarmonyLib { public class HarmonyPatch : Attribute { public HarmonyPatch(Type t,string n){} } }
namespace AUAnticheat.anticheat { internal static class LobbyChat { public static void ProtectionNotice(){} } }
namespace Hazel
{
    internal class MessageReader
    {
        public byte[] Buffer;
        public int Offset;
        public int Position;
        public int Length;
        public int BytesRemaining => Length - Position;
        public int ReadInt32()
        {
            int value = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(Buffer.AsSpan(Offset + Position, 4));
            Position += 4;
            return value;
        }
    }
}
internal class AmongUsClient
{
    public static AmongUsClient Instance;
    public readonly HashSet<int> Clients = new();
    public object FindClientById(int id) => Clients.Contains(id) ? this : null;
}
namespace AUAnticheat
{
    internal static class AUAnticheatPlugin
    {
        internal static readonly FakeLog Log = new();
        internal static readonly FakeNotifications notifications = new();
    }
    internal class FakeLog { public void LogWarning(string message) { } }
    internal class FakeNotifications { public void Send(string title, string message) { } }
}
