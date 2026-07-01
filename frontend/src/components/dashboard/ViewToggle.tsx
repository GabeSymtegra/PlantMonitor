import { ToggleButton, ToggleButtonGroup } from "@mui/material";
import { useState } from "react";

export default function ViewToggle() {
  const [view, setView] = useState("table");

  return (
    <ToggleButtonGroup
      value={view}
      exclusive
      onChange={(_, value) => {
        if (value) setView(value);
      }}
      size="small"
    >
      <ToggleButton value="table">Table</ToggleButton>

      <ToggleButton value="cards">Cards</ToggleButton>
    </ToggleButtonGroup>
  );
}