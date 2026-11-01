using System;
using BepInEx.Configuration;

namespace CLASSIFIED
{
    /// <summary>
    /// Read by the F12 configuration manager by name, through reflection: only the fields used
    /// here. Lets a setting draw itself, which is how the showreel gets a button.
    /// </summary>
    internal sealed class ConfigurationManagerAttributes
    {
        public Action<ConfigEntryBase> CustomDrawer;
        public bool HideDefaultButton;
    }
}
