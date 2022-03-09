﻿namespace AprsSharp.Applications.Console
{
    using System;
    using System.Threading.Tasks;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.IO;
    using AprsSharp.Connections.AprsIs;
    using AprsSharp.Parsers.Aprs;

    /// <summary>
    /// The public class that will be called when building the console application.
    /// It is the main class that will have functionality of calling and decoding the packets.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// A function matching the delegate event to print the received packet.
        /// </summary>
        /// <param name="p">A <see cref="Packet"/> to be printed.</param>
        public static void PrintPacket(Packet p)
        {
            Console.WriteLine();
            Console.WriteLine($"Received type: {p.InfoField.Type}");

            Console.WriteLine($"    Sender: {p.Sender}");
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

            Console.WriteLine();
        }

        /// <summary>
        /// Main method that takes in raw packet strings.
        /// </summary>
        /// <param name="args"> The input arguments for the program i.e packets which will be strings.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task Main(string[] args)
        {
            // Create a root command with some options
            var rootCommand = new RootCommand
                {
                new Option<string>(
                    aliases: new string[] { "--password", "-pwd", "-pass" },
                    getDefaultValue: () => AprsIsConnection.AprsIsConstants.DefaultPassword,
                    description: "A user password whose argument is parsed as a string"),
                new Option<string>(
                    aliases: new string[] { "--callsign", "-c", "-cgn" },
                    getDefaultValue: () => AprsIsConnection.AprsIsConstants.DefaultCallsign,
                    description: "A user callsign parsed as a string"),
                new Option<string>(
                    aliases: new string[] { "--server", "-s", "-svr" },
                    getDefaultValue: () => AprsIsConnection.AprsIsConstants.DefaultServerName,
                    description: "A specified server parsed as a string"),
                new Option<string>(
                    aliases: new string[] { "--filter", "-f" },
                    getDefaultValue: () => AprsIsConnection.AprsIsConstants.DefaultFilter,
                    description: "A user filter parsed as a string"),
                };
            rootCommand.Description = "AprsSharp console app";

            // The paremeters of the handler method are matched according to the names of the options
            rootCommand.Handler = CommandHandler.Create<string?, string?, string?, string?, IConsole>(HandleAprsConnection);

            rootCommand.Invoke(args);
        }

        /// <summary>
        /// The method that will handle APRS connection and getting packets.
        /// </summary>
        /// <param name="callsign"> The user callsign that they should input.</param>
        /// <param name="password"> The user password.</param>
        /// <param name="server"> The specified server to connect.</param>
        /// <param name="filter"> The filter that will be used for receiving the packets.</param>
        /// <param name="console"> Flexibility in running in different consoles.</param>
        public static void HandleAprsConnection(string? callsign, string? password, string? server, string? filter, IConsole console)
        {
            using TcpConnection tcpConnection = new TcpConnection();
            AprsIsConnection n = new AprsIsConnection(tcpConnection);
            n.ReceivedPacket += PrintPacket;

            string? input;

            // get input from the user
            Console.Write("Enter your callsign: ");
            input = Console.ReadLine();
            string callsign = !string.IsNullOrWhiteSpace(input) ? input : throw new ArgumentException("Callsign must be provided");

            Console.Write("Enter your password (optional): ");
            input = Console.ReadLine();
            string password = !string.IsNullOrWhiteSpace(input) ? input : "-1";

            Console.Write("Enter server (optional): ");
            input = Console.ReadLine();
            string server = !string.IsNullOrWhiteSpace(input) ? input : "rotate.aprs2.net";

            Console.Write("Enter your filter (optional): ");
            input = Console.ReadLine();
            string filter = !string.IsNullOrWhiteSpace(input) ? input : "r/50.5039/4.4699/50";

            await n.Receive(callsign, password, server, filter);
        }
    }
}
