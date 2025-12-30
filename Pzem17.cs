using System;
using System.IO.Ports;
using System.Threading;

namespace ConsoleApp16
{
    public class Pzem17 : IDisposable
    {
        private SerialPort _serialPort;
        public byte SlaveAddress { get; set; }

        public Pzem17(string portName, byte slaveAddress = 0x01)
        {
            SlaveAddress = slaveAddress;
            // PZEM-017 usually requires StopBits.Two
            _serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.Two);
            _serialPort.ReadTimeout = 1000; 
            _serialPort.WriteTimeout = 1000;
            _serialPort.RtsEnable = false;
            _serialPort.DtrEnable = false;
        }

        public void Open()
        {
            if (!_serialPort.IsOpen)
            {
                _serialPort.Open();
            }
        }

        public void UpdatePortSettings(int baudRate, Parity parity, StopBits stopBits, bool rtsEnable)
        {
            bool wasOpen = _serialPort.IsOpen;
            if (wasOpen) _serialPort.Close();

            _serialPort.BaudRate = baudRate;
            _serialPort.Parity = parity;
            _serialPort.StopBits = stopBits;
            _serialPort.RtsEnable = rtsEnable;
            _serialPort.DtrEnable = rtsEnable; 
            _serialPort.DataBits = 8;

            if (wasOpen) 
            {
                _serialPort.Open();
                _serialPort.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();
            }
        }

