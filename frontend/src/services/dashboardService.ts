import type { ProductionLine } from "../types/ProductionLine";

export async function getProductionLines(): Promise<ProductionLine[]> {

    return [

        {
            id: 1,
            lineNumber: 1,
            product: "PVC Pipe",
            status: "Running",
            controlMode: "Auto",
            totalLength: 15423,
            runtime: "04:12:33",
            plcIp: "192.168.1.10"
        },

        {
            id: 2,
            lineNumber: 2,
            product: "PEX Tubing",
            status: "Stopped",
            controlMode: "Manual",
            totalLength: 8422,
            runtime: "01:22:17",
            plcIp: "192.168.1.11"
        },

        {
            id: 3,
            lineNumber: 3,
            product: "ABS Pipe",
            status: "Faulted",
            controlMode: "Auto",
            totalLength: 2241,
            runtime: "00:18:44",
            plcIp: "192.168.1.12"
        }

    ];

}