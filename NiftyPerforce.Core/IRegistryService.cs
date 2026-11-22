// SPDX-FileCopyrightText: 2006-2017 Jim Tilander
// SPDX-FileCopyrightText: 2017-2026 Lambert Clara
//
// SPDX-License-Identifier: MIT

using Microsoft.Win32;

namespace NiftyPerforce.Core
{
    public interface IRegistryService
    {
        string? GetValue(string key, string name, RegistryHive hive);
    }
}
