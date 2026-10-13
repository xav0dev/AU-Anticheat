using AUAnticheat.anticheat;
using System.Text;

int assertions = 0;
void Check(bool success, string name)
{
    assertions++;
    if(!success) throw new Exception(name);
}
byte[] Pack(uint value)
{
    List<byte> result = new();
    do
    {
        byte next = (byte)(value & 127);
        value >>= 7;
        result.Add(value == 0 ? next : (byte)(next | 128));
    } while(value != 0);
    return result.ToArray();
}
SequenceEvidenceTests.Run(Check);
byte[] Text(string value)
{
    byte[] bytes = Encoding.UTF8.GetBytes(value);
    return Pack((uint)bytes.Length).Concat(bytes).ToArray();
}
byte[] Child(byte tag, params byte[] body) => new byte[] { (byte)body.Length, (byte)(body.Length >> 8), tag }.Concat(body).ToArray();
byte[] Rpc(byte call, params byte[] payload) => Child(2, new byte[] { 1, call }.Concat(payload).ToArray());
bool Valid(byte call, params byte[] payload) => PacketRules.ValidateRpc(call, payload, out _);

SenderEvidence.Reset();
using(var outer = SenderEvidence.Begin("ShipStatus", 67, 10, 1))
{
    SenderEvidence.NoteCandidate(2, 100);
    SenderEvidence.Record();
    Check(outer.ConflictingClaims, "object owner and claimed actor disagreement retained, not resolved by guessing");
    for(int n=0;n<10000;n++) SenderEvidence.Record();
    Check(SenderEvidence.Count==1, "replayed claims are coalesced without growing evidence memory");
    Check(SenderEvidence.Summary().Contains("Sender UNKNOWN") && SenderEvidence.Summary().Contains("NOT applied"), "repetition never authenticates or applies candidate points");
    using(var inner = SenderEvidence.Begin("PlayerControl", 44, 20, 3))
    {
        Check(ReferenceEquals(SenderEvidence.Current,inner), "nested message owns current evidence");
        SenderEvidence.NoteObjectClaim(99,20);
        Check(inner.ConflictingClaims, "forged object field marked contradictory");
    }
    Check(ReferenceEquals(SenderEvidence.Current,outer), "nested message restores previous evidence");
}
Check(SenderEvidence.Current==null,"disposal does not leak identity into next message");
try { using var scope=SenderEvidence.Begin("test",1,2,3); throw new InvalidOperationException(); } catch(InvalidOperationException){}
Check(SenderEvidence.Current==null,"exceptions do not leak evidence context");
var stale=SenderEvidence.Begin("old lobby",1,2,3);
SenderEvidence.Reset(); stale.Dispose();
Check(SenderEvidence.Current==null && SenderEvidence.Count==0,"old lobby scope cannot restore stale identity");
for(uint i=0;i<1000;i++)
{
    using var scope=SenderEvidence.Begin("test",1,i,1);
    SenderEvidence.Record();
}
Check(SenderEvidence.Count==100,"evidence capped at 100 lobby-local records");
Check(SenderEvidence.Summary(0)=="","zero history limit respected");
SenderEvidence.Reset();

foreach(uint value in new uint[] { 0, 1, 127, 128, 16383, 16384, int.MaxValue, uint.MaxValue })
{
    byte[] encoded = Pack(value);
    int cursor = 0;
    Check(PacketRules.TryPackedUInt(encoded, ref cursor, out uint decoded) && value == decoded && cursor == encoded.Length, "unsigned boundary round trip");
    for(int n = 0; n < encoded.Length; n++)
    {
        cursor = 0;
        Check(!PacketRules.TryPackedUInt(encoded.AsSpan(0, n), ref cursor, out _), "truncated varint must fail");
    }
}
foreach(byte[] invalid in new[] { new byte[] {128}, new byte[] {255,255,255,255,16}, new byte[] {128,128,128,128,128,0} })
{
    int cursor = 0;
    Check(!PacketRules.TryPackedUInt(invalid, ref cursor, out _), "overflow/unterminated varint rejected");
}

