using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

internal static class StartupBindingTests
{
    // Inspect both compiled assemblies without loading native Unity/IL2CPP types.
    public static void Run(string pluginPath, string gamePath, Action<bool, string> check)
    {
        using var pluginFile = File.OpenRead(pluginPath);
        using var gameFile = File.OpenRead(gamePath);
        using var pluginPe = new PEReader(pluginFile);
        using var gamePe = new PEReader(gameFile);
        var plugin = pluginPe.GetMetadataReader();
        var game = gamePe.GetMetadataReader();
        check(plugin.GetString(plugin.GetAssemblyDefinition().Name) == "AU-Anticheat", "assembly uses AU-Anticheat branding");
        check(plugin.TypeDefinitions.Any(handle =>
        {
            var type = plugin.GetTypeDefinition(handle);
            return plugin.GetString(type.Namespace) == "AUAnticheat" && plugin.GetString(type.Name) == "AUAnticheatPlugin";
        }), "renamed plugin entry point is present");
        check(!plugin.TypeDefinitions.Any(handle =>
        {
            var type = plugin.GetTypeDefinition(handle);
            return plugin.GetString(type.Namespace).Contains("XavAnticheat", StringComparison.OrdinalIgnoreCase) ||
                plugin.GetString(type.Namespace).Contains("Hydra", StringComparison.OrdinalIgnoreCase);
        }), "compiled namespaces have no previous project names");
        check(!plugin.MemberReferences.Any(handle =>
        {
            var member = plugin.GetMemberReference(handle);
            if(plugin.GetString(member.Name) != "DrawTexture" || member.Parent.Kind != HandleKind.TypeReference) return false;
            var type = plugin.GetTypeReference((TypeReferenceHandle)member.Parent);
            return plugin.GetString(type.Namespace) == "UnityEngine" && plugin.GetString(type.Name) == "GUI";
        }), "entire plugin avoids stripped GUI.DrawTexture, including notification cards");
        check(plugin.GetAssemblyDefinition().Version == new Version(0, 9, 0, 1), "beta uses a compatible numeric assembly version");
        var postfix = Find(plugin, "OnPlayerLeft", "Postfix");
        var target = Find(game, "AmongUsClient", "OnPlayerLeft");
        var parameters = postfix.GetParameters().Select(h => plugin.GetParameter(h)).Where(p => p.SequenceNumber > 0).ToArray();
        check(parameters.Length == 1 && plugin.GetString(parameters[0].Name) == "__0", "disconnect hook must bind argument zero, not an interop parameter name");
        check(FirstArgument(plugin, postfix, 1) == "InnerNet.ClientData", "compiled disconnect hook accepts ClientData");
        check(FirstArgument(game, target, 2) == "InnerNet.ClientData", "installed game's first disconnect argument matches hook type");
        var lobbyPostfix = Find(plugin, "ObserveLobby", "Postfix");
        check(!lobbyPostfix.GetParameters().Any(h => plugin.GetParameter(h).SequenceNumber > 0), "lobby observation hook needs no named game arguments");
        int lobbyHooks = 0;
        foreach(var handle in plugin.TypeDefinitions)
        {
            var type = plugin.GetTypeDefinition(handle);
            if(plugin.GetString(type.Name) != "ObserveLobby") continue;
            foreach(var methodHandle in type.GetMethods())
            {
                var method = plugin.GetMethodDefinition(methodHandle);
                if(plugin.GetString(method.Name) != "Postfix") continue;
                check(!method.GetParameters().Any(h => plugin.GetParameter(h).SequenceNumber > 0), "each lobby hook binds without fragile parameter names");
                lobbyHooks++;
            }
        }
        check(lobbyHooks == 2, "movement and minimum-level lobby hooks compiled");
        Find(game, "LobbyBehaviour", "FixedUpdate");
        var snapPrefix = Find(plugin, "BeforeLobbySnap", "Prefix");
        var snapNames = snapPrefix.GetParameters().Select(h => plugin.GetParameter(h)).Where(p => p.SequenceNumber > 0).Select(p => plugin.GetString(p.Name)).ToArray();
        check(snapNames.SequenceEqual(new[] { "__instance", "__0", "__1" }), "snap interceptor binds by ordinal");
        var snapTarget = Find(game, "CustomNetworkTransform", "HandleRpc");
        check(snapTarget.GetParameters().Count(h => game.GetParameter(h).SequenceNumber > 0) == 2, "installed snap RPC has two arguments");
        foreach(var binding in new[] {
            ("BeforeInfluencerDecode", "SocialMediumFeedSystem", "HandleSpiritGuideMessage", new[]{"__0"}, 1),
            ("BeforeInfluencerImages", "SocialMediumFeedSystem", "AddImageToFeed", new[]{"__instance","__0"}, 1),
            ("OnGhostRoleAssigned", "PlayerControl", "CoSetRole", new[]{"__instance","__0"}, 2) })
        {
            var hook = Find(plugin, binding.Item1, "Prefix");
            var names = hook.GetParameters().Select(h => plugin.GetParameter(h)).Where(p => p.SequenceNumber > 0).Select(p => plugin.GetString(p.Name));
            check(names.SequenceEqual(binding.Item4), "new role hook uses ordinal binding");
            var original = Find(game, binding.Item2, binding.Item3);
            check(original.GetParameters().Count(h => game.GetParameter(h).SequenceNumber > 0) == binding.Item5, "v19 target parameter count matches");
        }
        foreach(var binding in new[] {
            ("OnJoin", "Postfix", "AmongUsClient", "OnPlayerJoined", new[]{"__0"}, 1),
            ("OnOffer", "Postfix", "LobbyBehaviour", "HandleLobbyTimerExtensionRequest", new[]{"__instance","__0","__1","__2","__3"}, 5),
            ("OnKillResult", "Prefix", "PlayerControl", "MurderPlayer", new[]{"__0","__1","__state"}, 2),
            ("OnKillResult", "Postfix", "PlayerControl", "MurderPlayer", new[]{"__instance","__0","__state"}, 2),
            ("OnEnd", "Prefix", "AmongUsClient", "OnGameEnd", new[]{"__0"}, 1),
            ("ObserveLocalChat", "Postfix", "PlayerControl", "RpcSendChat", new[]{"__instance","__result"}, 1),
            ("ObserveAppliedLevel", "Postfix", "PlayerControl", "SetLevel", new[]{"__instance","__0"}, 1),
            ("ObserveLocalChat", "Prefix", "PlayerControl", "RpcSendChat", new[]{"__instance","__0"}, 1),
            ("FilterDisplayedChat", "Prefix", "ChatController", "AddChat", new[]{"__0","__1"}, 3),
            ("BeforeKickOrBan", "Prefix", "InnerNetClient", "KickPlayer", new[]{"__instance","__0"}, 2),
            ("BeforeLateRejection", "Prefix", "InnerNetClient", "SendLateRejection", new[]{"__instance","__0"}, 2),
            ("ObserveChatRender", "Prefix", "ChatController", "AddChat", new[]{"__0","__1"}, 3),
            ("PreserveLeavingRole", "Prefix", "AmongUsClient", "OnPlayerLeft", new[]{"__0"}, 2),
            ("CaptureAssignedRole", "Postfix", "RoleManager", "SetRole", new[]{"__0","__1"}, 2),
            ("OnPlayerLeft", "Prefix", "AmongUsClient", "OnPlayerLeft", new[]{"__0","__1"}, 2),
            ("BeforeResults", "Prefix", "MeetingHud", "VotingComplete", new[]{"__0"}, 5),
            ("MeetingStarted", "Prefix", "GameData", "OnMeetingStart", Array.Empty<string>(), 0),
            ("ClearLobbyServices", "Postfix", "AmongUsClient", "OnDisconnected", Array.Empty<string>(), 0) })
        {
            var hook = Find(plugin, binding.Item1, binding.Item2);
            var names = hook.GetParameters().Select(h => plugin.GetParameter(h)).Where(p => p.SequenceNumber > 0).Select(p => plugin.GetString(p.Name));
            check(names.SequenceEqual(binding.Item5), "lobby/report/meeting hook binds by ordinal");
            var original = Find(game, binding.Item3, binding.Item4);
            check(original.GetParameters().Count(h => game.GetParameter(h).SequenceNumber > 0) == binding.Item6, $"{binding.Item3}.{binding.Item4} target count matches");
        }
        var sendSignature = game.GetBlobReader(Find(game, "PlayerControl", "RpcSendChat").Signature);
        sendSignature.ReadSignatureHeader();
        check(sendSignature.ReadCompressedInteger() == 1 && sendSignature.ReadByte() == 0x02,
            "ordinary chat target returns bool for the successful-send hook");
        Find(game, "ChatController", "get_MAX_CHAT_SEND_RATE");
        Find(game, "ChatController", "get_timeSinceLastMessage");
        Find(game, "ChatController", "set_timeSinceLastMessage");
        foreach(var item in new[]{("RpcCalls","SpiritGuideMessage",67), ("RpcCalls","QueueOverruleVotes",66), ("RoleTypes","SpiritGuide",21)})
        {
            bool found = false;
            foreach(var handle in game.TypeDefinitions)
            {
                var type = game.GetTypeDefinition(handle);
                if(game.GetString(type.Name) != item.Item1) continue;
                foreach(var fieldHandle in type.GetFields())
                {
                    var field = game.GetFieldDefinition(fieldHandle);
                    if(game.GetString(field.Name) != item.Item2 || field.GetDefaultValue().IsNil) continue;
                    var constant = game.GetConstant(field.GetDefaultValue());
                    var value = game.GetBlobReader(constant.Value);
                    int numeric = constant.TypeCode == ConstantTypeCode.Byte ? value.ReadByte() : value.ReadUInt16();
                    found = numeric == item.Item3;
                }
            }
            check(found, "v19 role/RPC identifier matches installed metadata");
        }
        Console.WriteLine("PASS: compiled disconnect and lobby-hook bindings match installed game metadata checks.");
    }

