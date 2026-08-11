import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Button,
  CircularProgress,
  Divider,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  FormControlLabel,
  FormControl,
  Checkbox,
  Chip,
  InputLabel,
  LinearProgress,
  MenuItem,
  Paper,
  Select,
  Snackbar,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import { useCallback, useEffect, useMemo, useState } from "react";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";

import {
  addLine,
  deleteLine,
  getAllLines,
  updateLine,
} from "../services/dashboardService";
import {
  testPlcConnection,
  type PlcConnectionOptions,
  type PlcProcessorType,
  type PlcConnectionResult,
} from "../services/plcConnectionService";
import {
  activateCommissionedLine,
  autoMapTagCatalog,
  getCommissioningReadiness,
  getLineProtocolAssignment,
  browsePlcTags,
  getLineTagCatalog,
  getTagSlots,
  readPlcTag,
  replaceLineTagCatalog,
  upsertLineProtocolAssignment,
  type LineTagCatalogEntry,
  type PlcTagBrowseItem,
  type PlcTagReadResult,
  type TagSlotDefinition,
} from "../services/plcTagBrowserService";
import { ApiRequestError } from "../services/api/client";
import { useDashboard } from "../context/useDashboard";
import { LineStatus } from "../types/LineStatus";
import type { LineLifecycleState, PlcManufacturer, ProductionLine } from "../types/ProductionLine";
import {
  applyOtaPackage,
  checkOtaRelease,
  getOtaApplyStatus,
  getOtaStageStatus,
  prepareOtaApply,
  reauthenticateAdmin,
  stageOtaPackage,
  type OtaApplyOperationStatus,
  type OtaReleaseCheck,
  type OtaStageOperationStatus,
} from "../services/otaService";

// -----------------------------------------------------------------------------
// Form model and local utilities
// -----------------------------------------------------------------------------

type LineConfigForm = {
  lineNumber: number;
  lineName: string;
  product: string;
  recipeId: string;
  machineId: string;
  operatorName: string;
  plcIp: string;
  manufacturer: PlcManufacturer;
  lineLifecycleState: LineLifecycleState;
};

type LinePlcSettingsForm = {
  pollIntervalMs: number;
  routePath: string;
  processorType: PlcProcessorType;
  rack: number;
  slot: number;
  connectionTimeoutMs: number;
  readTimeoutMs: number;
  retryCount: number;
  retryDelayMs: number;
};

const emptyLineForm: LineConfigForm = {
  lineNumber: 1,
  lineName: "",
  product: "",
  recipeId: "",
  machineId: "",
  operatorName: "",
  plcIp: "",
  manufacturer: "Siemens",
  lineLifecycleState: "Draft",
};

const defaultPlcSettings: LinePlcSettingsForm = {
  pollIntervalMs: 2000,
  routePath: "1,0",
  processorType: "S7-1217C",
  rack: 0,
  slot: 1,
  connectionTimeoutMs: 3000,
  readTimeoutMs: 3000,
  retryCount: 1,
  retryDelayMs: 250,
};

function isSiemensManufacturer(manufacturer: PlcManufacturer): boolean {
  return manufacturer === "Siemens";
}

function isValidIpv4Address(value: string): boolean {
  const ipv4Pattern =
    /^(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}$/;

  return ipv4Pattern.test(value.trim());
}

function formatLineTitle(line: ProductionLine) {
  return `${line.lineName} - ${line.product}`;
}

function formatConnectionTaskTrace(result: PlcConnectionResult): string {
  const lines = [
    `Task 1 complete: prepared ${result.driver} PLC connection test`,
    `Task 2 complete: sent connection request for ${result.ipAddress}`,
    `Task 3 complete: received PLC response`,
    `Task 4 result: isConnected = ${String(result.isConnected)}`,
    `Task 5 detail: ${result.message}`,
  ];

  if (result.controllerName) {
    lines.push(`Task 6 detail: controller = ${result.controllerName}`);
  }

  if (typeof result.responseTimeMs === "number") {
    lines.push(`Task 7 detail: responseTimeMs = ${result.responseTimeMs}`);
  }

  return lines.join("\n");
}

function formatRequestError(requestError: ApiRequestError): string {
  const lines = [requestError.message];

  if (requestError.responseBody) {
    lines.push(`Response body: ${requestError.responseBody}`);
  }

  return lines.join("\n");
}

function tryGetProblemCode(requestError: ApiRequestError): string | null {
  if (!requestError.responseBody) {
    return null;
  }

  try {
    const parsed = JSON.parse(requestError.responseBody) as { code?: unknown };
    return typeof parsed.code === "string" ? parsed.code : null;
  } catch {
    return null;
  }
}

function formatTagValueDisplay(value?: string | null): string {
  if (!value) {
    return "Unavailable";
  }

  const trimmed = value.trim();

  if (!trimmed.startsWith("{") && !trimmed.startsWith("[")) {
    return value;
  }

  try {
    return JSON.stringify(JSON.parse(trimmed), null, 2);
  } catch {
    return value;
  }
}

function describeTagReadability(result: PlcTagReadResult): string {
  if (result.canRead === true) {
    return "Direct value read succeeded.";
  }

  if (result.error) {
    return "Metadata/diagnostic payload returned because a direct value read was not available.";
  }

  return "Direct value read was not available.";
}

