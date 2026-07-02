import type { ProductionLine } from "../types/ProductionLine";

export interface DiameterSensor {
  id: string;
  label: string;
  diameterMm: number;
  status: "Normal" | "Warning" | "Fault";
  deviationMm: number;
}

export interface LineDetailModel {
  line: ProductionLine;
  pressuresPsi: {
    extruder: number;
    dieHead: number;
    cooling: number;
  };
  temperaturesC: {
    zone1: number;
    zone2: number;
    zone3: number;
    die: number;
  };
  motorSpeedsRpm: {
    puller: number;
    cutter: number;
  };
  diameterSensors: DiameterSensor[];
  updatedAt: string;
}