// Legal sample structures include death/ghost roles, reports crossing meetings,
// scanner cleanup, shapeshift revert, and ordinary impostor kill results.
var legal = new (byte Call, byte[] Body)[] {
    (1, Pack(0)), (1, Pack(127)), (7, new byte[]{2}), (11, new byte[]{255}),
    (11, new byte[]{3}), (12, new byte[]{2,1,0,0,0}), (15, new byte[]{0,255}),
    (15, new byte[]{1,0}), (19, Pack(4)), (20, Pack(4)), (21, new byte[6]),
    (24, new byte[]{3,253}), (26, new byte[8]), (27, new byte[]{3}),
    (31, new byte[]{1,255}), (35, new byte[]{3,1,128}), (38, Pack(666)),
    (44, new byte[]{3,0,0}), (44, new byte[]{4,0,1}), (46, new byte[]{1,0}),
    (47, new byte[]{2}), (48, new byte[]{2}), (55, new byte[]{1,1}),
    (5, Text("Player")), (13, Text("こんにちは 😃")), (39, Text("hat_normal")),
    (6, new byte[4].Concat(Text("Player")).ToArray()), (8, new byte[5])
};
foreach(var sample in legal)
{
    Check(Valid(sample.Call, sample.Body), $"valid RPC {sample.Call}");
    Check(PacketRules.ValidateBatch(Rpc(sample.Call, sample.Body), true, out _), "valid complete envelope");
}
Check(!Valid(44, 3, 0, 2), "invalid role boolean");
Check(!Valid(15, 9, 1), "invalid scanner boolean");
Check(!Valid(47, 128), "truncated kill target");
Check(!Valid(26, new byte[7]), "truncated vote-kick pair");
Check(!Valid(35, 3, 128), "truncated system actor");
Check(!Valid(13, 2, 0xc0, 0xaf), "invalid UTF8");
Check(!Valid(13, 200, 1, 65), "string extends past message");
Check(!Valid(39, Text(new string('x', 513))), "bounded cosmetic string");
Check(!Valid(23, Pack(uint.MaxValue)), "host result allocation overflow");
Check(Valid(200, 128, 128), "unknown extension not convicted");
Check(Valid(15, 1, 0, 42), "future trailing fields allowed");

// v19 Influencer and Judge payloads, verified against public serializers.
for(int n = 1; n <= 3; n++)
{
    byte[] images = Pack((uint)n).Concat(Enumerable.Range(0,n).Select(i => (byte)i)).ToArray();
    Check(Valid(67, images), "legal Influencer image list");
    Check(PacketRules.ValidateBatch(Rpc(67, images), true, out _), "Influencer envelope allowed");
    for(int cut = 0; cut < images.Length; cut++) Check(!Valid(67, images.Take(cut).ToArray()), "truncated image list blocked before allocation");
}
Check(!Valid(67, 0), "zero images blocked");
Check(!Valid(67, 4, 0, 1, 2, 3), "four images blocked");
Check(!Valid(67, Pack(uint.MaxValue)), "image allocation overflow blocked");
Check(!Valid(67, 128, 128, 128, 128, 128, 0), "unterminated image length blocked");
Check(Valid(67, 1, 255), "catalog index checked in rendering adapter, not guessed at packet layer");
Check(Valid(66, 1, 2, 255, 255), "Judge nonce accepted");
for(int n = 0; n < 4; n++) Check(!Valid(66, new byte[n]), "truncated Judge message blocked");
Check(PacketRules.ValidateBatch(Child(8), true, out _), "known deprecated settings tag is not an invented cheat");
Check(PacketRules.ValidateBatch(Child(207), true, out _), "known Xbox tag allowed without storing identifiers");
for(int tag = 0; tag <= 255; tag++)
    Check(PacketRules.KnownGameDataTag((byte)tag) == new[]{1,2,4,5,6,7,8,207}.Contains(tag), "v19 tag set");
