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

const registryIndex = {
    Section: ["game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.tsx", "Section"],
    ToolButton: ["game-ui/game/components/tool-options/tool-button/tool-button.tsx", "ToolButton"],
    ToolButtonTheme: ["game-ui/game/components/tool-options/tool-button/tool-button.module.scss", "classes"],
    Checkbox: ["game-ui/common/input/toggle/checkbox/checkbox.tsx", "Checkbox"],
    CheckboxTheme: ["game-ui/game/components/statistics-panel/menu/item/statistics-item.module.scss", "classes"],
    FOCUS_DISABLED: ["game-ui/common/focus/focus-key.ts", "FOCUS_DISABLED"],
};

export class VanillaComponentResolver {
    public static get instance(): VanillaComponentResolver {
        return this._instance!;
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

    public get Checkbox(): ((props: any) => JSX.Element) | undefined {
        return this.cachedData.Checkbox ?? this.updateCache("Checkbox");
    }

    public get CheckboxTheme(): { label?: string } | undefined {
        return this.cachedData.CheckboxTheme ?? this.updateCache("CheckboxTheme");
    }

    public get FOCUS_DISABLED(): unknown {
        return this.cachedData.FOCUS_DISABLED ?? this.updateCache("FOCUS_DISABLED");
    }
}
