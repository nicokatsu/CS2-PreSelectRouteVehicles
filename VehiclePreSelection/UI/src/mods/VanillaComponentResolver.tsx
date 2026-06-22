import { ModuleRegistry } from "cs2/modding";
import { ReactNode } from "react";

type PropsSection = {
    title?: string | null;
    uiTag?: string;
    children: ReactNode;
};

type ToolButtonProps = {
    focusKey?: unknown;
    src?: string;
    selected?: boolean;
    multiSelect?: boolean;
    disabled?: boolean;
    tooltip?: ReactNode | null;
    selectSound?: unknown;
    uiTag?: string;
    className?: string;
    children?: ReactNode;
    onSelect?: (value: unknown) => unknown;
};

type InfoWrapBoxProps = {
    className?: string;
    children?: ReactNode;
};

type GameDropdownTheme = {
    dropdownToggle?: string;
    indicator?: string;
    dropdownMenu?: string;
    scrollable?: string;
    dropdownItem?: string;
};

type DropdownFlagItemTheme = {
    dropdownFlagItem?: string;
    toggle?: string;
    label?: string;
};

type DropdownFlagItemProps = {
    focusKey?: unknown;
    value: unknown;
    checked?: boolean;
    disabled?: boolean;
    theme?: DropdownFlagItemTheme;
    className?: string;
    children?: ReactNode;
    onChange?: (value: any, checked: boolean) => unknown;
};

type SelectVehiclesSectionTheme = {
    dropdown?: string;
    dropdownLabel?: string;
    wrapbox?: string;
    item?: string;
    pill?: string;
    thumb?: string;
    label?: string;
};

const registryIndex = {
    Section: ["game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.tsx", "Section"],
    ToolButton: ["game-ui/game/components/tool-options/tool-button/tool-button.tsx", "ToolButton"],
    ToolButtonTheme: ["game-ui/game/components/tool-options/tool-button/tool-button.module.scss", "classes"],
    DropdownFlagItem: ["game-ui/common/input/dropdown/items/dropdown-flag-item.tsx", "DropdownFlagItem"],
    GameDropdownTheme: ["game-ui/game/themes/game-dropdown.module.scss", "classes"],
    InfoWrapBox: ["game-ui/game/components/selected-info-panel/shared-components/info-section/info-wrap-box.tsx", "InfoWrapBox"],
    SelectVehiclesSectionTheme: ["game-ui/game/components/selected-info-panel/selected-info-sections/route-sections/select-vehicles-section/select-vehicles-section.module.scss", "classes"],
    SelectVehiclesDropdownItemTheme: ["game-ui/game/components/selected-info-panel/selected-info-sections/route-sections/select-vehicles-section/select-vehicles-dropdown-item.module.scss", "classes"],
    FOCUS_DISABLED: ["game-ui/common/focus/focus-key.ts", "FOCUS_DISABLED"],
};

export class VanillaComponentResolver {
    public static get instance(): VanillaComponentResolver | undefined {
        return this._instance;
    }

    private static _instance?: VanillaComponentResolver;

    public static setRegistry(registry: ModuleRegistry) {
        this._instance = new VanillaComponentResolver(registry);
    }

    private registryData: ModuleRegistry;

    constructor(registry: ModuleRegistry) {
        this.registryData = registry;
    }

    private cachedData: Partial<Record<keyof typeof registryIndex, any>> = {};

    private updateCache(entry: keyof typeof registryIndex) {
        const entryData = registryIndex[entry];
        return (this.cachedData[entry] = this.registryData.registry.get(entryData[0])?.[entryData[1]]);
    }

    public get Section(): ((props: PropsSection) => JSX.Element) | undefined {
        return this.cachedData.Section ?? this.updateCache("Section");
    }

    public get ToolButton(): ((props: ToolButtonProps) => JSX.Element) | undefined {
        return this.cachedData.ToolButton ?? this.updateCache("ToolButton");
    }

    public get ToolButtonTheme(): { ToolButton?: string } | undefined {
        return this.cachedData.ToolButtonTheme ?? this.updateCache("ToolButtonTheme");
    }

    public get DropdownFlagItem(): ((props: DropdownFlagItemProps) => JSX.Element) | undefined {
        return this.cachedData.DropdownFlagItem ?? this.updateCache("DropdownFlagItem");
    }

    public get GameDropdownTheme(): GameDropdownTheme | undefined {
        return this.cachedData.GameDropdownTheme ?? this.updateCache("GameDropdownTheme");
    }

    public get InfoWrapBox(): ((props: InfoWrapBoxProps) => JSX.Element) | undefined {
        return this.cachedData.InfoWrapBox ?? this.updateCache("InfoWrapBox");
    }

    public get SelectVehiclesSectionTheme(): SelectVehiclesSectionTheme | undefined {
        return this.cachedData.SelectVehiclesSectionTheme ?? this.updateCache("SelectVehiclesSectionTheme");
    }

    public get SelectVehiclesDropdownItemTheme(): DropdownFlagItemTheme | undefined {
        return this.cachedData.SelectVehiclesDropdownItemTheme ?? this.updateCache("SelectVehiclesDropdownItemTheme");
    }

    public get FOCUS_DISABLED(): unknown {
        return this.cachedData.FOCUS_DISABLED ?? this.updateCache("FOCUS_DISABLED");
    }
}
