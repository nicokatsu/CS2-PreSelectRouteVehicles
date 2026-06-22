import { bindValue, trigger, useValue } from "cs2/api";
import { ModuleRegistryExtend } from "cs2/modding";
import { Dropdown, DropdownToggle } from "cs2/ui";
import { Children, Fragment, cloneElement, isValidElement, ReactElement, ReactNode } from "react";
import { VanillaComponentResolver } from "./VanillaComponentResolver";
import styles from "./route-vehicle-tool-section.module.scss";
import colorRandomIcon from "../imgs/color-random.svg";

type VehicleOption = {
    entityIndex: number;
    name: string;
    id?: string;
    thumbnail?: string;
    objectRequirementIcons?: string[] | null;
};

type ExtendableComponentResult = {
    props?: {
        children?: ReactNode;
    };
} & ReactElement;

const group = "vehiclePreSelection";
const isPlanningRoute$ = bindValue<boolean>(group, "isPlanningRoute", false);
const supportsSecondarySelection$ = bindValue<boolean>(group, "supportsSecondarySelection", false);
const availablePrimaryVehiclesJson$ = bindValue<string>(group, "availablePrimaryVehiclesJson", "[]");
const availableSecondaryVehiclesJson$ = bindValue<string>(group, "availableSecondaryVehiclesJson", "[]");
const selectedPrimaryIndicesJson$ = bindValue<string>(group, "selectedPrimaryIndicesJson", "[]");
const selectedSecondaryIndicesJson$ = bindValue<string>(group, "selectedSecondaryIndicesJson", "[]");
const currentPrimaryVehicle$ = bindValue<string>(group, "currentPrimaryVehicle", "");
const currentSecondaryVehicle$ = bindValue<string>(group, "currentSecondaryVehicle", "");
const autoRandomColorEnabled$ = bindValue<boolean>(group, "autoRandomColorEnabled", false);

const parseVehicleOptions = (value: string): VehicleOption[] => {
    try {
        const parsed = JSON.parse(value);
        return Array.isArray(parsed) ? parsed : [];
    } catch {
        return [];
    }
};

const parseSelectedIndices = (value: string) => {
    try {
        const parsed = JSON.parse(value);
        return new Set<number>(Array.isArray(parsed) ? parsed.filter((item) => typeof item === "number") : []);
    } catch {
        return new Set<number>();
    }
};

const isVehicleSelected = (vehicle: VehicleOption, selectedEntityIndices: Set<number>) =>
    selectedEntityIndices.has(vehicle.entityIndex);

const getSelectedVehicle = (vehicles: VehicleOption[], selectedEntityIndices: Set<number>) => {
    for (let i = 0; i < vehicles.length; i += 1) {
        if (isVehicleSelected(vehicles[i], selectedEntityIndices)) {
            return vehicles[i];
        }
    }

    return null;
};

const getSelectionPreviewLabel = (vehicles: VehicleOption[], selectedEntityIndices: Set<number>) => {
    const selectedVehicles = vehicles.filter((vehicle) => isVehicleSelected(vehicle, selectedEntityIndices));
    if (selectedVehicles.length === 0) {
        return "";
    }

    if (selectedVehicles.length === 1) {
        return selectedVehicles[0].name;
    }

    return `${selectedVehicles[0].name}...`;
};

const getSelectedVehicles = (vehicles: VehicleOption[], selectedEntityIndices: Set<number>) =>
    vehicles.filter((vehicle) => isVehicleSelected(vehicle, selectedEntityIndices));

const joinClasses = (...classes: Array<string | undefined | null | false>) =>
    classes.filter(Boolean).join(" ");

const renderVehicleImages = (vehicle: VehicleOption, className: string | undefined, keyPrefix: string) => (
    <Fragment>
        {vehicle.thumbnail ? <img key={`${keyPrefix}-thumbnail`} src={vehicle.thumbnail} className={className} /> : null}
        {vehicle.objectRequirementIcons?.map((icon, index) => (
            <img key={`${keyPrefix}-req-${index}`} src={icon} className={className} />
        ))}
    </Fragment>
);

