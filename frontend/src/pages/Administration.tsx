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
  FormControl,
  InputLabel,
  LinearProgress,
  List,
  ListItemButton,
  ListItemText,
  MenuItem,
  Paper,
  Select,
  Stack,
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
  processorType: "ControlLogix" | "CompactLogix" | "Micro800";
  connectionTimeoutMs: number;
  readTimeoutMs: number;
  retryCount: number;
  retryDelayMs: number;
};

const emptyLineForm: LineConfigForm = {
  lineNumber: 1,
  lineName: "",
  product: "000-000-00-0",
  recipeId: "Unknown",
  machineId: "Unknown",
  operatorName: "Unknown",
  plcIp: "",
  manufacturer: "AllenBradley",
  lineLifecycleState: "Draft",
};

const defaultPlcSettings: LinePlcSettingsForm = {
  pollIntervalMs: 2000,
  routePath: "1,0",
  processorType: "ControlLogix",
  connectionTimeoutMs: 3000,
  readTimeoutMs: 3000,
  retryCount: 1,
  retryDelayMs: 250,
};

const productSerialPattern = /^\d{3}-\d{3}-\d{2}-\d{1}$/;

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
  const [tagSearch, setTagSearch] = useState("");
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
    const search = tagSearch.trim().toLowerCase();

    if (!search) {
      return discoveredTags;
    }

    return discoveredTags.filter((tag) => {
      const parentPath = tag.parentPath?.toLowerCase() ?? "";
      return tag.name.toLowerCase().includes(search) || parentPath.includes(search);
    });
  }, [discoveredTags, tagSearch]);

  const hasDiscoveredTags = discoveredTags.length > 0;

  function toConnectionOptions(settings: LinePlcSettingsForm): PlcConnectionOptions {
    return {
      routePath: settings.routePath.trim() || "1,0",
      processorType: settings.processorType,
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
      setPlcSettings({
        pollIntervalMs: assignment.pollIntervalMs,
        routePath: assignment.routePath,
        processorType: assignment.processorType,
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
    setTagSearch("");
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
      setTagCatalog((previous) =>
        previous.map((entry) => ({
          ...entry,
          driver: String(value),
        }))
      );
    }

    setForm((previous) => ({
      ...previous,
      [key]: value,
    }));
  }

  function validateForm(): string | null {
    const trimmedProduct = form.product.trim();
    const trimmedLineName = form.lineName.trim();
    const trimmedIp = form.plcIp.trim();

    if (!trimmedLineName) {
      return "Line name is required.";
    }

    if (!form.product.trim()) {
      return "Product is required.";
    }

    if (!productSerialPattern.test(trimmedProduct)) {
      return "Product serial must match xxx-xxx-xx-x using digits.";
    }

    if (!trimmedIp) {
      return "PLC IP is required.";
    }

    if (!isValidIpv4Address(trimmedIp)) {
      return "PLC IP must be a valid IPv4 address (example: 192.168.1.105).";
    }

    const duplicateIp = lines.some((line) => {
      if (selectedLineId !== "new" && line.id === selectedLineId) {
        return false;
      }

      return line.plcIp.trim() === trimmedIp;
    });

    if (duplicateIp) {
      return `PLC IP ${trimmedIp} is already assigned to another line.`;
    }

    if (!trimmedProduct) {
      return "Product is required.";
    }

    return null;
  }

  async function handleSave() {
    setError("");
    setSuccess("");

    const validationError = validateForm();

    if (validationError) {
      setError(validationError);
      return;
    }

    if (selectedLineId === "new") {
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

      const createdLine = await addLine({
        ...form,
        lineNumber: nextLineNumber,
        lineName: form.lineName.trim(),
        recipeId: form.recipeId.trim() || "Unknown",
        machineId: form.machineId.trim() || "Unknown",
        operatorName: form.operatorName.trim() || "Unknown",
        startDateTime: new Date().toISOString(),
        status: LineStatus.Offline,
        timeInStatus: "00:00:00",
        controlMode: "Auto",
        percentAutoMode: 100,
        autoVariance: 0,
        percentManualMode: 0,
        manualVariance: 0,
        totalVariance: 0,
        totalLength: 0,
        runtime: "00:00:00",
        product: form.product.trim(),
        plcIp: form.plcIp.trim(),
        isActive: false,
      });

      await upsertLineProtocolAssignment(createdLine.id, {
        manufacturer: form.manufacturer,
        presetName: "BasicStatus",
        presetVersion: 1,
        pollIntervalMs: plcSettings.pollIntervalMs,
        routePath: plcSettings.routePath.trim() || "1,0",
        processorType: plcSettings.processorType,
        connectionTimeoutMs: plcSettings.connectionTimeoutMs,
        readTimeoutMs: plcSettings.readTimeoutMs,
        retryCount: plcSettings.retryCount,
        retryDelayMs: plcSettings.retryDelayMs,
      });

      await replaceLineTagCatalog(createdLine.id, form.manufacturer, tagCatalog);

      const readiness = await getCommissioningReadiness(createdLine.id);
      setCommissioningReady(readiness.isReady);

      if (readiness.isReady) {
        await activateCommissionedLine(createdLine.id);
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
      const current = refreshed.find((line) => line.id === createdLine.id);
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

      setSuccess("Line added successfully. Configuration and tag assignments were saved.");

      try {
        await refreshDashboard();
      } catch {
        // Keep admin flow successful even if dashboard refresh falls back to polling.
      }

      return;
    } else {
      await updateLine(selectedLineId, {
        lineNumber: selectedLine?.lineNumber ?? form.lineNumber,
        lineName: form.lineName.trim(),
        product: form.product.trim(),
        recipeId: form.recipeId.trim() || "Unknown",
        machineId: form.machineId.trim() || "Unknown",
        operatorName: form.operatorName.trim() || "Unknown",
        manufacturer: form.manufacturer,
        plcIp: form.plcIp.trim(),
        lineLifecycleState: form.lineLifecycleState,
      });
      setSuccess("Line updated successfully.");
    }

    const refreshed = await getAllLines();
    setLines(refreshed);

    const assignmentLineId = selectedLineId;

    if (assignmentLineId) {
      await upsertLineProtocolAssignment(assignmentLineId, {
        manufacturer: form.manufacturer,
        presetName: "BasicStatus",
        presetVersion: 1,
        pollIntervalMs: plcSettings.pollIntervalMs,
        routePath: plcSettings.routePath.trim() || "1,0",
        processorType: plcSettings.processorType,
        connectionTimeoutMs: plcSettings.connectionTimeoutMs,
        readTimeoutMs: plcSettings.readTimeoutMs,
        retryCount: plcSettings.retryCount,
        retryDelayMs: plcSettings.retryDelayMs,
      });
    }

    try {
      await refreshDashboard();
    } catch {
      // Keep admin flow successful even if dashboard refresh falls back to polling.
    }

    const current = refreshed.find((line) => line.id === selectedLineId);
    if (current) {
      setForm((previous) => ({
        ...previous,
        lineLifecycleState: current.lineLifecycleState,
      }));
    }
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
    setSuccess("Line deleted successfully.");

    try {
      await refreshDashboard();
    } catch {
      // Keep admin flow successful even if dashboard refresh falls back to polling.
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
        await discoverTags(result.driver, form.plcIp.trim());
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

  async function discoverTags(driver: PlcConnectionResult["driver"], ipAddress: string) {
    resetTagBrowser();
    setBrowsingTags(true);

    try {
      const tags = await browsePlcTags({
        driver,
        ipAddress,
        options: toConnectionOptions(plcSettings),
      });

      if (tags.length === 0) {
        setTagBrowserStatus(
          driver === "Siemens"
            ? "Siemens connection succeeded, but no configured fallback tags are available yet. Live Siemens symbol browsing will require an external symbol source."
            : "PLC connection succeeded, but no readable/discoverable tags were returned for this controller filter."
        );
        return;
      }

      setDiscoveredTags(tags);
      setTagBrowserStatus(
        driver === "Siemens"
          ? "Showing available configured Siemens tags. Live Siemens symbol browsing is not available from the current driver alone."
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
  const canTestConnection = isValidIpv4Address(form.plcIp) && hasValidRoutePath && !testingConnection;
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
              label="Product Serial"
              value={form.product}
              onChange={(event) => updateForm("product", event.target.value)}
              fullWidth
              placeholder="123-456-78-9"
            />

            <TextField
              label="Recipe ID"
              value={form.recipeId}
              onChange={(event) => updateForm("recipeId", event.target.value)}
              fullWidth
              placeholder="RCP-101"
            />
          </Stack>

          <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
            <TextField
              label="Machine ID"
              value={form.machineId}
              onChange={(event) => updateForm("machineId", event.target.value)}
              fullWidth
              placeholder="MX-001"
            />

            <TextField
              label="Operator"
              value={form.operatorName}
              onChange={(event) => updateForm("operatorName", event.target.value)}
              fullWidth
              placeholder="Unknown"
            />

            <TextField
              label="PLC IP Address"
              value={form.plcIp}
              onChange={(event) => updateForm("plcIp", event.target.value)}
              fullWidth
              placeholder="192.168.1.105"
            />
          </Stack>

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
                    label="Search Tags"
                    value={tagSearch}
                    onChange={(event) => setTagSearch(event.target.value)}
                    size="small"
                    sx={{ minWidth: { xs: "100%", md: 260 } }}
                  />
                </Stack>

                <Stack direction={{ xs: "column", lg: "row" }} spacing={2} alignItems="stretch">
                  <Paper variant="outlined" sx={{ flex: 1, minHeight: 320 }}>
                    <List sx={{ maxHeight: 320, overflowY: "auto", p: 0 }}>
                      {filteredTags.map((tag) => (
                        <ListItemButton
                          key={tag.name}
                          selected={selectedTag?.name === tag.name}
                          onClick={() => void handleSelectTag(tag)}
                        >
                          <ListItemText
                            primary={tag.name}
                            secondary={`${tag.dataType} | ${tag.parentPath ?? "root"}`}
                          />
                        </ListItemButton>
                      ))}
                    </List>

                    {!filteredTags.length ? (
                      <Box sx={{ p: 2 }}>
                        <Typography color="text.secondary">
                          No tags match the current search.
                        </Typography>
                      </Box>
                    ) : null}
                  </Paper>

                  <Paper variant="outlined" sx={{ flex: 1, p: 2, minHeight: 320 }}>
                    <Stack spacing={1.5}>
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
                            Reading {selectedTag.name}...
                          </Typography>
                        </Stack>
                      ) : null}

                      {selectedTagResult ? (
                        <>
                          <TextField label="Tag Name" value={selectedTagResult.name} fullWidth disabled />
                          <TextField label="Data Type" value={selectedTagResult.dataType} fullWidth disabled />
                          <TextField
                            label="Parent Path"
                            value={selectedTag?.parentPath ?? "root"}
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
                      <MenuItem value="">Unassigned</MenuItem>
                      {discoveredTags.map((tag) => (
                        <MenuItem key={`${entry.logicalKey}:${tag.name}`} value={tag.name}>
                          {`${tag.name} (${tag.dataType})`}
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
    </Stack>
  );
}
