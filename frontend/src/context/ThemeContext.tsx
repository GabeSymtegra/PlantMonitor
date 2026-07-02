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

interface ThemeContextType {
    mode: ThemeMode;
    isDarkMode: boolean;
    appearance: AppearanceSettings;
    toggleMode: () => void;
    setMode: (mode: ThemeMode) => void;
    updateAppearance: (updates: Partial<AppearanceSettings>) => void;
    resetAppearance: () => void;
}

const STORAGE_KEY = "plantmonitor-theme-mode";
const APPEARANCE_STORAGE_KEY = "plantmonitor-appearance-settings";

function resolveInitialMode(): ThemeMode {
    const stored = localStorage.getItem(STORAGE_KEY);

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
    const stored = localStorage.getItem(APPEARANCE_STORAGE_KEY);

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
        localStorage.setItem(STORAGE_KEY, mode);
    }, [mode]);

    useEffect(() => {
        localStorage.setItem(APPEARANCE_STORAGE_KEY, JSON.stringify(appearance));
    }, [appearance]);

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