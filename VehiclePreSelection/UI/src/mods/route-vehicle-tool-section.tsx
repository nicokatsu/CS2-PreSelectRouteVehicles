import { bindValue, trigger, useValue } from "cs2/api";
import { ModuleRegistryExtend } from "cs2/modding";
import { Dropdown, DropdownToggle, Scrollable } from "cs2/ui";
import { Children, Fragment, cloneElement, isValidElement, ReactElement, ReactNode, useLayoutEffect, useRef, useState } from "react";
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

const OFFICIAL_WRAPBOX_CLASS = "wrapbox_yNA";
const OFFICIAL_ITEM_CLASS = "item_yJO";
const OFFICIAL_PILL_CLASS = "pill_rPg";
const OFFICIAL_THUMB_CLASS = "thumb_LJh";
const OFFICIAL_TEXT_CLASS = "label_j_K";
const OFFICIAL_DROPDOWN_TOGGLE_CLASS = "dropdown-toggle_ODx dropdown-toggle_prl";
const OFFICIAL_DROPDOWN_INDICATOR_CLASS = "indicator_JII";
const OFFICIAL_DROPDOWN_MENU_CLASS = "dropdown-menu_xq2 dropdown-menu_Swd";
const OFFICIAL_DROPDOWN_ITEM_CLASS = "dropdown-item_R_l";
const OFFICIAL_SECTION_DROPDOWN_CLASS = "dropdown_Hq9";
const OFFICIAL_SECTION_DROPDOWN_LABEL_CLASS = "dropdown-label_VgD";
const OFFICIAL_FLAG_LABEL_CLASS = "label_VM3";

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

const renderRequirementIcons = (vehicle: VehicleOption) =>
    vehicle.objectRequirementIcons?.length ? (
        <div className={styles.requirementsInline}>
            {vehicle.objectRequirementIcons.map((icon, index) => (
                <img key={`${vehicle.entityIndex}-req-${index}`} src={icon} className={styles.optionRequirement} />
            ))}
        </div>
    ) : null;

const renderVehicleOption = (vehicle: VehicleOption, selected: boolean, disabled: boolean) => (
    <div className={`${OFFICIAL_DROPDOWN_ITEM_CLASS} ${styles.option} ${selected ? styles.optionSelected : ""} ${disabled ? styles.optionDisabled : ""}`}>
        <div className={styles.checkboxHost}>
            {(() => {
                const Checkbox = VanillaComponentResolver.instance.Checkbox;
                const checkboxLabelClass = VanillaComponentResolver.instance.CheckboxTheme?.label;

                return Checkbox ? (
                    <Checkbox
                        checked={selected}
                        disabled={disabled}
                        className={checkboxLabelClass}
                    />
                ) : null;
            })()}
        </div>
        {vehicle.thumbnail ? <img src={vehicle.thumbnail} className={`${styles.optionThumb} ${OFFICIAL_THUMB_CLASS}`} /> : null}
        {renderRequirementIcons(vehicle)}
        <div className={`${styles.optionLabel} ${OFFICIAL_FLAG_LABEL_CLASS}`} style={{ color: "rgba(255,255,255,0.96)" }}>{vehicle.name}</div>
    </div>
);

const renderSelectionPreview = (vehicle: VehicleOption | null, fallback: string) => (
    <div className={styles.selectionPreview}>
        <div className={styles.selectionMain}>
            {vehicle?.thumbnail ? <img src={vehicle.thumbnail} className={styles.selectionThumb} /> : null}
            {vehicle ? renderRequirementIcons(vehicle) : null}
            {fallback ? <div className={`${styles.selectionLabel} ${OFFICIAL_SECTION_DROPDOWN_LABEL_CLASS}`}>{fallback}</div> : null}
        </div>
    </div>
);

