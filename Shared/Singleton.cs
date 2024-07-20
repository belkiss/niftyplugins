// Copyright (C) 2006-2017 Jim Tilander, 2017-2024 Lambert Clara. See the COPYING file in the project root for full license information.

namespace Aurora
{
    public sealed class Singleton<T>
        where T : class, new()
    {
        private Singleton()
        {
        }

        public static T Instance { get; set; } = new T();
    }
}
