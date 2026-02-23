using Microsoft.Win32;

namespace qbPortWeaver
{
    public static class StartupManager
    {
        private const string RUN_REGISTRY_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";

        // Check if startup with Windows is enabled
        public static bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RUN_REGISTRY_KEY);
                return key?.GetValue(AppConstants.APP_NAME) != null;
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogDebug($"StartupManager.IsStartupEnabled: {ex.Message}");
                return false;
            }
        }

        // Enable/disable startup with Windows
        public static void SetStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RUN_REGISTRY_KEY, true);
                if (key == null)
                {
                    LogManager.Instance.LogMessage("Failed to update startup setting: could not open registry Run key", "WARN");
                    return;
                }

                if (enable)
                    key.SetValue(AppConstants.APP_NAME, Application.ExecutablePath);
                else
                    key.DeleteValue(AppConstants.APP_NAME, false);
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogMessage($"Failed to update startup setting: {ex.Message}", "WARN");
            }
        }
    }
}
