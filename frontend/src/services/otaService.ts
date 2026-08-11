import { apiGet, apiPost } from "./api/client";

export interface OtaReleaseCheck {
  status: "ok" | "unavailable" | "error";
  currentVersion: string;
  latestVersion?: string | null;
  hasUpdate: boolean;
  releaseUrl?: string | null;
  publishedAtUtc?: string | null;
  summary?: string | null;
  message: string;
  checkedAtUtc: string;
}

export interface AdminReauthRequest {
  password: string;
  scope: string;
}

export interface AdminReauthResult {
  scope: string;
  token: string;
  expiresAtUtc: string;
  verifiedAtUtc: string;
}

export interface OtaPrepareApplyRequest {
  targetVersion: string;
  reauthToken: string;
}

export interface OtaPrepareApplyResult {
  status: string;
  targetVersion: string;
  message: string;
  preparedAtUtc: string;
}

export interface OtaStagePackageRequest {
  targetVersion: string;
  packageUrl: string;
  expectedSha256?: string | null;
  reauthToken: string;
}

export interface OtaStageOperationStatus {
  operationId: string;
  status: string;
  targetVersion: string;
  packagePath?: string | null;
  packageSizeBytes?: number | null;
  sha256?: string | null;
  isChecksumMatch?: boolean | null;
  message?: string | null;
  startedAtUtc: string;
  completedAtUtc?: string | null;
}

export interface OtaApplyRequest {
  stageOperationId: string;
  reauthToken: string;
  forceHealthFailure?: boolean;
}

export interface OtaApplyOperationStatus {
  operationId: string;
  status: string;
  targetVersion: string;
  previousVersion: string;
  currentVersion: string;
  appliedPackagePath?: string | null;
  healthCheckStatus?: string | null;
  rolledBack: boolean;
  message?: string | null;
  startedAtUtc: string;
  completedAtUtc?: string | null;
}

export async function checkOtaRelease(): Promise<OtaReleaseCheck> {
  return apiGet<OtaReleaseCheck>("/admin/system/ota/check");
}

export async function reauthenticateAdmin(
  request: AdminReauthRequest
): Promise<AdminReauthResult> {
  return apiPost<AdminReauthResult, AdminReauthRequest>(
    "/admin/system/reauth",
    request
  );
}

export async function prepareOtaApply(
  request: OtaPrepareApplyRequest
): Promise<OtaPrepareApplyResult> {
  return apiPost<OtaPrepareApplyResult, OtaPrepareApplyRequest>(
    "/admin/system/ota/prepare-apply",
    request
  );
}

export async function stageOtaPackage(
  request: OtaStagePackageRequest
): Promise<OtaStageOperationStatus> {
  return apiPost<OtaStageOperationStatus, OtaStagePackageRequest>(
    "/admin/system/ota/stage",
    request
  );
}

export async function getOtaStageStatus(
  operationId: string
): Promise<OtaStageOperationStatus> {
  return apiGet<OtaStageOperationStatus>(`/admin/system/ota/stage/${operationId}`);
}

export async function applyOtaPackage(
  request: OtaApplyRequest
): Promise<OtaApplyOperationStatus> {
  return apiPost<OtaApplyOperationStatus, OtaApplyRequest>(
    "/admin/system/ota/apply",
    request
  );
}

export async function getOtaApplyStatus(
  operationId: string
): Promise<OtaApplyOperationStatus> {
  return apiGet<OtaApplyOperationStatus>(`/admin/system/ota/apply/${operationId}`);
}
