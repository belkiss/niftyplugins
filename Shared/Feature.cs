using EnvDTE;

namespace Aurora
{
    public abstract class Feature
    {
        private readonly string mName;

        public string Name => mName;

        protected Feature(string name)
        {
            mName = name;
        }
    };

    public abstract class PreCommandFeature : Feature
    {
        protected Plugin mPlugin;

        protected PreCommandFeature(Plugin plugin, string name)
            : base(name)
        {
            mPlugin = plugin;
        }
    };
}
