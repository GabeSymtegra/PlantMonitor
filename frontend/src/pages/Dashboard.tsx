import { Alert, Box, Button } from "@mui/material";
import { useMemo, useState } from "react";

import { useDashboard } from "../context/useDashboard";

import SearchBar from "../components/dashboard/SearchBar";
import ViewToggle from "../components/dashboard/ViewToggle";
import LineTable from "../components/dashboard/LineTable";
import LineCards from "../components/dashboard/LineCards";

function normalize(value: string): string {
  return value.toLowerCase().replace(/[^a-z0-9]/g, "");
}

function isSubsequence(target: string, query: string): boolean {
  let queryIndex = 0;

  for (let i = 0; i < target.length && queryIndex < query.length; i += 1) {
    if (target[i] === query[queryIndex]) {
      queryIndex += 1;
    }
  }

  return queryIndex === query.length;
}

function levenshteinDistance(a: string, b: string): number {
  if (!a.length) {
    return b.length;
  }

  if (!b.length) {
    return a.length;
  }

  const rows = a.length + 1;
  const cols = b.length + 1;
  const matrix = Array.from({ length: rows }, () => Array<number>(cols).fill(0));

  for (let i = 0; i < rows; i += 1) {
    matrix[i][0] = i;
  }

  for (let j = 0; j < cols; j += 1) {
    matrix[0][j] = j;
  }

  for (let i = 1; i < rows; i += 1) {
    for (let j = 1; j < cols; j += 1) {
      const substitutionCost = a[i - 1] === b[j - 1] ? 0 : 1;

      matrix[i][j] = Math.min(
        matrix[i - 1][j] + 1,
        matrix[i][j - 1] + 1,
        matrix[i - 1][j - 1] + substitutionCost
      );
    }
  }

  return matrix[rows - 1][cols - 1];
}

function matchScore(target: string, query: string): number {
  const normalizedTarget = normalize(target);
  const normalizedQuery = normalize(query);

  if (!normalizedQuery || !normalizedTarget) {
    return 0;
  }

  if (normalizedTarget === normalizedQuery) {
    return 120;
  }

  if (normalizedTarget.startsWith(normalizedQuery)) {
    return 95;
  }

  if (normalizedTarget.includes(normalizedQuery)) {
    return 80;
  }

  if (isSubsequence(normalizedTarget, normalizedQuery)) {
    return 65;
  }

  if (normalizedQuery.length >= 3) {
    const distance = levenshteinDistance(normalizedTarget, normalizedQuery);

    if (distance <= 2) {
      return 50 - distance * 10;
    }
  }

  return 0;
}

export default function Dashboard() {
  const { view, dashboard, error, refresh } = useDashboard();
  const [searchTerm, setSearchTerm] = useState("");

  const searchSuggestions = useMemo(() => {
    const lines = dashboard?.lines ?? [];
    const values = new Set<string>();
    const query = searchTerm.trim();

    for (const line of lines) {
      values.add(line.lineName);
      values.add(`Line ${line.lineNumber}`);
      values.add(line.lineNumber.toString());
      values.add(line.product);
      values.add(line.plcIp);
      values.add(line.status);
      values.add(line.controlMode);
      values.add(line.manufacturer);
      values.add(line.id.toString());
    }

    const allValues = Array.from(values).filter((value) => value.trim().length > 0);

    if (!query) {
      return allValues.slice(0, 20);
    }

    return allValues
      .map((value) => ({
        value,
        score: matchScore(value, query),
      }))
      .filter((entry) => entry.score > 0)
      .sort((left, right) => right.score - left.score || left.value.localeCompare(right.value))
      .slice(0, 20)
      .map((entry) => entry.value);
  }, [dashboard?.lines, searchTerm]);

  const visibleLines = useMemo(() => {
    const lines = dashboard?.lines ?? [];
    const normalizedSearch = searchTerm.trim();

    if (!normalizedSearch) {
      return lines;
    }

    return lines.filter((line) => {
      const searchTargets = [
        line.lineNumber.toString(),
        `Line ${line.lineNumber}`,
        line.lineName,
        line.product,
        line.status,
        line.controlMode,
        line.plcIp,
        line.manufacturer,
        line.id.toString(),
      ];

      return searchTargets.some((target) => matchScore(target, normalizedSearch) > 0);
    });
  }, [dashboard?.lines, searchTerm]);

  return (
    <Box>
      <Box
        sx={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: { xs: "stretch", sm: "center" },
          flexDirection: { xs: "column", sm: "row" },
          mb: 3,
          gap: 2,
          minWidth: 0,
        }}
      >
        <Box sx={{ flex: 1, minWidth: 0, width: "100%" }}>
          <SearchBar
            value={searchTerm}
            onChange={setSearchTerm}
            suggestions={searchSuggestions}
          />
        </Box>

        <ViewToggle />
      </Box>

      {error ? (
        <Alert
          severity="error"
          sx={{ mb: 2 }}
          action={
            <Button color="inherit" size="small" onClick={() => void refresh()}>
              Retry
            </Button>
          }
        >
          {error}
        </Alert>
      ) : null}

      {view === "table" ? <LineTable lines={visibleLines} /> : <LineCards lines={visibleLines} />}
    </Box>
  );
}