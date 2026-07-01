import { BrowserRouter, Routes, Route } from "react-router-dom";

import AppShell from "../layouts/AppShell";

import Dashboard from "../pages/Dashboard";
import Login from "../pages/Login";
import Settings from "../pages/Settings";

function Placeholder({ title }: { title: string }) {
    return (
        <h1 style={{ padding: 40 }}>{title}</h1>
    );
}

export default function AppRoutes() {
    return (
        <BrowserRouter>

            <Routes>

                <Route path="/login" element={<Login />} />

                <Route element={<AppShell />}>

                    <Route path="/" element={<Dashboard />} />

                    <Route
                        path="/lines"
                        element={<Placeholder title="Production Lines" />}
                    />

                    <Route
                        path="/products"
                        element={<Placeholder title="Products" />}
                    />

                    <Route
                        path="/reports"
                        element={<Placeholder title="Reports" />}
                    />

                    <Route
                        path="/administration"
                        element={<Placeholder title="Administration" />}
                    />

                    <Route
                        path="/settings"
                        element={<Settings />}
                    />

                </Route>

            </Routes>

        </BrowserRouter>
    );
}