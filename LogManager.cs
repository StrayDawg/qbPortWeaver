using System.Diagnostics;
using System.Text;

namespace qbPortWeaver
{
    public sealed class LogManager
    {
        private const long MAX_SIZE               = 5 * 1024 * 1024; // 5 MB
        private const int  MAX_LOG_FILES          = 3;   // Keep only 3 logfiles total (including current)
        private const int  ROTATION_CHECK_INTERVAL = 100; // Check rotation every N writes

        // Static instance for global access
        public static LogManager Instance { get; private set; } = null!;

        private readonly string _logFilePath;
        private readonly object _lockObject = new object();
        private int _writeCount;

        // Debug mode flag (when true, LogDebug writes to the log file)
        private volatile bool _debugMode;
        public bool DebugMode
        {
            get => _debugMode;
            set => _debugMode = value;
        }

        // Constructor — enforces singleton; throws if called more than once
        public LogManager(string logFilePath)
        {
            if (Instance != null)
                throw new InvalidOperationException($"{nameof(LogManager)} has already been initialized");
            _logFilePath = logFilePath;
            Instance = this;
        }

        // Log message to logfile (thread-safe)
        public void LogMessage(string message, string type)
        {
            lock (_lockObject)
            {
                try
                {
                    // Check if rotation is needed periodically (every N writes)
                    _writeCount++;
                    if (_writeCount >= ROTATION_CHECK_INTERVAL)
                    {
                        _writeCount = 0;
                        RotateIfNeeded();
                    }

                    string paddedType = type.PadRight(5);
                    string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {paddedType} | {message}{Environment.NewLine}";

                    using var fs = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                    using var writer = new StreamWriter(fs, Encoding.UTF8);
                    writer.Write(logEntry);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"LogManager.LogMessage: {ex.Message}");
                }
            }
        }

        // Log debug message to logfile only when DebugMode is enabled (thread-safe)
        public void LogDebug(string message)
        {
            if (!DebugMode) return;
            LogMessage(message, "DEBUG");
        }

        // Clear all log files and start fresh (thread-safe)
        public void ClearLogs()
        {
            lock (_lockObject)
            {
                try
                {
                    // Delete rotated backup files
                    for (int i = 1; i < MAX_LOG_FILES; i++)
                    {
                        string backup = $"{_logFilePath}.{i}";
                        if (File.Exists(backup))
                            File.Delete(backup);
                    }

                    if (File.Exists(_logFilePath))
                        File.Delete(_logFilePath);

                    _writeCount = 0;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"LogManager.ClearLogs: {ex.Message}");
                }
            }

            // Write fresh entry outside the lock (LogMessage acquires its own lock)
            LogMessage("Logs cleared by user", "INFO");
        }

        // Check logfile size and rotate if exceeds MAX_SIZE (public entry point, acquires lock)
        public void CheckAndRotateLogFile()
        {
            lock (_lockObject)
            {
                RotateIfNeeded();
            }
        }

        // Internal rotation check (must be called while holding _lockObject)
        private void RotateIfNeeded()
        {
            try
            {
                if (!File.Exists(_logFilePath))
                    return;

                var fileInfo = new FileInfo(_logFilePath);
                if (fileInfo.Length > MAX_SIZE)
                {
                    // Delete oldest backup if we already have max files
                    string oldestBackup = $"{_logFilePath}.{MAX_LOG_FILES - 1}";
                    if (File.Exists(oldestBackup))
                        File.Delete(oldestBackup);

                    // Rotate existing backup files (.1 -> .2, current -> .1)
                    RotateBackupFiles();

                    string backupPath = $"{_logFilePath}.1";
                    File.Move(_logFilePath, backupPath, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LogManager.RotateIfNeeded: {ex.Message}");
            }
        }

        // Rotate existing backup files (.1 -> .2, etc.)
        private void RotateBackupFiles()
        {
            for (int i = MAX_LOG_FILES - 2; i >= 1; i--)
            {
                string currentBackup = $"{_logFilePath}.{i}";
                string nextBackup = $"{_logFilePath}.{i + 1}";

                if (File.Exists(currentBackup))
                    File.Move(currentBackup, nextBackup, overwrite: true);
            }
        }
    }
}
