import { ModRegistrar } from "cs2/modding";
import { RouteVehicleMouseToolOptions } from "mods/route-vehicle-tool-section";
import { VanillaComponentResolver } from "mods/VanillaComponentResolver";

const MOUSE_TOOL_OPTIONS_MODULE = "game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.tsx";

const register: ModRegistrar = (moduleRegistry) => {
    VanillaComponentResolver.setRegistry(moduleRegistry);
    moduleRegistry.extend(MOUSE_TOOL_OPTIONS_MODULE, "MouseToolOptions", RouteVehicleMouseToolOptions);
};

export default register;
