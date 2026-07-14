import { lazy, Suspense } from "react";
import { BrowserRouter, Routes, Route } from "react-router-dom";

import AppShell from "../layouts/AppShell";

const Dashboard = lazy(() => import("../pages/Dashboard"));
const Lines = lazy(() => import("../pages/Lines"));
const Login = lazy(() => import("../pages/Login"));
const Administration = lazy(() => import("../pages/Administration"));
const LineDetails = lazy(() => import("../pages/LineDetails"));
const Settings = lazy(() => import("../pages/Settings"));
const StatusBoard = lazy(() => import("../pages/StatusBoard"));
const Forbidden = lazy(() => import("../pages/Forbidden"));
const Reports = lazy(() => import("../pages/Reports"));
import ProtectedRoute from "./ProtectedRoute";

function PagePlaceholder({ title }: { title: string }) {
    return <h1 style={{ padding: 40 }}>{title}</h1>;
}

export default function AppRoutes() {
    return (
        <BrowserRouter>
            <Suspense fallback={<div style={{ padding: 40 }}>Loading...</div>}>

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
                            element={<Reports />}
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
            </Suspense>

        </BrowserRouter>
    );
}