for(int flags = 0; flags < 64; flags++)
    Check(MovementSafetyRules.CanInspectLivingMovement((flags&1)!=0,(flags&2)!=0,(flags&4)!=0,(flags&8)!=0,(flags&16)!=0,(flags&32)!=0) == (flags == 3), "ghosts, unknown state and transitions never trigger living movement enforcement");
for(int count = -1; count <= 5; count++) Check(InfluencerRules.ValidImageCount(count) == (count>=1 && count<=3), "image count bounds");
for(int catalog = 0; catalog <= 256; catalog++)
    for(int index = -1; index <= 256; index++)
        Check(InfluencerRules.ValidImageIndex(index,catalog) == (index>=0 && index<catalog), "sprite index bounds including future catalog sizes");
var imageWindow = new InfluencerBurstWindow();
for(int n = 0; n < 64; n++) Check(imageWindow.TryAccept(0), "generous simultaneous ghost-message burst allowed");
for(int n = 0; n < 1000; n++) Check(!imageWindow.TryAccept(9.99), "image flood held without growing storage");
Check(imageWindow.TryAccept(10), "image budget recovers at boundary");
Check(!imageWindow.TryAccept(double.NaN), "invalid clock does not defeat cap");
Check(imageWindow.TryAccept(-1), "clock rewind resets window");
imageWindow.Reset();
Check(imageWindow.TryAccept(0), "new lobby resets image budget");

byte[] good = Rpc(11, 255);
Check(!PacketRules.ValidateBatch(good.Concat(new byte[]{0,0}).ToArray(), true, out _), "bad tail rejects entire batch before dispatch");
Check(!PacketRules.ValidateBatch(new byte[]{255,255,2,1}, true, out _), "oversized child rejected");
Check(!PacketRules.ValidateBatch(Child(3), true, out _), "invalid tag rejected");
Check(PacketRules.ValidateBatch(Child(200), false, out _), "custom tag policy respected");
Check(PacketRules.ValidateBatch(Enumerable.Range(0,96).SelectMany(_ => Child(7,1)).ToArray(), true, out _), "96 messages allowed");
Check(!PacketRules.ValidateBatch(Enumerable.Range(0,97).SelectMany(_ => Child(7,1)).ToArray(), true, out _), "97 messages rejected atomically");
Check(PacketRules.ValidateBatch(Child(6, Pack(1).Concat(Text("OnlineGame")).ToArray()), true, out _), "valid scene");

