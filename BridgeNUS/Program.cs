using System;
//using System.Collections;
using System.Collections.Generic;
//using System.Data;
//using System.IO;
using System.Linq;
//using System.Reflection.Emit;
//using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices.WindowsRuntime;
//using System.Runtime.Remoting.Messaging;
//using System.Text;
//using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
//using static System.Net.Mime.MediaTypeNames;

namespace BridgeNUS
{
    internal class Program
    {
        public static List<ulong> BLE1507Address = new List<ulong>();
        
        const string strTitle = "BridgeNUS for BLE1507. (C) 2023 Crane-elec. Co., Ltd.";
        const string strTargetServiceUUID = "6E400001-B5A3-F393-E0A9-E50E24DCCA9E";
        const string strTargetCharacteristicsRX = "6E400002-B5A3-F393-E0A9-E50E24DCCA9E";
        const string strTargetCharacteristicsTX = "6E400003-B5A3-F393-E0A9-E50E24DCCA9E";

        static BluetoothLEAdvertisementWatcher watcher;
        static ulong TargetAddress = 0UL;

        static void Main(string[] args)
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var ver = asm.GetName();

            Console.WriteLine(strTitle);
            Console.WriteLine("Build: {0}", ver.Version);
            Console.WriteLine("BLE1507 UUID(NUS) : " + strTargetServiceUUID);
            Console.WriteLine("Characteristics RX, send to BLE1507 : " + strTargetCharacteristicsRX);
            Console.WriteLine("Characteristics TX, receive from BLE1507 : " + strTargetCharacteristicsTX);
            Console.WriteLine("");
            Console.WriteLine("Press any key to start.");
            Console.ReadKey();

            Task.Run(AsyncMain).Wait();

            LOG("EXIT");
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
            Environment.Exit(0);
        }
        static async Task AsyncMain()
        {

            // Scanning BLE1507.
            Console.Write("Scanning.");
            watcher = new BluetoothLEAdvertisementWatcher();
            watcher.Received += Watcher_Received;
            watcher.ScanningMode = BluetoothLEScanningMode.Active;//.Passive;

            // Waiting for watcher_Received() event.
            watcher.Start();
            await Task.Delay(1000);
            Console.Write(".");
            await Task.Delay(1000);
            Console.Write(".");
            await Task.Delay(1000);
            Console.WriteLine("");
            watcher.Stop();

            // Discovered BLE1507?
            if (BLE1507Address.Count == 0)
            {
                LOG("Not found.");
                return;
            }

            // List up MAC address.
            foreach (var addr in BLE1507Address.Distinct())
            {
                    LOG(addr.ToString("X6"));
            }

            // Connecting.
            // NOTE: The first of list set to target.
            TargetAddress = BLE1507Address.First();
            Console.WriteLine("Connect to '" + TargetAddress.ToString("X6") + "'..." );
            var BLE1507_device = await BluetoothLEDevice.FromBluetoothAddressAsync(TargetAddress);
            LOG(BLE1507_device.Name.ToString());
            //LOG(BLE1507_device.ConnectionStatus.ToString());

            // Detecting services.
            Console.WriteLine("Detecting services '" + strTargetServiceUUID + "'...");
            var BLE1507_services = await BLE1507_device.GetGattServicesForUuidAsync(new Guid(strTargetServiceUUID));
            LOG(BLE1507_services.Status.ToString());
            //LOG(BLE1507_services.Services.Count.ToString());

            // Detecting characteristics RX.
            Console.WriteLine("Detecting characteristics RX '" + strTargetCharacteristicsRX + "'...");
            var BLE1507_characteristicRX = await BLE1507_services.Services.First().GetCharacteristicsForUuidAsync(new Guid(strTargetCharacteristicsRX));
            LOG(BLE1507_characteristicRX.Status.ToString());

            // Detecting characteristics TX.
            Console.WriteLine("Detecting characteristics TX '" + strTargetCharacteristicsTX + "'...");
            var BLE1507_characteristicTX = await BLE1507_services.Services.First().GetCharacteristicsForUuidAsync(new Guid(strTargetCharacteristicsTX));
            LOG(BLE1507_characteristicTX.Status.ToString());

            // Set "Notify" to CCCD (Client Characteristic Configuration Descriptor).
            await BLE1507_characteristicTX.Characteristics.First().WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);

            // Set callback function for discover the BLE1507_characteristicTX value is changed.
            BLE1507_characteristicTX.Characteristics.First().ValueChanged += ValueTX_Changed_BLE1507;

            // Display description.
            Console.WriteLine("");
            Console.WriteLine("Description:");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("    When Spresense send a message to BLE1507, sent the message can receive here.");
            Console.WriteLine("    Example, send \"Hello!\" and enter(CR line feed) using UART-Serial from Spresense to BLE1507.");
            Console.Write("    Display the received message to the following: ");
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Write("[From BLE1507]");
            Console.ResetColor();
            Console.WriteLine("Hello!");
            Console.WriteLine("");

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("    When you input a message and enter here, the message send to Spresense through BLE1507.");
            Console.WriteLine("    Example, input \"Hi!\" and enter, Spresense receive the message using UART-Serial from BLE1507.");
            Console.Write("    Display the sent message to the following: ");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write("[To BLE1507]");
            Console.ResetColor();
            Console.WriteLine("Hi!");
            Console.WriteLine("");

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("    To exit, input \"EXIT\" in uppercase and enter.");
            Console.ResetColor();

            // Send and receive routine
            Console.WriteLine("Ready.");
            while (true)
            {
                string inputline = Console.ReadLine();

                if(inputline == "EXIT")
                {
                    break;
                }
                else if(0 < inputline.Length)
                {
                    // Convert a input message from "string" to "byte[]" for WriteValueAsync() function.
                    byte[] msg = System.Text.Encoding.ASCII.GetBytes(inputline);

                    // Send message to BLE1507
                    try
                    {
                        await BLE1507_characteristicRX.Characteristics.First().WriteValueAsync(msg.AsBuffer(), GattWriteOption.WriteWithoutResponse);
                    }
                    catch (Exception ex)
                    {
                        LOG("Error. Disconnection may have occurred.");
                        break;
                    }

                    // Local echo
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Write("[To BLE1507]");
                    Console.ResetColor();
                    foreach (var b in msg.ToArray())
                    {
                        Console.Write(((char)b).ToString());
                    }
                    Console.WriteLine("");

                }

            }

            return;
        }
        private static void ValueTX_Changed_BLE1507(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Write("[From BLE1507]");
            Console.ResetColor();
            // Getting value format is "IBuffer". need to convert to "byte[]".
            foreach (var b in args.CharacteristicValue.ToArray())
            {
                Console.Write(((char)b).ToString());
            }
            Console.WriteLine("");

        }

        private static void Watcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            // Get the UUIDs of BLE Advertisement Service device.
            var bleServiceUUIDs = args.Advertisement.ServiceUuids;
            if (0 < bleServiceUUIDs.Count)
            {
                foreach (var uuidone in bleServiceUUIDs)
                {
                    // Target is NUS only here.
                    if (uuidone.ToString().ToUpper() == strTargetServiceUUID)
                    {
                        // When UUID is NUS, add to list.
                        Program.BLE1507Address.Add(args.BluetoothAddress);
                    }
                }
            }
        }

        private static void LOG(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(">>>" + message);
            Console.ResetColor();
        }
    }
}
