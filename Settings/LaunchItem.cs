namespace Launcher.Settings
{
    /// <summary>
    /// A user-registered application shown as an icon in the launcher window.
    /// </summary>
    public sealed class LaunchItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }
}
