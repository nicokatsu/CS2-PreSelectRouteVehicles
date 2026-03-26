using Colossal.Logging;
using Game;
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
            updateSystem.UpdateAt<RouteVehicleSelectionUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<RouteVehicleSelectionApplySystem>(SystemUpdatePhase.UIUpdate);
        }

        public void OnDispose()
        {
        }
    }
}