// Administration combines line configuration, PLC connection diagnostics, tag
// discovery, and tag-to-logical-slot mapping in one workflow-heavy screen.
export default function Administration() {
  const { refresh: refreshDashboard } = useDashboard();
  const [lines, setLines] = useState<ProductionLine[]>([]);
  const [selectedLineId, setSelectedLineId] = useState<number | "new">("new");
  const [form, setForm] = useState<LineConfigForm>(emptyLineForm);
  const [error, setError] = useState<string>("");
  const [success, setSuccess] = useState<string>("");
  const [testingConnection, setTestingConnection] = useState(false);
  const [connectionResult, setConnectionResult] = useState<PlcConnectionResult | null>(null);
  const [browsingTags, setBrowsingTags] = useState(false);
  const [readingTagName, setReadingTagName] = useState<string | null>(null);
  const [tagFilter, setTagFilter] = useState("");
  const [browseScope, setBrowseScope] = useState("DB1");
  const [siemensDbList, setSiemensDbList] = useState<string[]>([]);
  const [selectedSiemensDb, setSelectedSiemensDb] = useState("");
  const [tagBrowserError, setTagBrowserError] = useState("");
  const [tagBrowserStatus, setTagBrowserStatus] = useState("");
  const [discoveredTags, setDiscoveredTags] = useState<PlcTagBrowseItem[]>([]);
  const [selectedTag, setSelectedTag] = useState<PlcTagBrowseItem | null>(null);
  const [selectedTagResult, setSelectedTagResult] = useState<PlcTagReadResult | null>(null);
  const [tagSlots, setTagSlots] = useState<TagSlotDefinition[]>([]);
  const [tagCatalog, setTagCatalog] = useState<LineTagCatalogEntry[]>([]);
  const [loadingTagCatalog, setLoadingTagCatalog] = useState(false);
  const [savingTagCatalog, setSavingTagCatalog] = useState(false);
  const [autoMapping, setAutoMapping] = useState(false);
  const [mappingStatus, setMappingStatus] = useState("");
  const [commissioningChecking, setCommissioningChecking] = useState(false);
  const [activatingLine, setActivatingLine] = useState(false);
  const [commissioningStatus, setCommissioningStatus] = useState("");
  const [commissioningReady, setCommissioningReady] = useState<boolean | null>(null);
  const [plcSettings, setPlcSettings] = useState<LinePlcSettingsForm>(defaultPlcSettings);
  const [showAdvancedSettings, setShowAdvancedSettings] = useState(false);
  const [showDiagnostics, setShowDiagnostics] = useState(false);
  const [testConnectionCompleted, setTestConnectionCompleted] = useState(false);
  const [autoPopulateCompleted, setAutoPopulateCompleted] = useState(false);
  const [newLineTagAssignmentsSaved, setNewLineTagAssignmentsSaved] = useState(false);
  const [confirmDeleteOpen, setConfirmDeleteOpen] = useState(false);
  const [confirmOverwriteOpen, setConfirmOverwriteOpen] = useState(false);
  const [duplicateLineTarget, setDuplicateLineTarget] = useState<ProductionLine | null>(null);
  const [successToastOpen, setSuccessToastOpen] = useState(false);
  const [otaCheckLoading, setOtaCheckLoading] = useState(false);
  const [otaCheckError, setOtaCheckError] = useState("");
  const [otaCheckResult, setOtaCheckResult] = useState<OtaReleaseCheck | null>(null);
  const [otaPrepareStatus, setOtaPrepareStatus] = useState("");
  const [otaStagePackageUrl, setOtaStagePackageUrl] = useState("");
  const [otaStageExpectedSha256, setOtaStageExpectedSha256] = useState("");
  const [otaStageStatus, setOtaStageStatus] = useState("");
  const [otaStageOperation, setOtaStageOperation] = useState<OtaStageOperationStatus | null>(null);
  const [otaStageLoading, setOtaStageLoading] = useState(false);
  const [otaApplyStatus, setOtaApplyStatus] = useState("");
  const [otaApplyOperation, setOtaApplyOperation] = useState<OtaApplyOperationStatus | null>(null);
  const [otaApplyLoading, setOtaApplyLoading] = useState(false);
  const [otaForceHealthFailure, setOtaForceHealthFailure] = useState(false);
  const [reauthScope, setReauthScope] = useState<"ota-apply" | "ota-stage">("ota-apply");
  const [reauthAction, setReauthAction] = useState<"prepare" | "stage" | "apply">("prepare");
  const [reauthDialogOpen, setReauthDialogOpen] = useState(false);
  const [reauthPassword, setReauthPassword] = useState("");
  const [reauthSubmitting, setReauthSubmitting] = useState(false);

  const activeLines = useMemo(
    () => lines.filter((line) => line.lineLifecycleState === "Active"),
    [lines]
  );

  const selectedLine = useMemo(() => {
    if (selectedLineId === "new") {
      return null;
    }

    return lines.find((line) => line.id === selectedLineId) ?? null;
  }, [lines, selectedLineId]);

  const filteredTags = useMemo(() => {
    const search = tagFilter.trim().toLowerCase();

    if (!search) {
      return discoveredTags;
    }

    return discoveredTags.filter((tag) => {
      const parentPath = tag.parentPath?.toLowerCase() ?? "";
      const displayName = tag.displayName?.toLowerCase() ?? "";
      const description = tag.description?.toLowerCase() ?? "";
      return tag.name.toLowerCase().includes(search)
        || displayName.includes(search)
        || description.includes(search)
        || parentPath.includes(search);
    });
  }, [discoveredTags, tagFilter]);

  const sortedFilteredTags = useMemo(() => {
    return [...filteredTags].sort((left, right) => {
      const leftPath = (left.parentPath ?? "root").toUpperCase();
      const rightPath = (right.parentPath ?? "root").toUpperCase();

      if (leftPath !== rightPath) {
        return leftPath.localeCompare(rightPath, undefined, { numeric: true });
      }

      const leftLabel = left.displayName ?? left.name;
      const rightLabel = right.displayName ?? right.name;
      return leftLabel.localeCompare(rightLabel, undefined, { numeric: true });
    });
  }, [filteredTags]);

  const tagAreaSummary = useMemo(() => {
    const counts = new Map<string, number>();

    for (const tag of discoveredTags) {
      const key = tag.parentPath?.trim() || "root";
      counts.set(key, (counts.get(key) ?? 0) + 1);
    }

    return Array.from(counts.entries())
      .sort((left, right) => left[0].localeCompare(right[0], undefined, { numeric: true }))
      .map(([area, count]) => ({ area, count }));
  }, [discoveredTags]);

  const hasDiscoveredTags = discoveredTags.length > 0;

  function findDuplicateLineByConnection(): ProductionLine | null {
    const trimmedIp = form.plcIp.trim();

    if (!trimmedIp) {
      return null;
    }

    return lines.find((line) => {
      if (selectedLineId !== "new" && line.id === selectedLineId) {
        return false;
      }

      return line.plcIp.trim() === trimmedIp && line.manufacturer === form.manufacturer;
    }) ?? null;
  }

  function showSuccessMessage(message: string) {
    setSuccess(message);
    setSuccessToastOpen(true);
  }

  function toConnectionOptions(settings: LinePlcSettingsForm): PlcConnectionOptions {
    const normalizedRoutePath = isSiemensManufacturer(form.manufacturer)
      ? `${Math.max(0, settings.rack)},${Math.max(0, settings.slot)}`
      : settings.routePath.trim() || "1,0";

    return {
      routePath: normalizedRoutePath,
      processorType: settings.processorType,
      rack: Math.max(0, settings.rack),
      slot: Math.max(0, settings.slot),
      connectionTimeoutMs: Math.max(500, settings.connectionTimeoutMs),
      readTimeoutMs: Math.max(500, settings.readTimeoutMs),
      retryCount: Math.max(0, settings.retryCount),
      retryDelayMs: Math.max(0, settings.retryDelayMs),
    };
  }

  function updatePlcSetting<K extends keyof LinePlcSettingsForm>(
    key: K,
    value: LinePlcSettingsForm[K]
  ) {
    setPlcSettings((previous) => ({
      ...previous,
      [key]: value,
    }));
  }

  // Initial data for the page comes from line configuration and slot metadata.
  useEffect(() => {
    void loadLines();
    void loadTagSlots();
    void loadOtaReleaseCheck();
  }, []);

  // ---------------------------------------------------------------------------
  // Data loading helpers
  // ---------------------------------------------------------------------------

  async function loadLines() {
    const fetchedLines = await getAllLines();
    setLines(fetchedLines);
  }

  async function loadTagSlots() {
    try {
      const slots = await getTagSlots();
      setTagSlots(slots);
    } catch {
      setTagSlots([]);
    }
  }

  async function loadOtaReleaseCheck() {
    setOtaCheckLoading(true);
    setOtaCheckError("");

    try {
      const result = await checkOtaRelease();
      setOtaCheckResult(result);
      if (!otaStagePackageUrl && result.releaseUrl) {
        setOtaStagePackageUrl(result.releaseUrl);
      }
    } catch (requestError) {
      const message =
        requestError instanceof ApiRequestError
          ? formatRequestError(requestError)
          : requestError instanceof Error
            ? requestError.message
            : "OTA release check failed.";
      setOtaCheckError(message);
      setOtaCheckResult(null);
    } finally {
      setOtaCheckLoading(false);
    }
  }

  async function handlePrepareOtaApply() {
    setOtaPrepareStatus("");

    if (!otaCheckResult?.latestVersion) {
      setOtaPrepareStatus("No target release version is available yet.");
      return;
    }

    setReauthScope("ota-apply");
    setReauthAction("prepare");
    setReauthDialogOpen(true);
  }

  async function handleStageOtaPackage() {
    setOtaStageStatus("");

    if (!otaCheckResult?.latestVersion) {
      setOtaStageStatus("No target release version is available yet.");
      return;
    }

    if (!otaStagePackageUrl.trim()) {
      setOtaStageStatus("Package URL is required for staging.");
      return;
    }

    setReauthScope("ota-stage");
    setReauthAction("stage");
    setReauthDialogOpen(true);
  }

  async function handleApplyStagedPackage() {
    setOtaApplyStatus("");

    if (!otaStageOperation?.operationId) {
      setOtaApplyStatus("Stage a package first before applying.");
      return;
    }

    if (otaStageOperation.status !== "staged") {
      setOtaApplyStatus("The selected stage operation is not in staged state.");
      return;
    }

    setReauthScope("ota-apply");
    setReauthAction("apply");
    setReauthDialogOpen(true);
  }

  async function confirmAdminReauthForOta() {
    setReauthSubmitting(true);
    setOtaPrepareStatus("");
    setOtaStageStatus("");

    try {
      const reauth = await reauthenticateAdmin({
        password: reauthPassword,
        scope: reauthScope,
      });

      if (reauthAction === "stage") {
        const targetVersion = otaCheckResult?.latestVersion ?? "";
        setOtaStageLoading(true);
        const staged = await stageOtaPackage({
          targetVersion,
          packageUrl: otaStagePackageUrl.trim(),
          expectedSha256: otaStageExpectedSha256.trim() || null,
          reauthToken: reauth.token,
        });
        setOtaStageOperation(staged);
        setOtaStageStatus(
          `${staged.message ?? "Staging completed."} Operation: ${staged.operationId}. Status: ${staged.status}.`
        );
      } else if (reauthAction === "apply") {
        if (!otaStageOperation?.operationId) {
          setOtaApplyStatus("No staged operation is available to apply.");
        } else {
          setOtaApplyLoading(true);
          const applied = await applyOtaPackage({
            stageOperationId: otaStageOperation.operationId,
            reauthToken: reauth.token,
            forceHealthFailure: otaForceHealthFailure,
          });
          setOtaApplyOperation(applied);
          setOtaApplyStatus(
            `${applied.message ?? "Apply flow completed."} Operation: ${applied.operationId}. Status: ${applied.status}.`
          );
        }
      } else {
        const targetVersion = otaCheckResult?.latestVersion ?? "";
        const prepared = await prepareOtaApply({
          targetVersion,
          reauthToken: reauth.token,
        });

        setOtaPrepareStatus(
          `${prepared.message} Target: ${prepared.targetVersion}. Status: ${prepared.status}.`
        );
      }

      setSuccess(`Admin re-auth confirmed at ${new Date(reauth.verifiedAtUtc).toLocaleString()}.`);
      setSuccessToastOpen(true);
      setReauthPassword("");
      setReauthDialogOpen(false);
    } catch (requestError) {
      const message =
        requestError instanceof ApiRequestError
          ? formatRequestError(requestError)
          : requestError instanceof Error
            ? requestError.message
            : "Admin re-authentication failed.";
      if (reauthAction === "stage") {
        setOtaStageStatus(message);
      } else if (reauthAction === "apply") {
        setOtaApplyStatus(message);
      } else {
        setOtaPrepareStatus(message);
      }
    } finally {
      setOtaStageLoading(false);
      setOtaApplyLoading(false);
      setReauthSubmitting(false);
    }
  }

  async function refreshOtaStageStatus() {
    if (!otaStageOperation?.operationId) {
      return;
    }

    setOtaStageLoading(true);
    try {
      const status = await getOtaStageStatus(otaStageOperation.operationId);
      setOtaStageOperation(status);
      setOtaStageStatus(
        `${status.message ?? "Staging status loaded."} Operation: ${status.operationId}. Status: ${status.status}.`
      );
    } catch (requestError) {
      const message =
        requestError instanceof ApiRequestError
          ? formatRequestError(requestError)
          : requestError instanceof Error
            ? requestError.message
            : "Unable to read OTA stage status.";
      setOtaStageStatus(message);
    } finally {
      setOtaStageLoading(false);
    }
  }

  async function refreshOtaApplyStatus() {
    if (!otaApplyOperation?.operationId) {
      return;
    }

    setOtaApplyLoading(true);
    try {
      const status = await getOtaApplyStatus(otaApplyOperation.operationId);
      setOtaApplyOperation(status);
      setOtaApplyStatus(
        `${status.message ?? "Apply status loaded."} Operation: ${status.operationId}. Status: ${status.status}.`
      );
    } catch (requestError) {
      const message =
        requestError instanceof ApiRequestError
          ? formatRequestError(requestError)
          : requestError instanceof Error
            ? requestError.message
            : "Unable to read OTA apply status.";
      setOtaApplyStatus(message);
    } finally {
      setOtaApplyLoading(false);
    }
  }

  const buildCatalogFromSlots = useCallback((slots: TagSlotDefinition[], existing?: LineTagCatalogEntry[]) => {
    return slots.map((slot, index) => {
      const matched = existing?.find((entry) => entry.logicalKey === slot.logicalKey);
      return {
        logicalKey: slot.logicalKey,
        displayName: slot.displayName,
        driver: form.manufacturer,
        plcAddress: matched?.plcAddress ?? "",
        dataType: matched?.dataType ?? "real",
        unit: matched?.unit ?? null,
        scale: matched?.scale ?? 1,
        description: slot.description ?? matched?.description ?? null,
        isEnabled: matched?.isEnabled ?? true,
        sortOrder: matched?.sortOrder ?? index,
        readFrequencyMs: matched?.readFrequencyMs ?? 1000,
        isRequired: slot.isRequired,
      } satisfies LineTagCatalogEntry;
    });
  }, [form.manufacturer]);

  const loadLineTagCatalog = useCallback(async (lineId: number) => {
    setLoadingTagCatalog(true);
    setMappingStatus("");
    try {
      const existing = await getLineTagCatalog(lineId);
      setTagCatalog(buildCatalogFromSlots(tagSlots, existing));
    } catch {
      setTagCatalog(buildCatalogFromSlots(tagSlots));
    } finally {
      setLoadingTagCatalog(false);
    }
  }, [buildCatalogFromSlots, tagSlots]);

  const loadProtocolAssignment = useCallback(async (lineId: number) => {
    try {
      const assignment = await getLineProtocolAssignment(lineId);
      const routeSegments = assignment.routePath.split(",");
      const fallbackRack = Number(routeSegments[0] ?? 0);
      const fallbackSlot = Number(routeSegments[1] ?? 1);

      setPlcSettings({
        pollIntervalMs: assignment.pollIntervalMs,
        routePath: assignment.routePath,
        processorType: assignment.processorType,
        rack: assignment.rack ?? (Number.isFinite(fallbackRack) ? fallbackRack : 0),
        slot: assignment.slot ?? (Number.isFinite(fallbackSlot) ? fallbackSlot : 1),
        connectionTimeoutMs: assignment.connectionTimeoutMs,
        readTimeoutMs: assignment.readTimeoutMs,
        retryCount: assignment.retryCount,
        retryDelayMs: assignment.retryDelayMs,
      });
    } catch {
      setPlcSettings(defaultPlcSettings);
    }
  }, []);

  useEffect(() => {
    if (selectedLineId === "new") {
      setTagCatalog([]);
      setPlcSettings(defaultPlcSettings);
      return;
    }

    void loadLineTagCatalog(selectedLineId);
    void loadProtocolAssignment(selectedLineId);
  }, [loadLineTagCatalog, loadProtocolAssignment, selectedLineId]);

  function resetTagBrowser() {
    setBrowsingTags(false);
    setReadingTagName(null);
    setTagFilter("");
    setSiemensDbList([]);
    setSelectedSiemensDb("");
    setTagBrowserError("");
    setTagBrowserStatus("");
    setDiscoveredTags([]);
    setSelectedTag(null);
    setSelectedTagResult(null);
    setAutoPopulateCompleted(false);
    setNewLineTagAssignmentsSaved(false);
  }

  function handleSelectLine(value: number | "new") {
    setSelectedLineId(value);
    setError("");
    setSuccess("");
    setConnectionResult(null);
    setMappingStatus("");
    resetTagBrowser();

    if (value === "new") {
      setForm(emptyLineForm);
      setPlcSettings(defaultPlcSettings);
      setTestConnectionCompleted(false);
      setAutoPopulateCompleted(false);
      setNewLineTagAssignmentsSaved(false);
      return;
    }

    const selected = lines.find((line) => line.id === value);

    if (selected) {
      const lineForm: LineConfigForm = {
        lineNumber: selected.lineNumber,
        lineName: selected.lineName,
        product: selected.product,
        recipeId: selected.recipeId || "Unknown",
        machineId: selected.machineId || "Unknown",
        operatorName: selected.operatorName || "Unknown",
        plcIp: selected.plcIp,
        manufacturer: selected.manufacturer,
        lineLifecycleState: selected.lineLifecycleState,
      };

      setForm(lineForm);
      setTestConnectionCompleted(true);
      setAutoPopulateCompleted(true);
      setNewLineTagAssignmentsSaved(true);
    }
  }

  function updateForm<K extends keyof LineConfigForm>(
    key: K,
    value: LineConfigForm[K]
  ) {
    if (key === "manufacturer" || key === "plcIp") {
      setConnectionResult(null);
      setTestConnectionCompleted(false);
      setSuccess("");
      resetTagBrowser();
      setMappingStatus("");
    }

    if (key === "manufacturer") {
      const nextManufacturer = value as PlcManufacturer;

      setPlcSettings((previous) => ({
        ...previous,
        processorType: isSiemensManufacturer(nextManufacturer)
          ? "S7-1217C"
          : "ControlLogix",
        routePath: isSiemensManufacturer(nextManufacturer)
          ? `${Math.max(0, previous.rack)},${Math.max(0, previous.slot)}`
          : previous.routePath || "1,0",
      }));

      setTagCatalog((previous) =>
        previous.map((entry) => ({
          ...entry,
          driver: String(value),
        }))
      );

      setBrowseScope((previous) => {
        if (isSiemensManufacturer(nextManufacturer)) {
          return previous.trim().length > 0 ? previous : "DB1";
        }

        return "";
      });

      setSiemensDbList([]);
      setSelectedSiemensDb("");
    }

    setForm((previous) => ({
      ...previous,
      [key]: value,
    }));
  }

  function validateForm(): string | null {
    const trimmedLineName = form.lineName.trim();
    const trimmedIp = form.plcIp.trim();

    if (!trimmedLineName) {
      return "Line name is required.";
    }

    if (!trimmedIp) {
      return "PLC IP is required.";
    }

    if (!isValidIpv4Address(trimmedIp)) {
      return "PLC IP must be a valid IPv4 address (example: 192.168.1.105).";
    }

    if (selectedLineId !== "new") {
      const duplicateIp = lines.some((line) => {
        if (line.id === selectedLineId) {
          return false;
        }

        return line.plcIp.trim() === trimmedIp && line.manufacturer === form.manufacturer;
      });

      if (duplicateIp) {
        return `PLC IP ${trimmedIp} is already assigned to another line.`;
      }
    }

    return null;
  }

  function buildLineEditPayload(existingLine?: ProductionLine): Partial<ProductionLine> {
    return {
      lineNumber: existingLine?.lineNumber ?? form.lineNumber,
      lineName: form.lineName.trim(),
      product: existingLine?.product ?? form.product.trim(),
      recipeId: existingLine?.recipeId ?? form.recipeId.trim(),
      machineId: existingLine?.machineId ?? form.machineId.trim(),
      operatorName: existingLine?.operatorName ?? form.operatorName.trim(),
      plcIp: form.plcIp.trim(),
      manufacturer: form.manufacturer,
      lineLifecycleState: form.lineLifecycleState,
    };
  }

  function openOverwritePrompt(existingLine?: ProductionLine | null) {
    setDuplicateLineTarget(existingLine ?? findDuplicateLineByConnection());
    setConfirmOverwriteOpen(true);
  }

  async function handleSave() {
    setError("");
    setSuccess("");

    try {
      const validationError = validateForm();

      if (validationError) {
        setError(validationError);
        return;
      }

      if (selectedLineId === "new") {
        const duplicateLine = findDuplicateLineByConnection();
        if (duplicateLine) {
          openOverwritePrompt(duplicateLine);
          return;
        }

        await createOrOverwriteLine();
        return;
      }

      await updateLine(selectedLineId, {
        ...buildLineEditPayload(selectedLine ?? undefined),
      });
      showSuccessMessage("Line updated successfully.");

      const refreshed = await getAllLines();
      setLines(refreshed);

      await upsertLineProtocolAssignment(selectedLineId, {
        manufacturer: form.manufacturer,
        presetName: "BasicStatus",
        presetVersion: 1,
        pollIntervalMs: plcSettings.pollIntervalMs,
        routePath: isSiemensManufacturer(form.manufacturer)
          ? `${Math.max(0, plcSettings.rack)},${Math.max(0, plcSettings.slot)}`
          : plcSettings.routePath.trim() || "1,0",
        processorType: plcSettings.processorType,
        rack: Math.max(0, plcSettings.rack),
        slot: Math.max(0, plcSettings.slot),
        connectionTimeoutMs: plcSettings.connectionTimeoutMs,
        readTimeoutMs: plcSettings.readTimeoutMs,
        retryCount: plcSettings.retryCount,
        retryDelayMs: plcSettings.retryDelayMs,
      });

      try {
        await refreshDashboard();
      } catch {
        // Keep admin flow successful even if dashboard refresh falls back to polling.
      }

      const current = refreshed.find((line) => line.id === selectedLineId);
      if (current) {
        setForm({
          lineNumber: current.lineNumber,
          lineName: current.lineName,
          product: current.product,
          recipeId: current.recipeId || "",
          machineId: current.machineId || "",
          operatorName: current.operatorName || "",
          plcIp: current.plcIp,
          manufacturer: current.manufacturer,
          lineLifecycleState: current.lineLifecycleState,
        });
      }

      try {
        const readiness = await getCommissioningReadiness(selectedLineId);
        setCommissioningReady(readiness.isReady);
        setCommissioningStatus(
          readiness.isReady
            ? `Commissioning check passed. ${readiness.mappedRequiredTagCount}/${readiness.requiredTagCount} required slots are mapped.`
            : `Commissioning check found gaps. ${readiness.mappedRequiredTagCount}/${readiness.requiredTagCount} required slots mapped.\n${readiness.issues.join("\n") || "Commissioning requirements are not fully satisfied yet."}`
        );
      } catch {
        // Keep the save successful even if readiness cannot be re-read.
      }
    } catch (requestError) {
      const message =
        requestError instanceof ApiRequestError
          ? formatRequestError(requestError)
          : requestError instanceof Error
            ? requestError.message
            : "Saving the line failed.";
      setError(message);
    }
  }

  async function createOrOverwriteLine(existingLine?: ProductionLine) {
    if (!testConnectionCompleted || connectionResult?.isConnected !== true) {
      setError("Run a successful Test Connection before adding the line.");
      return;
    }

    if (!autoPopulateCompleted) {
      setError("Run Auto Populate before adding the line.");
      return;
    }

    if (!newLineTagAssignmentsSaved) {
      setError("Save Tag Assignments before adding the line.");
      return;
    }

    const nextLineNumber = lines.length
      ? Math.max(...lines.map((line) => line.lineNumber || 0)) + 1
      : 1;

    const baseLinePayload = {
      ...buildLineEditPayload(existingLine),
      lineNumber: existingLine?.lineNumber ?? nextLineNumber,
      startDateTime: new Date().toISOString(),
      status: LineStatus.Offline,
      timeInStatus: "00:00:00",
      controlMode: "Auto" as const,
      percentAutoMode: 100,
      autoVariance: 0,
      percentManualMode: 0,
      manualVariance: 0,
      totalVariance: 0,
      totalLength: 0,
      runtime: "00:00:00",
      product: existingLine?.product ?? form.product.trim(),
      plcIp: form.plcIp.trim(),
      isActive: false,
    } as Omit<ProductionLine, "id">;

    const savedLine = existingLine
      ? await updateLine(existingLine.id, baseLinePayload)
      : await createLineWithNextAvailableNumber(baseLinePayload, nextLineNumber);

    if (!savedLine) {
      return;
    }

    await upsertLineProtocolAssignment(savedLine.id, {
      manufacturer: form.manufacturer,
      presetName: "BasicStatus",
      presetVersion: 1,
      pollIntervalMs: plcSettings.pollIntervalMs,
      routePath: isSiemensManufacturer(form.manufacturer)
        ? `${Math.max(0, plcSettings.rack)},${Math.max(0, plcSettings.slot)}`
        : plcSettings.routePath.trim() || "1,0",
      processorType: plcSettings.processorType,
      rack: Math.max(0, plcSettings.rack),
      slot: Math.max(0, plcSettings.slot),
      connectionTimeoutMs: plcSettings.connectionTimeoutMs,
      readTimeoutMs: plcSettings.readTimeoutMs,
      retryCount: plcSettings.retryCount,
      retryDelayMs: plcSettings.retryDelayMs,
    });

    await replaceLineTagCatalog(savedLine.id, form.manufacturer, tagCatalog);

    const readiness = await getCommissioningReadiness(savedLine.id);
    setCommissioningReady(readiness.isReady);

    if (readiness.isReady) {
      await activateCommissionedLine(savedLine.id);
      setCommissioningStatus("Commissioning passed and line was activated automatically.");
    } else {
      const issues = readiness.issues.length > 0
        ? readiness.issues.join("\n")
        : "Commissioning requirements are not fully satisfied yet.";

      setCommissioningStatus(
        `Line saved in Draft state. Resolve these issues, then activate:\n${issues}`
      );
    }

    const refreshed = await getAllLines();
    setLines(refreshed);
    const current = refreshed.find((line) => line.id === savedLine.id);
    if (current) {
      setSelectedLineId(current.id);
      setForm({
        lineNumber: current.lineNumber,
        lineName: current.lineName,
        product: current.product,
        recipeId: current.recipeId || "Unknown",
        machineId: current.machineId || "Unknown",
        operatorName: current.operatorName || "Unknown",
        plcIp: current.plcIp,
        manufacturer: current.manufacturer,
        lineLifecycleState: current.lineLifecycleState,
      });
    }

    showSuccessMessage(
      existingLine
        ? `Line "${existingLine.lineName}" was overwritten successfully and all screens were refreshed.`
        : "Line added successfully. Configuration and tag assignments were saved."
    );

    try {
      await refreshDashboard();
    } catch {
      // Keep admin flow successful even if dashboard refresh falls back to polling.
    }
  }

  async function createLineWithNextAvailableNumber(
    baseLinePayload: Omit<ProductionLine, "id">,
    startingLineNumber: number
  ) {
    let candidateLineNumber = startingLineNumber;

    for (let attempt = 0; attempt < 25; attempt += 1) {
      try {
        return await addLine({
          ...baseLinePayload,
          lineNumber: candidateLineNumber,
        });
      } catch (requestError) {
        if (!(requestError instanceof ApiRequestError)) {
          throw requestError;
        }

        if (tryGetProblemCode(requestError) === "duplicate_plc_connection") {
          openOverwritePrompt();
          return null;
        }

        if (tryGetProblemCode(requestError) !== "line_number_conflict") {
          throw requestError;
        }

        candidateLineNumber += 1;
      }
    }

    throw new Error("Unable to assign a free line number after multiple attempts.");
  }

  async function handleDeleteLine() {
    if (selectedLineId === "new") {
      return;
    }

    setConfirmDeleteOpen(true);
  }

  async function confirmDeleteLine() {
    if (selectedLineId === "new") {
      setConfirmDeleteOpen(false);
      return;
    }

    setError("");
    setSuccess("");
    setConfirmDeleteOpen(false);

    await deleteLine(selectedLineId);
    const refreshed = await getAllLines();
    setLines(refreshed);
    setSelectedLineId("new");
    setForm(emptyLineForm);
    showSuccessMessage("Line deleted successfully.");

    try {
      await refreshDashboard();
    } catch {
      // Keep admin flow successful even if dashboard refresh falls back to polling.
    }
  }

  async function confirmOverwriteLine() {
    if (!duplicateLineTarget) {
      setConfirmOverwriteOpen(false);
      return;
    }

    setConfirmOverwriteOpen(false);
    setError("");
    setSuccess("");

    try {
      const targetLine = duplicateLineTarget ?? findDuplicateLineByConnection();
      await createOrOverwriteLine(targetLine ?? undefined);
      setDuplicateLineTarget(null);
    } catch (requestError) {
      const message =
        requestError instanceof ApiRequestError
          ? formatRequestError(requestError)
          : requestError instanceof Error
            ? requestError.message
            : "Overwriting the line failed.";
      setError(message);
    }
  }

  async function handleTestConnection() {
    setError("");
    setSuccess("");
    setTagBrowserError("");
    setTestingConnection(true);

    try {
      const result = await testPlcConnection({
        driver: form.manufacturer,
        ipAddress: form.plcIp.trim(),
        options: toConnectionOptions(plcSettings),
      });

      setConnectionResult(result);

      if (result.isConnected) {
        setTestConnectionCompleted(true);
        setSuccess("PLC connection verified successfully.");
        await discoverTags(result.driver, form.plcIp.trim(), browseScope);
      } else {
        setTestConnectionCompleted(false);
        resetTagBrowser();
        setError(formatConnectionTaskTrace(result));
      }
    } catch (requestError) {
      const message =
        requestError instanceof ApiRequestError
          ? formatRequestError(requestError)
          : requestError instanceof Error
            ? requestError.message
            : "PLC connection test failed.";

      setConnectionResult({
        isConnected: false,
        driver: form.manufacturer,
        ipAddress: form.plcIp.trim(),
        message,
      });
      setTestConnectionCompleted(false);
      resetTagBrowser();
      setError(message);
    } finally {
      setTestingConnection(false);
    }
  }

  async function discoverTags(
    driver: PlcConnectionResult["driver"],
    ipAddress: string,
    searchScope?: string
  ) {
    setReadingTagName(null);
    setTagFilter("");
    setTagBrowserError("");
    setTagBrowserStatus("");
    setDiscoveredTags([]);
    setSelectedTag(null);
    setSelectedTagResult(null);
    setAutoPopulateCompleted(false);
    setNewLineTagAssignmentsSaved(false);
    setBrowsingTags(true);
    const normalizedScope = searchScope?.trim() ?? "";

    try {
      const tags = await browsePlcTags({
        driver,
        ipAddress,
        options: toConnectionOptions(plcSettings),
        search: normalizedScope.length > 0 ? normalizedScope : undefined,
      });

      const folderOnlyDbList =
        driver === "Siemens"
        && tags.length > 0
        && tags.every((tag) => tag.isFolder && tag.parentPath === "DB");

      if (folderOnlyDbList) {
        const dbNames = tags
          .map((tag) => tag.name)
          .filter((name) => /^DB\d+$/i.test(name))
          .sort((left, right) => {
            const leftNumber = Number(left.replace(/\D/g, ""));
            const rightNumber = Number(right.replace(/\D/g, ""));
            return leftNumber - rightNumber;
          });

        setSiemensDbList(dbNames);
        if (dbNames.length > 0) {
          setSelectedSiemensDb((previous) =>
            dbNames.includes(previous) ? previous : dbNames[0]
          );
          setBrowseScope((previous) => previous.trim().length > 0 ? previous : dbNames[0]);
        }

        setTagBrowserStatus(
          dbNames.length > 0
            ? `Discovered ${dbNames.length} data block${dbNames.length === 1 ? "" : "s"}. Select a DB and load its addresses.`
            : "Connected to Siemens PLC but no readable data blocks were discovered."
        );

        return;
      }

      if (tags.length === 0) {
        setTagBrowserStatus(
          driver === "Siemens"
            ? `Siemens connection succeeded, but no readable addresses were discovered${normalizedScope ? ` in scope "${normalizedScope}"` : " with the current browse scope"}. Try DB1, DB10, or a specific area like M/I/Q.`
            : "PLC connection succeeded, but no readable/discoverable tags were returned for this controller filter."
        );
        return;
      }

      setDiscoveredTags(tags);
      if (driver === "Siemens" && /^DB\d+$/i.test(normalizedScope)) {
        setSelectedSiemensDb(normalizedScope.toUpperCase());
      }
      setTagBrowserStatus(
        driver === "Siemens"
          ? `Discovered ${tags.length} readable Siemens address${tags.length === 1 ? "" : "es"}${normalizedScope ? ` from scope "${normalizedScope}"` : ""}.`
          : `Discovered ${tags.length} controller tag${tags.length === 1 ? "" : "s"}. Select a tag to read its current value.`
      );
    } catch (requestError) {
      const message =
        requestError instanceof ApiRequestError
          ? formatRequestError(requestError)
          : requestError instanceof Error
            ? requestError.message
            : "Tag discovery failed.";

      setTagBrowserError(message);
    } finally {
      setBrowsingTags(false);
    }
  }

  async function handleSelectTag(tag: PlcTagBrowseItem) {
    setSelectedTag(tag);
    setSelectedTagResult(null);
    setReadingTagName(tag.name);

    try {
      const result = await readPlcTag({
        driver: form.manufacturer,
        ipAddress: form.plcIp.trim(),
        options: toConnectionOptions(plcSettings),
        tagName: tag.name,
      });

      setSelectedTagResult(result);
    } catch (requestError) {
      const message =
        requestError instanceof ApiRequestError
          ? formatRequestError(requestError)
          : requestError instanceof Error
            ? requestError.message
            : "Tag read failed.";

      setSelectedTagResult({
        name: tag.name,
        dataType: tag.dataType,
        lastReadUtc: new Date().toISOString(),
        canRead: false,
        canWrite: false,
        error: message,
      });
    } finally {
      setReadingTagName(null);
    }
  }

  function updateTagCatalog(logicalKey: string, updates: Partial<LineTagCatalogEntry>) {
    if (selectedLineId === "new") {
      setNewLineTagAssignmentsSaved(false);
    }

    setTagCatalog((previous) =>
      previous.map((entry) =>
        entry.logicalKey === logicalKey
          ? {
              ...entry,
              ...updates,
            }
          : entry
      )
    );
  }

  async function handleAutoMapTagCatalog(connectedResult?: PlcConnectionResult) {
    setMappingStatus("");

    const source = connectedResult ?? connectionResult;

    if (!source?.isConnected) {
      setMappingStatus("Run a successful PLC connection test before auto-map.");
      return;
    }

    setAutoMapping(true);
    try {
      const slots = tagSlots.length > 0 ? tagSlots : await getTagSlots();

      if (tagSlots.length === 0) {
        setTagSlots(slots);
      }

      const result = await autoMapTagCatalog({
        driver: source.driver,
        ipAddress: source.ipAddress,
        options: toConnectionOptions(plcSettings),
        search: isSiemensManufacturer(form.manufacturer)
          ? (selectedSiemensDb.trim() || browseScope.trim() || undefined)
          : (browseScope.trim() || undefined),
      });

      const suggestedByKey = new Map(
        result.suggestedMappings.map((entry) => [entry.logicalKey, entry])
      );

      const baseCatalog = tagCatalog.length > 0
        ? tagCatalog
        : buildCatalogFromSlots(slots);

      let filledBlankCount = 0;
      let preservedManualCount = 0;

      const mergedCatalog = baseCatalog.map((entry) => {
        const suggestion = suggestedByKey.get(entry.logicalKey);
        const hasManualAssignment = entry.plcAddress.trim().length > 0;

        if (hasManualAssignment) {
          preservedManualCount += 1;
          return entry;
        }

        if (!suggestion || suggestion.plcAddress.trim().length === 0) {
          return entry;
        }

        filledBlankCount += 1;
        return {
          ...entry,
          plcAddress: suggestion.plcAddress,
          dataType: suggestion.dataType,
          driver: suggestion.driver,
        };
      });

      setTagCatalog(mergedCatalog);
      setAutoPopulateCompleted(true);
      setNewLineTagAssignmentsSaved(false);

      const lowConfidence = result.suggestionDetails
        .filter((suggestion) => suggestion.confidence < 80)
        .map((suggestion) => suggestion.logicalKey);

      const guidance = lowConfidence.length > 0
        ? ` Low confidence suggestions: ${lowConfidence.join(", ")}.`
        : "";

      const preservationSummary = preservedManualCount > 0
        ? ` Preserved ${preservedManualCount} manual assignment${preservedManualCount === 1 ? "" : "s"}.`
        : "";

      const fillSummary = ` Filled ${filledBlankCount} blank slot${filledBlankCount === 1 ? "" : "s"}.`;

      if (result.missingLogicalKeys.length > 0) {
        setMappingStatus(
          `Auto-map completed with gaps.${fillSummary}${preservationSummary} Missing: ${result.missingLogicalKeys.join(", ")}. Use the PLC Tag dropdowns to override any slot manually.${guidance}`
        );
      } else {
        setMappingStatus(`Auto-map completed.${fillSummary}${preservationSummary} Review the suggested assignments or override any slot manually before saving.${guidance}`);
      }
    } catch (requestError) {
      const message =
        requestError instanceof ApiRequestError
          ? formatRequestError(requestError)
          : requestError instanceof Error
            ? requestError.message
            : "Auto-map request failed.";

      setMappingStatus(message);
      setAutoPopulateCompleted(false);
    } finally {
      setAutoMapping(false);
    }
  }

  async function handleSaveTagCatalog() {
    setMappingStatus("");

    if (selectedLineId === "new") {
      if (!testConnectionCompleted || connectionResult?.isConnected !== true) {
        setMappingStatus("Run Test Connection before saving tag assignments.");
        return;
      }

      if (!autoPopulateCompleted) {
        setMappingStatus("Run Auto Populate before saving tag assignments.");
        return;
      }

      setNewLineTagAssignmentsSaved(true);
      setMappingStatus("Tag assignments staged. Click Add Line to persist configuration and assignments.");
      return;
    }

    setSavingTagCatalog(true);
    try {
      const saved = await replaceLineTagCatalog(selectedLineId, form.manufacturer, tagCatalog);
      setTagCatalog(buildCatalogFromSlots(tagSlots, saved));
      setMappingStatus("Tag assignments saved.");
      try {
        await refreshDashboard();
      } catch {
        // Keep admin flow successful even if dashboard refresh falls back to polling.
      }
    } catch (requestError) {
      const message =
        requestError instanceof ApiRequestError
          ? formatRequestError(requestError)
          : requestError instanceof Error
            ? requestError.message
            : "Saving tag assignments failed.";
      setMappingStatus(message);
    } finally {
      setSavingTagCatalog(false);
    }
  }

  async function handleCommissioningCheck() {
    setCommissioningStatus("");
    setCommissioningReady(null);

    if (selectedLineId === "new") {
      setCommissioningStatus("Save the line first, then run commissioning readiness checks.");
      return;
    }

    setCommissioningChecking(true);
    try {
      const result = await getCommissioningReadiness(selectedLineId);
      setCommissioningReady(result.isReady);

      if (result.isReady) {
        setCommissioningStatus(
          `Commissioning check passed. ${result.mappedRequiredTagCount}/${result.requiredTagCount} required slots are mapped.`
        );
      } else {
        const issues = result.issues.length > 0
          ? result.issues.join("\n")
          : "Commissioning check failed due to missing required mappings.";

        setCommissioningStatus(
          `Commissioning check found gaps. ${result.mappedRequiredTagCount}/${result.requiredTagCount} required slots mapped.\n${issues}`
        );
      }
    } catch (requestError) {
      const message =
        requestError instanceof ApiRequestError
          ? formatRequestError(requestError)
          : requestError instanceof Error
            ? requestError.message
            : "Commissioning check failed.";

      setCommissioningStatus(message);
    } finally {
      setCommissioningChecking(false);
    }
  }

  async function handleActivateCommissionedLine() {
    setCommissioningStatus("");

    if (selectedLineId === "new") {
      setCommissioningStatus("Save the line first, then activate commissioning.");
      return;
    }

    setActivatingLine(true);

    try {
      const readiness = await getCommissioningReadiness(selectedLineId);
      setCommissioningReady(readiness.isReady);

      if (!readiness.isReady) {
        const issues = readiness.issues.length > 0
          ? readiness.issues.join("\n")
          : "Commissioning validation failed.";

        setCommissioningStatus(
          `Line remains non-active. Resolve commissioning issues before activation.\n${issues}`
        );
        return;
      }

      await activateCommissionedLine(selectedLineId);

      const refreshed = await getAllLines();
      setLines(refreshed);

      const current = refreshed.find((line) => line.id === selectedLineId);
      if (current) {
        setForm((previous) => ({
          ...previous,
          lineLifecycleState: current.lineLifecycleState,
        }));
      }

      setCommissioningStatus("Line activation completed. Lifecycle state is now Active.");
      setCommissioningReady(true);
      try {
        await refreshDashboard();
      } catch {
        // Keep admin flow successful even if dashboard refresh falls back to polling.
      }
    } catch (requestError) {
      const message =
        requestError instanceof ApiRequestError
          ? formatRequestError(requestError)
          : requestError instanceof Error
            ? requestError.message
            : "Line activation failed.";

      setCommissioningStatus(message);
      setCommissioningReady(false);
    } finally {
      setActivatingLine(false);
    }
  }

  const hasValidRoutePath = /^\d+(,\d+)*$/.test(plcSettings.routePath.trim());
  const hasValidRackSlot = plcSettings.rack >= 0 && plcSettings.slot >= 0;
  const canTestConnection = isValidIpv4Address(form.plcIp)
    && (isSiemensManufacturer(form.manufacturer) ? hasValidRackSlot : hasValidRoutePath)
    && !testingConnection;
  const isNewLine = selectedLineId === "new";
  const canSaveLine = isNewLine
    ? testConnectionCompleted && autoPopulateCompleted && newLineTagAssignmentsSaved
    : true;
  const stepStatuses = [
    {
      label: "Test Connection",
      complete: testConnectionCompleted,
    },
    {
      label: "Auto Populate",
      complete: autoPopulateCompleted,
    },
    {
      label: "Manual Tag Edits",
      complete: autoPopulateCompleted,
    },
    {
      label: "Save Tag Assignments",
      complete: newLineTagAssignmentsSaved,
    },
    {
      label: "Add Line",
      complete: selectedLineId !== "new",
    },
  ];
  const totalSteps = stepStatuses.length;
  const completedStepCount = stepStatuses.filter((step) => step.complete).length;
  const currentStepIndex = stepStatuses.findIndex((step) => !step.complete);
  const displayStepNumber = currentStepIndex === -1 ? totalSteps : currentStepIndex + 1;
  const currentStepLabel = currentStepIndex === -1
    ? "Complete"
    : stepStatuses[currentStepIndex].label;
  const progressPercent = (completedStepCount / totalSteps) * 100;

  return (
    <Stack spacing={3}>
      <Typography variant="h4" sx={{ fontWeight: 700 }}>
        Administration
      </Typography>

      <Paper sx={{ p: 3 }}>
        <Stack spacing={1.5}>
          <Typography variant="h6">Active Lines</Typography>
          <Typography color="text.secondary">
            {activeLines.length} active line{activeLines.length === 1 ? "" : "s"}
          </Typography>

          <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1 }}>
            {activeLines.map((line) => (
              <Button
                key={line.id}
                variant={selectedLineId === line.id ? "contained" : "outlined"}
                onClick={() => handleSelectLine(line.id)}
              >
                {`Line ${line.lineNumber} - ${line.lineName}`}
              </Button>
            ))}
          </Box>
        </Stack>
      </Paper>

      <Paper sx={{ p: 3 }}>
        <Stack spacing={2}>
          <Stack
            direction={{ xs: "column", sm: "row" }}
            justifyContent="space-between"
            alignItems={{ xs: "flex-start", sm: "center" }}
            spacing={1}
          >
            <Box>
              <Typography variant="h6">Over-The-Air Updates</Typography>
              <Typography color="text.secondary">
                Release checks, package staging with checksum verification, and controlled OTA apply with health-check rollback.
              </Typography>
            </Box>

            <Button
              variant="outlined"
              onClick={() => {
                void loadOtaReleaseCheck();
              }}
              disabled={otaCheckLoading}
            >
              {otaCheckLoading ? "Checking..." : "Check Updates"}
            </Button>
          </Stack>

          {otaCheckError ? (
            <Alert severity="warning">
              <Typography variant="body2" sx={{ whiteSpace: "pre-line" }}>
                {otaCheckError}
              </Typography>
            </Alert>
          ) : null}

          {otaCheckResult ? (
            <Stack spacing={1.5}>
              <Alert severity={otaCheckResult.hasUpdate ? "info" : "success"}>
                <Typography variant="body2">{otaCheckResult.message}</Typography>
                <Typography variant="body2">Current Version: {otaCheckResult.currentVersion}</Typography>
                <Typography variant="body2">Latest Version: {otaCheckResult.latestVersion ?? "Unavailable"}</Typography>
                <Typography variant="body2">Status: {otaCheckResult.status}</Typography>
              </Alert>

              {otaCheckResult.publishedAtUtc ? (
                <Typography variant="body2" color="text.secondary">
                  Published: {new Date(otaCheckResult.publishedAtUtc).toLocaleString()}
                </Typography>
              ) : null}

              {otaCheckResult.summary ? (
                <Typography variant="body2" color="text.secondary">
                  Release Summary: {otaCheckResult.summary}
                </Typography>
              ) : null}

              {otaCheckResult.releaseUrl ? (
                <Button
                  variant="text"
                  onClick={() => {
                    window.open(otaCheckResult.releaseUrl ?? "", "_blank", "noopener,noreferrer");
                  }}
                  sx={{ width: "fit-content", px: 0 }}
                >
                  Open Release Notes
                </Button>
              ) : null}

              <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
                <Button
                  variant="contained"
                  color="warning"
                  onClick={() => {
                    void handlePrepareOtaApply();
                  }}
                  disabled={!otaCheckResult.latestVersion || otaCheckLoading || reauthSubmitting}
                >
                  Prepare OTA Apply (Re-auth Required)
                </Button>
                <Typography variant="body2" color="text.secondary" sx={{ alignSelf: "center" }}>
                  Re-auth tokens are required for both stage and apply operations.
                </Typography>
              </Stack>

              <Paper variant="outlined" sx={{ p: 2 }}>
                <Stack spacing={1.5}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                    OTA Package Staging
                  </Typography>

                  <TextField
                    label="Package URL"
                    value={otaStagePackageUrl}
                    onChange={(event) => setOtaStagePackageUrl(event.target.value)}
                    fullWidth
                    placeholder="https://github.com/.../releases/download/vX.Y.Z/PlantMonitor.zip"
                    helperText="Use a direct downloadable artifact URL or a local file path on the host."
                  />

                  <TextField
                    label="Expected SHA-256 (optional)"
                    value={otaStageExpectedSha256}
                    onChange={(event) => setOtaStageExpectedSha256(event.target.value)}
                    fullWidth
                    placeholder="Hex digest used to verify integrity after download"
                  />

                  <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
                    <Button
                      variant="contained"
                      onClick={() => {
                        void handleStageOtaPackage();
                      }}
                      disabled={!otaCheckResult?.latestVersion || reauthSubmitting || otaStageLoading}
                    >
                      Stage Package (Re-auth Required)
                    </Button>

                    <Button
                      variant="outlined"
                      onClick={() => {
                        void refreshOtaStageStatus();
                      }}
                      disabled={!otaStageOperation?.operationId || otaStageLoading}
                    >
                      Refresh Stage Status
                    </Button>
                  </Stack>

                  {otaStageStatus ? (
                    <Alert severity={otaStageOperation?.status === "staged" ? "success" : otaStageOperation?.status === "failed" ? "error" : "info"}>
                      <Typography variant="body2" sx={{ whiteSpace: "pre-line" }}>
                        {otaStageStatus}
                      </Typography>
                    </Alert>
                  ) : null}

                  {otaStageOperation ? (
                    <Stack spacing={0.5}>
                      <Typography variant="body2">Operation ID: {otaStageOperation.operationId}</Typography>
                      <Typography variant="body2">Target Version: {otaStageOperation.targetVersion}</Typography>
                      <Typography variant="body2">Status: {otaStageOperation.status}</Typography>
                      {typeof otaStageOperation.packageSizeBytes === "number" ? (
                        <Typography variant="body2">Package Size: {otaStageOperation.packageSizeBytes.toLocaleString()} bytes</Typography>
                      ) : null}
                      {otaStageOperation.sha256 ? (
                        <Typography variant="body2">SHA-256: {otaStageOperation.sha256}</Typography>
                      ) : null}
                      {typeof otaStageOperation.isChecksumMatch === "boolean" ? (
                        <Typography variant="body2">Checksum Match: {String(otaStageOperation.isChecksumMatch)}</Typography>
                      ) : null}
                      {otaStageOperation.packagePath ? (
                        <Typography variant="body2">Staged Path: {otaStageOperation.packagePath}</Typography>
                      ) : null}
                    </Stack>
                  ) : null}

                  <Divider />

                  <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                    OTA Apply and Rollback Health Check
                  </Typography>

                  <FormControlLabel
                    control={
                      <Checkbox
                        checked={otaForceHealthFailure}
                        onChange={(event) => setOtaForceHealthFailure(event.target.checked)}
                      />
                    }
                    label="Force health check failure (simulate automatic rollback)"
                  />

                  <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
                    <Button
                      variant="contained"
                      color="warning"
                      onClick={() => {
                        void handleApplyStagedPackage();
                      }}
                      disabled={!otaStageOperation?.operationId || otaStageOperation?.status !== "staged" || reauthSubmitting || otaApplyLoading}
                    >
                      Apply Staged Package (Re-auth Required)
                    </Button>

                    <Button
                      variant="outlined"
                      onClick={() => {
                        void refreshOtaApplyStatus();
                      }}
                      disabled={!otaApplyOperation?.operationId || otaApplyLoading}
                    >
                      Refresh Apply Status
                    </Button>
                  </Stack>

                  {otaApplyStatus ? (
                    <Alert severity={otaApplyOperation?.rolledBack ? "warning" : otaApplyOperation?.status === "applied" ? "success" : "info"}>
                      <Typography variant="body2" sx={{ whiteSpace: "pre-line" }}>
                        {otaApplyStatus}
                      </Typography>
                    </Alert>
                  ) : null}

                  {otaApplyOperation ? (
                    <Stack spacing={0.5}>
                      <Typography variant="body2">Operation ID: {otaApplyOperation.operationId}</Typography>
                      <Typography variant="body2">Target Version: {otaApplyOperation.targetVersion}</Typography>
                      <Typography variant="body2">Previous Version: {otaApplyOperation.previousVersion}</Typography>
                      <Typography variant="body2">Current Version: {otaApplyOperation.currentVersion}</Typography>
                      <Typography variant="body2">Status: {otaApplyOperation.status}</Typography>
                      <Typography variant="body2">Health Check: {otaApplyOperation.healthCheckStatus ?? "unknown"}</Typography>
                      <Typography variant="body2">Rolled Back: {String(otaApplyOperation.rolledBack)}</Typography>
                      {otaApplyOperation.appliedPackagePath ? (
                        <Typography variant="body2">Applied Package Path: {otaApplyOperation.appliedPackagePath}</Typography>
                      ) : null}
                    </Stack>
                  ) : null}
                </Stack>
              </Paper>

              {otaPrepareStatus ? (
                <Alert severity="info">
                  <Typography variant="body2" sx={{ whiteSpace: "pre-line" }}>
                    {otaPrepareStatus}
                  </Typography>
                </Alert>
              ) : null}
            </Stack>
          ) : null}
        </Stack>
      </Paper>

      <Paper sx={{ p: 3 }}>
        <Stack spacing={2.5}>
          <Stack
            direction={{ xs: "column", sm: "row" }}
            justifyContent="space-between"
            alignItems={{ xs: "flex-start", sm: "center" }}
            spacing={2}
          >
            <Typography variant="h6">Line Configuration</Typography>

            <FormControl size="small" sx={{ minWidth: 220 }}>
              <InputLabel id="line-select-label">Record</InputLabel>
              <Select
                labelId="line-select-label"
                value={selectedLineId}
                label="Record"
                onChange={(event) =>
                  handleSelectLine(event.target.value as number | "new")
                }
              >
                <MenuItem value="new">Add New Line</MenuItem>
                {lines.map((line) => (
                  <MenuItem key={line.id} value={line.id}>
                    {formatLineTitle(line)}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          </Stack>

          <Divider />

          {error ? (
            <Alert severity="error">
              <Typography variant="body2" sx={{ whiteSpace: "pre-line" }}>
                {error}
              </Typography>
            </Alert>
          ) : null}
          {success ? (
            <Alert severity="success">
              <Typography variant="body2" sx={{ whiteSpace: "pre-line" }}>
                {success}
              </Typography>
            </Alert>
          ) : null}

          <Paper variant="outlined" sx={{ p: 1.5 }}>
            <Stack spacing={1}>
              <Stack
                direction={{ xs: "column", sm: "row" }}
                justifyContent="space-between"
                alignItems={{ xs: "flex-start", sm: "center" }}
                spacing={0.5}
              >
                <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                  Commissioning Progress
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {`Step ${displayStepNumber} of ${totalSteps}: ${currentStepLabel}`}
                </Typography>
              </Stack>
              <LinearProgress
                variant="determinate"
                value={progressPercent}
                aria-label="Commissioning Progress"
                sx={{ height: 8, borderRadius: 999 }}
              />
            </Stack>
          </Paper>

          <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
            <TextField
              label="Line Name"
              value={form.lineName}
              onChange={(event) => updateForm("lineName", event.target.value)}
              fullWidth
              placeholder="Main Extruder"
            />

            <FormControl fullWidth>
              <InputLabel id="manufacturer-label">PLC Driver</InputLabel>
              <Select
                labelId="manufacturer-label"
                label="PLC Driver"
                value={form.manufacturer}
                onChange={(event) =>
                  updateForm("manufacturer", event.target.value as PlcManufacturer)
                }
              >
                <MenuItem value="AllenBradley">AllenBradley</MenuItem>
                <MenuItem value="Siemens">Siemens</MenuItem>
              </Select>
            </FormControl>
          </Stack>

          <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
            <TextField
              label="PLC IP Address"
              value={form.plcIp}
              onChange={(event) => updateForm("plcIp", event.target.value)}
              fullWidth
              placeholder="192.168.1.105"
            />

            <FormControl fullWidth>
              <InputLabel id="lifecycle-state-label">Lifecycle State</InputLabel>
              <Select
                labelId="lifecycle-state-label"
                label="Lifecycle State"
                value={form.lineLifecycleState}
                onChange={(event) =>
                  updateForm("lineLifecycleState", event.target.value as LineLifecycleState)
                }
              >
                <MenuItem value="Draft">Draft</MenuItem>
                <MenuItem value="Commissioning">Commissioning</MenuItem>
                <MenuItem value="Active">Active</MenuItem>
                <MenuItem value="CommissioningFailed">CommissioningFailed</MenuItem>
                <MenuItem value="Disabled">Disabled</MenuItem>
              </Select>
            </FormControl>
          </Stack>

          <Alert severity="info">
            Line metadata fields are defaulted automatically. Save changes to update the selected line.
          </Alert>

          <Accordion expanded={showAdvancedSettings} onChange={(_, expanded) => setShowAdvancedSettings(expanded)}>
            <AccordionSummary expandIcon={<ExpandMoreIcon />}>
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                Advanced Connection Settings
              </Typography>
            </AccordionSummary>
            <AccordionDetails>
              <Stack spacing={2}>
                <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
                  <TextField
                    label="Poll Interval (ms)"
                    type="number"
                    value={plcSettings.pollIntervalMs}
                    onChange={(event) =>
                      updatePlcSetting("pollIntervalMs", Number(event.target.value))
                    }
                    fullWidth
                    inputProps={{ min: 500, max: 60000 }}
                  />

                  {isSiemensManufacturer(form.manufacturer) ? (
                    <>
                      <TextField
                        label="Rack"
                        type="number"
                        value={plcSettings.rack}
                        onChange={(event) =>
                          updatePlcSetting("rack", Number(event.target.value))
                        }
                        fullWidth
                        inputProps={{ min: 0, max: 7 }}
                      />

                      <TextField
                        label="Slot"
                        type="number"
                        value={plcSettings.slot}
                        onChange={(event) =>
                          updatePlcSetting("slot", Number(event.target.value))
                        }
                        fullWidth
                        inputProps={{ min: 0, max: 31 }}
                      />

                      <FormControl fullWidth>
                        <InputLabel id="processor-type-label">CPU Family</InputLabel>
                        <Select
                          labelId="processor-type-label"
                          label="CPU Family"
                          value={plcSettings.processorType}
                          onChange={(event) =>
                            updatePlcSetting(
                              "processorType",
                              event.target.value as LinePlcSettingsForm["processorType"]
                            )
                          }
                        >
                          <MenuItem value="S7-1217C">S7-1217C</MenuItem>
                          <MenuItem value="S7-1200">S7-1200</MenuItem>
                          <MenuItem value="S7-1500">S7-1500</MenuItem>
                        </Select>
                      </FormControl>
                    </>
                  ) : (
                    <>
                      <TextField
                        label="Route/Path"
                        value={plcSettings.routePath}
                        onChange={(event) => updatePlcSetting("routePath", event.target.value)}
                        fullWidth
                        placeholder="1,0"
                      />

                      <FormControl fullWidth>
                        <InputLabel id="processor-type-label">Processor Type</InputLabel>
                        <Select
                          labelId="processor-type-label"
                          label="Processor Type"
                          value={plcSettings.processorType}
                          onChange={(event) =>
                            updatePlcSetting(
                              "processorType",
                              event.target.value as LinePlcSettingsForm["processorType"]
                            )
                          }
                        >
                          <MenuItem value="ControlLogix">ControlLogix</MenuItem>
                          <MenuItem value="CompactLogix">CompactLogix</MenuItem>
                          <MenuItem value="Micro800">Micro800</MenuItem>
                        </Select>
                      </FormControl>
                    </>
                  )}
                </Stack>

                <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
                  <TextField
                    label="Connection Timeout (ms)"
                    type="number"
                    value={plcSettings.connectionTimeoutMs}
                    onChange={(event) =>
                      updatePlcSetting("connectionTimeoutMs", Number(event.target.value))
                    }
                    fullWidth
                    inputProps={{ min: 500, max: 30000 }}
                  />

                  <TextField
                    label="Read Timeout (ms)"
                    type="number"
                    value={plcSettings.readTimeoutMs}
                    onChange={(event) =>
                      updatePlcSetting("readTimeoutMs", Number(event.target.value))
                    }
                    fullWidth
                    inputProps={{ min: 500, max: 30000 }}
                  />

                  <TextField
                    label="Retry Count"
                    type="number"
                    value={plcSettings.retryCount}
                    onChange={(event) =>
                      updatePlcSetting("retryCount", Number(event.target.value))
                    }
                    fullWidth
                    inputProps={{ min: 0, max: 5 }}
                  />

                  <TextField
                    label="Retry Delay (ms)"
                    type="number"
                    value={plcSettings.retryDelayMs}
                    onChange={(event) =>
                      updatePlcSetting("retryDelayMs", Number(event.target.value))
                    }
                    fullWidth
                    inputProps={{ min: 0, max: 10000 }}
                  />
                </Stack>
              </Stack>
            </AccordionDetails>
          </Accordion>

          <Paper
            variant="outlined"
            sx={{
              p: 2,
              borderColor: connectionResult?.isConnected ? "success.main" : "divider",
              backgroundColor: connectionResult?.isConnected ? "success.50" : "background.paper",
            }}
          >
            <Stack spacing={2}>
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                PLC Connection Test
              </Typography>

              <Typography color="text.secondary">
                Step 1 of 5: test the selected driver against the entered PLC IP.
              </Typography>

              {connectionResult ? (
                <Alert severity={connectionResult.isConnected ? "success" : "error"}>
                  <Stack spacing={0.5}>
                    <Typography variant="body2" sx={{ whiteSpace: "pre-line" }}>
                      {connectionResult.message}
                    </Typography>
                    <Typography variant="body2">
                      Is Connected: {String(connectionResult.isConnected)}
                    </Typography>
                    <Typography variant="body2">
                      Driver: {connectionResult.driver} | IP: {connectionResult.ipAddress}
                    </Typography>
                    {connectionResult.controllerName ? (
                      <Typography variant="body2">
                        Controller: {connectionResult.controllerName}
                      </Typography>
                    ) : null}
                    {connectionResult.firmware ? (
                      <Typography variant="body2">Firmware: {connectionResult.firmware}</Typography>
                    ) : null}
                    {typeof connectionResult.responseTimeMs === "number" ? (
                      <Typography variant="body2">
                        Response time: {connectionResult.responseTimeMs} ms
                      </Typography>
                    ) : null}
                  </Stack>
                </Alert>
              ) : null}

              <Stack direction={{ xs: "column", sm: "row" }} spacing={2} justifyContent="flex-end">
                <Button
                  variant="outlined"
                  onClick={handleTestConnection}
                  disabled={!canTestConnection}
                >
                  {testingConnection ? (
                    <Stack direction="row" spacing={1} alignItems="center">
                      <CircularProgress size={16} color="inherit" />
                      <span>Testing...</span>
                    </Stack>
                  ) : (
                    "Test Connection"
                  )}
                </Button>
              </Stack>

              <Alert severity={testConnectionCompleted ? "success" : "info"}>
                <Typography variant="body2">
                  {testConnectionCompleted
                    ? "Step 1 complete. Continue with Auto Populate."
                    : "Complete Step 1 before tag mapping actions are available."}
                </Typography>
              </Alert>
            </Stack>
          </Paper>

          {browsingTags ? (
            <Alert severity="info">
              <Stack direction="row" spacing={1} alignItems="center">
                <CircularProgress size={16} color="inherit" />
                <Typography variant="body2">Discovering available PLC tags...</Typography>
              </Stack>
            </Alert>
          ) : null}

          {tagBrowserError ? (
            <Alert severity="warning">
              <Typography variant="body2" sx={{ whiteSpace: "pre-line" }}>
                {tagBrowserError}
              </Typography>
            </Alert>
          ) : null}

          {tagBrowserStatus && !hasDiscoveredTags ? (
            <Alert severity="info">
              <Typography variant="body2" sx={{ whiteSpace: "pre-line" }}>
                {tagBrowserStatus}
              </Typography>
            </Alert>
          ) : null}

          <Paper variant="outlined" sx={{ p: 2 }}>
            <Stack spacing={1.5}>
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                PLC Tag Discovery Scope
              </Typography>

              <Stack
                direction={{ xs: "column", sm: "row" }}
                spacing={1.5}
                alignItems={{ xs: "stretch", sm: "center" }}
              >
                {isSiemensManufacturer(form.manufacturer) ? (
                  <Typography variant="body2" color="text.secondary">
                    Choose a DB from the list, then discover tags.
                  </Typography>
                ) : (
                  <>
                    <TextField
                      label="Controller Filter"
                      value={browseScope}
                      onChange={(event) => setBrowseScope(event.target.value)}
                      fullWidth
                      placeholder="Machine"
                      helperText="Optional filter used when the driver supports scoped browse."
                    />

                    <Button
                      variant="outlined"
                      onClick={() => {
                        if (!connectionResult?.isConnected) {
                          return;
                        }

                        void discoverTags(connectionResult.driver, form.plcIp.trim(), browseScope);
                      }}
                      disabled={browsingTags || !connectionResult?.isConnected}
                    >
                      {browsingTags ? "Discovering..." : "Discover Tags"}
                    </Button>
                  </>
                )}
              </Stack>

              {isSiemensManufacturer(form.manufacturer) ? (
                <Stack spacing={1.25}>
                  <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
                    <Button
                      variant="outlined"
                      onClick={() => {
                        if (!connectionResult?.isConnected) {
                          return;
                        }

                        void discoverTags(connectionResult.driver, form.plcIp.trim(), "DB");
                      }}
                      disabled={browsingTags || !connectionResult?.isConnected}
                    >
                      {browsingTags ? "Scanning DBs..." : "Load DB List"}
                    </Button>

                    <FormControl fullWidth disabled={siemensDbList.length === 0}>
                      <InputLabel id="siemens-db-select-label">Data Block</InputLabel>
                      <Select
                        labelId="siemens-db-select-label"
                        label="Data Block"
                        value={selectedSiemensDb}
                        onChange={(event) => {
                          const value = event.target.value;
                          setSelectedSiemensDb(value);
                          setBrowseScope(value);
                        }}
                      >
                        {siemensDbList.map((dbName) => (
                          <MenuItem key={dbName} value={dbName}>
                            {dbName}
                          </MenuItem>
                        ))}
                      </Select>
                    </FormControl>
                  </Stack>

                  <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} alignItems={{ xs: "stretch", sm: "center" }}>
                    <Button
                      variant="contained"
                      onClick={() => {
                        if (!connectionResult?.isConnected || !selectedSiemensDb) {
                          return;
                        }

                        void discoverTags(connectionResult.driver, form.plcIp.trim(), selectedSiemensDb);
                      }}
                      disabled={browsingTags || !connectionResult?.isConnected || !selectedSiemensDb}
                    >
                      {browsingTags ? "Discovering..." : "Discover Tags"}
                    </Button>
                  </Stack>

                  <Typography variant="body2" color="text.secondary">
                    Step A: Load DB List. Step B: choose a DB. Step C: Discover Tags.
                  </Typography>
                </Stack>
              ) : null}
            </Stack>
          </Paper>

          {hasDiscoveredTags ? (
            <Paper variant="outlined" sx={{ p: 2 }}>
              <Stack spacing={2}>
                <Stack
                  direction={{ xs: "column", md: "row" }}
                  justifyContent="space-between"
                  alignItems={{ xs: "flex-start", md: "center" }}
                  spacing={2}
                >
                  <Stack spacing={0.5}>
                    <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                      PLC Tag Browser
                    </Typography>
                    <Typography color="text.secondary">
                      {tagBrowserStatus}
                    </Typography>
                  </Stack>

                  <TextField
                    label="Filter Discovered Tags"
                    value={tagFilter}
                    onChange={(event) => setTagFilter(event.target.value)}
                    size="small"
                    sx={{ minWidth: { xs: "100%", md: 260 } }}
                  />
                </Stack>

                {tagAreaSummary.length > 0 ? (
                  <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", rowGap: 1 }}>
                    {tagAreaSummary.map((item) => (
                      <Chip key={item.area} label={`${item.area}: ${item.count}`} variant="outlined" size="small" />
                    ))}
                  </Stack>
                ) : null}

                <Stack direction={{ xs: "column", lg: "row" }} spacing={2} alignItems="stretch" sx={{ minHeight: { lg: 420 } }}>
                  <Paper variant="outlined" sx={{ flex: 1, display: "flex", flexDirection: "column", minHeight: 420 }}>
                    <TableContainer sx={{ flex: 1, overflow: "auto" }}>
                      <Table stickyHeader size="small" aria-label="discovered-plc-tags">
                        <TableHead>
                          <TableRow>
                            <TableCell>Tag</TableCell>
                            <TableCell>Address</TableCell>
                            <TableCell>Area</TableCell>
                            <TableCell>Type</TableCell>
                            <TableCell>Comment</TableCell>
                          </TableRow>
                        </TableHead>
                        <TableBody>
                          {sortedFilteredTags.map((tag) => (
                            <TableRow
                              key={tag.name}
                              hover
                              selected={selectedTag?.name === tag.name}
                              onClick={() => void handleSelectTag(tag)}
                              sx={{ cursor: "pointer" }}
                            >
                              <TableCell>{tag.displayName ?? tag.name}</TableCell>
                              <TableCell>{tag.name}</TableCell>
                              <TableCell>{tag.parentPath ?? "root"}</TableCell>
                              <TableCell>{tag.dataType}</TableCell>
                              <TableCell>{tag.description ?? (tag.isFolder ? "folder" : "")}</TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    </TableContainer>

                    {!sortedFilteredTags.length ? (
                      <Box sx={{ p: 2 }}>
                        <Typography color="text.secondary">
                          No tags match the current search.
                        </Typography>
                      </Box>
                    ) : null}
                  </Paper>

                  <Paper variant="outlined" sx={{ flex: 1, p: 2, display: "flex", flexDirection: "column", minHeight: 420 }}>
                    <Stack spacing={1.5} sx={{ flex: 1 }}>
                      <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                        Tag Details
                      </Typography>

                      {!selectedTag ? (
                        <Typography color="text.secondary">
                          Select a tag to read its current value.
                        </Typography>
                      ) : null}

                      {selectedTag && readingTagName === selectedTag.name ? (
                        <Stack direction="row" spacing={1} alignItems="center">
                          <CircularProgress size={16} />
                          <Typography color="text.secondary">
                            Reading {selectedTag.displayName ?? selectedTag.name}...
                          </Typography>
                        </Stack>
                      ) : null}

                      <Stack spacing={1.5} sx={{ flex: 1, justifyContent: selectedTagResult ? "flex-start" : "center" }}>
                        {selectedTagResult ? (
                          <>
                          <TextField
                            label="Tag Name"
                            value={selectedTag?.displayName ?? selectedTagResult.name}
                            fullWidth
                            disabled
                          />
                          <TextField label="PLC Address" value={selectedTagResult.name} fullWidth disabled />
                          <TextField label="Data Type" value={selectedTagResult.dataType} fullWidth disabled />
                          <TextField
                            label="Parent Path"
                            value={selectedTag?.parentPath ?? "root"}
                            fullWidth
                            disabled
                          />
                          <TextField
                            label="Description"
                            value={selectedTag?.description ?? ""}
                            fullWidth
                            disabled
                          />
                          <TextField
                            label="Value / Payload"
                            value={formatTagValueDisplay(selectedTagResult.value)}
                            fullWidth
                            disabled
                            multiline
                            minRows={6}
                            InputProps={{
                              sx: {
                                alignItems: "flex-start",
                                fontFamily: "Consolas, 'Courier New', monospace",
                              },
                            }}
                          />
                          <TextField
                            label="Last Read Time (UTC)"
                            value={selectedTagResult.lastReadUtc}
                            fullWidth
                            disabled
                          />
                          <TextField
                            label="Readable"
                            value={String(selectedTagResult.canRead ?? false)}
                            fullWidth
                            disabled
                          />
                          <TextField
                            label="Read Status"
                            value={describeTagReadability(selectedTagResult)}
                            fullWidth
                            disabled
                          />
                          {selectedTagResult.error ? (
                            <Alert severity="info">
                              <Typography variant="body2" sx={{ whiteSpace: "pre-line" }}>
                                {selectedTagResult.error}
                              </Typography>
                            </Alert>
                          ) : null}
                          </>
                        ) : null}
                      </Stack>
                    </Stack>
                  </Paper>
                </Stack>
              </Stack>
            </Paper>
          ) : null}

          <Paper variant="outlined" sx={{ p: 2 }}>
            <Stack spacing={2}>
              <Stack
                direction={{ xs: "column", sm: "row" }}
                justifyContent="space-between"
                alignItems={{ xs: "flex-start", sm: "center" }}
                spacing={1.5}
              >
                <Stack spacing={0.4}>
                  <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                    Tag Slot Assignment
                  </Typography>
                  <Typography color="text.secondary">
                    Steps 2-4: Auto Populate, adjust tags manually if needed, then Save Tag Assignments. Each PLC tag can only be used once per line.
                  </Typography>
                </Stack>

                <Stack direction="row" spacing={1}>
                  <Button
                    variant="outlined"
                    onClick={handleCommissioningCheck}
                    disabled={commissioningChecking || selectedLineId === "new"}
                  >
                    {commissioningChecking ? "Checking..." : "Commissioning Check"}
                  </Button>

                  <Button
                    variant="contained"
                    color="success"
                    onClick={handleActivateCommissionedLine}
                    disabled={activatingLine || selectedLineId === "new"}
                  >
                    {activatingLine ? "Activating..." : "Activate Line"}
                  </Button>

                  <Button
                    variant="outlined"
                    onClick={() => {
                      void handleAutoMapTagCatalog();
                    }}
                    disabled={
                      autoMapping
                      || !testConnectionCompleted
                      || !connectionResult?.isConnected
                    }
                  >
                    {autoMapping ? "Auto-Mapping..." : "Auto Populate"}
                  </Button>

                  <Button
                    variant="contained"
                    onClick={handleSaveTagCatalog}
                    disabled={
                      savingTagCatalog
                      || (selectedLineId === "new" && (!testConnectionCompleted || !autoPopulateCompleted))
                    }
                  >
                    {savingTagCatalog ? "Saving..." : "Save Tag Assignments"}
                  </Button>
                </Stack>
              </Stack>

              <Alert severity={newLineTagAssignmentsSaved ? "success" : "info"}>
                <Typography variant="body2">
                  {newLineTagAssignmentsSaved
                    ? "Step 4 complete. Tag assignments are ready."
                    : "Complete Step 4 by saving tag assignments before Add Line."}
                </Typography>
              </Alert>

              {mappingStatus ? (
                <Alert severity="info">
                  <Typography variant="body2" sx={{ whiteSpace: "pre-line" }}>
                    {mappingStatus}
                  </Typography>
                </Alert>
              ) : null}

              {commissioningStatus ? (
                <Alert severity={commissioningReady === false ? "warning" : "success"}>
                  <Typography variant="body2" sx={{ whiteSpace: "pre-line" }}>
                    {commissioningStatus}
                  </Typography>
                </Alert>
              ) : null}

              {loadingTagCatalog ? (
                <Stack direction="row" spacing={1} alignItems="center">
                  <CircularProgress size={16} />
                  <Typography color="text.secondary">Loading tag assignments...</Typography>
                </Stack>
              ) : null}

              <Stack spacing={1.5}>
                {tagCatalog.map((entry) => (
                  <Stack key={entry.logicalKey} direction={{ xs: "column", lg: "row" }} spacing={1.5}>
                    <TextField
                      label="Logical Key"
                      value={entry.logicalKey}
                      disabled
                      sx={{ minWidth: { lg: 180 } }}
                    />

                    <TextField
                      label="Display"
                      value={entry.displayName}
                      disabled
                      sx={{ minWidth: { lg: 170 } }}
                    />

                    <TextField
                      select
                      fullWidth
                      label="PLC Tag"
                      value={entry.plcAddress}
                      onChange={(event) => {
                        const selectedTag = discoveredTags.find((tag) => tag.name === event.target.value);
                        updateTagCatalog(entry.logicalKey, {
                          plcAddress: event.target.value,
                          dataType: selectedTag?.dataType ?? entry.dataType,
                        });
                      }}
                    >
                      <MenuItem value="">Not filled</MenuItem>
                      {discoveredTags.map((tag) => (
                        <MenuItem key={`${entry.logicalKey}:${tag.name}`} value={tag.name}>
                          {`${tag.displayName ?? tag.name} [${tag.name}] (${tag.dataType})`}
                        </MenuItem>
                      ))}
                    </TextField>

                    <TextField
                      select
                      label="Data Type"
                      value={entry.dataType}
                      onChange={(event) => {
                        updateTagCatalog(entry.logicalKey, {
                          dataType: event.target.value,
                        });
                      }}
                      sx={{ minWidth: { lg: 110 } }}
                    >
                      <MenuItem value="bool">bool</MenuItem>
                      <MenuItem value="int">int</MenuItem>
                      <MenuItem value="dint">dint</MenuItem>
                      <MenuItem value="real">real</MenuItem>
                      <MenuItem value="string">string</MenuItem>
                    </TextField>
                  </Stack>
                ))}
              </Stack>

            </Stack>
          </Paper>

          <Stack direction="row" justifyContent="flex-end">
            {selectedLineId !== "new" ? (
              <Button
                variant="outlined"
                color="error"
                onClick={handleDeleteLine}
                sx={{ mr: 1.5 }}
              >
                Delete Line
              </Button>
            ) : null}
            <Button variant="contained" onClick={handleSave} disabled={!canSaveLine}>
              {selectedLineId === "new" ? "Add Line" : "Save Changes"}
            </Button>
          </Stack>

          <Accordion expanded={showDiagnostics} onChange={(_, expanded) => setShowDiagnostics(expanded)}>
            <AccordionSummary expandIcon={<ExpandMoreIcon />}>
              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                Diagnostics (Read Only)
              </Typography>
            </AccordionSummary>
            <AccordionDetails>
              <Stack spacing={2}>
                <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
                  <TextField
                    label="Status (PLC)"
                    value={selectedLine?.status ?? "Offline"}
                    fullWidth
                    disabled
                  />

                  <TextField
                    label="Control Mode (PLC)"
                    value={selectedLine?.controlMode ?? "Auto"}
                    fullWidth
                    disabled
                  />

                  <TextField
                    label="Lifecycle State"
                    value={selectedLine?.lineLifecycleState ?? "Draft"}
                    fullWidth
                    disabled
                  />
                </Stack>

                <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
                  <TextField
                    label="Total Length (ft) (PLC)"
                    value={selectedLine ? selectedLine.totalLength.toLocaleString() : "0"}
                    fullWidth
                    disabled
                  />

                  <TextField
                    label="Runtime (PLC)"
                    value={selectedLine?.runtime ?? "00:00:00"}
                    fullWidth
                    disabled
                  />
                </Stack>

                <Alert severity="info">
                  Status, control mode, total length, and runtime are PLC-driven values and cannot be edited here.
                </Alert>
              </Stack>
            </AccordionDetails>
          </Accordion>
        </Stack>
      </Paper>

      <Dialog open={confirmDeleteOpen} onClose={() => setConfirmDeleteOpen(false)}>
        <DialogTitle>Delete line configuration</DialogTitle>
        <DialogContent>
          <DialogContentText>
            This will remove the line from active use and keep the action in your audit trail. Continue?
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmDeleteOpen(false)}>Cancel</Button>
          <Button color="error" variant="contained" onClick={() => void confirmDeleteLine()}>
            Delete Line
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog
        open={confirmOverwriteOpen}
        onClose={() => {
          setConfirmOverwriteOpen(false);
          setDuplicateLineTarget(null);
        }}
      >
        <DialogTitle>Overwrite existing line?</DialogTitle>
        <DialogContent>
          <Stack spacing={1}>
            <DialogContentText>
              {duplicateLineTarget
                ? `PLC IP ${form.plcIp.trim()} is already assigned to line ${duplicateLineTarget.lineNumber} "${duplicateLineTarget.lineName}".`
                : `This PLC IP is already assigned to another line.`}
            </DialogContentText>
            <DialogContentText>
              Saving now will update the existing line with the current configuration. All line settings, PLC assignment, and commissioning state will be refreshed.
            </DialogContentText>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button
            onClick={() => {
              setConfirmOverwriteOpen(false);
              setDuplicateLineTarget(null);
            }}
          >
            Cancel
          </Button>
          <Button variant="contained" color="warning" onClick={() => void confirmOverwriteLine()}>
            Yes, Overwrite
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog
        open={reauthDialogOpen}
        onClose={() => {
          if (reauthSubmitting || otaStageLoading || otaApplyLoading) {
            return;
          }

          setReauthDialogOpen(false);
          setReauthPassword("");
        }}
      >
        <DialogTitle>Confirm Admin Password</DialogTitle>
        <DialogContent>
          <Stack spacing={1.5} sx={{ pt: 0.5, minWidth: { xs: 260, sm: 420 } }}>
            <DialogContentText>
              {reauthAction === "stage"
                ? "Re-enter your admin password to authorize OTA package staging and checksum verification."
                : reauthAction === "apply"
                  ? "Re-enter your admin password to authorize OTA apply and automatic rollback checks."
                  : "Re-enter your admin password to authorize sensitive OTA actions."}
            </DialogContentText>
            <TextField
              label="Admin Password"
              type="password"
              value={reauthPassword}
              onChange={(event) => setReauthPassword(event.target.value)}
              autoFocus
              fullWidth
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button
            onClick={() => {
              setReauthDialogOpen(false);
              setReauthPassword("");
            }}
            disabled={reauthSubmitting || otaStageLoading || otaApplyLoading}
          >
            Cancel
          </Button>
          <Button
            variant="contained"
            onClick={() => {
              void confirmAdminReauthForOta();
            }}
            disabled={reauthSubmitting || otaStageLoading || otaApplyLoading || reauthPassword.trim().length === 0}
          >
            {reauthSubmitting || otaStageLoading || otaApplyLoading ? "Authorizing..." : "Authorize"}
          </Button>
        </DialogActions>
      </Dialog>

      <Snackbar
        open={successToastOpen}
        autoHideDuration={4000}
        onClose={() => setSuccessToastOpen(false)}
        anchorOrigin={{ vertical: "top", horizontal: "right" }}
      >
        <Alert onClose={() => setSuccessToastOpen(false)} severity="success" variant="filled" sx={{ width: "100%" }}>
          {success}
        </Alert>
      </Snackbar>
    </Stack>
  );
}
