// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.

namespace Aurora
{
    public sealed class Singleton<T>
        where T : class, new()
    {
        private Singleton() { }

        private static T instance = new T();

        public static T Instance { get => instance; set => instance = value; }
    }
}