// Random malformed inputs must be bounded and must never throw or mutate input.
Random random = new(20260913);
for(int trial = 0; trial < 25000; trial++)
{
    byte[] input = new byte[random.Next(0, 512)];
    random.NextBytes(input);
    byte[] before = (byte[])input.Clone();
    PacketRules.ValidateBatch(input, true, out _);
    PacketRules.ValidateRpc((byte)random.Next(0, 70), input, out _);
    int cursor = 0;
    PacketRules.TryPackedUInt(input, ref cursor, out _);
    Check(input.SequenceEqual(before), "validator must not modify input");
}
// Production adapter must honor Offset AND Position, preserve pooled reader
// state, and reject bad bounds before allocating/copying from the buffer.
byte[] framed = new byte[]{99,99,99}.Concat(good).Concat(new byte[]{99}).ToArray();
var reader = new Hazel.MessageReader { Buffer = framed, Offset = 2, Position = 1, Length = good.Length + 1 };
PacketPreflight.Reset();
Check(PacketPreflight.ValidateBatch(reader, true), "adapter reads offset slice");
Check(reader.Position == 1 && framed[0] == 99, "adapter preserves caller state");
reader.Length++;
Check(!PacketPreflight.ValidateBatch(reader, true), "adapter rejects malformed tail");
Check(reader.Position == 1 && PacketPreflight.BlockedPackets == 1, "block counted without reader movement");
reader.Offset = int.MaxValue;
Check(!PacketPreflight.ValidateBatch(reader, true), "offset overflow rejected");
reader.Offset = -1;
reader.Position = 0;
Check(!PacketPreflight.ValidateBatch(reader, true), "negative offset rejected");
reader.Offset = 0;
reader.Length = 65536;
Check(!PacketPreflight.ValidateBatch(reader, true), "copy allocation bounded");
Check(!PacketPreflight.ValidateBatch(null, true), "null reader blocked");
PacketPreflight.Reset();
Check(PacketPreflight.BlockedPackets == 0 && PacketPreflight.LastBlockReason == "None", "round counter reset");
Check(!MovementRecoveryRules.Crossed(1, 0), "casts beginning inside a wall are ambiguous");
Check(!MovementRecoveryRules.Crossed(1, 0.9f), "wall edge overlap is tolerated");
Check(MovementRecoveryRules.Crossed(1, 0.5f), "clear penetration beyond wall margin");
Check(!MovementRecoveryRules.Crossed(1, 2), "invalid hit distance is not a crossing");
Check(!MovementRecoveryRules.Crossed(float.NaN, 0.5f), "NaN geometry is not evidence");
Check(!MovementRecoveryRules.Finite(float.PositiveInfinity, 0), "infinite points cannot become checkpoints");
Check(MovementRecoveryRules.Fresh(0.1f), "recent snapshot usable");
Check(!MovementRecoveryRules.Fresh(2), "long stalled frames invalidate snapshots");
Check(!MovementRecoveryRules.Fresh(-1), "negative interval invalid");
Check(!MovementRecoveryRules.Fresh(float.NaN), "NaN interval invalid");
Check(!MovementRecoveryRules.ConfirmedMotion(1, 0.5f), "one motion observation cannot trigger recovery");
Check(!MovementRecoveryRules.ConfirmedMotion(4, 0.05f), "buffered burst is not sustained movement");
Check(MovementRecoveryRules.ConfirmedMotion(3, 0.15f), "persistent crossing can be recovered");
Check(!MovementRecoveryRules.ConfirmedMotion(3, 5), "stale crossing series discarded");
for(int flags = 0; flags < 8; flags++)
{
    bool local = (flags & 1) != 0, host = (flags & 2) != 0, server = (flags & 4) != 0;
    Check(MovementRecoveryRules.CanBroadcast(local, host, server) == (host && !server), "only actual host authority can broadcast recovery, not even local ownership overrides server authority");
}
var bounded = new Queue<int>();
for(int i = 0; i < 100000; i++) FloodSafety.Enqueue(bounded, i);
Check(bounded.Count == FloodSafety.MaximumSamples, "flood observations retain bounded memory");
Check(bounded.Peek() == 100000 - FloodSafety.MaximumSamples && bounded.Last() == 99999, "bounded queue retains latest observations");
var votes = new VoteReplayWindow();
Check(votes.Accept(1, 2, 0), "first legitimate vote accepted");
Check(!votes.Accept(1, 2, 0.1), "duplicate pair filtered");
Check(votes.Accept(3, 2, 0.1), "independent voter for same target unaffected");
Check(votes.Accept(1, 4, 0.1), "same voter for different target unaffected");
Check(!votes.Accept(1, 2, 1.99), "replay window boundary protected");
Check(votes.Accept(1, 2, 2), "later vote allowed after replay window");
votes.Forget(1);
Check(votes.Accept(1, 2, 2.01) && votes.Accept(1, 4, 2.01), "departed source cleared");
votes.Forget(2);
Check(votes.Accept(3, 2, 2.02), "departed target cleared for all voters");
votes.Reset();
Check(votes.Count == 0 && votes.Accept(1, 2, 2.03), "new session clears replay state");
votes.Reset();
for(int i = 0; i < 2048; i++) Check(votes.Accept(i, i + 1, 3), "new distinct pair not blocked by another pair's budget");
Check(votes.Count == VoteReplayWindow.MaximumPairs, "spoofed ID spray cannot grow replay state beyond cap");
Check(votes.Accept(2047, 2048, double.NaN), "invalid local clock does not convict a vote");
Check(votes.Accept(2047, 2048, 1), "clock reversal recovers without persistent lockout");
AmongUsClient.Instance = new AmongUsClient();
AmongUsClient.Instance.Clients.UnionWith(new[] { 1, 2, 3 });
Hazel.MessageReader VoteReader(int source, int target) => new() {
    Buffer = new byte[]{ 99, 99 }.Concat(BitConverter.GetBytes(source)).Concat(BitConverter.GetBytes(target)).ToArray(),
    Offset = 1, Position = 1, Length = 9
};
VoteKickGuard.Reset();
var voteReader = VoteReader(1, 2);
Check(VoteKickGuard.Validate(voteReader) && voteReader.Position == 1, "vote guard preserves reader with nonzero offset and position");
Check(!VoteKickGuard.Validate(voteReader) && voteReader.Position == 1, "duplicate vote blocked without consuming reader");
Check(VoteKickGuard.Validate(VoteReader(3, 2)), "guard allows independent voters");
Check(!VoteKickGuard.Validate(VoteReader(1, 1)), "self vote rejected");
Check(!VoteKickGuard.Validate(VoteReader(99, 2)), "unknown claimed voter rejected without attribution");
Check(!VoteKickGuard.Validate(VoteReader(1, 99)), "unknown target rejected");
voteReader.Length--;
Check(!VoteKickGuard.Validate(voteReader) && voteReader.Position == 1, "truncated pair rejected without cursor change");
VoteKickGuard.Forget(2);
Check(VoteKickGuard.Validate(VoteReader(1, 2)), "departed IDs cleared in adapter");
VoteKickGuard.Reset();
Check(VoteKickGuard.RejectedVotes == 0, "vote counter reset");
for(int flags = 0; flags < 64; flags++)
    Check(LobbyAdmissionRules.ShouldBan((flags&1)!=0,(flags&2)!=0,(flags&4)!=0,(flags&8)!=0,(flags&16)!=0,(flags&32)!=0,0,10) == (flags==23), "immediate ban requires enabled host waiting lobby, ready data, remote player and no exception");
