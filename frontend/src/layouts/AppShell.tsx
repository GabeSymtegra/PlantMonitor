import { Box } from "@mui/material";
import Navbar from "../components/layout/Navbar";
import Sidebar from "../components/layout/Sidebar";

interface AppShellProps {
  children: React.ReactNode;
}

export default function AppShell({ children }: AppShellProps) {
  return (
    <>
      <Navbar />

      <Box sx={{ display: "flex" }}>
        <Sidebar />

        <Box
          component="main"
          sx={{
            flexGrow: 1,
            p: 3,
            minHeight: "100vh",
            backgroundColor: "background.default",
          }}
        >
          {children}
        </Box>
      </Box>
    </>
  );
}