import { Box, Grid, Typography } from "@mui/material";

import Navbar from "../components/layout/Navbar";
import Sidebar from "../components/layout/Sidebar";
import LineCard from "../components/lines/LineCard";

export default function Dashboard() {
  return (
    <>
      <Navbar />

      <Box sx={{ display: "flex" }}>

        <Sidebar />

        <Box sx={{ flexGrow: 1, p: 4 }}>

          <Typography variant="h4" gutterBottom>
            Production Lines
          </Typography>

          <Grid container spacing={3}>

            <Grid>
              <LineCard />
            </Grid>

            <Grid>
              <LineCard />
            </Grid>

            <Grid>
              <LineCard />
            </Grid>

            <Grid>
              <LineCard />
            </Grid>

          </Grid>

        </Box>

      </Box>
    </>
  );
}