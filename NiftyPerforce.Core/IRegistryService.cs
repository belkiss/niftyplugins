// SPDX-FileCopyrightText: 2006-2017 Jim Tilander
// SPDX-FileCopyrightText: 2017-2026 Lambert Clara
//
// SPDX-License-Identifier: MIT

using System;
using Microsoft.Win32;

namespace NiftyPerforce.Core
{
    public interface IRegistryService
    {
        string? GetValue(string key, string name, RegistryHive hive);
    }

    public sealed class DefaultRegistryService : IRegistryService
    {
        public string? GetValue(string key, string name, RegistryHive hive)
        {
            if (hive == RegistryHive.LocalMachine || hive == RegistryHive.CurrentUser)
            {
                RegistryKey registryKey = hive == RegistryHive.LocalMachine ? Registry.LocalMachine : Registry.CurrentUser;
                using RegistryKey? subKey = registryKey.OpenSubKey(key);
                return subKey?.GetValue(name) as string;
            }

            throw new NotImplementedException($"Registry must be either LocalMachine or CurrentUser, {hive} is not supported.");
        }
    }
}