const renderVehicleOption = (
    vehicle: VehicleOption,
    theme: { item?: string; thumb?: string; label?: string }
) => (
    <div className={theme.item}>
        {renderVehicleImages(vehicle, theme.thumb, `option-${vehicle.entityIndex}`)}
        <div className={theme.label}>{vehicle.name}</div>
    </div>
);

const renderSelectionPreview = (vehicle: VehicleOption | null, fallback: string, labelClassName?: string) => (
    <div className={styles.selectionPreview}>
        <div className={styles.selectionMain}>
            {vehicle ? renderVehicleImages(vehicle, styles.selectionThumb, `preview-${vehicle.entityIndex}`) : null}
            {fallback ? <div className={joinClasses(styles.selectionLabel, labelClassName)}>{fallback}</div> : null}
        </div>
    </div>
);

const renderSelectedPills = (vehicles: VehicleOption[], selectedEntityIndices: Set<number>) => {
    const selectedVehicles = getSelectedVehicles(vehicles, selectedEntityIndices);
    if (selectedVehicles.length === 0) {
        return null;
    }

    const vanilla = VanillaComponentResolver.instance;
    const InfoWrapBox = vanilla?.InfoWrapBox;
    const theme = vanilla?.SelectVehiclesSectionTheme;
    if (!InfoWrapBox || !theme) {
        return null;
    }

    const content = selectedVehicles.map((vehicle) => (
        <div key={`pill-${vehicle.entityIndex}`} className={joinClasses(theme.item, theme.pill)}>
            {renderVehicleImages(vehicle, theme.thumb, `pill-${vehicle.entityIndex}`)}
            <div className={theme.label}>{vehicle.name}</div>
        </div>
    ));
    const className = joinClasses(styles.selectedWrapbox, theme.wrapbox);

    return <InfoWrapBox className={className}>{content}</InfoWrapBox>;
};

const shouldShowDropdown = (vehicles: VehicleOption[]) => vehicles.length > 1;

const withAppendedToolSection = (result: ExtendableComponentResult, section: JSX.Element) => {
    if (!isValidElement(result)) {
        return result;
    }

    const nextChildren = [...Children.toArray(result.props?.children), section];
    return cloneElement(result, {
        ...result.props,
        children: nextChildren,
    });
};

const isColorSectionTitle = (title: ReactNode) =>
    isValidElement(title) && title.props != null && Object.prototype.hasOwnProperty.call(title.props, "hash");

type VehiclePickerProps = {
    vehicles: VehicleOption[];
    selectedIndices: Set<number>;
    onToggle: (index: number) => void;
};

const VehiclePicker = ({
    vehicles,
    selectedIndices,
    onToggle,
}: VehiclePickerProps) => {
    const selectedVehicle = getSelectedVehicle(vehicles, selectedIndices);
    const previewLabel = getSelectionPreviewLabel(vehicles, selectedIndices);
    const vanilla = VanillaComponentResolver.instance;
    const DropdownFlagItem = vanilla?.DropdownFlagItem;
    const focusDisabled = vanilla?.FOCUS_DISABLED;
    const gameDropdownTheme = vanilla?.GameDropdownTheme;
    const selectVehiclesTheme = vanilla?.SelectVehiclesSectionTheme;
    const selectVehiclesDropdownItemTheme = vanilla?.SelectVehiclesDropdownItemTheme;

    if (!DropdownFlagItem || !gameDropdownTheme || !selectVehiclesTheme || !selectVehiclesDropdownItemTheme) {
        return null;
    }

    const content = (
        <Fragment>
            {vehicles.map((vehicle, index) => {
                const isSelected = isVehicleSelected(vehicle, selectedIndices);
                const disableUnselect = isSelected && selectedIndices.size === 1;
                return (
                    <DropdownFlagItem
                        key={`${vehicle.entityIndex}-${index}`}
                        value={index}
                        checked={isSelected}
                        disabled={disableUnselect}
                        focusKey={focusDisabled}
                        theme={selectVehiclesDropdownItemTheme}
                        onChange={() => onToggle(index)}
                    >
                        {renderVehicleOption(vehicle, selectVehiclesTheme)}
                    </DropdownFlagItem>
                );
            })}
        </Fragment>
    );

    return (
        <div className={styles.dropdownShell}>
            <Dropdown
                alignment="left"
                content={content}
                theme={gameDropdownTheme}
            >
                <DropdownToggle className={joinClasses(selectVehiclesTheme.dropdown, styles.dropdownToggle)}>
                    {renderSelectionPreview(
                        selectedVehicle,
                        previewLabel,
                        selectVehiclesTheme.dropdownLabel
                    )}
                </DropdownToggle>
            </Dropdown>
            {renderSelectedPills(vehicles, selectedIndices)}
        </div>
    );
};

