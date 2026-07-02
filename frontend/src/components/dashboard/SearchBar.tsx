import { Autocomplete, TextField } from "@mui/material";

interface SearchBarProps {
  value: string;
  onChange: (value: string) => void;
  suggestions: string[];
}

export default function SearchBar({ value, onChange, suggestions }: SearchBarProps) {
  return (
    <Autocomplete
      freeSolo
      options={suggestions}
      inputValue={value}
      onInputChange={(_, newInputValue) => onChange(newInputValue)}
      onChange={(_, newValue) => {
        if (typeof newValue === "string") {
          onChange(newValue);
        }
      }}
      autoHighlight
      selectOnFocus
      clearOnBlur={false}
      handleHomeEndKeys
      renderInput={(params) => (
        <TextField
          {...params}
          fullWidth
          label="Search Production Lines"
          placeholder="Name, line #, product serial, PLC IP..."
          variant="outlined"
          size="small"
        />
      )}
    />
  );
}