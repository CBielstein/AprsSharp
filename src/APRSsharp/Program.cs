﻿namespace AprsSharp.Applications.Console
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using AprsSharp.Connections.AprsIs;
    using AprsSharp.Parsers.Aprs;
    using AprsSharp.Protocols.KISS;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// The public class that will be called when building the console application.
    /// It is the main class that will have functionality of calling and decoding the packets.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// The modes in which this program can operate.
        /// </summary>
        public enum Mode
        {
            /// <summary>
            /// Receives packets from APRS-IS
            /// </summary>
            APRSIS,

            /// <summary>
            /// Interfaces with a TNC via TCP
            /// </summary>
            TCPTNC,
        }

        /// <summary>
        /// A function matching the delegate event to print the received packet.
        /// </summary>
        /// <param name="p">A <see cref="Packet"/> to be printed.</param>
        public static void PrintPacket(Packet p)
        {
            Console.WriteLine();
            Console.WriteLine($"Received type: {p.InfoField.Type}");

            Console.WriteLine($"    Sender: {p.Sender}");
            Console.WriteLine($"    Destination: {p.Destination}");
            Console.WriteLine($"    Path: {string.Join(',', p.Path)}");
            Console.WriteLine($"    Received At: {p.ReceivedTime} {p.ReceivedTime?.Kind}");
            Console.WriteLine($"    Type: {p.InfoField.Type}");

            // TODO Issue #103: Reduce copy/paste below
            // TODO Issue #103: Clean up position printing:
                // * Position lat/long encoding uses symbol IDs, not the most user-friendly
                // * Gridsquare print out should probably print the correct number of characters based on ambiguitiy
            if (p.InfoField is PositionInfo pi)
            {
                Console.WriteLine($"    Timestamp: {pi.Timestamp?.DateTime} {pi.Timestamp?.DateTime.Kind}");
                Console.WriteLine($"    Position: {pi.Position.Encode()} ({pi.Position.EncodeGridsquare(4, false)})");
                Console.WriteLine($"    Comment: {pi.Comment}");
                Console.WriteLine($"    Has Messaging: {pi.HasMessaging}");

                if (p.InfoField is WeatherInfo wi)
                {
                    Console.WriteLine($"Wind direction (degrees): {wi.WindDirection}");
                    Console.WriteLine($"Wind speed (one-minute sustained): {wi.WindSpeed}");
                    Console.WriteLine($"Wind gust (5 minute max, mph): {wi.WindGust}");
                    Console.WriteLine($"Temperature (F): {wi.Temperature}");
                    Console.WriteLine($"1-hour rainfall (100th of inch): {wi.Rainfall1Hour}");
                    Console.WriteLine($"24-hour rainfall (100th of inch): {wi.Rainfall24Hour}");
                    Console.WriteLine($"Rainfall since midnight (100th of inch): {wi.RainfallSinceMidnight}");
                    Console.WriteLine($"Humidity: {wi.Humidity}");
                    Console.WriteLine($"Barometric pressure: {wi.BarometricPressure}");
                    Console.WriteLine($"Luminosity: {wi.Luminosity}");
                    Console.WriteLine($"Raw rain: {wi.RainRaw}");
                    Console.WriteLine($"Snow (inches, last 24 hours): {wi.Snow}");
                    Console.WriteLine($"Weather Comment: {wi.Comment}");
                }
            }
            else if (p.InfoField is StatusInfo si)
            {
                Console.WriteLine($"    Timestamp: {si.Timestamp?.DateTime} {si.Timestamp?.DateTime.Kind}");
                Console.WriteLine($"    Position: {si.Position?.Encode()} ({si.Position?.EncodeGridsquare(4, false)})");
                Console.WriteLine($"    Comment: {si.Comment}");
            }
            else if (p.InfoField is MaidenheadBeaconInfo mbi)
            {
                Console.WriteLine($"    Position: {mbi.Position.EncodeGridsquare(4, false)}");
                Console.WriteLine($"    Comment: {mbi.Comment}");
            }
            else if (p.InfoField is MessageInfo mi)
            {
                Console.WriteLine($"    To: {mi.Addressee}");
                Console.WriteLine($"    Message: {mi.Content}");
                Console.WriteLine($"    ID: {mi.Id}");
            }
            else if (p.InfoField is UnsupportedInfo ui)
            {
                Console.WriteLine($"    Content: {ui.Content}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Main method that takes in raw packet strings.
        /// </summary>
        /// <param name="args"> Parameters to be passed as commandline arguments.</param>
        public static void Main(string[] args)
        {
            // Create a root command with some options
            var rootCommand = new RootCommand
                {
                new Option<Mode>(
                    aliases: new string[] { "--mode", "-m" },
                    getDefaultValue: () => Mode.APRSIS,
                    description: "The mode of operation for this program"),
                new Option<string>(
                    aliases: new string[] { "--callsign", "-c", "--cgn" },
                    getDefaultValue: () => AprsIsClient.AprsIsConstants.DefaultCallsign,
                    description: "A user callsign parsed as a string"),
                new Option<string>(
                    aliases: new string[] { "--password", "-p", "--pwd", "--pass" },
                    getDefaultValue: () => AprsIsClient.AprsIsConstants.DefaultPassword,
                    description: "A user password whose argument is parsed as a string"),
                new Option<string>(
                    aliases: new string[] { "--server", "-s", "--svr" },
                    getDefaultValue: () => AprsIsClient.AprsIsConstants.DefaultServerName,
                    description: "A specified server parsed as a string, used for APRS-IS or TCP TNCs"),
                new Option<int>(
                    aliases: new string[] { "--port" },
                    getDefaultValue: () => 8001,
                    description: "A TCP port for use with TCP TNCs"),
                new Option<string>(
                    aliases: new string[] { "--filter", "-f" },
                    getDefaultValue: () => AprsIsClient.AprsIsConstants.DefaultFilter,
                    description: "A user filter parsed as a string"),
                new Option<LogLevel>(
                    aliases: new string[] { "--verbosity", "-v" },
                    getDefaultValue: () => LogLevel.Warning,
                    description: "Set the verbosity of console logging."),
                };
            rootCommand.Description = "AprsSharp Console App";

            // The parameters of the handler method are matched according to the names of the options
            rootCommand.Handler = CommandHandler.Create<Mode, string, string, string, int, string, LogLevel>(Execute);

            rootCommand.Invoke(args);
        }

        /// <summary>
        /// Executes functionality using the provided command line arguments.
        /// </summary>
        /// <param name="mode">The mode of operation for this invocation of the program.</param>
        /// <param name="callsign">The user callsign that they should input.</param>
        /// <param name="password">The user password.</param>
        /// <param name="server">The specified server to connect (either APRS-IS or TCP TNC).</param>
        /// <param name="port">A port to use for connection in TCP TNC.</param>
        /// <param name="filter">The filter that will be used for receiving the packets.</param>
        /// <param name="verbosity">The minimum level for an event to be logged to the console.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task Execute(
            Mode mode,
            string callsign,
            string password,
            string server,
            int port,
            string filter,
            LogLevel verbosity)
        {
            using ILoggerFactory loggerFactory = LoggerFactory.Create(config =>
            {
                config.ClearProviders()
                    .AddConsole()
                    .SetMinimumLevel(verbosity);
            });

            switch (mode)
            {
                case Mode.APRSIS:
                {
                    Console.WriteLine($"Connecting to APRS-IS server: {server}");

                    using AprsIsClient n = new AprsIsClient(loggerFactory.CreateLogger<AprsIsClient>());
                    n.ReceivedPacket += PrintPacket;

                    Task receive = n.Receive(callsign, password, server, filter);

                    while (true)
                    {
                        ConsoleKeyInfo input = Console.ReadKey();

                        if (input.Key == ConsoleKey.Q)
                        {
                            n.Disconnect();
                            await receive;
                            break;
                        }
                    }

                    break;
                }

                case Mode.TCPTNC:
                {
                    Console.WriteLine($"Connecting to TNC via TCP: {server}:{port}");

                    using TNCInterface tnc = new TcpTNC(server, port, 0);
                    tnc.FrameReceivedEvent += (sender, args) =>
                    {
                        var byteArray = args.Data.ToArray();
                        var packet = new Packet(byteArray);
                        PrintPacket(packet);
                    };

                    Console.WriteLine("Enter status to send, else q to quit");

                    do
                    {
                        var input = Console.ReadLine();
                        if (string.Equals(input, "q", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }

                        var packet = new Packet(callsign, new List<string>(), new StatusInfo((Timestamp?)null, input));
                        tnc.SendData(packet.Encode(Packet.Format.AX25));
                    }
                    while (true);

                    break;
                }

                default:
                    throw new ArgumentException($"Unsupported execution mode: {mode}");
            }
        }
    }
}