export const RouteVehicleToolSection = () => {
    const vanilla = VanillaComponentResolver.instance;
    const isPlanningRoute = useValue(isPlanningRoute$);
    const supportsSecondarySelection = useValue(supportsSecondarySelection$);
    const availablePrimaryVehicles = parseVehicleOptions(useValue(availablePrimaryVehiclesJson$));
    const availableSecondaryVehicles = parseVehicleOptions(useValue(availableSecondaryVehiclesJson$));
    const selectedPrimaryIndices = parseSelectedIndices(useValue(selectedPrimaryIndicesJson$));
    const selectedSecondaryIndices = parseSelectedIndices(useValue(selectedSecondaryIndicesJson$));
    const showPrimaryDropdown = shouldShowDropdown(availablePrimaryVehicles);
    const showSecondaryDropdown = supportsSecondarySelection && shouldShowDropdown(availableSecondaryVehicles);
    const Section = vanilla?.Section;

    if (!isPlanningRoute || !Section) {
        return null;
    }

    return (
        <Fragment>
            {showPrimaryDropdown ? (
                <Section>
                    <VehiclePicker
                        vehicles={availablePrimaryVehicles}
                        selectedIndices={selectedPrimaryIndices}
                        onToggle={(index) => trigger(group, "togglePrimaryIndex", index)}
                    />
                </Section>
            ) : null}
            {showSecondaryDropdown ? (
                <Section>
                    <VehiclePicker
                        vehicles={availableSecondaryVehicles}
                        selectedIndices={selectedSecondaryIndices}
                        onToggle={(index) => trigger(group, "toggleSecondaryIndex", index)}
                    />
                </Section>
            ) : null}
        </Fragment>
    );
};

export const RouteVehicleColorSection: ModuleRegistryExtend = (Component: any) => {
    return (props) => {
        const vanilla = VanillaComponentResolver.instance;
        const isPlanningRoute = useValue(isPlanningRoute$);
        const autoRandomColorEnabled = useValue(autoRandomColorEnabled$);
        const ToolButton = vanilla?.ToolButton;
        const toolButtonTheme = vanilla?.ToolButtonTheme;
        const focusDisabled = vanilla?.FOCUS_DISABLED;

        if (!isPlanningRoute || !ToolButton || !isColorSectionTitle(props?.title)) {
            return <Component {...props} />;
        }

        return (
            <Component
                {...props}
                children={(
                    <div className={styles.colorSectionRow}>
                        <div className={styles.colorSectionField}>
                            {props.children}
                        </div>
                        <ToolButton
                            src={colorRandomIcon}
                            selected={autoRandomColorEnabled}
                            onSelect={() => trigger(group, "setAutoRandomColorEnabled", !autoRandomColorEnabled)}
                            focusKey={focusDisabled}
                            className={`${toolButtonTheme?.ToolButton ?? ""} ${styles.colorToggleButtonInline}`.trim()}
                        />
                    </div>
                )}
            />
        );
    };
};

export const RouteVehicleMouseToolOptions: ModuleRegistryExtend = (Component: any) => {
    return (props) => {
        const result = Component(props) as ExtendableComponentResult;
        return withAppendedToolSection(result, <RouteVehicleToolSection />);
    };
};