Check(!LobbyAdmissionRules.ShouldBan(true,true,true,false,true,false,9,10), "exact level minimum admitted");
Check(!LobbyAdmissionRules.ShouldBan(true,true,true,false,true,false,uint.MaxValue,10), "unknown level sentinel never banned");
Check(!LobbyAdmissionRules.ShouldBan(true,true,true,false,true,false,0,1), "level one is valid at minimum one");
Check(LobbyAdmissionRules.ShouldBan(true,true,true,false,true,false,8,10), "level below minimum triggers ban without delay parameter");
Check(!LobbyAdmissionRules.ShouldBan(true,true,true,false,true,false,0,0), "invalid minimum disabled");
Check(LobbyAdmissionRules.ShouldBan(true,true,true,false,true,false,100,200), "minimums above 100 supported");
Check(!LobbyAdmissionRules.ShouldBan(true,true,true,false,true,false,uint.MaxValue-1,uint.MaxValue), "maximum displayed level does not overflow");
foreach(ushort seq in new ushort[] { 0, 1, 10000, ushort.MaxValue })
foreach(byte op in new byte[] { 0, 1, 2, 3, 4, 5 })
foreach(byte vent in new byte[] { 0, 7, 255 })
{
    byte[] payload = { (byte)seq, (byte)(seq >> 8), op, vent };
    Check(VentilationRules.TryRead(payload, out ushort actualSeq, out byte actualOp, out byte actualVent) &&
        actualSeq == seq && actualOp == op && actualVent == vent, "ventilation sequence never mistaken for vent ID");
}
for(int op = 0; op <= 255; op++)
    Check(VentilationRules.TryRead(new byte[] { 255, 255, (byte)op, 7 }, out _, out _, out _) == (op <= 5), "all operation-byte boundaries validated");
for(int length = 0; length < 4; length++)
    Check(!VentilationRules.TryRead(new byte[length], out _, out _, out _), "truncated ventilation header rejected");
