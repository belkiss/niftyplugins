// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.

namespace Aurora
{
    public sealed class Singleton<T>
        where T : class, new()
    {
        private Singleton() { }

#pragma warning disable CA2211 // Non-constant fields should not be visible
        public static T Instance = new T();
#pragma warning restore CA2211 // Non-constant fields should not be visible
    }
}
