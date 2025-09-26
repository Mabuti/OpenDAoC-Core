using System;
using System.Reflection;
using System.Threading;
using DOL.Events;
using DOL.Logging;

namespace DOL.GS.Mimic
{
    public static class MimicCleanupScheduler
    {
        private static readonly Logger Log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);
        private static readonly object Sync = new();
        private static Timer? _cleanupTimer;

        [GameServerStartedEvent]
        public static void Start(DOLEvent e, object sender, EventArgs args)
        {
            lock (Sync)
            {
                _cleanupTimer ??= new Timer(_ => RunCleanup(), null, CleanupInterval, CleanupInterval);
            }
        }

        [ScriptUnloadedEvent]
        public static void Stop(DOLEvent e, object sender, EventArgs args)
        {
            lock (Sync)
            {
                if (_cleanupTimer == null)
                    return;

                _cleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _cleanupTimer.Dispose();
                _cleanupTimer = null;
            }
        }

        private static void RunCleanup()
        {
            try
            {
                int removed = MimicManager.ClearUngroupedMimics();

                if (removed > 0 && Log.IsInfoEnabled)
                    Log.Info($"Cleared {removed} ungrouped mimics during scheduled cleanup.");
            }
            catch (Exception ex)
            {
                if (Log.IsErrorEnabled)
                    Log.Error("Error while clearing ungrouped mimics.", ex);
            }
        }
    }
}