const renderSelectedPills = (vehicles: VehicleOption[], selectedEntityIndices: Set<number>) => {
    const selectedVehicles = getSelectedVehicles(vehicles, selectedEntityIndices);
    if (selectedVehicles.length === 0) {
        return null;
    }

    return (
        <div className={`${styles.selectedWrapbox} ${OFFICIAL_WRAPBOX_CLASS}`}>
            {selectedVehicles.map((vehicle) => (
                <div key={`pill-${vehicle.entityIndex}`} className={`${styles.selectedPill} ${OFFICIAL_ITEM_CLASS} ${OFFICIAL_PILL_CLASS}`}>
                    {vehicle.thumbnail ? <img src={vehicle.thumbnail} className={`${styles.selectedThumb} ${OFFICIAL_THUMB_CLASS}`} /> : null}
                    {vehicle.objectRequirementIcons?.map((icon, index) => (
                        <img key={`pill-${vehicle.entityIndex}-req-${index}`} src={icon} className={`${styles.selectedThumb} ${OFFICIAL_THUMB_CLASS}`} />
                    ))}
                    <div className={`${styles.selectedText} ${OFFICIAL_TEXT_CLASS}`}>{vehicle.name}</div>
                </div>
            ))}
        </div>
    );
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
    const shellRef = useRef<HTMLDivElement | null>(null);
    const [menuWidth, setMenuWidth] = useState<number>(0);
    const selectedVehicle = getSelectedVehicle(vehicles, selectedIndices);
    const previewLabel = getSelectionPreviewLabel(vehicles, selectedIndices);

    useLayoutEffect(() => {
        const updateWidth = () => {
            const nextWidth = shellRef.current?.getBoundingClientRect().width ?? 0;
            if (nextWidth > 0) {
                setMenuWidth(nextWidth);
            }
        };

        updateWidth();
        window.addEventListener("resize", updateWidth);
        return () => window.removeEventListener("resize", updateWidth);
    }, []);

    const content = (
        <div className={styles.dropdownMenu} style={menuWidth > 0 ? { width: `${menuWidth}px` } : undefined}>
            <Scrollable vertical trackVisibility="scrollable" className={styles.dropdownScrollable}>
                <div className={styles.dropdownList}>
                    {vehicles.map((vehicle, index) => {
                        const isSelected = isVehicleSelected(vehicle, selectedIndices);
                        const disableUnselect = isSelected && selectedIndices.size === 1;
                        return (
                            <div
                                key={`${vehicle.entityIndex}-${index}`}
                                className={`${styles.dropdownButton} ${disableUnselect ? styles.dropdownButtonDisabled : ""}`}
                                onMouseDown={(event) => {
                                    event.preventDefault();
                                    event.stopPropagation();
                                    if (disableUnselect) {
                                        return;
                                    }
                                    onToggle(index);
                                }}
                                role="button"
                            >
                                {renderVehicleOption(vehicle, isSelected, disableUnselect)}
                            </div>
                        );
                    })}
                </div>
            </Scrollable>
        </div>
    );

    return (
        <div className={styles.dropdownShell} ref={shellRef}>
            <Dropdown
                alignment="left"
                content={content}
                theme={{
                    dropdownMenu: `${OFFICIAL_DROPDOWN_MENU_CLASS} ${styles.dropdownMenu}`,
                    scrollable: styles.dropdownScrollable,
                }}
            >
                <DropdownToggle
                    theme={{
                        dropdownToggle: `${OFFICIAL_DROPDOWN_TOGGLE_CLASS} ${OFFICIAL_SECTION_DROPDOWN_CLASS} ${styles.dropdownToggle}`,
                        label: styles.selectionLabelSlot,
                        indicator: `${OFFICIAL_DROPDOWN_INDICATOR_CLASS} ${styles.selectionChevron}`,
                    }}
                    className={styles.dropdownToggleRoot}
                >
                    {renderSelectionPreview(selectedVehicle, previewLabel)}
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
    const Section = vanilla.Section;

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
        const ToolButton = vanilla.ToolButton;
        const toolButtonTheme = vanilla.ToolButtonTheme;
        const focusDisabled = vanilla.FOCUS_DISABLED;

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
                            selected={autoRandomColorEnabled}
                            onSelect={() => trigger(group, "setAutoRandomColorEnabled", !autoRandomColorEnabled)}
                            focusKey={focusDisabled}
                            className={`${toolButtonTheme?.ToolButton ?? ""} ${styles.colorToggleButtonInline}`.trim()}
                        >
                            <span className={styles.centeredContentButton} style={{ backgroundImage: `url(${colorRandomIcon})` }} />
                        </ToolButton>
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
