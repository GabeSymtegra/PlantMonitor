import { BrowserRouter, Routes, Route } from "react-router-dom";

import AppShell from "../layouts/AppShell";

import Dashboard from "../pages/Dashboard";
import Lines from "../pages/Lines";
import Login from "../pages/Login";
import Administration from "../pages/Administration";
import LineDetails from "../pages/LineDetails";
import Settings from "../pages/Settings";
import StatusBoard from "../pages/StatusBoard";
import Forbidden from "../pages/Forbidden";
import ProtectedRoute from "./ProtectedRoute";

function PagePlaceholder({ title }: { title: string }) {
    return <h1 style={{ padding: 40 }}>{title}</h1>;
}

export default function AppRoutes() {
    return (
        <BrowserRouter>

            <Routes>

                <Route path="/login" element={<Login />} />

                <Route element={<ProtectedRoute />}>

                    <Route path="/status-board" element={<StatusBoard />} />

                    <Route element={<AppShell />}>

                        <Route path="/" element={<Dashboard />} />

                        <Route path="/lines" element={<Lines />} />

                        <Route path="/lines/:id" element={<LineDetails />} />

                        <Route path="/forbidden" element={<Forbidden />} />

                        <Route
                            path="/products"
                            element={<PagePlaceholder title="Products" />}
                        />

                        <Route
                            path="/reports"
                            element={<PagePlaceholder title="Reports" />}
                        />

                        <Route element={<ProtectedRoute requiredRoles={["Admin"]} />}>
                            <Route
                                path="/administration"
                                element={<Administration />}
                            />

                            <Route
                                path="/settings"
                                element={<Settings />}
                            />
                        </Route>

                    </Route>

                </Route>

            </Routes>

        </BrowserRouter>
    );
}