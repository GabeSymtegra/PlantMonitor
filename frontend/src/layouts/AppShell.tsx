import { Box } from "@mui/material";
import { Outlet } from "react-router-dom";

import Navbar from "../components/layout/Navbar";
import Sidebar from "../components/layout/Sidebar";

export default function AppShell() {
    return (
        <Box sx={{ display: "flex" }}>

            <Sidebar />

            <Box
                sx={{
                    flexGrow: 1,
                    display: "flex",
                    flexDirection: "column",
                }}
            >
                <Navbar />

                <Box sx={{ p: 4 }}>

                    <Outlet />

                </Box>

            </Box>

        </Box>
    );
}