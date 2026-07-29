import {
    createContext,
    useEffect,
    useMemo,
    useState,
    type ReactNode,
} from "react";
import { CssBaseline, ThemeProvider } from "@mui/material";

import { getAppTheme } from "../theme/theme";

export type ThemeMode = "light" | "dark";
export type TextSize = "small" | "medium" | "large";

export interface AppearanceSettings {
    monitorName: string;
    headerColor: string;
    backgroundColor: string;
    tableColor: string;
    textSize: TextSize;
    boldText: boolean;
}

export const DEFAULT_APPEARANCE_SETTINGS: AppearanceSettings = {
    monitorName: "Plant",
    headerColor: "#1565C0",
    backgroundColor: "#F4F6F8",
    tableColor: "#E3F2FD",
    textSize: "medium",
    boldText: false,
};

const SPEC_OPS_COLORS: Pick<
    AppearanceSettings,
    "headerColor" | "backgroundColor" | "tableColor"
> = {
    headerColor: "#0B1F35",
    backgroundColor: "#121820",
    tableColor: "#223247",
};

const CLASSIC_PLANT_COLORS: Pick<
    AppearanceSettings,
    "headerColor" | "backgroundColor" | "tableColor"
> = {
    headerColor: DEFAULT_APPEARANCE_SETTINGS.headerColor,
    backgroundColor: DEFAULT_APPEARANCE_SETTINGS.backgroundColor,
    tableColor: DEFAULT_APPEARANCE_SETTINGS.tableColor,
};

interface ThemeContextType {
    mode: ThemeMode;
    isDarkMode: boolean;
    appearance: AppearanceSettings;
    toggleMode: () => void;
    setMode: (mode: ThemeMode) => void;
    updateAppearance: (updates: Partial<AppearanceSettings>) => void;
    resetAppearance: () => void;
}

const STORAGE_KEY_PREFIX = "plantmonitor-theme-mode";
const APPEARANCE_STORAGE_KEY_PREFIX = "plantmonitor-appearance-settings";
const AUTH_USER_STORAGE_KEY = "plantmonitor-auth-user";

function getStorageScopeSuffix(): string {
    const rawUser = localStorage.getItem(AUTH_USER_STORAGE_KEY);
    if (!rawUser) {
        return "anonymous";
    }

    try {
        const parsed = JSON.parse(rawUser) as { username?: string };
        const normalized = parsed.username?.trim().toLowerCase();
        return normalized ? `user:${normalized}` : "anonymous";
    } catch {
        return "anonymous";
    }
}

function buildScopedStorageKey(prefix: string): string {
    return `${prefix}:${getStorageScopeSuffix()}`;
}

function resolveInitialMode(): ThemeMode {
    const scopedStorageKey = buildScopedStorageKey(STORAGE_KEY_PREFIX);
    const stored = localStorage.getItem(scopedStorageKey)
        ?? localStorage.getItem(STORAGE_KEY_PREFIX);

    if (stored === "light" || stored === "dark") {
        return stored;
    }

    if (window.matchMedia("(prefers-color-scheme: dark)").matches) {
        return "dark";
    }

    return "light";
}

export const ThemeContext = createContext<ThemeContextType | undefined>(
    undefined
);

function resolveInitialAppearance(): AppearanceSettings {
    const scopedStorageKey = buildScopedStorageKey(APPEARANCE_STORAGE_KEY_PREFIX);
    const stored = localStorage.getItem(scopedStorageKey)
        ?? localStorage.getItem(APPEARANCE_STORAGE_KEY_PREFIX);

    if (!stored) {
        return DEFAULT_APPEARANCE_SETTINGS;
    }

    try {
        const parsed = JSON.parse(stored) as Partial<AppearanceSettings>;

        return {
            monitorName:
                parsed.monitorName?.trim() ||
                DEFAULT_APPEARANCE_SETTINGS.monitorName,
            headerColor:
                parsed.headerColor || DEFAULT_APPEARANCE_SETTINGS.headerColor,
            backgroundColor:
                parsed.backgroundColor ||
                DEFAULT_APPEARANCE_SETTINGS.backgroundColor,
            tableColor:
                parsed.tableColor || DEFAULT_APPEARANCE_SETTINGS.tableColor,
            textSize:
                parsed.textSize === "small" ||
                parsed.textSize === "medium" ||
                parsed.textSize === "large"
                    ? parsed.textSize
                    : DEFAULT_APPEARANCE_SETTINGS.textSize,
            boldText:
                typeof parsed.boldText === "boolean"
                    ? parsed.boldText
                    : DEFAULT_APPEARANCE_SETTINGS.boldText,
        };
    } catch {
        return DEFAULT_APPEARANCE_SETTINGS;
    }
}

export function AppThemeProvider({ children }: { children: ReactNode }) {
    const [mode, setModeState] = useState<ThemeMode>(() => resolveInitialMode());
    const [appearance, setAppearance] = useState<AppearanceSettings>(() =>
        resolveInitialAppearance()
    );

    useEffect(() => {
        localStorage.setItem(buildScopedStorageKey(STORAGE_KEY_PREFIX), mode);
    }, [mode]);

    useEffect(() => {
        localStorage.setItem(
            buildScopedStorageKey(APPEARANCE_STORAGE_KEY_PREFIX),
            JSON.stringify(appearance)
        );
    }, [appearance]);

    useEffect(() => {
        const modeColors = mode === "dark" ? SPEC_OPS_COLORS : CLASSIC_PLANT_COLORS;

        setAppearance((previous) => {
            const hasModeColors =
                previous.headerColor.toLowerCase() === modeColors.headerColor.toLowerCase()
                && previous.backgroundColor.toLowerCase() === modeColors.backgroundColor.toLowerCase()
                && previous.tableColor.toLowerCase() === modeColors.tableColor.toLowerCase();

            if (hasModeColors) {
                return previous;
            }

            return {
                ...previous,
                ...modeColors,
            };
        });
    }, [mode]);

    function setMode(nextMode: ThemeMode) {
        setModeState(nextMode);
    }

    function updateAppearance(updates: Partial<AppearanceSettings>) {
        setAppearance((previous) => {
            const nextMonitorName =
                updates.monitorName === undefined
                    ? previous.monitorName
                    : updates.monitorName.trim() || previous.monitorName;

            return {
                ...previous,
                ...updates,
                monitorName: nextMonitorName,
            };
        });
    }

    function resetAppearance() {
        setAppearance(DEFAULT_APPEARANCE_SETTINGS);
    }

    function toggleMode() {
        setModeState((previous) => (previous === "light" ? "dark" : "light"));
    }

    const value = useMemo(
        () => ({
            mode,
            isDarkMode: mode === "dark",
            appearance,
            toggleMode,
            setMode,
            updateAppearance,
            resetAppearance,
        }),
        [appearance, mode]
    );

    const muiTheme = useMemo(() => getAppTheme(mode, appearance), [appearance, mode]);

    return (
        <ThemeContext.Provider value={value}>
            <ThemeProvider theme={muiTheme}>
                <CssBaseline />
                {children}
            </ThemeProvider>
        </ThemeContext.Provider>
    );
}