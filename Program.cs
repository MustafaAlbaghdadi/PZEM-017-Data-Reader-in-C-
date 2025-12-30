using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO.Ports;

namespace ConsoleApp16
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // List available ports
            string[] ports = SerialPort.GetPortNames();
            Console.WriteLine("Available COM Ports:");
            foreach (var p in ports)
            {
                Console.WriteLine($"- {p}");
            }

            // Change "COM3" to your actual COM port
            string comPort = "COM7"; 
            if (!ports.Contains(comPort))
            {
                Console.WriteLine($"Warning: {comPort} not found in available ports.");
                if (ports.Length > 0) comPort = ports[0];
            }
            
            Console.WriteLine($"Starting Scan on {comPort}...");
            
            try
            {
                using (var pzem = new Pzem17(comPort))
                {
                    pzem.Open();
                    
                    Console.WriteLine("Scanning with Function Code 0x04 (Input Registers)...");
                    Console.WriteLine("Press Ctrl+C to stop.");

                    // Fixed settings based on user info
                    int baud = 9600;
                    var stopBitsOptions = new[] { StopBits.One, StopBits.Two };
                    var rtsOptions = new[] { false, true };
                    // Prioritize Address 2 since we found it there before, then 1
                    var addressOptions = new byte[] { 2, 1, 3, 4, 5 };

                    while (true)
                    {
                        foreach (var stopBit in stopBitsOptions)
                        {
                            foreach (var rts in rtsOptions)
                            {
                                pzem.UpdatePortSettings(baud, Parity.None, stopBit, rts);
                                
                                foreach (var addr in addressOptions)
                                {
                                    pzem.SlaveAddress = addr;
                                    Console.Write($"Ping: 9600/N/{stopBit}/RTS={rts}/Addr={addr}... ");
                                    
                                    if (pzem.Ping())
                                    {
                                        Console.WriteLine("RESPONSE RECEIVED!");
                                        Console.WriteLine("--------------------------------------------------");
                                        Console.WriteLine($"FOUND WORKING SETTINGS: 9600/N/{stopBit}/RTS={rts}/Addr={addr}");
                                        Console.WriteLine("--------------------------------------------------");
                                        
                                        // Switch to reading mode
                                        while (true)
                                        {
                                            try
                                            {
                                                var data = pzem.ReadData();
                                                Console.Clear();
                                                Console.WriteLine("PZEM-017 Readings:");
                                                Console.WriteLine("------------------");
                                                Console.WriteLine(data.ToString());
                                                Console.WriteLine("------------------");
                                                Console.WriteLine($"Last Update: {DateTime.Now}");
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"Read Error: {ex.Message}");
                                            }
                                            Thread.Sleep(1000);
                                        }
                                    }
                                    else
                                    {
                                        Console.Write("\r");
                                    }
                                }
                            }
                        }
                        Console.WriteLine("Cycle complete. Retrying...");
                        Thread.Sleep(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to open port: {ex.Message}");
            }
            
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
