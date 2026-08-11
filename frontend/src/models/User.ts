export type UserRole = "Viewer" | "Operator" | "Admin";

export interface User {
	username: string;
	role: UserRole;
	mustChangePassword?: boolean;
}
