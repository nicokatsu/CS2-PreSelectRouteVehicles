import { ModuleRegistry } from "cs2/modding";
import { ReactNode } from "react";

type PropsSection = {
    title?: string | null;
    uiTag?: string;
    children: ReactNode;
};

const registryIndex = {
    Section: ["game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.tsx", "Section"],
    Checkbox: ["game-ui/common/input/toggle/checkbox/checkbox.tsx", "Checkbox"],
    CheckboxTheme: ["game-ui/game/components/statistics-panel/menu/item/statistics-item.module.scss", "classes"],
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

    public get Checkbox(): ((props: any) => JSX.Element) | undefined {
        return this.cachedData.Checkbox ?? this.updateCache("Checkbox");
    }

    public get CheckboxTheme(): { label?: string } | undefined {
        return this.cachedData.CheckboxTheme ?? this.updateCache("CheckboxTheme");
    }
}
