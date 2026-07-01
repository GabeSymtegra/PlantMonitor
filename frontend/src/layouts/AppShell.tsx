import { Box } from "@mui/material";
import { useState } from "react";
import { Outlet } from "react-router-dom";

import Navbar from "../components/layout/Navbar";
import Sidebar from "../components/layout/Sidebar";

export default function AppShell() {
    const [sidebarOpen, setSidebarOpen] = useState(true);

    function handleToggleSidebar() {
        setSidebarOpen((previous) => !previous);
    }

    return (
        <Box
            sx={{
                display: "flex",
                minHeight: "100vh",
            }}
        >

            <Sidebar open={sidebarOpen} />

            <Box
                sx={{
                    flexGrow: 1,
                    display: "flex",
                    flexDirection: "column",
                }}
            >
                <Navbar
                    sidebarOpen={sidebarOpen}
                    onToggleSidebar={handleToggleSidebar}
                />

                <Box sx={{ p: 4 }}>

                    <Outlet />

                </Box>

            </Box>

        </Box>
    );
}