        public void Close()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }

        public PzemData ReadData()
        {
            // Modbus RTU Frame: Address (1) + Function (1) + Start Addr (2) + Count (2) + CRC (2)
            byte[] request = new byte[8];
            request[0] = SlaveAddress;
            request[1] = 0x04; // Read Input Registers (Function Code 04)
            request[2] = 0x00; // Start Address High
            request[3] = 0x00; // Start Address Low
            request[4] = 0x00; // Number of Registers High
            request[5] = 0x08; // Number of Registers Low (Read 8 registers: V, I, P, E, Alarms)
            
            byte[] crc = CalculateCRC(request, 6);
            request[6] = crc[0];
            request[7] = crc[1];

            // Clear buffer
            _serialPort.DiscardInBuffer();
            _serialPort.Write(request, 0, request.Length);

            // Give the device some time to respond
            Thread.Sleep(100);

            // Response: Address (1) + Function (1) + Byte Count (1) + Data (16) + CRC (2) = 21 bytes
            byte[] buffer = new byte[21];
            int bytesRead = 0;
            int totalBytesToRead = 21;
            
            // Wait for data
            int timeoutCounter = 0;
            while (_serialPort.BytesToRead < 5 && timeoutCounter < 20) // Wait for at least header
            {
                Thread.Sleep(100);
                timeoutCounter++;
            }

            if (_serialPort.BytesToRead == 0)
            {
                 throw new TimeoutException("No data received. Check: 1. Power to PZEM (Shunt side) 2. A/B Wiring 3. Slave Address.");
            }

            // Check for Modbus Exception (5 bytes: Addr + Func + ExceptionCode + CRC)
            if (_serialPort.BytesToRead == 5)
            {
                byte[] errorBuffer = new byte[5];
                _serialPort.Read(errorBuffer, 0, 5);
                // Function code | 0x80 indicates error
                if ((errorBuffer[1] & 0x80) != 0)
                {
                    throw new Exception($"Modbus Exception Code: {errorBuffer[2]}");
                }
            }

            // Wait for full frame
            while (_serialPort.BytesToRead < totalBytesToRead && timeoutCounter < 25)
            {
                Thread.Sleep(100);
                timeoutCounter++;
            }

            if (_serialPort.BytesToRead < totalBytesToRead)
            {
                 throw new TimeoutException($"Received partial data: {_serialPort.BytesToRead} bytes, expected {totalBytesToRead}.");
            }

            // Read all data
            while (bytesRead < totalBytesToRead)
            {
                int read = _serialPort.Read(buffer, bytesRead, totalBytesToRead - bytesRead);
                if (read == 0) throw new TimeoutException("No data received from PZEM-017");
                bytesRead += read;
            }

            // Validate CRC
            byte[] responseCrc = CalculateCRC(buffer, 19); // CRC over first 19 bytes
            if (responseCrc[0] != buffer[19] || responseCrc[1] != buffer[20])
            {
                throw new Exception("CRC Error");
            }

            return ParseData(buffer);
        }

        public bool Ping()
        {
            try
            {
                // Try to read just 1 register (Voltage) using Function Code 04
                byte[] request = new byte[8];
                request[0] = SlaveAddress;
                request[1] = 0x04; // Function Code 04
                request[2] = 0x00; 
                request[3] = 0x00; 
                request[4] = 0x00; 
                request[5] = 0x01; // Read 1 register
                
                byte[] crc = CalculateCRC(request, 6);
                request[6] = crc[0];
                request[7] = crc[1];

                _serialPort.DiscardInBuffer();
                _serialPort.Write(request, 0, request.Length);
                Thread.Sleep(200);

                if (_serialPort.BytesToRead >= 7) // Addr+Func+Count+Data(2)+CRC(2) = 7 bytes
                {
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private PzemData ParseData(byte[] buffer)
        {
            // Data starts at index 3
            // 0x0000: Voltage (0.01V)
            double voltage = (buffer[3] << 8 | buffer[4]) * 0.01;

            // 0x0001: Current (0.01A)
            double current = (buffer[5] << 8 | buffer[6]) * 0.01;

            // 0x0002-0x0003: Power (0.1W) - Low word first in register map? 
            // Usually Modbus is Big Endian for 16-bit registers.
            // Register 2 is Low 16 bits, Register 3 is High 16 bits according to some docs, 
            // but standard PZEM usually sends High word then Low word or follows standard Modbus Big Endian.
            // Let's assume standard 32-bit int from two 16-bit registers.
            // Actually PZEM-017 datasheet says:
            // 0x0002: Power Low 16 bits
            // 0x0003: Power High 16 bits
            // This implies Little Endian for the 32-bit value across registers? Or just split.
            // Let's try standard interpretation: 
            // Power = (Reg3 << 16) | Reg2
            int powerRaw = (buffer[7] << 8 | buffer[8]) | ((buffer[9] << 8 | buffer[10]) << 16);
            double power = powerRaw * 0.1;

            // 0x0004-0x0005: Energy (1Wh)
            // Energy = (Reg5 << 16) | Reg4
            int energyRaw = (buffer[11] << 8 | buffer[12]) | ((buffer[13] << 8 | buffer[14]) << 16);
            double energy = energyRaw; // 1Wh unit

            return new PzemData
            {
                Voltage = voltage,
                Current = current,
                Power = power,
                Energy = energy
            };
        }

        private byte[] CalculateCRC(byte[] data, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            return new byte[] { (byte)(crc & 0xFF), (byte)(crc >> 8) };
        }

        public byte[] ReadRawBytes()
        {
            // Just read whatever is there for debugging
            if (_serialPort.BytesToRead > 0)
            {
                byte[] buffer = new byte[_serialPort.BytesToRead];
                _serialPort.Read(buffer, 0, buffer.Length);
                return buffer;
            }
            return new byte[0];
        }

        public void Dispose()
        {
            if (_serialPort != null)
            {
                if (_serialPort.IsOpen) _serialPort.Close();
                _serialPort.Dispose();
            }
        }
    }

    public class PzemData
    {
        public double Voltage { get; set; }
        public double Current { get; set; }
        public double Power { get; set; }
        public double Energy { get; set; }

        public override string ToString()
        {
            return $"Voltage: {Voltage:F2} V, Current: {Current:F2} A, Power: {Power:F2} W, Energy: {Energy:F0} Wh";
        }
    }
}
