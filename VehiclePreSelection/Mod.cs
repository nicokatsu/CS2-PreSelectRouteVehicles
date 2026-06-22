using Colossal.Logging;
using Game;
using System;
using System.Diagnostics;
using Game.Modding;
using Game.SceneFlow;

namespace VehiclePreSelection
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(VehiclePreSelection)}.{nameof(Mod)}")
            .SetShowsErrorsInUI(false);

        public void OnLoad(UpdateSystem updateSystem)
        {
            LogEssential($"[VehiclePreSelection] Loading mod assembly version {typeof(Mod).Assembly.GetName().Version}. Registering UI and apply systems at UIUpdate.");
            updateSystem.UpdateAt<RouteVehicleSelectionUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<RouteVehicleSelectionApplySystem>(SystemUpdatePhase.UIUpdate);
            LogEssential("[VehiclePreSelection] Systems registered.");
        }

        public void OnDispose()
        {
            LogEssential("[VehiclePreSelection] Disposing mod.");
        }

        internal static void LogEssential(string message)
        {
            log.Info(message);
        }

        [Conditional("DEBUG")]
        internal static void LogDiagnostic(string message)
        {
            log.Info(message);
        }

        internal static void LogException(Exception exception, string message)
        {
            log.Info(exception, $"[ERROR] {message}");
        }
    }
}