    private static MethodDefinition Find(MetadataReader reader, string typeName, string methodName)
    {
        foreach(var handle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(handle);
            if(reader.GetString(type.Name) != typeName) continue;
            foreach(var methodHandle in type.GetMethods())
            {
                var method = reader.GetMethodDefinition(methodHandle);
                if(reader.GetString(method.Name) == methodName) return method;
            }
        }
        throw new Exception($"Missing {typeName}.{methodName}");
    }

    private static string FirstArgument(MetadataReader reader, MethodDefinition method, int parameterCount)
    {
        var signature = reader.GetBlobReader(method.Signature);
        var header = signature.ReadSignatureHeader();
        if(header.IsGeneric) throw new Exception("Unexpected generic hook signature");
        if(signature.ReadCompressedInteger() != parameterCount || signature.ReadByte() != 0x01 || signature.ReadByte() != 0x12)
            throw new Exception("Unexpected disconnect signature");
        var handle = signature.ReadTypeHandle();
        if(handle.Kind == HandleKind.TypeReference)
        {
            var type = reader.GetTypeReference((TypeReferenceHandle)handle);
            return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
        }
        if(handle.Kind == HandleKind.TypeDefinition)
        {
            var type = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
            return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
        }
        throw new Exception("Unexpected disconnect argument type");
    }
}