for(byte op = 0; op <= 5; op++) Check(VentilationRules.IsMovement(op) == (op >= 2 && op <= 4), "enter/exit/move grant movement grace, cleaning operations do not");
for(int flags = 0; flags < 16; flags++)
{
    bool authority = (flags & 1) != 0, initialized = (flags & 2) != 0, transition = (flags & 4) != 0, dead = (flags & 8) != 0;
    Check(VentilationRules.ShouldCheckEntryRole(authority, initialized, transition, dead) == (authority && initialized && !transition && !dead), "vent entry checks respect role/authority/transition availability");
}
Check(VentilationRules.RoleAllowsEntry(false, true), "Engineer allowed when transient CanVent flag is false");
Check(VentilationRules.RoleAllowsEntry(true, false), "other vent-capable roles allowed");
Check(!VentilationRules.RoleAllowsEntry(false, false), "stable incompatible role still checked by actual authority");
var scans = new ScannerReplayWindow();
Check(scans.Accept(1,true,255,0), "first scan start allowed");
Check(!scans.Accept(1,true,255,.1), "same active sequence replay suppressed locally");
Check(scans.Accept(2,true,255,.1), "scan sequence belongs to object, not whole lobby");
Check(scans.Accept(1,false,255,.2) && scans.Accept(1,false,255,.3), "scan stop cleanup always allowed");
Check(scans.Accept(1,true,255,.4), "start after stop permitted");
Check(scans.Accept(1,true,0,.5), "byte sequence wrap allowed");
Check(scans.Accept(1,true,0,2.5), "same sequence allowed outside short replay window");
Check(scans.Accept(1,true,0,double.NaN) && scans.Accept(1,true,0,1), "clock fault does not convict scan");
scans.Reset(); Check(scans.Accept(1,true,0,1), "new round clears replay history");
Check(SceneBoundaryRules.Usable(-5,-5,5,5,2), "usable collision envelope");
Check(!SceneBoundaryRules.Usable(-5,-5,5,5,1) && !SceneBoundaryRules.Usable(0,0,0,0,3), "incomplete geometry cannot define a boundary");
Check(!SceneBoundaryRules.Usable(float.NaN,0,10,10,4) && !SceneBoundaryRules.Usable(-500,0,501,10,4), "nonfinite or unreasonable geometry disabled");
Check(!SceneBoundaryRules.Outside(7,0,-5,-5,5,5) && SceneBoundaryRules.Outside(7.1f,0,-5,-5,5,5), "outer boundary includes conservative two-unit margin");
Check(!SceneBoundaryRules.Outside(float.NaN,0,-5,-5,5,5), "unknown position is not positional proof");
Check(!SceneBoundaryRules.Sustained(30,.5f) && !SceneBoundaryRules.Sustained(3,3) && SceneBoundaryRules.Sustained(4,2), "position warning requires time and independent samples");
SenderEvidence.Reset();
using(var targetContext = SenderEvidence.Begin("test target",12,1,2,"Victim"))
{
 Check(SenderEvidence.TargetSummary.Contains("Victim"), "target survives without sender verification");
 SenderEvidence.NoteCandidate(3,100); SenderEvidence.Record();
 Check(SenderEvidence.Summary().Contains("Affected: Victim") && SenderEvidence.Summary().Contains("Sender UNKNOWN") && SenderEvidence.Summary().Contains("Conflicting claims"), "target, conflict and sender uncertainty remain separate");
}
Check(SenderEvidence.TargetSummary.Contains("unknown"), "target context does not leak into next event");
ConfigMigrationTests.Run(Check);
if(args.Length == 2) StartupBindingTests.Run(args[0], args[1], Check);
else if(args.Length != 0) throw new ArgumentException("Pass both plugin DLL and game interop DLL paths, or neither.");
Console.WriteLine($"PASS: {assertions} assertions including 25,000 seeded malformed-input cases and production adapter checks. No game/native runtime required.");
