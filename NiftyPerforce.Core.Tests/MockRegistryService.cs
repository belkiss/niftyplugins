// Copyright (C) 2006-2017 Jim Tilander, 2017-2025 Lambert Clara. See the COPYING file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Win32;

namespace NiftyPerforce.Core.Tests
{
    public sealed class MockRegistryService : IRegistryService
    {
        public string? GetValue(string key, string name, RegistryHive hive)
        {
            if (_values?.TryGetValue((key, name, hive), out string? value) ?? false)
            {
                return value;
            }

            return null;
        }

        public void SetValue(string key, string name, RegistryHive hive, string value)
        {
            _values ??= new Dictionary<(string Key, string Name, RegistryHive Hive), string>();
            _values.Add((key, name, hive), value);
        }

        private Dictionary<(string Key, string Name, RegistryHive Hive), string>? _values;
    }
}
