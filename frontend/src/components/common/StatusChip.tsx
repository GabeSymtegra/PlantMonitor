import Chip from "@mui/material/Chip";

type Status =
  | "Running"
  | "Stopped"
  | "Bleedout"
  | "Startup"
  | "Faulted"
  | "Offline"
  | "Maintenance";

interface Props {
  status: Status;
}

export default function StatusChip({ status }: Props) {
  const config = {
    Running: {
      label: "Running",
      color: "success" as const,
    },

    Stopped: {
      label: "Stopped",
      color: "warning" as const,
    },

    Bleedout: {
      label: "Bleedout",
      color: "info" as const,
    },

    Startup: {
      label: "Startup",
      color: "secondary" as const,
    },

    Faulted: {
      label: "Faulted",
      color: "error" as const,
    },

    Offline: {
      label: "Offline",
      color: "default" as const,
    },

    Maintenance: {
      label: "Maintenance",
      color: "info" as const,
    },
  };

  const chip = config[status];

  return (
    <Chip
      label={chip.label}
      color={chip.color}
      size="small"
      variant="filled"
    />
  );
}