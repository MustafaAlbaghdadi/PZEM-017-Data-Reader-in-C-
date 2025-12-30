# PZEM-017 Data Acquisition Tool (C#)

A robust C# console application designed to communicate with the **PZEM-017 DC Energy Meter** via Modbus RTU. This tool reads Voltage, Current, Power, and Energy consumption data and displays it in real-time.

## 🚀 Features

*   **Auto-Discovery Scanner:** Automatically scans for the correct COM port settings (StopBits, RTS) and Modbus Slave Address (1-5).
*   **Real-Time Monitoring:** Displays Voltage (V), Current (A), Power (W), and Energy (Wh) continuously.
*   **Robust Error Handling:** Handles timeouts, CRC errors, and connection drops gracefully.
*   **Modbus RTU Implementation:** Custom lightweight Modbus implementation specifically tuned for PZEM-017 quirks (Function Code 0x04).

## 🛠️ Prerequisites

### Software
*   Windows OS
*   .NET Framework 4.8 Runtime
*   USB-to-RS485 Driver (e.g., CH340, FTDI) installed.

### Hardware
*   **PZEM-017** DC Energy Meter (with Shunt).
*   **USB to RS485 Converter**.
*   DC Power Source (Battery or Power Supply) connected to the Shunt side (Required to power the PZEM's communication circuit).

## 🔌 Wiring Guide

| USB-RS485 Adapter | PZEM-017 |
| :--- | :--- |
| **A (D+)** | **A** |
| **B (D-)** | **B** |
| **GND** (Optional) | **-** |

> **⚠️ Important:** The PZEM-017 RS485 interface is isolated. It **will not work** if you only connect the USB cable. You **MUST** have voltage (>7V) connected to the measurement terminals (Shunt side) to power the internal chip.

## 📥 Installation & Usage

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/yourusername/PZEM-017-Reader.git
    ```
2.  **Open the solution** in Visual Studio.
3.  **Build** the project (Release/Debug).
4.  **Run** the application.
    *   The program will list available COM ports.
    *   It will automatically scan for the device.
    *   Once found, it will start displaying readings.

## 🔍 Troubleshooting

If you see "No data received" or "Scan Failed":

1.  **Check Power:** Ensure the PZEM-017 has voltage on the input terminals.
2.  **Swap A/B Wires:** Sometimes adapters are labeled incorrectly. Try swapping A and B.
3.  **Check COM Port:** Ensure no other software (like the official PZEM software) is using the port.
4.  **Drivers:** Verify the USB-RS485 driver is installed and the device appears in Device Manager.

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
