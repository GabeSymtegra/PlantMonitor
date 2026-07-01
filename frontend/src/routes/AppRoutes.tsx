import { BrowserRouter, Routes, Route } from "react-router-dom";

import AppShell from "../layouts/AppShell";

import Dashboard from "../pages/Dashboard";
import Login from "../pages/Login";
import Administration from "../pages/Administration";
import Settings from "../pages/Settings";
import ProtectedRoute from "./ProtectedRoute";

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
                        element={<ProtectedRoute />}
                    >
                        <Route index element={<Administration />} />
                    </Route>

                    <Route
                        path="/settings"
                        element={<Settings />}
                    />

                </Route>

            </Routes>

        </BrowserRouter>
    );
}