import { createTheme, getContrastRatio } from "@mui/material/styles";

import type { AppearanceSettings, ThemeMode } from "../context/ThemeContext";

function resolveBaseFontSize(textSize: AppearanceSettings["textSize"]) {
    if (textSize === "small") {
        return 13;
    }

    if (textSize === "large") {
        return 17;
    }

    return 15;
}

function getReadableTextColor(background: string) {
    const whiteContrast = getContrastRatio(background, "#FFFFFF");
    const darkContrast = getContrastRatio(background, "#0F172A");

    return whiteContrast >= darkContrast ? "#FFFFFF" : "#0F172A";
}

export function getAppTheme(mode: ThemeMode, appearance: AppearanceSettings) {
    const baseFontSize = resolveBaseFontSize(appearance.textSize);
    const defaultWeight = appearance.boldText ? 600 : 400;
    const headerTextColor = getReadableTextColor(appearance.headerColor);
    const tableHeaderTextColor = getReadableTextColor(appearance.tableColor);

    return createTheme({
        palette: {
            mode,
            primary: {
                main: appearance.headerColor,
                contrastText: headerTextColor,
            },
            secondary: {
                main: mode === "dark" ? "#66BB6A" : "#2E7D32",
            },
            background: {
                default: appearance.backgroundColor,
                paper: mode === "dark" ? "#171C22" : "#FFFFFF",
            },
            text: {
                primary: mode === "dark" ? "#E7ECF2" : "#0F172A",
                secondary: mode === "dark" ? "#A9B4C2" : "#475569",
            },
            divider: mode === "dark" ? "rgba(231, 236, 242, 0.16)" : "rgba(15, 23, 42, 0.14)",
        },
        shape: {
            borderRadius: 10,
        },
        typography: {
            fontSize: baseFontSize,
            fontWeightRegular: defaultWeight,
            fontWeightMedium: appearance.boldText ? 700 : 500,
            fontWeightBold: 800,
        },
        components: {
            MuiCssBaseline: {
                styleOverrides: {
                    html: {
                        backgroundColor: appearance.backgroundColor,
                    },
                    body: {
                        backgroundColor: appearance.backgroundColor,
                    },
                    "#root": {
                        backgroundColor: appearance.backgroundColor,
                    },
                },
            },
            MuiAppBar: {
                styleOverrides: {
                    root: {
                        backgroundImage: "none",
                        backgroundColor: appearance.headerColor,
                        color: headerTextColor,
                    },
                },
            },
            MuiPaper: {
                styleOverrides: {
                    root: {
                        backgroundImage: "none",
                    },
                },
            },
            MuiTableCell: {
                styleOverrides: {
                    head: {
                        backgroundColor: appearance.tableColor,
                        color: tableHeaderTextColor,
                    },
                },
            },
        },
    });
}