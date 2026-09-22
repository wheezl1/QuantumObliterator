using BepInEx.Logging;

namespace QuantumObliterator
{
    internal static class QOLog
    {
        internal static ManualLogSource Source;

        internal static void Info(string msg) => Source?.LogInfo(msg);
        internal static void Warn(string msg) => Source?.LogWarning(msg);
        internal static void Error(string msg) => Source?.LogError(msg);

        /// <summary>Chatty diagnostics, gated on the local VerboseLogging config.</summary>
        internal static void Debug(string msg)
        {
            if (QOConfig.VerboseLogging != null && QOConfig.VerboseLogging.Value)
            {
                Source?.LogInfo(msg);
            }
        }
    }
}
