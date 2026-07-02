import { Box } from "@mui/material";
import { Outlet } from "react-router-dom";

import Navbar from "../components/layout/Navbar";
import Sidebar from "../components/layout/Sidebar";

export default function AppShell() {
    return (
        <Box
            sx={{
                display: "flex",
                minHeight: "100vh",
                backgroundColor: "background.default",
            }}
        >

            <Sidebar />

            <Box
                sx={{
                    flexGrow: 1,
                    minWidth: 0,
                    display: "flex",
                    flexDirection: "column",
                    backgroundColor: "background.default",
                }}
            >
                <Navbar />

                <Box sx={{ p: { xs: 2, md: 3 }, minWidth: 0 }}>

                    <Outlet />

                </Box>

            </Box>

        </Box>
    );
}