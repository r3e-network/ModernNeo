// Copyright (C) 2015-2025 The Neo Project.
//
// ListPluginsRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.Plugins;
using Neo.RPC;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    public sealed class ListPluginsRpcMethod : IRpcMethod
    {
        public string Name => "listplugins";

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            if (parameters is not null && parameters.Count > 0)
                throw new RpcException(RpcError.InvalidParams.Code, "No parameters expected.");

            var result = new JArray();
            foreach (var plugin in Plugin.Plugins.OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                var interfaces = plugin.GetType().GetInterfaces()
                    .Where(i => i.Namespace is not null &&
                                i.Namespace.StartsWith("Neo.Plugins", StringComparison.Ordinal))
                    .Where(i => i != typeof(IApplicationLogProvider) && i != typeof(INep17Tracker))
                    .Select(i => i.Name)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal);

                var interfacesJson = new JArray();
                foreach (var name in interfaces)
                    interfacesJson.Add(name);

                result.Add(new JObject
                {
                    ["name"] = plugin.Name,
                    ["version"] = plugin.Version.ToString(),
                    ["interfaces"] = interfacesJson
                });
            }

            return Task.FromResult<JToken?>(result);
        }
    }
}
