import { ToggleButton, ToggleButtonGroup } from "@mui/material";

import { useDashboard } from "../../context/useDashboard";

export default function ViewToggle() {
  const { view, setView } = useDashboard();

  return (
    <ToggleButtonGroup
      value={view}
      exclusive
      size="small"
      onChange={(_, value) => {
        if (value) {
          setView(value);
        }
      }}
    >
      <ToggleButton value="table">
        Table
      </ToggleButton>

      <ToggleButton value="cards">
        Cards
      </ToggleButton>
    </ToggleButtonGroup>
  );
}