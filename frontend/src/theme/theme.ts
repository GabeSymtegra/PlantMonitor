import { createTheme } from "@mui/material/styles";

export const lightTheme = createTheme({
    palette: {
        mode: "light",
        primary: {
            main: "#1565C0",
        },
        secondary: {
            main: "#2E7D32",
        },
        background: {
            default: "#F4F6F8",
            paper: "#FFFFFF",
        },
    },
});

export const darkTheme = createTheme({
    palette: {
        mode: "dark",
        primary: {
            main: "#42A5F5",
        },
        secondary: {
            main: "#66BB6A",
        },
    },
});